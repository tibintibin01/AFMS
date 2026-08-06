using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

/// <summary>
/// Builds the combined in/out history by reading the stock-in and release lines
/// and merging them into one dated list.
/// </summary>
public sealed class HistoryService(
    InventoryDbContext context,
    ICurrentUser session) : IHistoryService
{
    public async Task<IReadOnlyList<HistoryEntry>> ListAsync(
        HistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        session.Require();

        var incoming = await LoadIncomingAsync(filter, cancellationToken);
        var outgoing = await LoadOutgoingAsync(filter, cancellationToken);

        var combined = incoming.Concat(outgoing);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim();
            combined = combined.Where(entry =>
                entry.ReferenceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                entry.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                entry.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                entry.PartyName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                entry.SerialLabel.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                entry.Detail.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return combined
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.ReferenceNumber)
            .ThenBy(entry => entry.ItemName)
            .ToList();
    }

    public async Task<IReadOnlyList<StockCardEntry>> GetStockCardAsync(
        Guid itemId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        session.Require();

        // The running balance must start from all history, then the requested
        // window is sliced off the front, otherwise the opening figure is wrong.
        var filter = new HistoryFilter(ItemId: itemId, IncludeVoided: false);
        var entries = (await ListAsync(filter, cancellationToken))
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.ReferenceNumber)
            .ToList();

        var card = new List<StockCardEntry>();
        var balance = 0L;

        foreach (var entry in entries)
        {
            balance += entry.QuantityIn - entry.QuantityOut;

            if (from is { } start && entry.Date < start) continue;
            if (to is { } end && entry.Date > end) continue;

            var particulars = entry.Direction == MovementDirection.In
                ? $"{entry.Detail}{(entry.PartyName == "-" ? "" : $" - {entry.PartyName}")}"
                : $"Released to {entry.PartyName}";

            card.Add(new StockCardEntry(
                entry.Date, entry.ReferenceNumber, particulars,
                entry.QuantityIn, entry.QuantityOut, balance, entry.SerialLabel));
        }

        return card;
    }

    private async Task<List<HistoryEntry>> LoadIncomingAsync(
        HistoryFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.Direction == MovementDirection.Out) return [];

        // A return is tied to a person, so filtering by recipient must still show it.
        var query = context.StockInLines.AsNoTracking().AsQueryable();

        if (!filter.IncludeVoided)
            query = query.Where(line => line.StockIn.Status == RecordStatus.Recorded);
        if (filter.ItemId is { } itemId)
            query = query.Where(line => line.ItemId == itemId);
        if (filter.RecipientId is { } recipientId)
            query = query.Where(line => line.StockIn.ReturnedByRecipientId == recipientId);
        if (filter.FromDate is { } start)
            query = query.Where(line => line.StockIn.EntryDate >= start);
        if (filter.ToDate is { } end)
            query = query.Where(line => line.StockIn.EntryDate <= end);

        var rows = await query
            .Select(line => new
            {
                line.StockInId,
                line.StockIn.ReferenceNumber,
                line.StockIn.EntryDate,
                line.StockIn.Source,
                line.StockIn.SourceName,
                line.StockIn.DocumentNumber,
                ReturnedByName = line.StockIn.ReturnedByRecipient != null
                    ? line.StockIn.ReturnedByRecipient.Name
                    : null,
                line.StockIn.Status,
                RecordedBy = line.StockIn.RecordedByUser.DisplayName,
                ItemCode = line.Item.Code,
                ItemName = line.Item.Name,
                line.Item.Unit,
                line.Quantity,
                line.SerialFrom,
                line.SerialTo
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new HistoryEntry(
            row.StockInId,
            row.ReferenceNumber,
            row.EntryDate,
            MovementDirection.In,
            row.ItemCode,
            row.ItemName,
            row.Unit,
            row.Quantity,
            SerialLabel(row.SerialFrom, row.SerialTo),
            row.ReturnedByName ?? row.SourceName ?? "-",
            SourceLabel(row.Source, row.DocumentNumber),
            row.Status,
            row.RecordedBy)).ToList();
    }

    private async Task<List<HistoryEntry>> LoadOutgoingAsync(
        HistoryFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.Direction == MovementDirection.In) return [];

        var query = context.ReleaseLines.AsNoTracking().AsQueryable();

        if (!filter.IncludeVoided)
            query = query.Where(line => line.Release.Status == RecordStatus.Recorded);
        if (filter.ItemId is { } itemId)
            query = query.Where(line => line.ItemId == itemId);
        if (filter.RecipientId is { } recipientId)
            query = query.Where(line => line.Release.RecipientId == recipientId);
        if (filter.FromDate is { } start)
            query = query.Where(line => line.Release.ReleaseDate >= start);
        if (filter.ToDate is { } end)
            query = query.Where(line => line.Release.ReleaseDate <= end);

        var rows = await query
            .Select(line => new
            {
                line.ReleaseId,
                line.Release.ReferenceNumber,
                line.Release.ReleaseDate,
                RecipientName = line.Release.Recipient.Name,
                line.Release.Purpose,
                line.Release.Status,
                RecordedBy = line.Release.RecordedByUser.DisplayName,
                ItemCode = line.Item.Code,
                ItemName = line.Item.Name,
                line.Item.Unit,
                line.Quantity,
                line.SerialFrom,
                line.SerialTo
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new HistoryEntry(
            row.ReleaseId,
            row.ReferenceNumber,
            row.ReleaseDate,
            MovementDirection.Out,
            row.ItemCode,
            row.ItemName,
            row.Unit,
            row.Quantity,
            SerialLabel(row.SerialFrom, row.SerialTo),
            row.RecipientName,
            string.IsNullOrWhiteSpace(row.Purpose) ? "Released" : row.Purpose,
            row.Status,
            row.RecordedBy)).ToList();
    }

    private static string SerialLabel(long? from, long? to) =>
        from is { } start && to is { } end
            ? (start == end ? start.ToString() : $"{start}-{end}")
            : "-";

    private static string SourceLabel(StockInSource source, string? documentNumber)
    {
        var label = source switch
        {
            StockInSource.Purchase => "Purchase / delivery",
            StockInSource.Transferred => "Transferred in",
            StockInSource.Returned => "Returned",
            StockInSource.BeginningBalance => "Beginning balance",
            StockInSource.Adjustment => "Adjustment",
            _ => source.ToString()
        };
        return string.IsNullOrWhiteSpace(documentNumber) ? label : $"{label} ({documentNumber})";
    }
}
