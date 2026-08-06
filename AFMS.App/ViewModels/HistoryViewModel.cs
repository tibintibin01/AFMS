using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.App.ViewModels;

/// <summary>The full searchable record of everything in and out.</summary>
public sealed class HistoryViewModel : PageViewModelBase
{
    private readonly IHistoryService _history;
    private readonly IItemService _items;
    private readonly IRecipientService _people;

    private string _searchText = string.Empty;
    private ItemOption? _filterItem;
    private RecipientOption? _filterPerson;
    private DirectionOption _direction = DirectionOption.All[0];
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool _includeVoided;

    public HistoryViewModel(IHistoryService history, IItemService items, IRecipientService people)
    {
        _history = history;
        _items = items;
        _people = people;

        SearchCommand = new AsyncRelayCommand(LoadAsync);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        ThisMonthCommand = new AsyncRelayCommand(() => SetRangeAsync(months: 0));
        LastMonthCommand = new AsyncRelayCommand(() => SetRangeAsync(months: 1));
    }

    public ObservableCollection<HistoryEntry> Rows { get; } = [];
    public ObservableCollection<ItemOption> ItemFilter { get; } = [];
    public ObservableCollection<RecipientOption> PeopleFilter { get; } = [];
    public IReadOnlyList<DirectionOption> Directions => DirectionOption.All;

    public ICommand SearchCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ThisMonthCommand { get; }
    public ICommand LastMonthCommand { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public ItemOption? FilterItem { get => _filterItem; set => SetProperty(ref _filterItem, value); }
    public RecipientOption? FilterPerson { get => _filterPerson; set => SetProperty(ref _filterPerson, value); }
    public DirectionOption SelectedDirection { get => _direction; set => SetProperty(ref _direction, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public bool IncludeVoided { get => _includeVoided; set => SetProperty(ref _includeVoided, value); }

    public long TotalIn => Rows.Sum(row => row.QuantityIn);
    public long TotalOut => Rows.Sum(row => row.QuantityOut);
    public int RowCount => Rows.Count;

    public Task InitializeAsync() => RunAsync(async () =>
    {
        if (ItemFilter.Count == 0)
        {
            ItemFilter.Add(new ItemOption(Guid.Empty, "ALL", "All items", string.Empty, false, 0));
            foreach (var item in await _items.ListOptionsAsync()) ItemFilter.Add(item);
            _filterItem = ItemFilter[0];
            RaisePropertyChanged(nameof(FilterItem));
        }

        if (PeopleFilter.Count == 0)
        {
            PeopleFilter.Add(new RecipientOption(Guid.Empty, "ALL", "Everyone"));
            foreach (var person in await _people.ListOptionsAsync()) PeopleFilter.Add(person);
            _filterPerson = PeopleFilter[0];
            RaisePropertyChanged(nameof(FilterPerson));
        }

        await LoadCoreAsync();
    });

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var rows = await _history.ListAsync(new HistoryFilter(
            SearchText,
            FilterItem is { } item && item.Id != Guid.Empty ? item.Id : null,
            FilterPerson is { } person && person.Id != Guid.Empty ? person.Id : null,
            SelectedDirection.Value,
            FromDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToDate is { } to ? DateOnly.FromDateTime(to) : null,
            IncludeVoided));

        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);

        RaisePropertyChanged(nameof(TotalIn));
        RaisePropertyChanged(nameof(TotalOut));
        RaisePropertyChanged(nameof(RowCount));
    }

    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        FilterItem = ItemFilter.FirstOrDefault();
        FilterPerson = PeopleFilter.FirstOrDefault();
        SelectedDirection = DirectionOption.All[0];
        FromDate = ToDate = null;
        IncludeVoided = false;
        await LoadAsync();
    }

    private async Task SetRangeAsync(int months)
    {
        var anchor = DateTime.Today.AddMonths(-months);
        FromDate = new DateTime(anchor.Year, anchor.Month, 1);
        ToDate = FromDate.Value.AddMonths(1).AddDays(-1);
        await LoadAsync();
    }
}

public sealed record DirectionOption(string Label, MovementDirection? Value)
{
    public static IReadOnlyList<DirectionOption> All { get; } =
    [
        new("In and out", null),
        new("Incoming only", MovementDirection.In),
        new("Released only", MovementDirection.Out)
    ];

    public override string ToString() => Label;
}
