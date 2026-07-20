using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public sealed class AccountableFormTypesViewModel : ManagementViewModelBase
{
    private readonly IAccountableFormTypeService _service;
    private readonly IUserDialogService _dialogs;
    private AccountableFormTypeDto? _selectedItem;
    private Guid? _editingId;
    private bool _isEditorOpen;
    private bool _isReadOnlyMode;
    private string _code = string.Empty;
    private string _name = string.Empty;
    private string? _description;
    private string? _category;
    private int _receiptsPerBooklet = 50;
    private string? _serialNumberPrefix;
    private int? _serialNumberLength;
    private bool _requiresIndividualTracking;
    private bool _editorIsActive = true;
    private string? _remarks;

    public AccountableFormTypesViewModel(IAccountableFormTypeService service, IUserDialogService dialogs)
    {
        _service = service; _dialogs = dialogs;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SearchCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new RelayCommand(BeginAdd);
        EditCommand = new RelayCommand(() => BeginEdit(false), () => SelectedItem is not null);
        ViewCommand = new RelayCommand(() => BeginEdit(true), () => SelectedItem is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen && !IsReadOnlyMode);
        CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => SelectedItem is not null);
    }

    public ObservableCollection<AccountableFormTypeDto> Items { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ViewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    public AccountableFormTypeDto? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                ((RelayCommand)EditCommand).RaiseCanExecuteChanged(); ((RelayCommand)ViewCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged();
            }
        }
    }
    public bool IsEditorOpen { get => _isEditorOpen; private set { if (SetProperty(ref _isEditorOpen, value)) ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged(); } }
    public bool IsReadOnlyMode { get => _isReadOnlyMode; private set { if (SetProperty(ref _isReadOnlyMode, value)) ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged(); } }
    public string Code { get => _code; set => SetProperty(ref _code, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string? Description { get => _description; set => SetProperty(ref _description, value); }
    public string? Category { get => _category; set => SetProperty(ref _category, value); }
    public int ReceiptsPerBooklet { get => _receiptsPerBooklet; set => SetProperty(ref _receiptsPerBooklet, value); }
    public string? SerialNumberPrefix { get => _serialNumberPrefix; set => SetProperty(ref _serialNumberPrefix, value); }
    public int? SerialNumberLength { get => _serialNumberLength; set => SetProperty(ref _serialNumberLength, value); }
    public bool RequiresIndividualTracking { get => _requiresIndividualTracking; set => SetProperty(ref _requiresIndividualTracking, value); }
    public bool EditorIsActive { get => _editorIsActive; set => SetProperty(ref _editorIsActive, value); }
    public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

    public Task InitializeAsync() => LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var items = await _service.ListAsync(SearchText, SelectedStatusFilter.Value);
        Items.Clear(); foreach (var item in items) Items.Add(item);
        SelectedItem = null;
    });

    private void BeginAdd()
    {
        _editingId = null; Code = string.Empty; Name = string.Empty; Description = null; Category = null;
        ReceiptsPerBooklet = 50; SerialNumberPrefix = null; SerialNumberLength = null;
        RequiresIndividualTracking = false; EditorIsActive = true; Remarks = null; IsReadOnlyMode = false; IsEditorOpen = true;
    }

    private void BeginEdit(bool readOnly)
    {
        if (SelectedItem is not { } item) return;
        _editingId = item.Id; Code = item.Code; Name = item.Name; Description = item.Description; Category = item.Category;
        ReceiptsPerBooklet = item.ReceiptsPerBooklet; SerialNumberPrefix = item.SerialNumberPrefix; SerialNumberLength = item.SerialNumberLength;
        RequiresIndividualTracking = item.RequiresIndividualReceiptTracking; EditorIsActive = item.IsActive; Remarks = item.Remarks;
        IsReadOnlyMode = readOnly; IsEditorOpen = true;
    }

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        await _service.SaveAsync(new SaveAccountableFormTypeRequest(_editingId, Code, Name, Description, Category, ReceiptsPerBooklet,
            SerialNumberPrefix, SerialNumberLength, RequiresIndividualTracking, EditorIsActive, Remarks));
        IsEditorOpen = false; await LoadAsync();
    }, _editingId is null ? "Accountable form type created." : "Accountable form type updated.");

    private async Task ToggleActiveAsync()
    {
        if (SelectedItem is not { } item) return;
        var action = item.IsActive ? "deactivate" : "reactivate";
        if (!_dialogs.Confirm($"Do you want to {action} '{item.Code} - {item.Name}'?", "Confirm status change")) return;
        await RunAsync(async () => { await _service.SetActiveAsync(item.Id, !item.IsActive); await LoadAsync(); }, $"Form type {action}d.");
    }
}
