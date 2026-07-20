using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class StockMovementHistoryViewModel : ManagementViewModelBase
{
    private readonly IInventoryService _inventory;
    private readonly IStockLookupService _lookups;
    private LookupOption<Guid?>? _selectedFormType;
    private LookupOption<StockMovementType?>? _selectedMovementType;
    private string? _serialNumberText;
    private DateTime? _fromDate;
    private DateTime? _toDate;

    public StockMovementHistoryViewModel(IInventoryService inventory, IStockLookupService lookups)
    { _inventory = inventory; _lookups = lookups; SearchCommand = new AsyncRelayCommand(SearchAsync); }
    public ObservableCollection<StockMovementDto> Items { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> FormTypes { get; } = [];
    public ObservableCollection<LookupOption<StockMovementType?>> MovementTypes { get; } = [];
    public ICommand SearchCommand { get; }
    public LookupOption<Guid?>? SelectedFormType { get => _selectedFormType; set => SetProperty(ref _selectedFormType, value); }
    public LookupOption<StockMovementType?>? SelectedMovementType { get => _selectedMovementType; set => SetProperty(ref _selectedMovementType, value); }
    public string? SerialNumberText { get => _serialNumberText; set => SetProperty(ref _serialNumberText, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public async Task InitializeAsync()
    {
        if (FormTypes.Count == 0)
        {
            FormTypes.Add(new(null, "All form types")); foreach (var item in await _lookups.ListFormTypesAsync()) FormTypes.Add(new(item.Id, $"{item.Code} - {item.Name}")); SelectedFormType = FormTypes[0];
            MovementTypes.Add(new(null, "All movement types")); foreach (var type in Enum.GetValues<StockMovementType>()) MovementTypes.Add(new(type, type.ToString())); SelectedMovementType = MovementTypes[0];
        }
        await SearchAsync();
    }
    private async Task SearchAsync() => await RunAsync(async () =>
    {
        long? serial = long.TryParse(SerialNumberText, out var parsed) ? parsed : null;
        var results = await _inventory.SearchMovementsAsync(new MovementHistoryFilter(SearchText, SelectedMovementType?.Value, SelectedFormType?.Value,
            FromDate is null ? null : DateOnly.FromDateTime(FromDate.Value), ToDate is null ? null : DateOnly.FromDateTime(ToDate.Value), serial));
        Items.Clear(); foreach (var item in results) Items.Add(item);
    });
}
