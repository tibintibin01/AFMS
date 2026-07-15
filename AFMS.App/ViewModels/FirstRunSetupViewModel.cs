using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;

namespace AFMS.App.ViewModels;

public sealed class FirstRunSetupViewModel : ObservableObject
{
    private readonly IFirstRunService _firstRunService;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    public FirstRunSetupViewModel(IFirstRunService firstRunService)
    {
        _firstRunService = firstRunService;
        CreateAdministratorCommand = new AsyncRelayCommand(CreateAdministratorAsync, CanSubmit, HandleException);
    }

    public event Action? SetupCompleted;

    public string Username
    {
        get => _username;
        set { if (SetProperty(ref _username, value)) RaiseCanExecuteChanged(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { if (SetProperty(ref _displayName, value)) RaiseCanExecuteChanged(); }
    }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) RaiseCanExecuteChanged(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { if (SetProperty(ref _confirmPassword, value)) RaiseCanExecuteChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand CreateAdministratorCommand { get; }

    private bool CanSubmit() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        Password.Length > 0 &&
        ConfirmPassword.Length > 0;

    private async Task CreateAdministratorAsync()
    {
        ErrorMessage = null;
        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _firstRunService.CreateAdministratorAsync(
                new CreateAdministratorRequest(Username, DisplayName, Password));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            Password = string.Empty;
            ConfirmPassword = string.Empty;
            SetupCompleted?.Invoke();
        }
        finally
        {
            IsBusy = false;
            RaiseCanExecuteChanged();
        }
    }

    private void HandleException(Exception exception) =>
        ErrorMessage = $"Administrator setup failed: {exception.Message}";

    private void RaiseCanExecuteChanged() => ((AsyncRelayCommand)CreateAdministratorCommand).RaiseCanExecuteChanged();
}
