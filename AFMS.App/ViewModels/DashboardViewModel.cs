using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>The at-a-glance screen: what is low, what is out, what happened lately.</summary>
public sealed class DashboardViewModel : PageViewModelBase
{
    private readonly IDashboardService _dashboard;
    private DashboardSummary? _summary;

    public DashboardViewModel(IDashboardService dashboard)
    {
        _dashboard = dashboard;
        RefreshCommand = new AsyncRelayCommand(InitializeAsync);
    }

    public ICommand RefreshCommand { get; }

    public ObservableCollection<ItemDto> LowStockItems { get; } = [];
    public ObservableCollection<HistoryEntry> RecentActivity { get; } = [];

    public int ActiveItems => _summary?.ActiveItems ?? 0;
    public int ActiveRecipients => _summary?.ActiveRecipients ?? 0;
    public long TotalOnHand => _summary?.TotalOnHand ?? 0;
    public int LowStockCount => _summary?.LowStockCount ?? 0;
    public long OutstandingQuantity => _summary?.OutstandingQuantity ?? 0;
    public int PeopleHoldingItems => _summary?.PeopleHoldingItems ?? 0;
    public int StockInThisMonth => _summary?.StockInThisMonth ?? 0;
    public int ReleasesThisMonth => _summary?.ReleasesThisMonth ?? 0;

    public bool HasLowStock => LowStockCount > 0;

    public string OutstandingSummary => PeopleHoldingItems == 0
        ? "Nothing is out with anyone."
        : $"{OutstandingQuantity:N0} item(s) still with {PeopleHoldingItems} " +
          $"{(PeopleHoldingItems == 1 ? "person" : "people")}.";

    public string MonthLabel => DateTime.Today.ToString("MMMM yyyy");

    public Task InitializeAsync() => RunAsync(async () =>
    {
        _summary = await _dashboard.GetAsync();

        LowStockItems.Clear();
        foreach (var item in _summary.LowStockItems) LowStockItems.Add(item);

        RecentActivity.Clear();
        foreach (var entry in _summary.RecentActivity) RecentActivity.Add(entry);

        foreach (var property in new[]
                 {
                     nameof(ActiveItems), nameof(ActiveRecipients), nameof(TotalOnHand),
                     nameof(LowStockCount), nameof(OutstandingQuantity), nameof(PeopleHoldingItems),
                     nameof(StockInThisMonth), nameof(ReleasesThisMonth),
                     nameof(HasLowStock), nameof(OutstandingSummary), nameof(MonthLabel)
                 })
        {
            RaisePropertyChanged(property);
        }
    });
}
