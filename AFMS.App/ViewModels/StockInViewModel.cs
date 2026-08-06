using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.App.ViewModels;

/// <summary>Recording what came in: deliveries, transfers, returns, and corrections.</summary>
public sealed class StockInViewModel : PageViewModelBase
{
    private readonly IStockInService _stockIn;
    private readonly IItemService _items;
    private readonly IRecipientService _people;
    private readonly IUserDialogService _dialogs;

    private IReadOnlyList<ItemOption> _itemOptions = [];

    private StockInDto? _selected;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private string _searchText = string.Empty;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;

    private DateTime _entryDate = DateTime.Today;
    private SourceOption _source = SourceOption.All[0];
    private string? _sourceName;
    private string? _documentNumber;
    private RecipientOption? _returnedBy;
    private string? _receivedBy;
    private string? _remarks;

    public StockInViewModel(
        IStockInService stockIn,
        IItemService items,
        IRecipientService people,
        IUserDialogService dialogs)
    {
        _stockIn = stockIn;
        _items = items;
        _people = people;
        _dialogs = dialogs;

        SearchCommand = new AsyncRelayCommand(LoadAsync);
        NewCommand = new AsyncRelayCommand(BeginNewAsync);
        EditCommand = new AsyncRelayCommand(BeginEditAsync, () => Selected?.Status == RecordStatus.Recorded);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen);
        CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        VoidCommand = new AsyncRelayCommand(VoidAsync, () => Selected?.Status == RecordStatus.Recorded);
        AddLineCommand = new RelayCommand(AddLine, () => IsEditorOpen);
        RemoveLineCommand = new RelayCommand(RemoveLine, () => SelectedLine is not null);
    }

    public ObservableCollection<StockInDto> Rows { get; } = [];
    public ObservableCollection<StockLineEditor> Lines { get; } = [];
    public ObservableCollection<RecipientOption> People { get; } = [];
    public IReadOnlyList<SourceOption> Sources => SourceOption.All;

    public ICommand SearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand VoidCommand { get; }
    public ICommand AddLineCommand { get; }
    public ICommand RemoveLineCommand { get; }

    public StockInDto? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            ((AsyncRelayCommand)EditCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)VoidCommand).RaiseCanExecuteChanged();
        }
    }

    private StockLineEditor? _selectedLine;
    public StockLineEditor? SelectedLine
    {
        get => _selectedLine;
        set
        {
            if (SetProperty(ref _selectedLine, value))
                ((RelayCommand)RemoveLineCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        private set
        {
            if (!SetProperty(ref _isEditorOpen, value)) return;
            ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddLineCommand).RaiseCanExecuteChanged();
        }
    }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }

    public DateTime EntryDate { get => _entryDate; set => SetProperty(ref _entryDate, value); }
    public string? SourceName { get => _sourceName; set => SetProperty(ref _sourceName, value); }
    public string? DocumentNumber { get => _documentNumber; set => SetProperty(ref _documentNumber, value); }
    public RecipientOption? ReturnedBy { get => _returnedBy; set => SetProperty(ref _returnedBy, value); }
    public string? ReceivedBy { get => _receivedBy; set => SetProperty(ref _receivedBy, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

    public SourceOption SelectedSource
    {
        get => _source;
        set
        {
            if (!SetProperty(ref _source, value)) return;
            if (!IsReturn) ReturnedBy = null;
            RaisePropertyChanged(nameof(IsReturn));
            RaisePropertyChanged(nameof(SourceNameLabel));
        }
    }

    /// <summary>Only a return names the person handing stock back.</summary>
    public bool IsReturn => SelectedSource.Value == StockInSource.Returned;

    public string SourceNameLabel => SelectedSource.Value switch
    {
        StockInSource.Purchase => "Supplier",
        StockInSource.Transferred => "Supplying office",
        _ => "Source (optional)"
    };

    public string EditorTitle => _editingId is null ? "Record incoming stock" : "Edit incoming stock";

    public long EditorTotal => Lines.Sum(line => line.Quantity);

    public async Task InitializeAsync()
    {
        await RunAsync(async () =>
        {
            await RefreshOptionsAsync();
            await LoadCoreAsync();
        });
    }

    private async Task RefreshOptionsAsync()
    {
        _itemOptions = await _items.ListOptionsAsync();
        var people = await _people.ListOptionsAsync();
        People.Clear();
        foreach (var person in people) People.Add(person);
    }

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var rows = await _stockIn.ListAsync(
            SearchText,
            FilterFrom is { } from ? DateOnly.FromDateTime(from) : null,
            FilterTo is { } to ? DateOnly.FromDateTime(to) : null);

        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);
        Selected = null;
    }

    private async Task BeginNewAsync() => await RunAsync(async () =>
    {
        await RefreshOptionsAsync();

        _editingId = null;
        EntryDate = DateTime.Today;
        SelectedSource = SourceOption.All[0];
        SourceName = DocumentNumber = Remarks = null;
        ReturnedBy = null;
        ReceivedBy = null;
        Lines.Clear();
        AddLine();
        Message = null;
        IsEditorOpen = true;
        RaisePropertyChanged(nameof(EditorTitle));
    });

    private async Task BeginEditAsync()
    {
        if (Selected is not { } row) return;

        await RunAsync(async () =>
        {
            await RefreshOptionsAsync();

            _editingId = row.Id;
            EntryDate = row.EntryDate.ToDateTime(TimeOnly.MinValue);
            SelectedSource = SourceOption.All.First(option => option.Value == row.Source);
            SourceName = row.SourceName;
            DocumentNumber = row.DocumentNumber;
            ReturnedBy = People.FirstOrDefault(person => person.Id == row.ReturnedByRecipientId);
            ReceivedBy = row.ReceivedBy;
            Remarks = row.Remarks;

            Lines.Clear();
            foreach (var line in row.Lines)
            {
                Lines.Add(StockLineEditor.FromLine(
                    _itemOptions, line.ItemId, line.Quantity, line.SerialFrom, line.SerialTo, line.Remarks));
            }

            Message = null;
            IsEditorOpen = true;
            RaisePropertyChanged(nameof(EditorTitle));
        });
    }

    private void AddLine()
    {
        var line = new StockLineEditor(_itemOptions);
        line.PropertyChanged += (_, _) => RaisePropertyChanged(nameof(EditorTotal));
        Lines.Add(line);
        SelectedLine = line;
        RaisePropertyChanged(nameof(EditorTotal));
    }

    private void RemoveLine()
    {
        if (SelectedLine is not { } line) return;
        Lines.Remove(line);
        SelectedLine = Lines.LastOrDefault();
        RaisePropertyChanged(nameof(EditorTotal));
    }

    private async Task SaveAsync()
    {
        var lines = Lines.Where(line => line.Item is not null).ToList();
        if (lines.Count == 0)
        {
            ReportError("Add at least one item before saving.");
            return;
        }

        await RunAsync(async () =>
        {
            var saved = await _stockIn.SaveAsync(new SaveStockInRequest(
                _editingId,
                DateOnly.FromDateTime(EntryDate),
                SelectedSource.Value,
                SourceName,
                DocumentNumber,
                IsReturn ? ReturnedBy?.Id : null,
                ReceivedBy,
                Remarks,
                lines.Select(line => line.ToInput()).ToList()));

            var wasNew = _editingId is null;
            IsEditorOpen = false;
            await RefreshOptionsAsync();
            await LoadCoreAsync();
            Selected = Rows.FirstOrDefault(row => row.Id == saved.Id);
            ReportSuccess(wasNew
                ? $"Recorded as {saved.ReferenceNumber}."
                : $"{saved.ReferenceNumber} updated.");
        });
    }

    private async Task VoidAsync()
    {
        if (Selected is not { } row) return;

        var reason = _dialogs.AskForText(
            $"Why is {row.ReferenceNumber} being voided? The entry stays on file with this note.",
            "Void entry");
        if (string.IsNullOrWhiteSpace(reason)) return;

        await RunAsync(async () =>
        {
            await _stockIn.VoidAsync(row.Id, reason);
            await LoadCoreAsync();
            ReportSuccess($"{row.ReferenceNumber} voided.");
        });
    }
}

/// <summary>Friendly labels for the stock-in source list.</summary>
public sealed record SourceOption(string Label, StockInSource Value)
{
    public static IReadOnlyList<SourceOption> All { get; } =
    [
        new("Purchase or delivery", StockInSource.Purchase),
        new("Transferred from another office", StockInSource.Transferred),
        new("Returned by a recipient", StockInSource.Returned),
        new("Beginning balance", StockInSource.BeginningBalance),
        new("Adjustment after count", StockInSource.Adjustment)
    ];

    public override string ToString() => Label;
}
