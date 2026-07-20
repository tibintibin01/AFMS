using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public sealed class CollectorsViewModel : ManagementViewModelBase
{
    private readonly ICollectorService _service; private readonly IBarangayService _barangays; private readonly IUserDialogService _dialogs;
    private CollectorDto? _selectedItem; private Guid? _editingId; private bool _isEditorOpen; private bool _isReadOnlyMode;
    private BarangayOption _selectedBarangayFilter = BarangayOption.All; private BarangayOption? _editorBarangay;
    private string _collectorNumber = string.Empty; private string _fullName = string.Empty; private string? _employeeNumber;
    private string? _position; private string? _officeOrDepartment; private string? _contactNumber; private string? _emailAddress;
    private string? _appointmentReference; private bool _editorIsActive = true; private string? _remarks;

    public CollectorsViewModel(ICollectorService service, IBarangayService barangays, IUserDialogService dialogs)
    {
        _service = service; _barangays = barangays; _dialogs = dialogs;
        LoadCommand = new AsyncRelayCommand(LoadAsync); SearchCommand = new AsyncRelayCommand(LoadAsync); AddCommand = new RelayCommand(BeginAdd);
        EditCommand = new RelayCommand(() => BeginEdit(false), () => SelectedItem is not null); ViewCommand = new RelayCommand(() => BeginEdit(true), () => SelectedItem is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen && !IsReadOnlyMode); CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => SelectedItem is not null);
        BarangayFilters.Add(BarangayOption.All);
    }

    public ObservableCollection<CollectorDto> Items { get; } = []; public ObservableCollection<BarangayOption> BarangayFilters { get; } = [];
    public ICommand LoadCommand { get; } public ICommand SearchCommand { get; } public ICommand AddCommand { get; } public ICommand EditCommand { get; }
    public ICommand ViewCommand { get; } public ICommand SaveCommand { get; } public ICommand CancelCommand { get; } public ICommand ToggleActiveCommand { get; }
    public CollectorDto? SelectedItem { get => _selectedItem; set { if (SetProperty(ref _selectedItem, value)) { ((RelayCommand)EditCommand).RaiseCanExecuteChanged(); ((RelayCommand)ViewCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged(); } } }
    public bool IsEditorOpen { get => _isEditorOpen; private set { if (SetProperty(ref _isEditorOpen, value)) ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged(); } }
    public bool IsReadOnlyMode { get => _isReadOnlyMode; private set { if (SetProperty(ref _isReadOnlyMode, value)) ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged(); } }
    public BarangayOption SelectedBarangayFilter { get => _selectedBarangayFilter; set => SetProperty(ref _selectedBarangayFilter, value); }
    public BarangayOption? EditorBarangay { get => _editorBarangay; set => SetProperty(ref _editorBarangay, value); }
    public string CollectorNumber { get => _collectorNumber; set => SetProperty(ref _collectorNumber, value); } public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string? EmployeeNumber { get => _employeeNumber; set => SetProperty(ref _employeeNumber, value); } public string? Position { get => _position; set => SetProperty(ref _position, value); }
    public string? OfficeOrDepartment { get => _officeOrDepartment; set => SetProperty(ref _officeOrDepartment, value); } public string? ContactNumber { get => _contactNumber; set => SetProperty(ref _contactNumber, value); }
    public string? EmailAddress { get => _emailAddress; set => SetProperty(ref _emailAddress, value); } public string? AppointmentReference { get => _appointmentReference; set => SetProperty(ref _appointmentReference, value); }
    public bool EditorIsActive { get => _editorIsActive; set => SetProperty(ref _editorIsActive, value); } public string? Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

    public async Task InitializeAsync()
    {
        await RunAsync(async () =>
        {
            var barangays = await _barangays.ListAsync(null, null);
            BarangayFilters.Clear(); BarangayFilters.Add(BarangayOption.All);
            foreach (var barangay in barangays.OrderBy(item => item.Name)) BarangayFilters.Add(new BarangayOption(barangay.Id, barangay.Name, barangay.IsActive));
            SelectedBarangayFilter = BarangayFilters[0]; await LoadCoreAsync();
        });
    }

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);
    private async Task LoadCoreAsync() { var items = await _service.ListAsync(SearchText, SelectedBarangayFilter.Id, SelectedStatusFilter.Value); Items.Clear(); foreach (var item in items) Items.Add(item); SelectedItem = null; }
    private void BeginAdd() { _editingId = null; CollectorNumber = FullName = string.Empty; EmployeeNumber = Position = OfficeOrDepartment = ContactNumber = EmailAddress = AppointmentReference = Remarks = null; EditorBarangay = BarangayFilters.FirstOrDefault(); EditorIsActive = true; IsReadOnlyMode = false; IsEditorOpen = true; }
    private void BeginEdit(bool readOnly) { if (SelectedItem is not { } item) return; _editingId = item.Id; CollectorNumber = item.CollectorNumber; FullName = item.FullName; EmployeeNumber = item.EmployeeNumber; Position = item.Position; OfficeOrDepartment = item.OfficeOrDepartment; ContactNumber = item.ContactNumber; EmailAddress = item.EmailAddress; AppointmentReference = item.AppointmentReference; Remarks = item.Remarks; EditorBarangay = BarangayFilters.FirstOrDefault(option => option.Id == item.BarangayId) ?? BarangayFilters[0]; EditorIsActive = item.IsActive; IsReadOnlyMode = readOnly; IsEditorOpen = true; }
    private async Task SaveAsync() => await RunAsync(async () => { await _service.SaveAsync(new SaveCollectorRequest(_editingId, CollectorNumber, FullName, EmployeeNumber, Position, OfficeOrDepartment, EditorBarangay?.Id, ContactNumber, EmailAddress, AppointmentReference, EditorIsActive, Remarks)); IsEditorOpen = false; await LoadCoreAsync(); }, _editingId is null ? "Collector created." : "Collector updated.");
    private async Task ToggleActiveAsync() { if (SelectedItem is not { } item) return; var action = item.IsActive ? "deactivate" : "reactivate"; if (!_dialogs.Confirm($"Do you want to {action} '{item.FullName}'? Historical records will remain available.", "Confirm status change")) return; await RunAsync(async () => { await _service.SetActiveAsync(item.Id, !item.IsActive); await LoadCoreAsync(); }, $"Collector {action}d."); }
}

public sealed record BarangayOption(Guid? Id, string Name, bool IsActive)
{
    public static BarangayOption All { get; } = new(null, "All barangays", true);
    public override string ToString() => IsActive ? Name : $"{Name} (Inactive)";
}
