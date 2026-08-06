using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class ReleaseService(
    InventoryDbContext context,
    ICurrentUser session,
    IAuditService audit,
    IReferenceNumberService references) : IReleaseService
{
    private readonly StockQueries _stock = new(context);

    public async Task<IReadOnlyList<ReleaseDto>> ListAsync(
        string? search = null,
        Guid? recipientId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool includeVoided = true,
        CancellationToken cancellationToken = default)
    {
        session.Require();

        var query = Complete().AsNoTracking();

        if (!includeVoided)
            query = query.Where(release => release.Status == RecordStatus.Recorded);
        if (recipientId is { } person)
            query = query.Where(release => release.RecipientId == person);
        if (from is { } start)
            query = query.Where(release => release.ReleaseDate >= start);
        if (to is { } end)
            query = query.Where(release => release.ReleaseDate <= end);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(release =>
                EF.Functions.Like(release.ReferenceNumber, $"%{term}%") ||
                EF.Functions.Like(release.Recipient.Name, $"%{term}%") ||
                EF.Functions.Like(release.Recipient.Code, $"%{term}%") ||
                EF.Functions.Like(release.Purpose ?? string.Empty, $"%{term}%") ||
                release.Lines.Any(line =>
                    EF.Functions.Like(line.Item.Code, $"%{term}%") ||
                    EF.Functions.Like(line.Item.Name, $"%{term}%")));
        }

        var releases = await query
            .OrderByDescending(release => release.ReleaseDate)
            .ThenByDescending(release => release.ReferenceNumber)
            .ToListAsync(cancellationToken);

        return releases.Select(Map).ToList();
    }

    public async Task<ReleaseDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        session.Require();
        var release = await Complete().AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return release is null ? null : Map(release);
    }

    /// <summary>
    /// Live check used by the release form while the user is still typing, so the
    /// warning appears before they try to save.
    /// </summary>
    public async Task<string?> CheckSerialAvailabilityAsync(
        Guid itemId,
        long from,
        long to,
        Guid? excludingReleaseId = null,
        CancellationToken cancellationToken = default)
    {
        session.Require();
        if (from < 0 || to < from) return "The ending serial must not be lower than the beginning serial.";
        return await _stock.DescribeSerialConflictAsync(
            itemId, new SerialRange(from, to), excludingReleaseId, cancellationToken);
    }

    public async Task<ReleaseDto> SaveAsync(SaveReleaseRequest request, CancellationToken cancellationToken = default)
    {
        var user = session.Require();
        var lines = await ValidateAsync(request, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Release release;
            var isNew = request.Id is null;

            if (request.Id is { } id)
            {
                // Start from a clean tracker so nothing loaded by validation lingers.
                context.ChangeTracker.Clear();

                release = await Complete().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("That release no longer exists.");
                if (release.Status == RecordStatus.Voided)
                    throw new ValidationException("A voided release cannot be edited. Record a new release instead.");

                // Snapshot before clearing: removing straight from the live navigation
                // mutates the collection while it is being enumerated.
                var previousLines = release.Lines.ToList();
                release.Lines.Clear();
                context.ReleaseLines.RemoveRange(previousLines);
            }
            else
            {
                release = new Release
                {
                    ReferenceNumber = await references.NextAsync("OUT", request.ReleaseDate.Year, cancellationToken),
                    ReleasedBy = string.Empty,
                    ReceivedBy = string.Empty,
                    RecordedByUserId = user.Id
                };
                context.Releases.Add(release);
            }

            release.ReleaseDate = request.ReleaseDate;
            release.RecipientId = request.RecipientId;
            release.Purpose = Clean(request.Purpose);
            release.ReleasedBy = request.ReleasedBy.Trim();
            release.ReceivedBy = request.ReceivedBy.Trim();
            release.Remarks = Clean(request.Remarks);

            foreach (var line in lines)
            {
                release.Lines.Add(new ReleaseLine
                {
                    ItemId = line.ItemId,
                    Quantity = line.Quantity,
                    SerialFrom = line.SerialFrom,
                    SerialTo = line.SerialTo,
                    Remarks = Clean(line.Remarks)
                });
            }

            var recipientName = await context.Recipients
                .Where(person => person.Id == request.RecipientId)
                .Select(person => person.Name)
                .SingleAsync(cancellationToken);

            await audit.RecordAsync(
                isNew ? ActivityType.ReleaseRecorded : ActivityType.ReleaseUpdated,
                isNew
                    ? $"Release {release.ReferenceNumber} to {recipientName} recorded ({lines.Sum(line => line.Quantity)} total)."
                    : $"Release {release.ReferenceNumber} to {recipientName} edited.",
                nameof(Release), release.Id, release.ReferenceNumber,
                new { Recipient = recipientName, LineCount = lines.Count },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetAsync(release.Id, cancellationToken)
                ?? throw new InvalidOperationException("The release was saved but could not be reloaded.");
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

        var release = await Complete().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("That release no longer exists.");
        if (release.Status == RecordStatus.Voided)
            throw new ValidationException("That release is already voided.");

        release.Status = RecordStatus.Voided;
        release.VoidReason = reason.Trim();
        release.VoidedAt = DateTimeOffset.UtcNow;

        await audit.RecordAsync(
            ActivityType.ReleaseVoided,
            $"Release {release.ReferenceNumber} to {release.Recipient.Name} voided.",
            nameof(Release), release.Id, release.ReferenceNumber,
            new { Reason = release.VoidReason },
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<StockLineInput>> ValidateAsync(
        SaveReleaseRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ErrorList();

        errors.AddIf(request.ReleaseDate > DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            "The release date cannot be in the future.");
        errors.AddIf(request.Lines.Count == 0, "Add at least one item.");
        errors.AddIf(string.IsNullOrWhiteSpace(request.ReleasedBy), "Enter who released the items.");
        errors.AddIf(string.IsNullOrWhiteSpace(request.ReceivedBy), "Enter who received the items.");

        var recipient = await context.Recipients.AsNoTracking()
            .SingleOrDefaultAsync(person => person.Id == request.RecipientId, cancellationToken);
        if (recipient is null)
            errors.Add("Choose who is receiving the items.");
        else if (!recipient.IsActive)
            errors.Add($"{recipient.Name} is hidden. Reactivate them before recording a release.");

        errors.ThrowIfAny();

        var itemIds = request.Lines.Select(line => line.ItemId).Distinct().ToList();
        var items = await context.Items.AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        // Quantity already counted against this release, so editing does not
        // double-count its own lines when checking stock on hand.
        var ownQuantities = request.Id is { } editingId
            ? await context.ReleaseLines
                .Where(line => line.ReleaseId == editingId)
                .GroupBy(line => line.ItemId)
                .Select(group => new { ItemId = group.Key, Quantity = group.Sum(line => line.Quantity) })
                .ToDictionaryAsync(row => row.ItemId, row => row.Quantity, cancellationToken)
            : [];

        var lines = new List<StockLineInput>();
        var serialsInForm = new List<(Guid ItemId, SerialRange Range)>();
        var requestedPerItem = new Dictionary<Guid, long>();

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
                    errors.Add($"{label} ({item.Code}): this item does not use serial numbers. Clear the serial boxes.");
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
                    errors.Add($"{label} ({item.Code}): serial {range} overlaps {clashInForm} on another line of this same release.");
                    continue;
                }

                // The check the office actually depends on: never release a serial
                // that is still out with somebody.
                var conflict = await _stock.DescribeSerialConflictAsync(
                    line.ItemId, range, request.Id, cancellationToken);
                if (conflict is not null)
                {
                    errors.Add($"{label} ({item.Code}): {conflict}");
                    continue;
                }

                serialsInForm.Add((line.ItemId, range));
            }
            else if (item.TracksSerials)
            {
                errors.Add($"{label} ({item.Code}): this item uses serial numbers, so enter the serial range being released.");
                continue;
            }

            requestedPerItem[line.ItemId] = requestedPerItem.GetValueOrDefault(line.ItemId) + line.Quantity;
            lines.Add(line with { Remarks = Clean(line.Remarks) });
        }

        // Stock check runs per item after totalling the form, so two lines for the
        // same item cannot slip past individually.
        foreach (var (itemId, requested) in requestedPerItem)
        {
            var item = items[itemId];
            var onHand = await _stock.GetOnHandAsync(itemId, cancellationToken);
            var available = onHand + ownQuantities.GetValueOrDefault(itemId);
            if (requested > available)
                errors.Add($"'{item.Code} - {item.Name}': only {available} {item.Unit} on hand " +
                           $"but {requested} is being released. Record the incoming stock first.");
        }

        errors.ThrowIfAny();
        return lines;
    }

    private IQueryable<Release> Complete() => context.Releases
        .Include(release => release.Recipient)
        .Include(release => release.RecordedByUser)
        .Include(release => release.Lines)
            .ThenInclude(line => line.Item);

    private static ReleaseDto Map(Release release) => new(
        release.Id,
        release.ReferenceNumber,
        release.ReleaseDate,
        release.RecipientId,
        release.Recipient.Code,
        release.Recipient.Name,
        release.Purpose,
        release.ReleasedBy,
        release.ReceivedBy,
        release.Remarks,
        release.Status,
        release.VoidReason,
        release.RecordedByUser?.DisplayName ?? "-",
        release.Lines
            .OrderBy(line => line.Item.Name)
            .Select(line => new ReleaseLineDto(
                line.Id, line.ItemId, line.Item.Code, line.Item.Name, line.Item.Unit,
                line.Quantity, line.SerialFrom, line.SerialTo, line.Remarks))
            .ToList());

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
