using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

/// <summary>
/// Answers "what does this person still have?" by taking everything released to
/// them and subtracting everything they handed back.
/// </summary>
public sealed class AccountabilityService(
    InventoryDbContext context,
    ICurrentUser session) : IAccountabilityService
{
    private readonly StockQueries _stock = new(context);

    public async Task<IReadOnlyList<OutstandingByRecipient>> ListOutstandingAsync(
        Guid? recipientId = null,
        CancellationToken cancellationToken = default)
    {
        session.Require();
        return await BuildAsync(recipientId, includeEmpty: false, cancellationToken);
    }

    public async Task<OutstandingByRecipient?> GetForRecipientAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        session.Require();
        var results = await BuildAsync(recipientId, includeEmpty: true, cancellationToken);
        return results.FirstOrDefault();
    }

    private async Task<List<OutstandingByRecipient>> BuildAsync(
        Guid? recipientId,
        bool includeEmpty,
        CancellationToken cancellationToken)
    {
        var totals = await _stock.GetAccountabilityTotalsAsync(recipientId, cancellationToken);

        var recipientQuery = context.Recipients.AsNoTracking();
        if (recipientId is { } person)
            recipientQuery = recipientQuery.Where(candidate => candidate.Id == person);
        var recipients = await recipientQuery.ToListAsync(cancellationToken);

        var itemIds = totals.Keys.Select(key => key.ItemId).Distinct().ToList();
        var items = await context.Items.AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var released = await _stock.GetReleasedSerialsAsync(null, null, cancellationToken);
        var returned = await _stock.GetReturnedSerialsAsync(null, cancellationToken);

        // Most recent release per recipient and item, for the "last released" column.
        var lastRelease = await context.ReleaseLines.AsNoTracking()
            .Where(line => line.Release.Status == RecordStatus.Recorded)
            .GroupBy(line => new { line.Release.RecipientId, line.ItemId })
            .Select(group => new
            {
                group.Key.RecipientId,
                group.Key.ItemId,
                Date = group.Max(line => line.Release.ReleaseDate)
            })
            .ToListAsync(cancellationToken);

        var lastReferences = await context.ReleaseLines.AsNoTracking()
            .Where(line => line.Release.Status == RecordStatus.Recorded)
            .Select(line => new
            {
                line.Release.RecipientId,
                line.ItemId,
                line.Release.ReleaseDate,
                line.Release.ReferenceNumber
            })
            .ToListAsync(cancellationToken);

        var results = new List<OutstandingByRecipient>();

        foreach (var recipient in recipients.OrderBy(person => person.Name))
        {
            var rows = new List<OutstandingItem>();

            foreach (var entry in totals.Where(pair => pair.Key.RecipientId == recipient.Id))
            {
                var itemId = entry.Key.ItemId;
                if (!items.TryGetValue(itemId, out var item)) continue;

                var outstanding = entry.Value.Released - entry.Value.Returned;
                if (outstanding <= 0) continue;

                var serialsHeld = "-";
                if (item.TracksSerials)
                {
                    var releasedToPerson = released
                        .Where(row => row.RecipientId == recipient.Id && row.ItemId == itemId)
                        .Select(row => row.Range);
                    var returnedByPerson = returned
                        .Where(row => row.RecipientId == recipient.Id && row.ItemId == itemId)
                        .Select(row => row.Range);
                    serialsHeld = SerialMath.Describe(SerialMath.Subtract(releasedToPerson, returnedByPerson));
                }

                var last = lastRelease.FirstOrDefault(row =>
                    row.RecipientId == recipient.Id && row.ItemId == itemId);
                var lastReference = last is null
                    ? null
                    : lastReferences
                        .Where(row => row.RecipientId == recipient.Id
                            && row.ItemId == itemId
                            && row.ReleaseDate == last.Date)
                        .OrderByDescending(row => row.ReferenceNumber)
                        .Select(row => row.ReferenceNumber)
                        .FirstOrDefault();

                rows.Add(new OutstandingItem(
                    itemId, item.Code, item.Name, item.Unit,
                    entry.Value.Released, entry.Value.Returned, outstanding,
                    serialsHeld, last?.Date, lastReference));
            }

            if (rows.Count == 0 && !includeEmpty) continue;

            results.Add(new OutstandingByRecipient(
                recipient.Id, recipient.Code, recipient.Name,
                recipient.Position, recipient.Office,
                rows.OrderBy(row => row.ItemName).ToList()));
        }

        return results
            .OrderByDescending(row => row.TotalQuantity)
            .ThenBy(row => row.RecipientName)
            .ToList();
    }
}
