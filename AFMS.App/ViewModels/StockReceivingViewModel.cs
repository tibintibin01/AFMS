using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class StockReceivingViewModel : ManagementViewModelBase
{
    private readonly IStockReceivingService _receiving;
    private readonly IStockLookupService _lookups;
    private readonly IUserDialogService _dialogs;
    private readonly Dictionary<Guid, int> _defaultBookletSizes = [];
    private readonly bool _canReverse;
    private StockReceiptDto? _selectedReceipt;
    private LookupOption<Guid>? _selectedFormType;
    private ReceivingItemEditor? _selectedItem;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private bool _isReadOnly;
    private string _referenceNumber = "Assigned when draft is saved";
    private DateTime _dateReceived = DateTime.Today;
    private string? _source;
    private string? _sourceDocumentNumber;
    private int _seriesYear = DateTime.Today.Year;
    private string? _storageLocation = "Municipal Treasury Office";
    private string? _receivedBy;
    private string? _remarks;
    private TransactionStatus _editorStatus = TransactionStatus.Draft;

    public StockReceivingViewModel(IStockReceivingService receiving, IStockLookupService lookups, IUserDialogService dialogs, IAuthorizationService authorization)
    {
        _receiving = receiving; _lookups = lookups; _dialogs = dialogs; _canReverse = authorization.HasPermission(SystemPermission.ApproveTransactions);
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SearchCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new RelayCommand(BeginAdd);
        OpenCommand = new AsyncRelayCommand(OpenSelectedAsync, () => SelectedReceipt is not null);
        EditCommand = new AsyncRelayCommand(EditSelectedAsync, () => SelectedReceipt?.Status == TransactionStatus.Draft);
        AddItemCommand = new RelayCommand(AddItem, () => IsEditorOpen && !IsReadOnly);
        RemoveItemCommand = new RelayCommand(RemoveItem, () => SelectedItem is not null && !IsReadOnly);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen && !IsReadOnly);
        CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => SelectedReceipt?.Status == TransactionStatus.Draft);
        FinalizeCommand = new AsyncRelayCommand(FinalizeAsync, () => SelectedReceipt?.Status == TransactionStatus.Submitted);
        ReverseCommand = new AsyncRelayCommand(ReverseAsync, () => _canReverse && SelectedReceipt?.Status == TransactionStatus.Finalized);
    }

    public ObservableCollection<StockReceiptDto> Receipts { get; } = [];
    public ObservableCollection<LookupOption<Guid>> FormTypes { get; } = [];
    public ObservableCollection<ReceivingItemEditor> Items { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand FinalizeCommand { get; }
    public ICommand ReverseCommand { get; }

    public StockReceiptDto? SelectedReceipt
    {
        get => _selectedReceipt;
        set
        {
            if (SetProperty(ref _selectedReceipt, value)) RaiseReceiptCommands();
        }
    }
    public LookupOption<Guid>? SelectedFormType { get => _selectedFormType; set => SetProperty(ref _selectedFormType, value); }
    public ReceivingItemEditor? SelectedItem { get => _selectedItem; set { if (SetProperty(ref _selectedItem, value)) ((RelayCommand)RemoveItemCommand).RaiseCanExecuteChanged(); } }
    public bool IsEditorOpen { get => _isEditorOpen; private set { if (SetProperty(ref _isEditorOpen, value)) RaiseEditorCommands(); } }
    public bool IsReadOnly { get => _isReadOnly; private set { if (SetProperty(ref _isReadOnly, value)) RaiseEditorCommands(); } }
    public string ReferenceNumber { get => _referenceNumber; private set => SetProperty(ref _referenceNumber, value); }
    public DateTime DateReceived { get => _dateReceived; set => SetProperty(ref _dateReceived, value); }
    public string? Source { get => _source; set => SetProperty(ref _source, value); }
    public string? SourceDocumentNumber { get => _sourceDocumentNumber; set => SetProperty(ref _sourceDocumentNumber, value); }
    public int SeriesYear { get => _seriesYear; set => SetProperty(ref _seriesYear, value); }
    public string? StorageLocation { get => _storageLocation; set => SetProperty(ref _storageLocation, value); }
    public string? ReceivedBy { get => _receivedBy; set => SetProperty(ref _receivedBy, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public TransactionStatus EditorStatus { get => _editorStatus; private set => SetProperty(ref _editorStatus, value); }

    public async Task InitializeAsync()
    {
        if (FormTypes.Count == 0)
        {
            foreach (var form in await _lookups.ListFormTypesAsync(true))
            {
                FormTypes.Add(new LookupOption<Guid>(form.Id, $"{form.Code} - {form.Name}"));
                _defaultBookletSizes[form.Id] = form.ReceiptsPerBooklet;
            }
        }
        await LoadAsync();
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var entries = await _receiving.ListAsync();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            entries = entries.Where(item => item.ReferenceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                            item.FormType.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                            (item.SourceOrIssuingOffice?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
        Receipts.Clear(); foreach (var entry in entries) Receipts.Add(entry);
        SelectedReceipt = null;
    });

    private void BeginAdd()
    {
        _editingId = null; ReferenceNumber = "Assigned when draft is saved"; DateReceived = DateTime.Today;
        SelectedFormType = FormTypes.FirstOrDefault(); Source = null; SourceDocumentNumber = null;
        SeriesYear = DateTime.Today.Year; StorageLocation = "Municipal Treasury Office"; ReceivedBy = null; Remarks = null;
        EditorStatus = TransactionStatus.Draft; Items.Clear(); AddItem(); IsReadOnly = false; IsEditorOpen = true;
    }

    private async Task OpenSelectedAsync() => await OpenAsync(true);
    private async Task EditSelectedAsync() => await OpenAsync(false);

    private async Task OpenAsync(bool readOnly) => await RunAsync(async () =>
    {
        if (SelectedReceipt is not { } selected) return;
        var receipt = await _receiving.GetAsync(selected.Id) ?? throw new InvalidOperationException("Receiving transaction was not found.");
        _editingId = receipt.Id; ReferenceNumber = receipt.ReferenceNumber; DateReceived = receipt.DateReceived.ToDateTime(TimeOnly.MinValue);
        SelectedFormType = FormTypes.FirstOrDefault(item => item.Value == receipt.AccountableFormTypeId);
        Source = receipt.SourceOrIssuingOffice; SourceDocumentNumber = receipt.SourceDocumentNumber; SeriesYear = receipt.SeriesYear;
        StorageLocation = receipt.StorageLocation; ReceivedBy = receipt.ReceivedBy; Remarks = receipt.Remarks; EditorStatus = receipt.Status;
        Items.Clear(); foreach (var item in receipt.Items) Items.Add(new ReceivingItemEditor(item.NumberOfBooklets, item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial));
        IsReadOnly = readOnly || receipt.Status != TransactionStatus.Draft; IsEditorOpen = true;
    });

    private void AddItem()
    {
        var size = SelectedFormType is not null && _defaultBookletSizes.TryGetValue(SelectedFormType.Value, out var configured) ? configured : 50;
        Items.Add(new ReceivingItemEditor(1, size, 0, size - 1));
        SelectedItem = Items[^1];
    }
    private void RemoveItem() { if (SelectedItem is { } item) Items.Remove(item); SelectedItem = null; }

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        var formType = SelectedFormType ?? throw new InvalidOperationException("Select an accountable form type.");
        var saved = await _receiving.SaveDraftAsync(new SaveStockReceiptDraftRequest(_editingId, DateOnly.FromDateTime(DateReceived), formType.Value,
            Source, SourceDocumentNumber, SeriesYear, StorageLocation, ReceivedBy, Remarks,
            Items.Select(item => new StockReceiptItemInput(item.NumberOfBooklets, item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial)).ToList()));
        _editingId = saved.Id; ReferenceNumber = saved.ReferenceNumber; IsEditorOpen = false; await LoadAsync();
    }, "Receiving draft saved.");

    private async Task SubmitAsync()
    {
        if (SelectedReceipt is not { } receipt || !_dialogs.Confirm($"Submit {receipt.ReferenceNumber} for finalization?", "Submit receiving transaction")) return;
        await RunAsync(async () => { await _receiving.SubmitAsync(receipt.Id); await LoadAsync(); }, "Receiving transaction submitted.");
    }

    private async Task FinalizeAsync()
    {
        if (SelectedReceipt is not { } receipt || !_dialogs.Confirm($"Finalize {receipt.ReferenceNumber}? This will generate inventory booklets and cannot be directly edited.", "Finalize stock receiving")) return;
        await RunAsync(async () => { await _receiving.FinalizeAsync(receipt.Id); IsEditorOpen = false; await LoadAsync(); }, "Stock receiving finalized and booklets generated.");
    }

    private async Task ReverseAsync()
    {
        if (SelectedReceipt is not { } receipt || !_dialogs.Confirm($"Reverse {receipt.ReferenceNumber}? Only untouched available booklets can be reversed.", "Reverse receiving transaction")) return;
        await RunAsync(async () => { await _receiving.ReverseAsync(receipt.Id, "Administrator-authorized correction"); IsEditorOpen = false; await LoadAsync(); }, "Receiving transaction reversed.");
    }

    private void RaiseReceiptCommands()
    {
        ((AsyncRelayCommand)OpenCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)EditCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SubmitCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)FinalizeCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ReverseCommand).RaiseCanExecuteChanged();
    }
    private void RaiseEditorCommands()
    {
        ((RelayCommand)AddItemCommand).RaiseCanExecuteChanged(); ((RelayCommand)RemoveItemCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
    }
}

