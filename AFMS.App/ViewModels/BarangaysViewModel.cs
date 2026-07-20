using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public sealed class BarangaysViewModel : ManagementViewModelBase
{
    private readonly IBarangayService _service; private readonly IUserDialogService _dialogs;
    private BarangayDto? _selectedItem; private Guid? _editingId; private bool _isEditorOpen;
    private string _code = string.Empty; private string _name = string.Empty; private string _municipality = string.Empty;
    private string _province = string.Empty; private bool _editorIsActive = true; private string? _remarks;

    public BarangaysViewModel(IBarangayService service, IUserDialogService dialogs)
    {
        _service = service; _dialogs = dialogs;
        LoadCommand = new AsyncRelayCommand(LoadAsync); SearchCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new RelayCommand(BeginAdd); EditCommand = new RelayCommand(BeginEdit, () => SelectedItem is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen); CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => SelectedItem is not null);
    }

    public ObservableCollection<BarangayDto> Items { get; } = [];
    public ICommand LoadCommand { get; } public ICommand SearchCommand { get; } public ICommand AddCommand { get; }
    public ICommand EditCommand { get; } public ICommand SaveCommand { get; } public ICommand CancelCommand { get; } public ICommand ToggleActiveCommand { get; }
    public BarangayDto? SelectedItem { get => _selectedItem; set { if (SetProperty(ref _selectedItem, value)) { ((RelayCommand)EditCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged(); } } }
    public bool IsEditorOpen { get => _isEditorOpen; private set { if (SetProperty(ref _isEditorOpen, value)) ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged(); } }
    public string Code { get => _code; set => SetProperty(ref _code, value); } public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Municipality { get => _municipality; set => SetProperty(ref _municipality, value); } public string Province { get => _province; set => SetProperty(ref _province, value); }
    public bool EditorIsActive { get => _editorIsActive; set => SetProperty(ref _editorIsActive, value); } public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public Task InitializeAsync() => LoadAsync();
    private async Task LoadAsync() => await RunAsync(async () => { var items = await _service.ListAsync(SearchText, SelectedStatusFilter.Value); Items.Clear(); foreach (var item in items) Items.Add(item); SelectedItem = null; });
    private void BeginAdd() { _editingId = null; Code = Name = Municipality = Province = string.Empty; EditorIsActive = true; Remarks = null; IsEditorOpen = true; }
    private void BeginEdit() { if (SelectedItem is not { } item) return; _editingId = item.Id; Code = item.Code; Name = item.Name; Municipality = item.Municipality; Province = item.Province; EditorIsActive = item.IsActive; Remarks = item.Remarks; IsEditorOpen = true; }
    private async Task SaveAsync() => await RunAsync(async () => { await _service.SaveAsync(new SaveBarangayRequest(_editingId, Code, Name, Municipality, Province, EditorIsActive, Remarks)); IsEditorOpen = false; await LoadAsync(); }, _editingId is null ? "Barangay created." : "Barangay updated.");
    private async Task ToggleActiveAsync() { if (SelectedItem is not { } item) return; var action = item.IsActive ? "deactivate" : "reactivate"; if (!_dialogs.Confirm($"Do you want to {action} '{item.Name}'?", "Confirm status change")) return; await RunAsync(async () => { await _service.SetActiveAsync(item.Id, !item.IsActive); await LoadAsync(); }, $"Barangay {action}d."); }
}
