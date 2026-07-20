using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class OperationalTransactionsViewModel : ManagementViewModelBase
{
    private readonly IOperationalTransactionService _service; private readonly IStockLookupService _lookups; private readonly IUserDialogService _dialogs; private readonly ITransactionAttachmentService _attachments;
    private LookupOption<Guid>? _sourceCollector, _targetCollector; private OwnedRangeDto? _selectedRange;
    private long? _beginningSerial, _endingSerial; private string _reason = string.Empty, _person = string.Empty, _remarks = string.Empty;
    private DateTime _transactionDate = DateTime.Today; private ReturnDisposition _disposition = ReturnDisposition.ReturnedToAvailableStock;
    private ExceptionType _exceptionType = ExceptionType.Spoiled; private decimal? _amountCollected;
    private UsageReportDto? _selectedUsage; private TransferDto? _selectedTransfer; private ReturnDto? _selectedReturn; private FormExceptionDto? _selectedException;

    public OperationalTransactionsViewModel(IOperationalTransactionService service, IStockLookupService lookups, IUserDialogService dialogs, ICurrentUserSession session, IAuthorizationService authorization, ITransactionAttachmentService attachments)
    {
        _service = service; _lookups = lookups; _dialogs = dialogs; _attachments = attachments; _person = session.User?.DisplayName ?? string.Empty;
        CanUsage = authorization.HasPermission(SystemPermission.RecordUsage); CanTransfer = authorization.HasPermission(SystemPermission.TransferStock);
        CanReturn = authorization.HasPermission(SystemPermission.ReturnStock); CanExceptions = authorization.HasPermission(SystemPermission.ManageExceptions);
        CanApprove = authorization.HasPermission(SystemPermission.ApproveTransactions);
        CanReverse = authorization.HasPermission(SystemPermission.ReverseOperationalTransactions);
        RefreshCommand = new AsyncRelayCommand(LoadAsync); RefreshRangesCommand = new AsyncRelayCommand(LoadRangesAsync);
        AddUsageRangeCommand = new RelayCommand(AddUsageRange); ClearUsageRangesCommand = new RelayCommand(() => PendingUsageRanges.Clear());
        SaveUsageCommand = new AsyncRelayCommand(SaveUsageAsync, () => CanUsage); FinalizeUsageCommand = new AsyncRelayCommand(FinalizeUsageAsync, () => CanUsage && SelectedUsage?.Status == TransactionStatus.Draft); ReverseUsageCommand = new AsyncRelayCommand(ReverseUsageAsync, () => CanReverse && SelectedUsage?.Status == TransactionStatus.Finalized && HasDecisionReason());
        SaveTransferCommand = new AsyncRelayCommand(SaveTransferAsync, () => CanTransfer); SubmitTransferCommand = new AsyncRelayCommand(SubmitTransferAsync, () => CanTransfer && SelectedTransfer?.Status == TransactionStatus.Draft); ApproveTransferCommand = new AsyncRelayCommand(ApproveTransferAsync, () => CanApprove && SelectedTransfer?.Status == TransactionStatus.Submitted); RejectTransferCommand = new AsyncRelayCommand(RejectTransferAsync, () => CanApprove && SelectedTransfer?.Status == TransactionStatus.Submitted && HasDecisionReason()); FinalizeTransferCommand = new AsyncRelayCommand(FinalizeTransferAsync, () => CanTransfer && SelectedTransfer?.Status == TransactionStatus.Approved); ReverseTransferCommand = new AsyncRelayCommand(ReverseTransferAsync, () => CanReverse && SelectedTransfer?.Status == TransactionStatus.Finalized && HasDecisionReason());
        SaveReturnCommand = new AsyncRelayCommand(SaveReturnAsync, () => CanReturn); SubmitReturnCommand = new AsyncRelayCommand(SubmitReturnAsync, () => CanReturn && SelectedReturn?.Status == TransactionStatus.Draft); ApproveReturnCommand = new AsyncRelayCommand(ApproveReturnAsync, () => CanApprove && SelectedReturn?.Status == TransactionStatus.Submitted); RejectReturnCommand = new AsyncRelayCommand(RejectReturnAsync, () => CanApprove && SelectedReturn?.Status == TransactionStatus.Submitted && HasDecisionReason()); FinalizeReturnCommand = new AsyncRelayCommand(FinalizeReturnAsync, () => CanReturn && SelectedReturn?.Status == TransactionStatus.Approved); ReverseReturnCommand = new AsyncRelayCommand(ReverseReturnAsync, () => CanReverse && SelectedReturn?.Status == TransactionStatus.Finalized && HasDecisionReason());
        SaveExceptionCommand = new AsyncRelayCommand(SaveExceptionAsync, () => CanExceptions); SubmitExceptionCommand = new AsyncRelayCommand(SubmitExceptionAsync, () => CanExceptions && SelectedException?.Status == TransactionStatus.Draft); ApproveExceptionCommand = new AsyncRelayCommand(ApproveExceptionAsync, () => CanApprove && SelectedException?.Status == TransactionStatus.Submitted); RejectExceptionCommand = new AsyncRelayCommand(RejectExceptionAsync, () => CanApprove && SelectedException?.Status == TransactionStatus.Submitted && HasDecisionReason()); FinalizeExceptionCommand = new AsyncRelayCommand(FinalizeExceptionAsync, () => CanExceptions && SelectedException?.Status == TransactionStatus.Approved); ReverseExceptionCommand = new AsyncRelayCommand(ReverseExceptionAsync, () => CanReverse && SelectedException?.Status == TransactionStatus.Finalized && HasDecisionReason());
        AttachTransferCommand = new AsyncRelayCommand(() => AttachAsync(AttachmentTransactionType.Transfer, SelectedTransfer?.Id), () => CanTransfer && SelectedTransfer is not null); AttachReturnCommand = new AsyncRelayCommand(() => AttachAsync(AttachmentTransactionType.Return, SelectedReturn?.Id), () => CanReturn && SelectedReturn is not null); AttachExceptionCommand = new AsyncRelayCommand(() => AttachAsync(AttachmentTransactionType.FormException, SelectedException?.Id), () => CanExceptions && SelectedException is not null);
    }

    public ObservableCollection<LookupOption<Guid>> Collectors { get; } = []; public ObservableCollection<OwnedRangeDto> OwnedRanges { get; } = [];
    public ObservableCollection<UsageRangeInput> PendingUsageRanges { get; } = []; public ObservableCollection<UsageReportDto> UsageReports { get; } = [];
    public ObservableCollection<TransferDto> Transfers { get; } = []; public ObservableCollection<ReturnDto> Returns { get; } = []; public ObservableCollection<FormExceptionDto> Exceptions { get; } = [];
    public Array ReturnDispositions => Enum.GetValues<ReturnDisposition>(); public Array ExceptionTypes => Enum.GetValues<ExceptionType>();
    public bool CanUsage { get; } public bool CanTransfer { get; } public bool CanReturn { get; } public bool CanExceptions { get; }
    public bool CanApprove { get; } public bool CanReverse { get; }
    public ICommand RefreshCommand { get; } public ICommand RefreshRangesCommand { get; } public ICommand AddUsageRangeCommand { get; } public ICommand ClearUsageRangesCommand { get; }
    public ICommand SaveUsageCommand { get; } public ICommand FinalizeUsageCommand { get; } public ICommand ReverseUsageCommand { get; }
    public ICommand SaveTransferCommand { get; } public ICommand SubmitTransferCommand { get; } public ICommand ApproveTransferCommand { get; } public ICommand RejectTransferCommand { get; } public ICommand FinalizeTransferCommand { get; } public ICommand ReverseTransferCommand { get; }
    public ICommand SaveReturnCommand { get; } public ICommand SubmitReturnCommand { get; } public ICommand ApproveReturnCommand { get; } public ICommand RejectReturnCommand { get; } public ICommand FinalizeReturnCommand { get; } public ICommand ReverseReturnCommand { get; }
    public ICommand SaveExceptionCommand { get; } public ICommand SubmitExceptionCommand { get; } public ICommand ApproveExceptionCommand { get; } public ICommand RejectExceptionCommand { get; } public ICommand FinalizeExceptionCommand { get; } public ICommand ReverseExceptionCommand { get; }
    public ICommand AttachTransferCommand { get; } public ICommand AttachReturnCommand { get; } public ICommand AttachExceptionCommand { get; }

    public LookupOption<Guid>? SourceCollector { get => _sourceCollector; set => SetProperty(ref _sourceCollector, value); }
    public LookupOption<Guid>? TargetCollector { get => _targetCollector; set => SetProperty(ref _targetCollector, value); }
    public OwnedRangeDto? SelectedRange { get => _selectedRange; set { if (SetProperty(ref _selectedRange, value) && value is not null) { BeginningSerial = value.BeginningSerial; EndingSerial = value.EndingSerial; } } }
    public long? BeginningSerial { get => _beginningSerial; set => SetProperty(ref _beginningSerial, value); } public long? EndingSerial { get => _endingSerial; set => SetProperty(ref _endingSerial, value); }
    public string Reason { get => _reason; set { if (SetProperty(ref _reason, value)) RaiseActionCommands(); } } public string Person { get => _person; set => SetProperty(ref _person, value); } public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public DateTime TransactionDate { get => _transactionDate; set => SetProperty(ref _transactionDate, value); } public ReturnDisposition Disposition { get => _disposition; set => SetProperty(ref _disposition, value); }
    public ExceptionType ExceptionType { get => _exceptionType; set => SetProperty(ref _exceptionType, value); } public decimal? AmountCollected { get => _amountCollected; set => SetProperty(ref _amountCollected, value); }
    public UsageReportDto? SelectedUsage { get => _selectedUsage; set { if (SetProperty(ref _selectedUsage, value)) RaiseActionCommands(); } } public TransferDto? SelectedTransfer { get => _selectedTransfer; set { if (SetProperty(ref _selectedTransfer, value)) RaiseActionCommands(); } }
    public ReturnDto? SelectedReturn { get => _selectedReturn; set { if (SetProperty(ref _selectedReturn, value)) RaiseActionCommands(); } } public FormExceptionDto? SelectedException { get => _selectedException; set { if (SetProperty(ref _selectedException, value)) RaiseActionCommands(); } }

    public async Task InitializeAsync()
    {
        if (Collectors.Count == 0) foreach (var x in (await _lookups.ListCollectorsAsync()).Where(x => x.IsActive)) Collectors.Add(new(x.Id, x.Name));
        SourceCollector ??= Collectors.FirstOrDefault(); TargetCollector ??= Collectors.Skip(1).FirstOrDefault(); await LoadRangesAsync(); await LoadAsync();
    }
    private async Task LoadAsync() => await RunAsync(async () =>
    {
        if (CanUsage) Replace(UsageReports, await _service.ListUsageAsync()); if (CanTransfer) Replace(Transfers, await _service.ListTransfersAsync()); if (CanReturn) Replace(Returns, await _service.ListReturnsAsync()); if (CanExceptions) Replace(Exceptions, await _service.ListExceptionsAsync());
    });
    private async Task LoadRangesAsync() => await RunAsync(async () => { Replace(OwnedRanges, await _service.ListOwnedRangesAsync(SourceCollector?.Value)); SelectedRange = OwnedRanges.FirstOrDefault(); });
    private void AddUsageRange() { if (SelectedRange is null || BeginningSerial is null || EndingSerial is null) { Message = "Select an owned range and enter serial numbers."; IsError = true; return; } PendingUsageRanges.Add(new(SelectedRange.BookletId, BeginningSerial.Value, EndingSerial.Value)); }
    private async Task SaveUsageAsync() => await RunAsync(async () =>
    {
        if (SourceCollector is null || SelectedRange is null) throw new InvalidOperationException("Select the collector and accountable-form range.");
        if (PendingUsageRanges.Count == 0) AddUsageRange(); if (PendingUsageRanges.Count == 0) return;
        await _service.SaveUsageDraftAsync(new(null, SourceCollector.Value, SelectedRange.BookletId == Guid.Empty ? Guid.Empty : await FormTypeId(), DateOnly.FromDateTime(TransactionDate), DateOnly.FromDateTime(TransactionDate), DateOnly.FromDateTime(TransactionDate), AmountCollected, Reason, Remarks, PendingUsageRanges.ToList()));
        PendingUsageRanges.Clear(); await LoadAsync();
    }, "Usage report draft saved.");
    private async Task<Guid> FormTypeId() { var rows = await _lookups.ListFormTypesAsync(); return rows.Single(x => x.Code == SelectedRange!.FormCode).Id; }
    private async Task FinalizeUsageAsync() { if (SelectedUsage is null || !_dialogs.Confirm("Finalize selected usage report?", "Finalize usage")) return; await RunAsync(async () => { await _service.FinalizeUsageAsync(SelectedUsage.Id); await LoadRangesAsync(); await LoadAsync(); }, "Usage finalized."); }
    private async Task ReverseUsageAsync() { if (SelectedUsage is null || !_dialogs.Confirm("Reverse selected usage report? The reason field is required.", "Reverse usage")) return; await RunAsync(async () => { await _service.ReverseUsageAsync(SelectedUsage.Id, Reason); await LoadRangesAsync(); await LoadAsync(); }, "Usage reversed."); }

    private async Task SaveTransferAsync() => await RunAsync(async () => { var s = RequireSelection(); if (TargetCollector is null) throw new InvalidOperationException("Select the receiving collector."); await _service.SaveTransferDraftAsync(new(null, DateOnly.FromDateTime(TransactionDate), SourceCollector!.Value, TargetCollector.Value, null, s.BookletId, BeginningSerial!.Value, EndingSerial!.Value, Reason, Person, TargetCollector.Label, Remarks)); await LoadAsync(); }, "Transfer draft saved.");
    private Task SubmitTransferAsync() => ExecuteTransferAsync("Submit", item => _service.SubmitTransferAsync(item.Id), "Transfer submitted.");
    private Task ApproveTransferAsync() => ExecuteTransferAsync("Approve", item => _service.ApproveTransferAsync(item.Id), "Transfer approved.");
    private Task RejectTransferAsync() => ExecuteTransferAsync("Reject", item => _service.RejectTransferAsync(item.Id, Reason), "Transfer rejected.");
    private Task FinalizeTransferAsync() => ExecuteTransferAsync("Finalize", item => _service.FinalizeTransferAsync(item.Id), "Transfer finalized.");
    private Task ReverseTransferAsync() => ExecuteTransferAsync("Reverse", item => _service.ReverseTransferAsync(item.Id, Reason), "Transfer reversed.");
    private async Task ExecuteTransferAsync(string action, Func<TransferDto, Task> operation, string success)
    { if (SelectedTransfer is not { } selected || !_dialogs.Confirm($"{action} transfer {selected.ReferenceNumber}?", $"{action} transfer")) return; await RunAsync(async () => { await operation(selected); await LoadRangesAsync(); await LoadAsync(); }, success); }
    private async Task SaveReturnAsync() => await RunAsync(async () => { var s = RequireSelection(); await _service.SaveReturnDraftAsync(new(null, DateOnly.FromDateTime(TransactionDate), SourceCollector!.Value, s.BookletId, BeginningSerial!.Value, EndingSerial!.Value, "Reported physical condition", Reason, Person, Disposition, Remarks)); await LoadAsync(); }, "Return draft saved.");
    private Task SubmitReturnAsync() => ExecuteReturnAsync("Submit", item => _service.SubmitReturnAsync(item.Id), "Return submitted.");
    private Task ApproveReturnAsync() => ExecuteReturnAsync("Approve", item => _service.ApproveReturnAsync(item.Id), "Return approved.");
    private Task RejectReturnAsync() => ExecuteReturnAsync("Reject", item => _service.RejectReturnAsync(item.Id, Reason), "Return rejected.");
    private Task FinalizeReturnAsync() => ExecuteReturnAsync("Finalize", item => _service.FinalizeReturnAsync(item.Id), "Return finalized.");
    private Task ReverseReturnAsync() => ExecuteReturnAsync("Reverse", item => _service.ReverseReturnAsync(item.Id, Reason), "Return reversed.");
    private async Task ExecuteReturnAsync(string action, Func<ReturnDto, Task> operation, string success)
    { if (SelectedReturn is not { } selected || !_dialogs.Confirm($"{action} return {selected.ReferenceNumber}?", $"{action} return")) return; await RunAsync(async () => { await operation(selected); await LoadRangesAsync(); await LoadAsync(); }, success); }
    private async Task SaveExceptionAsync() => await RunAsync(async () => { var s = RequireSelection(); await _service.SaveExceptionDraftAsync(new(null, DateOnly.FromDateTime(TransactionDate), ExceptionType, SourceCollector!.Value, s.BookletId, BeginningSerial!.Value, EndingSerial!.Value, Reason, null, Person, Remarks)); await LoadAsync(); }, "Exception draft saved.");
    private Task SubmitExceptionAsync() => ExecuteExceptionAsync("Submit", item => _service.SubmitExceptionAsync(item.Id), "Exception submitted.");
    private Task ApproveExceptionAsync() => ExecuteExceptionAsync("Approve", item => _service.ApproveExceptionAsync(item.Id), "Exception approved.");
    private Task RejectExceptionAsync() => ExecuteExceptionAsync("Reject", item => _service.RejectExceptionAsync(item.Id, Reason), "Exception rejected.");
    private Task FinalizeExceptionAsync() => ExecuteExceptionAsync("Finalize", item => _service.FinalizeExceptionAsync(item.Id), "Exception finalized.");
    private Task ReverseExceptionAsync() => ExecuteExceptionAsync("Reverse", item => _service.ReverseExceptionAsync(item.Id, Reason), "Exception reversed.");
    private async Task ExecuteExceptionAsync(string action, Func<FormExceptionDto, Task> operation, string success)
    { if (SelectedException is not { } selected || !_dialogs.Confirm($"{action} exception {selected.ReferenceNumber}?", $"{action} exception")) return; await RunAsync(async () => { await operation(selected); await LoadRangesAsync(); await LoadAsync(); }, success); }
    private async Task AttachAsync(AttachmentTransactionType type, Guid? transactionId)
    {
        if (transactionId is null) { IsError = true; Message = "Select a saved transaction before adding an attachment."; return; }
        var path = _dialogs.SelectAttachment(); if (path is null) return;
        await RunAsync(async () => { await _attachments.AddAsync(new(type, transactionId.Value, path)); }, "Supporting attachment stored securely in AFMS application data.");
    }
    private OwnedRangeDto RequireSelection() { if (SourceCollector is null || SelectedRange is null || BeginningSerial is null || EndingSerial is null) throw new InvalidOperationException("Select a collector, owned range, and serial range."); return SelectedRange; }
    private bool HasDecisionReason() => !string.IsNullOrWhiteSpace(Reason);
    private void RaiseActionCommands()
    {
        foreach (var command in new[] { FinalizeUsageCommand, ReverseUsageCommand, SubmitTransferCommand, ApproveTransferCommand,
                     RejectTransferCommand, FinalizeTransferCommand, ReverseTransferCommand, SubmitReturnCommand, ApproveReturnCommand,
                     RejectReturnCommand, FinalizeReturnCommand, ReverseReturnCommand, SubmitExceptionCommand, ApproveExceptionCommand,
                     RejectExceptionCommand, FinalizeExceptionCommand, ReverseExceptionCommand, AttachTransferCommand, AttachReturnCommand, AttachExceptionCommand })
            ((AsyncRelayCommand)command).RaiseCanExecuteChanged();
    }
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var x in values) target.Add(x); }
}
