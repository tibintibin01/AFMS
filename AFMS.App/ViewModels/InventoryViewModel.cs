using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class InventoryViewModel : ManagementViewModelBase
{
    private readonly IInventoryService _inventory;
    private readonly IStockLookupService _lookups;
    private InventoryBookletDto? _selectedBooklet;
    private InventoryDetailDto? _detail;
    private LookupOption<Guid?>? _selectedFormType;
    private LookupOption<Guid?>? _selectedCollector;
    private LookupOption<Guid?>? _selectedBarangay;
    private LookupOption<AccountableFormStatus?>? _selectedInventoryStatus;
    private string? _seriesYearText;
    private string? _serialNumberText;
    private bool _availableOnly;
    private bool _outstandingOnly;
    private DateTime? _receivedFrom;
    private DateTime? _receivedTo;

    public InventoryViewModel(IInventoryService inventory, IStockLookupService lookups)
    {
        _inventory = inventory; _lookups = lookups;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ViewDetailsCommand = new AsyncRelayCommand(LoadDetailAsync, () => SelectedBooklet is not null);
    }

    public ObservableCollection<InventoryBookletDto> Items { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> FormTypes { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> Collectors { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> Barangays { get; } = [];
    public ObservableCollection<LookupOption<AccountableFormStatus?>> InventoryStatuses { get; } = [];
    public ICommand SearchCommand { get; }
    public ICommand ViewDetailsCommand { get; }
    public InventoryBookletDto? SelectedBooklet { get => _selectedBooklet; set { if (SetProperty(ref _selectedBooklet, value)) ((AsyncRelayCommand)ViewDetailsCommand).RaiseCanExecuteChanged(); } }
    public InventoryDetailDto? Detail { get => _detail; private set => SetProperty(ref _detail, value); }
    public LookupOption<Guid?>? SelectedFormType { get => _selectedFormType; set => SetProperty(ref _selectedFormType, value); }
    public LookupOption<Guid?>? SelectedCollector { get => _selectedCollector; set => SetProperty(ref _selectedCollector, value); }
    public LookupOption<Guid?>? SelectedBarangay { get => _selectedBarangay; set => SetProperty(ref _selectedBarangay, value); }
    public LookupOption<AccountableFormStatus?>? SelectedInventoryStatus { get => _selectedInventoryStatus; set => SetProperty(ref _selectedInventoryStatus, value); }
    public string? SeriesYearText { get => _seriesYearText; set => SetProperty(ref _seriesYearText, value); }
    public string? SerialNumberText { get => _serialNumberText; set => SetProperty(ref _serialNumberText, value); }
    public bool AvailableOnly { get => _availableOnly; set => SetProperty(ref _availableOnly, value); }
    public bool OutstandingOnly { get => _outstandingOnly; set => SetProperty(ref _outstandingOnly, value); }
    public DateTime? ReceivedFrom { get => _receivedFrom; set => SetProperty(ref _receivedFrom, value); }
    public DateTime? ReceivedTo { get => _receivedTo; set => SetProperty(ref _receivedTo, value); }

    public async Task InitializeAsync()
    {
        if (FormTypes.Count == 0)
        {
            FormTypes.Add(new(null, "All form types")); foreach (var item in await _lookups.ListFormTypesAsync()) FormTypes.Add(new(item.Id, $"{item.Code} - {item.Name}")); SelectedFormType = FormTypes[0];
            Collectors.Add(new(null, "All collectors")); foreach (var item in await _lookups.ListCollectorsAsync()) Collectors.Add(new(item.Id, item.Name)); SelectedCollector = Collectors[0];
            Barangays.Add(new(null, "All barangays")); foreach (var item in await _lookups.ListBarangaysAsync()) Barangays.Add(new(item.Id, item.Name)); SelectedBarangay = Barangays[0];
            InventoryStatuses.Add(new(null, "All statuses")); foreach (var status in Enum.GetValues<AccountableFormStatus>()) InventoryStatuses.Add(new(status, status.ToString())); SelectedInventoryStatus = InventoryStatuses[0];
        }
        await SearchAsync();
    }

    private async Task SearchAsync() => await RunAsync(async () =>
    {
        int? year = int.TryParse(SeriesYearText, out var parsedYear) ? parsedYear : null;
        long? serial = long.TryParse(SerialNumberText, out var parsedSerial) ? parsedSerial : null;
        var results = await _inventory.SearchAsync(new InventoryFilter(SearchText, SelectedFormType?.Value, year, SelectedInventoryStatus?.Value,
            SelectedCollector?.Value, SelectedBarangay?.Value,
            ReceivedFrom is null ? null : DateOnly.FromDateTime(ReceivedFrom.Value),
            ReceivedTo is null ? null : DateOnly.FromDateTime(ReceivedTo.Value), serial, AvailableOnly, OutstandingOnly));
        Items.Clear(); foreach (var item in results) Items.Add(item); SelectedBooklet = null; Detail = null;
    });

    private async Task LoadDetailAsync() => await RunAsync(async () =>
    {
        Detail = SelectedBooklet is null ? null : await _inventory.GetDetailAsync(SelectedBooklet.Id);
    });
}