public sealed class ReceivingItemEditor : ObservableObject
{
    private int _numberOfBooklets;
    private int _receiptsPerBooklet;
    private long _beginningSerial;
    private long _endingSerial;
    public ReceivingItemEditor(int numberOfBooklets, int receiptsPerBooklet, long beginningSerial, long endingSerial)
    { _numberOfBooklets = numberOfBooklets; _receiptsPerBooklet = receiptsPerBooklet; _beginningSerial = beginningSerial; _endingSerial = endingSerial; }
    public int NumberOfBooklets { get => _numberOfBooklets; set { if (SetProperty(ref _numberOfBooklets, value)) RaisePropertyChanged(nameof(TotalReceiptQuantity)); } }
    public int ReceiptsPerBooklet { get => _receiptsPerBooklet; set { if (SetProperty(ref _receiptsPerBooklet, value)) RaisePropertyChanged(nameof(TotalReceiptQuantity)); } }
    public long BeginningSerial { get => _beginningSerial; set { if (SetProperty(ref _beginningSerial, value)) RaisePropertyChanged(nameof(TotalReceiptQuantity)); } }
    public long EndingSerial { get => _endingSerial; set { if (SetProperty(ref _endingSerial, value)) RaisePropertyChanged(nameof(TotalReceiptQuantity)); } }
    public long TotalReceiptQuantity => EndingSerial >= BeginningSerial ? EndingSerial - BeginningSerial + 1 : 0;
}
