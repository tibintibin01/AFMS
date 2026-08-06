using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

/// <summary>
/// Shared read helpers. Stock on hand and accountability are always worked out
/// from the recorded stock-in and release lines, so there is no separate ledger
/// that could drift out of step with the documents.
/// </summary>
internal sealed class StockQueries(InventoryDbContext context)
{
    /// <summary>One released serial range together with who received it.</summary>
    internal sealed record ReleasedSerials(
        Guid ReleaseId,
        string ReferenceNumber,
        DateOnly ReleaseDate,
        Guid ItemId,
        Guid RecipientId,
        string RecipientName,
        SerialRange Range);

    internal sealed record ReturnedSerials(Guid ItemId, Guid RecipientId, SerialRange Range);

    /// <summary>Total quantity in and out per item, counting recorded documents only.</summary>
    public async Task<Dictionary<Guid, (long In, long Out)>> GetItemTotalsAsync(
        CancellationToken cancellationToken)
    {
        var incoming = await context.StockInLines
            .Where(line => line.StockIn.Status == RecordStatus.Recorded)
            .GroupBy(line => line.ItemId)
            .Select(group => new { ItemId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);

        var outgoing = await context.ReleaseLines
            .Where(line => line.Release.Status == RecordStatus.Recorded)
            .GroupBy(line => line.ItemId)
            .Select(group => new { ItemId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<Guid, (long In, long Out)>();
        foreach (var row in incoming)
            totals[row.ItemId] = (row.Quantity, 0);
        foreach (var row in outgoing)
        {
            var existing = totals.TryGetValue(row.ItemId, out var current) ? current : (In: 0L, Out: 0L);
            totals[row.ItemId] = (existing.In, row.Quantity);
        }

        return totals;
    }

    public async Task<long> GetOnHandAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var incoming = await context.StockInLines
            .Where(line => line.ItemId == itemId && line.StockIn.Status == RecordStatus.Recorded)
            .SumAsync(line => (long?)line.Quantity, cancellationToken) ?? 0;
        var outgoing = await context.ReleaseLines
            .Where(line => line.ItemId == itemId && line.Release.Status == RecordStatus.Recorded)
            .SumAsync(line => (long?)line.Quantity, cancellationToken) ?? 0;
        return incoming - outgoing;
    }

    /// <summary>Every serial range that has been released, optionally narrowed to one item.</summary>
    public async Task<List<ReleasedSerials>> GetReleasedSerialsAsync(
        Guid? itemId,
        Guid? excludingReleaseId,
        CancellationToken cancellationToken)
    {
        var query = context.ReleaseLines
            .Where(line => line.Release.Status == RecordStatus.Recorded
                && line.SerialFrom != null
                && line.SerialTo != null);

        if (itemId is { } id) query = query.Where(line => line.ItemId == id);
        if (excludingReleaseId is { } skip) query = query.Where(line => line.ReleaseId != skip);

        var rows = await query
            .Select(line => new
            {
                line.ReleaseId,
                line.Release.ReferenceNumber,
                line.Release.ReleaseDate,
                line.ItemId,
                line.Release.RecipientId,
                RecipientName = line.Release.Recipient.Name,
                From = line.SerialFrom!.Value,
                To = line.SerialTo!.Value
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ReleasedSerials(
                row.ReleaseId, row.ReferenceNumber, row.ReleaseDate, row.ItemId,
                row.RecipientId, row.RecipientName, new SerialRange(row.From, row.To)))
            .ToList();
    }

    /// <summary>
    /// Serial ranges handed back by a recipient. Only stock-in entries marked as
    /// returns count, because those are the ones that clear accountability.
    /// </summary>
    public async Task<List<ReturnedSerials>> GetReturnedSerialsAsync(
        Guid? itemId,
        CancellationToken cancellationToken)
    {
        var query = context.StockInLines
            .Where(line => line.StockIn.Status == RecordStatus.Recorded
                && line.StockIn.Source == StockInSource.Returned
                && line.StockIn.ReturnedByRecipientId != null
                && line.SerialFrom != null
                && line.SerialTo != null);

        if (itemId is { } id) query = query.Where(line => line.ItemId == id);

        var rows = await query
            .Select(line => new
            {
                line.ItemId,
                RecipientId = line.StockIn.ReturnedByRecipientId!.Value,
                From = line.SerialFrom!.Value,
                To = line.SerialTo!.Value
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ReturnedSerials(row.ItemId, row.RecipientId, new SerialRange(row.From, row.To)))
            .ToList();
    }

    /// <summary>
    /// Serial ranges still out with someone for one item: everything released
    /// minus everything returned.
    /// </summary>
    public async Task<IReadOnlyList<SerialRange>> GetOutstandingSerialsAsync(
        Guid itemId,
        Guid? excludingReleaseId,
        CancellationToken cancellationToken)
    {
        var released = await GetReleasedSerialsAsync(itemId, excludingReleaseId, cancellationToken);
        if (released.Count == 0) return [];

        var returned = await GetReturnedSerialsAsync(itemId, cancellationToken);
        return SerialMath.Subtract(
            released.Select(row => row.Range),
            returned.Select(row => row.Range));
    }

    /// <summary>
    /// Explains why a serial range cannot be released again, naming who holds it
    /// and on which reference. Returns null when the range is free.
    /// </summary>
    public async Task<string?> DescribeSerialConflictAsync(
        Guid itemId,
        SerialRange candidate,
        Guid? excludingReleaseId,
        CancellationToken cancellationToken)
    {
        var outstanding = await GetOutstandingSerialsAsync(itemId, excludingReleaseId, cancellationToken);

        // Report the exact serials in conflict, not the whole range they fall inside.
        var clashes = SerialMath.Intersect(candidate, outstanding);
        if (clashes.Count == 0) return null;

        var released = await GetReleasedSerialsAsync(itemId, excludingReleaseId, cancellationToken);
        var holders = released
            .Where(row => clashes.Any(clash => clash.Overlaps(row.Range)))
            .OrderByDescending(row => row.ReleaseDate)
            .ThenByDescending(row => row.ReferenceNumber)
            .Select(row => $"{row.RecipientName} ({row.ReferenceNumber}, {row.ReleaseDate:MMM d, yyyy})")
            .Distinct()
            .Take(3)
            .ToList();

        var overlap = SerialMath.Describe(clashes);
        return holders.Count == 0
            ? $"Serial {overlap} is already released and has not been returned."
            : $"Serial {overlap} is still with {string.Join("; ", holders)}. Record a return first if it was handed back.";
    }

    /// <summary>
    /// Flags a serial range that was already recorded as received, so a delivery
    /// is not entered twice. Returns are excluded because a returned booklet is
    /// meant to re-enter stock under its original serials.
    /// </summary>
    public async Task<string?> DescribeReceiptConflictAsync(
        Guid itemId,
        SerialRange candidate,
        Guid? excludingStockInId,
        CancellationToken cancellationToken)
    {
        var query = context.StockInLines
            .Where(line => line.ItemId == itemId
                && line.StockIn.Status == RecordStatus.Recorded
                && line.StockIn.Source != StockInSource.Returned
                && line.SerialFrom != null
                && line.SerialTo != null
                && line.SerialFrom <= candidate.To
                && line.SerialTo >= candidate.From);

        if (excludingStockInId is { } skip) query = query.Where(line => line.StockInId != skip);

        var clash = await query
            .Select(line => new
            {
                line.StockIn.ReferenceNumber,
                line.StockIn.EntryDate,
                From = line.SerialFrom!.Value,
                To = line.SerialTo!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);

        return clash is null
            ? null
            : $"Serial {new SerialRange(clash.From, clash.To)} was already received on {clash.ReferenceNumber} ({clash.EntryDate:MMM d, yyyy}).";
    }

    /// <summary>Quantity released minus quantity returned, keyed by recipient and item.</summary>
    public async Task<Dictionary<(Guid RecipientId, Guid ItemId), (long Released, long Returned)>>
        GetAccountabilityTotalsAsync(Guid? recipientId, CancellationToken cancellationToken)
    {
        var releaseQuery = context.ReleaseLines
            .Where(line => line.Release.Status == RecordStatus.Recorded);
        if (recipientId is { } person)
            releaseQuery = releaseQuery.Where(line => line.Release.RecipientId == person);

        var released = await releaseQuery
            .GroupBy(line => new { line.Release.RecipientId, line.ItemId })
            .Select(group => new
            {
                group.Key.RecipientId,
                group.Key.ItemId,
                Quantity = group.Sum(line => line.Quantity)
            })
            .ToListAsync(cancellationToken);

        var returnQuery = context.StockInLines
            .Where(line => line.StockIn.Status == RecordStatus.Recorded
                && line.StockIn.Source == StockInSource.Returned
                && line.StockIn.ReturnedByRecipientId != null);
        if (recipientId is { } returner)
            returnQuery = returnQuery.Where(line => line.StockIn.ReturnedByRecipientId == returner);

        var returned = await returnQuery
            .GroupBy(line => new { RecipientId = line.StockIn.ReturnedByRecipientId!.Value, line.ItemId })
            .Select(group => new
            {
                group.Key.RecipientId,
                group.Key.ItemId,
                Quantity = group.Sum(line => line.Quantity)
            })
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<(Guid, Guid), (long Released, long Returned)>();
        foreach (var row in released)
            totals[(row.RecipientId, row.ItemId)] = (row.Quantity, 0);
        foreach (var row in returned)
        {
            var key = (row.RecipientId, row.ItemId);
            var current = totals.TryGetValue(key, out var existing) ? existing : (Released: 0L, Returned: 0L);
            totals[key] = (current.Released, row.Quantity);
        }

        return totals;
    }
}
