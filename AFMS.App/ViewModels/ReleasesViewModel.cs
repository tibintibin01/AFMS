using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.App.ViewModels;

/// <summary>
/// Recording what went out and to whom. This is the screen that warns before the
/// same receipt serial goes out twice.
/// </summary>
public sealed class ReleasesViewModel : PageViewModelBase
{
    private readonly IReleaseService _releases;
    private readonly IItemService _items;
    private readonly IRecipientService _people;
    private readonly IReportService _reports;
    private readonly IUserDialogService _dialogs;
    private readonly ICurrentUser _session;

    private IReadOnlyList<ItemOption> _itemOptions = [];

    private ReleaseDto? _selected;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private string _searchText = string.Empty;
    private RecipientOption? _filterPerson;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;

    private DateTime _releaseDate = DateTime.Today;
    private RecipientOption? _recipient;
    private string? _purpose;
    private string _releasedBy = string.Empty;
    private string _receivedBy = string.Empty;
    private string? _remarks;

    public ReleasesViewModel(
        IReleaseService releases,
        IItemService items,
        IRecipientService people,
        IReportService reports,
        IUserDialogService dialogs,
        ICurrentUser session)
    {
        _releases = releases;
        _items = items;
        _people = people;
        _reports = reports;
        _dialogs = dialogs;
        _session = session;

        SearchCommand = new AsyncRelayCommand(LoadAsync);
        NewCommand = new AsyncRelayCommand(BeginNewAsync);
        EditCommand = new AsyncRelayCommand(BeginEditAsync, () => Selected?.Status == RecordStatus.Recorded);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen);
        CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        VoidCommand = new AsyncRelayCommand(VoidAsync, () => Selected?.Status == RecordStatus.Recorded);
        PrintSlipCommand = new AsyncRelayCommand(PrintSlipAsync, () => Selected is not null);
        AddLineCommand = new RelayCommand(AddLine, () => IsEditorOpen);
        RemoveLineCommand = new RelayCommand(RemoveLine, () => SelectedLine is not null);
    }

    public ObservableCollection<ReleaseDto> Rows { get; } = [];
    public ObservableCollection<StockLineEditor> Lines { get; } = [];
    public ObservableCollection<RecipientOption> People { get; } = [];
    public ObservableCollection<RecipientOption> PeopleFilter { get; } = [];

    public ICommand SearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand VoidCommand { get; }
    public ICommand PrintSlipCommand { get; }
    public ICommand AddLineCommand { get; }
    public ICommand RemoveLineCommand { get; }

    public ReleaseDto? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            ((AsyncRelayCommand)EditCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)VoidCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)PrintSlipCommand).RaiseCanExecuteChanged();
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
    public RecipientOption? FilterPerson { get => _filterPerson; set => SetProperty(ref _filterPerson, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }

    public DateTime ReleaseDate { get => _releaseDate; set => SetProperty(ref _releaseDate, value); }
    public RecipientOption? Recipient { get => _recipient; set => SetProperty(ref _recipient, value); }
    public string? Purpose { get => _purpose; set => SetProperty(ref _purpose, value); }
    public string ReleasedBy { get => _releasedBy; set => SetProperty(ref _releasedBy, value); }
    public string ReceivedBy { get => _receivedBy; set => SetProperty(ref _receivedBy, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

    public string EditorTitle => _editingId is null ? "Release items" : "Edit release";

    public long EditorTotal => Lines.Sum(line => line.Quantity);

    /// <summary>True when any line is flagged, so the form can show a single clear notice.</summary>
    public bool HasSerialWarnings => Lines.Any(line => line.HasWarning);

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

        PeopleFilter.Clear();
        PeopleFilter.Add(new RecipientOption(Guid.Empty, "ALL", "Everyone"));
        foreach (var person in people) PeopleFilter.Add(person);
        FilterPerson ??= PeopleFilter[0];
    }

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var rows = await _releases.ListAsync(
            SearchText,
            FilterPerson is { } person && person.Id != Guid.Empty ? person.Id : null,
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
        ReleaseDate = DateTime.Today;
        Recipient = null;
        Purpose = Remarks = null;

        // The person signed in is almost always the one handing items over.
        ReleasedBy = _session.User?.DisplayName ?? string.Empty;
        ReceivedBy = string.Empty;

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
            ReleaseDate = row.ReleaseDate.ToDateTime(TimeOnly.MinValue);
            Recipient = People.FirstOrDefault(person => person.Id == row.RecipientId);
            Purpose = row.Purpose;
            ReleasedBy = row.ReleasedBy;
            ReceivedBy = row.ReceivedBy;
            Remarks = row.Remarks;

            Lines.Clear();
            foreach (var line in row.Lines)
            {
                var editor = StockLineEditor.FromLine(
                    _itemOptions, line.ItemId, line.Quantity, line.SerialFrom, line.SerialTo, line.Remarks,
                    CheckSerialsAsync);
                Track(editor);
                Lines.Add(editor);
            }

            Message = null;
            IsEditorOpen = true;
            RaisePropertyChanged(nameof(EditorTitle));
        });
    }

    private void AddLine()
    {
        var line = new StockLineEditor(_itemOptions, CheckSerialsAsync);
        Track(line);
        Lines.Add(line);
        SelectedLine = line;
        RaisePropertyChanged(nameof(EditorTotal));
    }

    private void Track(StockLineEditor line)
    {
        line.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(StockLineEditor.Quantity))
                RaisePropertyChanged(nameof(EditorTotal));
            if (args.PropertyName is nameof(StockLineEditor.Warning) or nameof(StockLineEditor.HasWarning))
                RaisePropertyChanged(nameof(HasSerialWarnings));
        };
    }

    private void RemoveLine()
    {
        if (SelectedLine is not { } line) return;
        Lines.Remove(line);
        SelectedLine = Lines.LastOrDefault();
        RaisePropertyChanged(nameof(EditorTotal));
        RaisePropertyChanged(nameof(HasSerialWarnings));
    }

    /// <summary>
    /// Asks the service whether a serial range is still free and shows the answer
    /// straight away, so a clash is caught before the user finishes the form.
    /// </summary>
    private async Task CheckSerialsAsync(StockLineEditor line)
    {
        if (line.Item is not { } item || line.SerialFrom is not { } from || line.SerialTo is not { } to)
            return;

        try
        {
            line.Warning = await _releases.CheckSerialAvailabilityAsync(item.Id, from, to, _editingId);
        }
        catch (Exception exception)
        {
            line.Warning = UserFacingError.From(exception);
        }
    }

    private async Task SaveAsync()
    {
        var lines = Lines.Where(line => line.Item is not null).ToList();
        if (lines.Count == 0)
        {
            ReportError("Add at least one item before saving.");
            return;
        }

        if (Recipient is null)
        {
            ReportError("Choose who is receiving the items.");
            return;
        }

        await RunAsync(async () =>
        {
            var saved = await _releases.SaveAsync(new SaveReleaseRequest(
                _editingId,
                DateOnly.FromDateTime(ReleaseDate),
                Recipient.Id,
                Purpose,
                ReleasedBy,
                ReceivedBy,
                Remarks,
                lines.Select(line => line.ToInput()).ToList()));

            var wasNew = _editingId is null;
            IsEditorOpen = false;
            await RefreshOptionsAsync();
            await LoadCoreAsync();
            Selected = Rows.FirstOrDefault(row => row.Id == saved.Id);
            ReportSuccess(wasNew
                ? $"Released as {saved.ReferenceNumber}. Use 'Print slip' for the signed copy."
                : $"{saved.ReferenceNumber} updated.");
        });
    }

    private async Task VoidAsync()
    {
        if (Selected is not { } row) return;

        var reason = _dialogs.AskForText(
            $"Why is {row.ReferenceNumber} being voided? The release stays on file with this note, " +
            "and any serials on it become available again.",
            "Void release");
        if (string.IsNullOrWhiteSpace(reason)) return;

        await RunAsync(async () =>
        {
            await _releases.VoidAsync(row.Id, reason);
            await LoadCoreAsync();
            ReportSuccess($"{row.ReferenceNumber} voided.");
        });
    }

    private async Task PrintSlipAsync()
    {
        if (Selected is not { } row) return;

        await RunAsync(async () =>
        {
            var path = await _reports.ExportReleaseSlipAsync(row.Id);
            ReportSuccess($"Slip saved to {path}");
            _dialogs.OpenFile(path);
        });
    }
}
