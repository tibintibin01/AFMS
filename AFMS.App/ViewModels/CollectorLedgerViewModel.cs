using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;

namespace AFMS.App.ViewModels;

public sealed class CollectorLedgerViewModel : ManagementViewModelBase
{
    private readonly ICollectorAccountabilityService _ledger;
    private readonly IStockLookupService _lookups;
    private LookupOption<Guid>? _collector;
    private LookupOption<Guid?>? _formType;
    private LookupOption<Guid?>? _barangay;
    private CollectorAccountabilityDetailDto? _detail;
    private string? _monthText;
    private string? _yearText;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool _outstandingOnly;
    public CollectorLedgerViewModel(ICollectorAccountabilityService ledger, IStockLookupService lookups)
    { _ledger = ledger; _lookups = lookups; SearchCommand = new AsyncRelayCommand(SearchAsync); }
    public ObservableCollection<CollectorLedgerEntryDto> Entries { get; } = [];
    public ObservableCollection<LookupOption<Guid>> Collectors { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> FormTypes { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> Barangays { get; } = [];
    public ICommand SearchCommand { get; }
    public LookupOption<Guid>? SelectedCollector { get => _collector; set => SetProperty(ref _collector, value); }
    public LookupOption<Guid?>? SelectedFormType { get => _formType; set => SetProperty(ref _formType, value); }
    public LookupOption<Guid?>? SelectedBarangay { get => _barangay; set => SetProperty(ref _barangay, value); }
    public CollectorAccountabilityDetailDto? Detail { get => _detail; private set => SetProperty(ref _detail, value); }
    public string? MonthText { get => _monthText; set => SetProperty(ref _monthText, value); }
    public string? YearText { get => _yearText; set => SetProperty(ref _yearText, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public bool OutstandingOnly { get => _outstandingOnly; set => SetProperty(ref _outstandingOnly, value); }
    public async Task InitializeAsync()
    {
        if (Collectors.Count == 0)
        {
            foreach (var item in await _lookups.ListCollectorsAsync()) Collectors.Add(new(item.Id, item.Name)); SelectedCollector = Collectors.FirstOrDefault();
            FormTypes.Add(new(null, "All form types")); foreach (var item in await _lookups.ListFormTypesAsync()) FormTypes.Add(new(item.Id, $"{item.Code} - {item.Name}")); SelectedFormType = FormTypes[0];
            Barangays.Add(new(null, "All barangays")); foreach (var item in await _lookups.ListBarangaysAsync()) Barangays.Add(new(item.Id, item.Name)); SelectedBarangay = Barangays[0];
        }
        await SearchAsync();
    }
    private async Task SearchAsync() => await RunAsync(async () =>
    {
        if (SelectedCollector is null) { Entries.Clear(); Detail = null; return; }
        int? month = int.TryParse(MonthText, out var parsedMonth) && parsedMonth is >= 1 and <= 12 ? parsedMonth : null;
        int? year = int.TryParse(YearText, out var parsedYear) ? parsedYear : null;
        var filter = new CollectorLedgerFilter(SelectedCollector.Value, SelectedFormType?.Value, SelectedBarangay?.Value, month, year,
            FromDate is null ? null : DateOnly.FromDateTime(FromDate.Value), ToDate is null ? null : DateOnly.FromDateTime(ToDate.Value), OutstandingOnly);
        var entries = await _ledger.GetLedgerAsync(filter); Entries.Clear(); foreach (var item in entries) Entries.Add(item);
        Detail = await _ledger.GetCollectorDetailAsync(SelectedCollector.Value);
    });
}
