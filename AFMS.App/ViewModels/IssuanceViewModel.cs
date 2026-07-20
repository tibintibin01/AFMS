using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class IssuanceViewModel : ManagementViewModelBase
{
    private readonly IIssuanceService _service;
    private readonly IStockLookupService _lookups;
    private readonly IIssuanceReceiptService _receipts;
    private readonly IUserDialogService _dialogs;
    private readonly string _currentUserName;
    private readonly bool _canPrepare;
    private readonly bool _canApprove;
    private readonly bool _canReverse;
    private IssuanceDto? _selectedIssuance;
    private LookupOption<Guid>? _selectedCollector;
    private LookupOption<Guid?>? _selectedBarangay;
    private LookupOption<Guid?>? _filterFormType;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private bool _isReadOnly;
    private DateTime _dateIssued = DateTime.Today;
    private string _referenceNumber = "Assigned when draft is saved";
    private string? _collectionArea;
    private string _purpose = "Collection of municipal revenues";
    private string _releasedBy = string.Empty;
    private string _receivedBy = string.Empty;
    private string? _remarks;
    private string _decisionReason = string.Empty;
    private string? _seriesYearFilter;
    private string? _storageFilter;
    private string? _beginningFilter;
    private string? _endingFilter;

    public IssuanceViewModel(IIssuanceService service, IStockLookupService lookups, IIssuanceReceiptService receipts,
        IUserDialogService dialogs, IAuthorizationService authorization, ICurrentUserSession session)
    {
        _service = service; _lookups = lookups; _receipts = receipts; _dialogs = dialogs;
        _currentUserName = session.User?.DisplayName ?? string.Empty;
        _canPrepare = authorization.HasPermission(SystemPermission.IssueStock);
        _canApprove = authorization.HasPermission(SystemPermission.ApproveTransactions);
        _canReverse = authorization.HasPermission(SystemPermission.ReverseIssuance);
        SearchCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(BeginAddAsync, () => _canPrepare);
        ViewCommand = new AsyncRelayCommand(() => OpenAsync(true), () => SelectedIssuance is not null);
        EditCommand = new AsyncRelayCommand(() => OpenAsync(false), () => _canPrepare && SelectedIssuance?.Status == TransactionStatus.Draft);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => _canPrepare && IsEditorOpen && !IsReadOnly);
        CloseCommand = new RelayCommand(() => IsEditorOpen = false);
        LoadAvailableCommand = new AsyncRelayCommand(LoadAvailableAsync, () => IsEditorOpen && !IsReadOnly);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => _canPrepare && SelectedIssuance?.Status == TransactionStatus.Draft);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => _canApprove && SelectedIssuance?.Status == TransactionStatus.Submitted);
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => _canApprove && SelectedIssuance?.Status == TransactionStatus.Submitted);
        FinalizeCommand = new AsyncRelayCommand(FinalizeAsync, () => _canPrepare && SelectedIssuance?.Status == TransactionStatus.Approved);
        ReverseCommand = new AsyncRelayCommand(ReverseAsync, () => _canReverse && SelectedIssuance?.Status == TransactionStatus.Finalized);
        PreviewCommand = new AsyncRelayCommand(() => CreateDocumentAsync(true), CanPrint);
        ExportCommand = new AsyncRelayCommand(() => CreateDocumentAsync(false), CanPrint);
    }

    public ObservableCollection<IssuanceDto> Issuances { get; } = [];
    public ObservableCollection<LookupOption<Guid>> Collectors { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> Barangays { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> FormTypes { get; } = [];
    public ObservableCollection<IssuableBookletRow> AvailableBooklets { get; } = [];
    public ICommand SearchCommand { get; } public ICommand AddCommand { get; } public ICommand ViewCommand { get; } public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; } public ICommand CloseCommand { get; } public ICommand LoadAvailableCommand { get; }
    public ICommand SubmitCommand { get; } public ICommand ApproveCommand { get; } public ICommand RejectCommand { get; }
    public ICommand FinalizeCommand { get; } public ICommand ReverseCommand { get; } public ICommand PreviewCommand { get; } public ICommand ExportCommand { get; }

    public IssuanceDto? SelectedIssuance { get => _selectedIssuance; set { if (SetProperty(ref _selectedIssuance, value)) RaiseCommands(); } }
    public LookupOption<Guid>? SelectedCollector { get => _selectedCollector; set { if (SetProperty(ref _selectedCollector, value) && string.IsNullOrWhiteSpace(ReceivedBy)) ReceivedBy = value?.Label ?? string.Empty; } }
    public LookupOption<Guid?>? SelectedBarangay { get => _selectedBarangay; set => SetProperty(ref _selectedBarangay, value); }
    public LookupOption<Guid?>? FilterFormType { get => _filterFormType; set => SetProperty(ref _filterFormType, value); }
    public bool IsEditorOpen { get => _isEditorOpen; private set { if (SetProperty(ref _isEditorOpen, value)) RaiseCommands(); } }
    public bool IsReadOnly { get => _isReadOnly; private set { if (SetProperty(ref _isReadOnly, value)) RaiseCommands(); } }
    public DateTime DateIssued { get => _dateIssued; set => SetProperty(ref _dateIssued, value); }
    public string ReferenceNumber { get => _referenceNumber; private set => SetProperty(ref _referenceNumber, value); }
    public string? CollectionArea { get => _collectionArea; set => SetProperty(ref _collectionArea, value); }
    public string Purpose { get => _purpose; set => SetProperty(ref _purpose, value); }
    public string ReleasedBy { get => _releasedBy; set => SetProperty(ref _releasedBy, value); }
    public string ReceivedBy { get => _receivedBy; set => SetProperty(ref _receivedBy, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public string DecisionReason { get => _decisionReason; set => SetProperty(ref _decisionReason, value); }
    public string? SeriesYearFilter { get => _seriesYearFilter; set => SetProperty(ref _seriesYearFilter, value); }
    public string? StorageFilter { get => _storageFilter; set => SetProperty(ref _storageFilter, value); }
    public string? BeginningFilter { get => _beginningFilter; set => SetProperty(ref _beginningFilter, value); }
    public string? EndingFilter { get => _endingFilter; set => SetProperty(ref _endingFilter, value); }

    public async Task InitializeAsync()
    {
        if (Collectors.Count == 0)
        {
            foreach (var item in (await _lookups.ListCollectorsAsync()).Where(item => item.IsActive)) Collectors.Add(new(item.Id, item.Name));
            Barangays.Add(new(null, "No barangay / collection area")); foreach (var item in (await _lookups.ListBarangaysAsync()).Where(item => item.IsActive)) Barangays.Add(new(item.Id, item.Name));
            FormTypes.Add(new(null, "All form types")); foreach (var item in await _lookups.ListFormTypesAsync()) FormTypes.Add(new(item.Id, $"{item.Code} - {item.Name}"));
            FilterFormType = FormTypes[0]; SelectedBarangay = Barangays[0];
        }
        await LoadAsync();
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var rows = await _service.ListAsync();
        if (!string.IsNullOrWhiteSpace(SearchText)) rows = rows.Where(item => item.ReferenceNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || item.Collector.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Issuances.Clear(); foreach (var item in rows) Issuances.Add(item); SelectedIssuance = null;
    });

    private async Task BeginAddAsync()
    {
        _editingId = null; ReferenceNumber = "Assigned when draft is saved"; DateIssued = DateTime.Today;
        SelectedCollector = Collectors.FirstOrDefault(); SelectedBarangay = Barangays.FirstOrDefault(); CollectionArea = null;
        Purpose = "Collection of municipal revenues"; ReleasedBy = _currentUserName; ReceivedBy = SelectedCollector?.Label ?? string.Empty; Remarks = null; DecisionReason = string.Empty;
        IsReadOnly = false; IsEditorOpen = true; await LoadAvailableAsync();
    }

    private async Task OpenAsync(bool readOnly) => await RunAsync(async () =>
    {
        if (SelectedIssuance is not { } selected) return;
        var item = await _service.GetAsync(selected.Id) ?? throw new InvalidOperationException("Issuance was not found.");
        _editingId = item.Id; ReferenceNumber = item.ReferenceNumber; DateIssued = item.DateIssued.ToDateTime(TimeOnly.MinValue);
        SelectedCollector = Collectors.FirstOrDefault(value => value.Value == item.CollectorId);
        SelectedBarangay = Barangays.FirstOrDefault(value => value.Value == item.BarangayId) ?? Barangays[0]; CollectionArea = item.BarangayOrArea;
        Purpose = item.Purpose; ReleasedBy = item.ReleasedBy; ReceivedBy = item.ReceivedBy; Remarks = item.Remarks; DecisionReason = item.RejectionReason ?? string.Empty;
        IsReadOnly = readOnly || item.Status != TransactionStatus.Draft; IsEditorOpen = true;
        await LoadAvailableAsync(item.Items.Select(value => (value.BookletId, value.BeginningSerial, value.EndingSerial)).ToHashSet());
    });

    private async Task LoadAvailableAsync() => await LoadAvailableAsync(new HashSet<(Guid BookletId, long BeginningSerial, long EndingSerial)>());
    private async Task LoadAvailableAsync(IReadOnlySet<(Guid BookletId, long BeginningSerial, long EndingSerial)> selected) => await RunAsync(async () =>
    {
        int? year = int.TryParse(SeriesYearFilter, out var parsedYear) ? parsedYear : null;
        long? beginning = long.TryParse(BeginningFilter, out var parsedBeginning) ? parsedBeginning : null;
        long? ending = long.TryParse(EndingFilter, out var parsedEnding) ? parsedEnding : null;
        var rows = await _service.ListAvailableBookletsAsync(new(FilterFormType?.Value, year, StorageFilter, beginning, ending), _editingId);
        AvailableBooklets.Clear(); foreach (var row in rows) AvailableBooklets.Add(new(row, selected.Contains((row.BookletId, row.BeginningSerial, row.EndingSerial))));
    });

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        var collector = SelectedCollector ?? throw new InvalidOperationException("Select a collector.");
        var saved = await _service.SaveDraftAsync(new(_editingId, DateOnly.FromDateTime(DateIssued), collector.Value, SelectedBarangay?.Value,
            CollectionArea, Purpose, ReleasedBy, ReceivedBy, Remarks, AvailableBooklets.Where(item => item.IsSelected).Select(item => item.Id).ToList()));
        _editingId = saved.Id; IsEditorOpen = false; await LoadAsync();
    }, "Issuance draft saved.");

    private async Task SubmitAsync() => await ExecuteStatusAsync("Submit", item => _service.SubmitAsync(item.Id), "Issuance submitted.");
    private async Task ApproveAsync() => await ExecuteStatusAsync("Approve", item => _service.ApproveAsync(item.Id), "Issuance approved.");
    private async Task RejectAsync() => await ExecuteStatusAsync("Reject", item => _service.RejectAsync(item.Id, DecisionReason), "Issuance rejected.");
    private async Task FinalizeAsync() => await ExecuteStatusAsync("Finalize", item => _service.FinalizeAsync(item.Id), "Issuance finalized and collector accountability updated.");
    private async Task ReverseAsync() => await ExecuteStatusAsync("Reverse", item => _service.ReverseAsync(item.Id, DecisionReason), "Issuance reversed.");

    private async Task ExecuteStatusAsync(string action, Func<IssuanceDto, Task> operation, string success)
    {
        if (SelectedIssuance is not { } item || !_dialogs.Confirm($"{action} issuance {item.ReferenceNumber}?", $"{action} issuance")) return;
        await RunAsync(async () => { await operation(item); IsEditorOpen = false; await LoadAsync(); }, success);
    }

    private bool CanPrint() => SelectedIssuance?.Status is TransactionStatus.Approved or TransactionStatus.Finalized or TransactionStatus.Reversed;
    private async Task CreateDocumentAsync(bool preview) => await RunAsync(async () =>
    {
        if (SelectedIssuance is not { } item) return;
        var path = await _receipts.GeneratePdfAsync(item.Id, forPrintPreview: preview);
        if (preview) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        Message = preview ? "Print preview opened in the default PDF viewer." : $"PDF exported to {path}";
    });

    private void RaiseCommands()
    {
        foreach (var command in new[] { ViewCommand, EditCommand, SaveCommand, LoadAvailableCommand, SubmitCommand, ApproveCommand, RejectCommand, FinalizeCommand, ReverseCommand, PreviewCommand, ExportCommand }.OfType<AsyncRelayCommand>()) command.RaiseCanExecuteChanged();
    }
}

public sealed class IssuableBookletRow : ObservableObject
{
    private bool _isSelected;
    public IssuableBookletRow(AvailableBookletDto item, bool selected) { Item = item; _isSelected = selected; }
    public AvailableBookletDto Item { get; }
    public Guid Id => Item.Id; public Guid BookletId => Item.BookletId; public string FormType => Item.FormType; public int SeriesYear => Item.SeriesYear; public int BookletNumber => Item.BookletNumber;
    public long BeginningSerial => Item.BeginningSerial; public long EndingSerial => Item.EndingSerial; public long Quantity => Item.Quantity; public string? StorageLocation => Item.StorageLocation;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}
