using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class DashboardService(
    InventoryDbContext context,
    ICurrentUser session,
    IItemService items,
    IHistoryService history) : IDashboardService
{
    private readonly StockQueries _stock = new(context);

    public async Task<DashboardSummary> GetAsync(CancellationToken cancellationToken = default)
    {
        session.Require();

        var allItems = await items.ListAsync(cancellationToken: cancellationToken);
        var activeItems = allItems.Where(item => item.IsActive).ToList();
        var lowStock = activeItems.Where(item => item.IsLow).OrderBy(item => item.OnHand).ToList();

        var activeRecipients = await context.Recipients.CountAsync(person => person.IsActive, cancellationToken);

        var accountability = await _stock.GetAccountabilityTotalsAsync(null, cancellationToken);
        var outstandingByPerson = accountability
            .GroupBy(entry => entry.Key.RecipientId)
            .Select(group => group.Sum(entry => Math.Max(0, entry.Value.Released - entry.Value.Returned)))
            .Where(total => total > 0)
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var stockInThisMonth = await context.StockIns.CountAsync(
            entry => entry.Status == RecordStatus.Recorded && entry.EntryDate >= monthStart,
            cancellationToken);
        var releasesThisMonth = await context.Releases.CountAsync(
            release => release.Status == RecordStatus.Recorded && release.ReleaseDate >= monthStart,
            cancellationToken);

        var recent = (await history.ListAsync(new HistoryFilter(), cancellationToken))
            .Take(12)
            .ToList();

        return new DashboardSummary(
            ActiveItems: activeItems.Count,
            ActiveRecipients: activeRecipients,
            TotalOnHand: activeItems.Sum(item => Math.Max(0, item.OnHand)),
            LowStockCount: lowStock.Count,
            OutstandingQuantity: outstandingByPerson.Sum(),
            PeopleHoldingItems: outstandingByPerson.Count,
            StockInThisMonth: stockInThisMonth,
            ReleasesThisMonth: releasesThisMonth,
            LowStockItems: lowStock.Take(8).ToList(),
            RecentActivity: recent);
    }
}
