using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public sealed class UserManagementViewModel : ManagementViewModelBase
{
    private readonly IUserAdministrationService _service; private readonly ICollectorService _collectors; private readonly IUserDialogService _dialogs;
    private UserDto? _selectedItem; private Guid? _editingId; private bool _isEditorOpen; private bool _isNewUser; private bool _isResetMode;
    private string _username = string.Empty; private string _fullName = string.Empty; private RoleDto? _selectedRole;
    private CollectorChoice? _selectedCollector; private bool _editorIsActive = true; private bool _mustChangePassword = true;
    private string _password = string.Empty; private string _confirmPassword = string.Empty;

    public UserManagementViewModel(IUserAdministrationService service, ICollectorService collectors, IUserDialogService dialogs)
    {
        _service = service; _collectors = collectors; _dialogs = dialogs;
        LoadCommand = new AsyncRelayCommand(InitializeAsync); SearchCommand = new AsyncRelayCommand(LoadUsersAsync); AddCommand = new RelayCommand(BeginAdd);
        EditCommand = new RelayCommand(BeginEdit, () => SelectedItem is not null); ResetPasswordCommand = new RelayCommand(BeginReset, () => SelectedItem is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditorOpen); CancelCommand = new RelayCommand(() => IsEditorOpen = false);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => SelectedItem is not null); ViewActivityCommand = new AsyncRelayCommand(ViewActivityAsync, () => SelectedItem is not null);
    }

    public ObservableCollection<UserDto> Items { get; } = []; public ObservableCollection<RoleDto> Roles { get; } = [];
    public ObservableCollection<CollectorChoice> Collectors { get; } = []; public ObservableCollection<AuditLogDto> Activity { get; } = [];
    public ICommand LoadCommand { get; } public ICommand SearchCommand { get; } public ICommand AddCommand { get; } public ICommand EditCommand { get; }
    public ICommand ResetPasswordCommand { get; } public ICommand SaveCommand { get; } public ICommand CancelCommand { get; }
    public ICommand ToggleActiveCommand { get; } public ICommand ViewActivityCommand { get; }
    public UserDto? SelectedItem { get => _selectedItem; set { if (SetProperty(ref _selectedItem, value)) { ((RelayCommand)EditCommand).RaiseCanExecuteChanged(); ((RelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)ViewActivityCommand).RaiseCanExecuteChanged(); } } }
    public bool IsEditorOpen { get => _isEditorOpen; private set { if (SetProperty(ref _isEditorOpen, value)) ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged(); } }
    public bool IsNewUser { get => _isNewUser; private set { if (SetProperty(ref _isNewUser, value)) OnEditorModeChanged(); } }
    public bool IsResetMode { get => _isResetMode; private set { if (SetProperty(ref _isResetMode, value)) OnEditorModeChanged(); } }
    public bool NeedsPassword => IsNewUser || IsResetMode;
    public bool IsProfileMode => !IsResetMode;
    public string Username { get => _username; set => SetProperty(ref _username, value); } public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public RoleDto? SelectedRole { get => _selectedRole; set => SetProperty(ref _selectedRole, value); } public CollectorChoice? SelectedCollector { get => _selectedCollector; set => SetProperty(ref _selectedCollector, value); }
    public bool EditorIsActive { get => _editorIsActive; set => SetProperty(ref _editorIsActive, value); } public bool MustChangePassword { get => _mustChangePassword; set => SetProperty(ref _mustChangePassword, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); } public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }

    public async Task InitializeAsync() => await RunAsync(async () =>
    {
        var roles = await _service.ListRolesAsync(); Roles.Clear(); foreach (var role in roles) Roles.Add(role);
        var collectors = await _collectors.ListAsync(null, null, null); Collectors.Clear(); Collectors.Add(CollectorChoice.None);
        foreach (var collector in collectors.OrderBy(item => item.FullName)) Collectors.Add(new CollectorChoice(collector.Id, collector.FullName, collector.IsActive));
        await LoadUsersCoreAsync();
    });

    private async Task LoadUsersAsync() => await RunAsync(LoadUsersCoreAsync);
    private async Task LoadUsersCoreAsync() { var users = await _service.ListAsync(SearchText, SelectedStatusFilter.Value); Items.Clear(); foreach (var user in users) Items.Add(user); SelectedItem = null; }
    private void BeginAdd() { _editingId = null; Username = FullName = Password = ConfirmPassword = string.Empty; SelectedRole = Roles.FirstOrDefault(); SelectedCollector = Collectors.FirstOrDefault(); EditorIsActive = true; MustChangePassword = true; IsNewUser = true; IsResetMode = false; IsEditorOpen = true; }
    private void BeginEdit() { if (SelectedItem is not { } item) return; _editingId = item.Id; Username = item.Username; FullName = item.FullName; SelectedRole = Roles.FirstOrDefault(role => role.Id == item.RoleId); SelectedCollector = Collectors.FirstOrDefault(collector => collector.Id == item.LinkedCollectorId) ?? Collectors.FirstOrDefault(); EditorIsActive = item.IsActive; MustChangePassword = item.MustChangePassword; Password = ConfirmPassword = string.Empty; IsNewUser = false; IsResetMode = false; IsEditorOpen = true; }
    private void BeginReset() { if (SelectedItem is not { } item) return; _editingId = item.Id; Username = item.Username; FullName = item.FullName; Password = ConfirmPassword = string.Empty; MustChangePassword = true; IsNewUser = false; IsResetMode = true; IsEditorOpen = true; }
    private async Task SaveAsync() => await RunAsync(async () =>
    {
        if ((IsNewUser || IsResetMode) && Password != ConfirmPassword) throw new InvalidOperationException("Passwords do not match.");
        if (IsNewUser)
        {
            if (SelectedRole is null) throw new InvalidOperationException("Select a role.");
            await _service.CreateAsync(new CreateUserRequest(Username, FullName, SelectedCollector?.Id, SelectedRole.Id, Password, MustChangePassword));
        }
        else if (IsResetMode) await _service.ResetPasswordAsync(new ResetPasswordRequest(_editingId!.Value, Password, MustChangePassword));
        else
        {
            if (SelectedRole is null) throw new InvalidOperationException("Select a role.");
            await _service.UpdateAsync(new UpdateUserRequest(_editingId!.Value, FullName, SelectedCollector?.Id, SelectedRole.Id, EditorIsActive, MustChangePassword));
        }
        Password = ConfirmPassword = string.Empty; IsEditorOpen = false; await LoadUsersCoreAsync();
    }, IsNewUser ? "User created." : IsResetMode ? "Password reset." : "User updated.");
    private async Task ToggleActiveAsync() { if (SelectedItem is not { } item) return; var action = item.IsActive ? "deactivate" : "activate"; if (!_dialogs.Confirm($"Do you want to {action} user '{item.Username}'?", "Confirm account status")) return; await RunAsync(async () => { await _service.SetActiveAsync(item.Id, !item.IsActive); await LoadUsersCoreAsync(); }, $"User {action}d."); }
    private async Task ViewActivityAsync() { if (SelectedItem is not { } item) return; await RunAsync(async () => { var entries = await _service.GetActivityAsync(item.Id); Activity.Clear(); foreach (var entry in entries) Activity.Add(entry); }); }
    private void OnEditorModeChanged()
    {
        RaisePropertyChanged(nameof(NeedsPassword));
        RaisePropertyChanged(nameof(IsProfileMode));
    }
}

public sealed record CollectorChoice(Guid? Id, string Name, bool IsActive)
{
    public static CollectorChoice None { get; } = new(null, "Not linked", true);
    public override string ToString() => IsActive ? Name : $"{Name} (Inactive)";
}
