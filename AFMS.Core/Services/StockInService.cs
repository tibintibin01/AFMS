using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class StockInService(
    InventoryDbContext context,
    ICurrentUser session,
    IAuditService audit,
    IReferenceNumberService references) : IStockInService
{
    private readonly StockQueries _stock = new(context);

    public async Task<IReadOnlyList<StockInDto>> ListAsync(
        string? search = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool includeVoided = true,
        CancellationToken cancellationToken = default)
    {
        session.Require();

        var query = Complete().AsNoTracking();

        if (!includeVoided)
            query = query.Where(entry => entry.Status == RecordStatus.Recorded);
        if (from is { } start)
            query = query.Where(entry => entry.EntryDate >= start);
        if (to is { } end)
            query = query.Where(entry => entry.EntryDate <= end);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(entry =>
                EF.Functions.Like(entry.ReferenceNumber, $"%{term}%") ||
                EF.Functions.Like(entry.SourceName ?? string.Empty, $"%{term}%") ||
                EF.Functions.Like(entry.DocumentNumber ?? string.Empty, $"%{term}%") ||
                entry.Lines.Any(line =>
                    EF.Functions.Like(line.Item.Code, $"%{term}%") ||
                    EF.Functions.Like(line.Item.Name, $"%{term}%")));
        }

        var entries = await query
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.ReferenceNumber)
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList();
    }

    public async Task<StockInDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        session.Require();
        var entry = await Complete().AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return entry is null ? null : Map(entry);
    }

    public async Task<StockInDto> SaveAsync(SaveStockInRequest request, CancellationToken cancellationToken = default)
    {
        var user = session.Require();
        var lines = await ValidateAsync(request, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            StockIn entry;
            var isNew = request.Id is null;

            if (request.Id is { } id)
            {
                // Start from a clean tracker so nothing loaded by validation lingers.
                context.ChangeTracker.Clear();

                entry = await Complete().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("That stock-in entry no longer exists.");
                if (entry.Status == RecordStatus.Voided)
                    throw new ValidationException("A voided entry cannot be edited. Record a new entry instead.");

                // Snapshot before clearing: removing straight from the live navigation
                // mutates the collection while it is being enumerated.
                var previousLines = entry.Lines.ToList();
                entry.Lines.Clear();
                context.StockInLines.RemoveRange(previousLines);
            }
            else
            {
                entry = new StockIn
                {
                    ReferenceNumber = await references.NextAsync("IN", request.EntryDate.Year, cancellationToken),
                    RecordedByUserId = user.Id
                };
                context.StockIns.Add(entry);
            }

            entry.EntryDate = request.EntryDate;
            entry.Source = request.Source;
            entry.SourceName = Clean(request.SourceName);
            entry.DocumentNumber = Clean(request.DocumentNumber);
            entry.ReturnedByRecipientId = request.Source == StockInSource.Returned ? request.ReturnedByRecipientId : null;
            entry.ReceivedBy = Clean(request.ReceivedBy) ?? user.DisplayName;
            entry.Remarks = Clean(request.Remarks);

            foreach (var line in lines)
            {
                entry.Lines.Add(new StockInLine
                {
                    ItemId = line.ItemId,
                    Quantity = line.Quantity,
                    SerialFrom = line.SerialFrom,
                    SerialTo = line.SerialTo,
                    Remarks = Clean(line.Remarks)
                });
            }

            await audit.RecordAsync(
                isNew ? ActivityType.StockInRecorded : ActivityType.StockInUpdated,
                isNew
                    ? $"Stock in {entry.ReferenceNumber} recorded ({lines.Sum(line => line.Quantity)} total)."
                    : $"Stock in {entry.ReferenceNumber} edited.",
                nameof(StockIn), entry.Id, entry.ReferenceNumber,
                new { entry.Source, entry.DocumentNumber, LineCount = lines.Count },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetAsync(entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("The entry was saved but could not be reloaded.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task VoidAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        session.Require();

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new ValidationException("Give a reason of at least 5 characters so the correction is documented.");

        var entry = await Complete().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("That stock-in entry no longer exists.");
        if (entry.Status == RecordStatus.Voided)
            throw new ValidationException("That entry is already voided.");

        // Voiding a receipt removes stock, so the items must not already be released out.
        foreach (var line in entry.Lines)
        {
            var onHand = await _stock.GetOnHandAsync(line.ItemId, cancellationToken);
            if (onHand - line.Quantity < 0)
                throw new ValidationException(
                    $"'{line.Item.Code} - {line.Item.Name}' would go below zero on hand. " +
                    "Void or correct the releases that used this stock first.");
        }

        entry.Status = RecordStatus.Voided;
        entry.VoidReason = reason.Trim();
        entry.VoidedAt = DateTimeOffset.UtcNow;

        await audit.RecordAsync(
            ActivityType.StockInVoided,
            $"Stock in {entry.ReferenceNumber} voided.",
            nameof(StockIn), entry.Id, entry.ReferenceNumber,
            new { Reason = entry.VoidReason },
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Checks the whole form before anything is written, so the user sees every
    /// problem at once instead of fixing them one save at a time.
    /// </summary>
    private async Task<List<StockLineInput>> ValidateAsync(
        SaveStockInRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ErrorList();

        errors.AddIf(request.EntryDate > DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            "The date received cannot be in the future.");
        errors.AddIf(request.Lines.Count == 0, "Add at least one item.");

        if (request.Source == StockInSource.Returned && request.ReturnedByRecipientId is null)
            errors.Add("Choose who returned the items.");
        if (request.Source != StockInSource.Returned && request.ReturnedByRecipientId is not null)
            errors.Add("Only a 'Returned by recipient' entry can name a person returning items.");

        if (request.ReturnedByRecipientId is { } recipientId)
        {
            var exists = await context.Recipients.AnyAsync(person => person.Id == recipientId, cancellationToken);
            errors.AddIf(!exists, "The selected person is no longer on file.");
        }

        errors.ThrowIfAny();

        var itemIds = request.Lines.Select(line => line.ItemId).Distinct().ToList();
        var items = await context.Items.AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var lines = new List<StockLineInput>();
        var serialsInForm = new List<(Guid ItemId, SerialRange Range)>();

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            var label = $"Line {index + 1}";

            if (!items.TryGetValue(line.ItemId, out var item))
            {
                errors.Add($"{label}: choose an item.");
                continue;
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"{label} ({item.Code}): quantity must be more than zero.");
                continue;
            }

            SerialRange? range = null;
            if (line.SerialFrom is not null || line.SerialTo is not null)
            {
                if (!item.TracksSerials)
                {
                    errors.Add($"{label} ({item.Code}): this item does not use serial numbers. " +
                               "Turn on serial tracking for the item first, or clear the serial boxes.");
                    continue;
                }

                if (line.SerialFrom is null || line.SerialTo is null)
                {
                    errors.Add($"{label} ({item.Code}): enter both the beginning and ending serial.");
                    continue;
                }

                if (line.SerialFrom < 0 || line.SerialTo < line.SerialFrom)
                {
                    errors.Add($"{label} ({item.Code}): the ending serial must not be lower than the beginning serial.");
                    continue;
                }

                range = new SerialRange(line.SerialFrom.Value, line.SerialTo.Value);

                if (range.Count != line.Quantity)
                {
                    errors.Add($"{label} ({item.Code}): serial {range} covers {range.Count} piece(s) " +
                               $"but the quantity says {line.Quantity}.");
                    continue;
                }

                var clashInForm = serialsInForm
                    .Where(existing => existing.ItemId == line.ItemId && existing.Range.Overlaps(range))
                    .Select(existing => existing.Range)
                    .FirstOrDefault();
                if (clashInForm is not null)
                {
                    errors.Add($"{label} ({item.Code}): serial {range} overlaps {clashInForm} on another line of this same entry.");
                    continue;
                }

                if (request.Source != StockInSource.Returned)
                {
                    var alreadyReceived = await _stock.DescribeReceiptConflictAsync(
                        line.ItemId, range, request.Id, cancellationToken);
                    if (alreadyReceived is not null)
                    {
                        errors.Add($"{label} ({item.Code}): {alreadyReceived}");
                        continue;
                    }
                }

                serialsInForm.Add((line.ItemId, range));
            }
            else if (item.TracksSerials)
            {
                errors.Add($"{label} ({item.Code}): this item uses serial numbers, so enter the serial range.");
                continue;
            }

            lines.Add(line with { Remarks = Clean(line.Remarks) });
        }

        errors.ThrowIfAny();
        return lines;
    }

    private IQueryable<StockIn> Complete() => context.StockIns
        .Include(entry => entry.ReturnedByRecipient)
        .Include(entry => entry.RecordedByUser)
        .Include(entry => entry.Lines)
            .ThenInclude(line => line.Item);

    private static StockInDto Map(StockIn entry) => new(
        entry.Id,
        entry.ReferenceNumber,
        entry.EntryDate,
        entry.Source,
        entry.SourceName,
        entry.DocumentNumber,
        entry.ReturnedByRecipientId,
        entry.ReturnedByRecipient?.Name,
        entry.ReceivedBy,
        entry.Remarks,
        entry.Status,
        entry.VoidReason,
        entry.RecordedByUser?.DisplayName ?? "-",
        entry.Lines
            .OrderBy(line => line.Item.Name)
            .Select(line => new StockInLineDto(
                line.Id, line.ItemId, line.Item.Code, line.Item.Name, line.Item.Unit,
                line.Quantity, line.SerialFrom, line.SerialTo, line.Remarks))
            .ToList());

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
