using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>The people items get released to.</summary>
public sealed class PeopleViewModel : PageViewModelBase
{
    private readonly IRecipientService _people;
    private readonly IUserDialogService _dialogs;

    private RecipientDto? _selected;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private string _searchText = string.Empty;
    private ActiveFilter _statusFilter = ActiveFilter.All[0];

    private string _code = string.Empty;
    private string _name = string.Empty;
    private string? _position;
    private string? _office;
    private string? _contactNumber;
    private string? _remarks;
    private bool _editorIsActive = true;

    public PeopleViewModel(IRecipientService people, IUserDialogService dialogs)
    {
        _people = people;
        _dialogs = dialogs;

        SearchCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new RelayCommand(BeginAdd);
        EditCommand = new RelayCommand(BeginEdit, () => Selected is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen);
        CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => Selected is not null);
    }

    public ObservableCollection<RecipientDto> Rows { get; } = [];
    public IReadOnlyList<ActiveFilter> StatusFilters => ActiveFilter.All;

    public ICommand SearchCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    public RecipientDto? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(ToggleActiveLabel));
        }
    }

    public string ToggleActiveLabel => Selected?.IsActive == false ? "Show again" : "Hide from lists";

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        private set
        {
            if (SetProperty(ref _isEditorOpen, value))
                ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public ActiveFilter SelectedStatusFilter { get => _statusFilter; set => SetProperty(ref _statusFilter, value); }

    public string Code { get => _code; set => SetProperty(ref _code, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string? Position { get => _position; set => SetProperty(ref _position, value); }
    public string? Office { get => _office; set => SetProperty(ref _office, value); }
    public string? ContactNumber { get => _contactNumber; set => SetProperty(ref _contactNumber, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public bool EditorIsActive { get => _editorIsActive; set => SetProperty(ref _editorIsActive, value); }

    public string EditorTitle => _editingId is null ? "New person" : "Edit person";

    public Task InitializeAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var rows = await _people.ListAsync(SearchText, SelectedStatusFilter.Value);
        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);
        Selected = null;
    }

    private void BeginAdd()
    {
        _editingId = null;
        Code = Name = string.Empty;
        Position = Office = ContactNumber = Remarks = null;
        EditorIsActive = true;
        Message = null;
        IsEditorOpen = true;
        RaisePropertyChanged(nameof(EditorTitle));
    }

    private void BeginEdit()
    {
        if (Selected is not { } row) return;

        _editingId = row.Id;
        Code = row.Code;
        Name = row.Name;
        Position = row.Position;
        Office = row.Office;
        ContactNumber = row.ContactNumber;
        Remarks = row.Remarks;
        EditorIsActive = row.IsActive;
        Message = null;
        IsEditorOpen = true;
        RaisePropertyChanged(nameof(EditorTitle));
    }

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        await _people.SaveAsync(new SaveRecipientRequest(
            _editingId, Code, Name, Position, Office, ContactNumber, Remarks, EditorIsActive));

        var wasNew = _editingId is null;
        IsEditorOpen = false;
        await LoadCoreAsync();
        ReportSuccess(wasNew ? "Person added." : "Person updated.");
    });

    private async Task ToggleActiveAsync()
    {
        if (Selected is not { } row) return;

        var hide = row.IsActive;
        var question = hide
            ? $"Hide '{row.Name}' from the release form? Past records stay exactly as they are."
            : $"Show '{row.Name}' in the release form again?";
        if (!_dialogs.Confirm(question, "Confirm")) return;

        await RunAsync(async () =>
        {
            await _people.SetActiveAsync(row.Id, !hide);
            await LoadCoreAsync();
            ReportSuccess(hide ? "Person hidden." : "Person restored.");
        });
    }
}
