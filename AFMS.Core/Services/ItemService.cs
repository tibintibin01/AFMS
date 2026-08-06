using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class ItemService(
    InventoryDbContext context,
    ICurrentUser session,
    IAuditService audit) : IItemService
{
    private static readonly string[] CommonUnits =
        ["pcs", "booklet", "box", "ream", "pad", "bottle", "set", "roll", "pack", "unit"];

    private readonly StockQueries _stock = new(context);

    public async Task<IReadOnlyList<ItemDto>> ListAsync(
        string? search = null,
        bool? isActive = null,
        bool onlyLow = false,
        CancellationToken cancellationToken = default)
    {
        session.Require();

        var query = context.Items.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.Like(item.Code, $"%{term}%") ||
                EF.Functions.Like(item.Name, $"%{term}%") ||
                EF.Functions.Like(item.Category ?? string.Empty, $"%{term}%"));
        }

        if (isActive is { } active)
            query = query.Where(item => item.IsActive == active);

        var items = await query
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var totals = await _stock.GetItemTotalsAsync(cancellationToken);

        var result = items.Select(item => Map(item, totals)).ToList();
        return onlyLow ? result.Where(item => item.IsLow).ToList() : result;
    }

    public async Task<ItemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        session.Require();
        var item = await context.Items.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (item is null) return null;

        var totals = await _stock.GetItemTotalsAsync(cancellationToken);
        return Map(item, totals);
    }

    public async Task<IReadOnlyList<ItemOption>> ListOptionsAsync(CancellationToken cancellationToken = default)
    {
        session.Require();
        var items = await context.Items.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Code, item.Name, item.Unit, item.TracksSerials })
            .ToListAsync(cancellationToken);
        var totals = await _stock.GetItemTotalsAsync(cancellationToken);

        return items
            .Select(item =>
            {
                var total = totals.TryGetValue(item.Id, out var value) ? value : (In: 0L, Out: 0L);
                return new ItemOption(item.Id, item.Code, item.Name, item.Unit, item.TracksSerials, total.In - total.Out);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListUnitsAsync(CancellationToken cancellationToken = default)
    {
        session.Require();
        var used = await context.Items.AsNoTracking()
            .Select(item => item.Unit)
            .Distinct()
            .ToListAsync(cancellationToken);

        return CommonUnits
            .Concat(used)
            .Where(unit => !string.IsNullOrWhiteSpace(unit))
            .Select(unit => unit.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(unit => unit, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ItemDto> SaveAsync(SaveItemRequest request, CancellationToken cancellationToken = default)
    {
        session.Require();

        var errors = new ErrorList();
        errors.AddIf(string.IsNullOrWhiteSpace(request.Code), "Item code is required.");
        errors.AddIf(string.IsNullOrWhiteSpace(request.Name), "Item name is required.");
        errors.AddIf(string.IsNullOrWhiteSpace(request.Unit), "Unit of measure is required.");
        errors.AddIf(request.ReorderLevel < 0, "Low-stock level cannot be negative.");
        errors.ThrowIfAny();

        var code = request.Code.Trim();
        var duplicate = await context.Items.AnyAsync(
            item => item.Code == code && (request.Id == null || item.Id != request.Id),
            cancellationToken);
        if (duplicate)
            throw new ValidationException($"Item code '{code}' is already used by another item.");

        Item item;
        if (request.Id is { } id)
        {
            item = await context.Items.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("That item no longer exists.");

            // Turning serial tracking off would orphan serials already recorded.
            if (item.TracksSerials && !request.TracksSerials)
            {
                var hasSerials =
                    await context.StockInLines.AnyAsync(line => line.ItemId == id && line.SerialFrom != null, cancellationToken) ||
                    await context.ReleaseLines.AnyAsync(line => line.ItemId == id && line.SerialFrom != null, cancellationToken);
                if (hasSerials)
                    throw new ValidationException(
                        "Serial numbers are already recorded for this item, so serial tracking cannot be switched off.");
            }

            item.Code = code;
            item.Name = request.Name.Trim();
            item.Unit = request.Unit.Trim();
            item.Category = Clean(request.Category);
            item.TracksSerials = request.TracksSerials;
            item.ReorderLevel = request.ReorderLevel;
            item.Remarks = Clean(request.Remarks);
            item.IsActive = request.IsActive;

            await audit.RecordAsync(ActivityType.ItemUpdated, $"Item '{item.Code} - {item.Name}' updated.",
                nameof(Item), item.Id, item.Code, cancellationToken: cancellationToken);
        }
        else
        {
            item = new Item
            {
                Code = code,
                Name = request.Name.Trim(),
                Unit = request.Unit.Trim(),
                Category = Clean(request.Category),
                TracksSerials = request.TracksSerials,
                ReorderLevel = request.ReorderLevel,
                Remarks = Clean(request.Remarks),
                IsActive = request.IsActive
            };
            context.Items.Add(item);

            await audit.RecordAsync(ActivityType.ItemCreated, $"Item '{item.Code} - {item.Name}' added.",
                nameof(Item), item.Id, item.Code, cancellationToken: cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        var totals = await _stock.GetItemTotalsAsync(cancellationToken);
        return Map(item, totals);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        session.Require();
        var item = await context.Items.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("That item no longer exists.");

        if (item.IsActive == isActive) return;
        item.IsActive = isActive;

        await audit.RecordAsync(
            ActivityType.ItemStatusChanged,
            $"Item '{item.Code} - {item.Name}' was {(isActive ? "reactivated" : "hidden from new entries")}.",
            nameof(Item), item.Id, item.Code, cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ItemDto Map(Item item, IReadOnlyDictionary<Guid, (long In, long Out)> totals)
    {
        var total = totals.TryGetValue(item.Id, out var value) ? value : (In: 0L, Out: 0L);
        return new ItemDto(
            item.Id, item.Code, item.Name, item.Unit, item.Category,
            item.TracksSerials, item.ReorderLevel, item.Remarks, item.IsActive,
            total.In, total.Out, total.In - total.Out);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
