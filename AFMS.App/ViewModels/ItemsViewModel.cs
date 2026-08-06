using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>The list of things being tracked: supplies and serial-numbered forms.</summary>
public sealed class ItemsViewModel : PageViewModelBase
{
    private readonly IItemService _items;
    private readonly IUserDialogService _dialogs;

    private ItemDto? _selected;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private string _searchText = string.Empty;
    private ActiveFilter _statusFilter = ActiveFilter.All[0];
    private bool _showLowOnly;

    private string _code = string.Empty;
    private string _name = string.Empty;
    private string _unit = "pcs";
    private string? _category;
    private bool _tracksSerials;
    private long _reorderLevel;
    private string? _remarks;
    private bool _editorIsActive = true;

    public ItemsViewModel(IItemService items, IUserDialogService dialogs)
    {
        _items = items;
        _dialogs = dialogs;

        SearchCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new RelayCommand(BeginAdd);
        EditCommand = new RelayCommand(BeginEdit, () => Selected is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen);
        CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => Selected is not null);
    }

    public ObservableCollection<ItemDto> Rows { get; } = [];
    public ObservableCollection<string> Units { get; } = [];
    public IReadOnlyList<ActiveFilter> StatusFilters => ActiveFilter.All;

    public ICommand SearchCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    public ItemDto? Selected
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
    public bool ShowLowOnly { get => _showLowOnly; set => SetProperty(ref _showLowOnly, value); }

    public string Code { get => _code; set => SetProperty(ref _code, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Unit { get => _unit; set => SetProperty(ref _unit, value); }
    public string? Category { get => _category; set => SetProperty(ref _category, value); }
    public long ReorderLevel { get => _reorderLevel; set => SetProperty(ref _reorderLevel, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public bool EditorIsActive { get => _editorIsActive; set => SetProperty(ref _editorIsActive, value); }

    public bool TracksSerials
    {
        get => _tracksSerials;
        set => SetProperty(ref _tracksSerials, value);
    }

    public string EditorTitle => _editingId is null ? "New item" : "Edit item";

    public async Task InitializeAsync()
    {
        await RunAsync(async () =>
        {
            var units = await _items.ListUnitsAsync();
            Units.Clear();
            foreach (var unit in units) Units.Add(unit);
            await LoadCoreAsync();
        });
    }

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var rows = await _items.ListAsync(SearchText, SelectedStatusFilter.Value, ShowLowOnly);
        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);
        Selected = null;
    }

    private void BeginAdd()
    {
        _editingId = null;
        Code = Name = string.Empty;
        Unit = Units.FirstOrDefault() ?? "pcs";
        Category = Remarks = null;
        TracksSerials = false;
        ReorderLevel = 0;
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
        Unit = row.Unit;
        Category = row.Category;
        TracksSerials = row.TracksSerials;
        ReorderLevel = row.ReorderLevel;
        Remarks = row.Remarks;
        EditorIsActive = row.IsActive;
        Message = null;
        IsEditorOpen = true;
        RaisePropertyChanged(nameof(EditorTitle));
    }

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        await _items.SaveAsync(new SaveItemRequest(
            _editingId, Code, Name, Unit, Category, TracksSerials, ReorderLevel, Remarks, EditorIsActive));

        var wasNew = _editingId is null;
        IsEditorOpen = false;
        await LoadCoreAsync();
        ReportSuccess(wasNew ? "Item added." : "Item updated.");
    });

    private async Task ToggleActiveAsync()
    {
        if (Selected is not { } row) return;

        var hide = row.IsActive;
        var question = hide
            ? $"Hide '{row.Name}' from the pick lists? Past records stay exactly as they are."
            : $"Show '{row.Name}' in the pick lists again?";
        if (!_dialogs.Confirm(question, "Confirm")) return;

        await RunAsync(async () =>
        {
            await _items.SetActiveAsync(row.Id, !hide);
            await LoadCoreAsync();
            ReportSuccess(hide ? "Item hidden." : "Item restored.");
        });
    }
}
