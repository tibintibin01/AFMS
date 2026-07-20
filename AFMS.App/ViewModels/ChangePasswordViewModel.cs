using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.App.Commands;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public sealed class ChangePasswordViewModel(IAuthenticationService authentication) : ObservableObject
{
    private string _currentPassword = string.Empty; private string _newPassword = string.Empty; private string _confirmPassword = string.Empty; private string? _errorMessage;
    public event Action? PasswordChanged;
    public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public ICommand SaveCommand => _saveCommand ??= new AsyncRelayCommand(SaveAsync, onException: exception => ErrorMessage = UserFacingError.From(exception));
    private AsyncRelayCommand? _saveCommand;
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (NewPassword != ConfirmPassword) { ErrorMessage = "New passwords do not match."; return; }
        var result = await authentication.ChangePasswordAsync(CurrentPassword, NewPassword);
        if (!result.Succeeded) { ErrorMessage = result.ErrorMessage; return; }
        CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        PasswordChanged?.Invoke();
    }
}
