using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    public LoginViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin, HandleException);
    }

    public event Action? LoginSucceeded;

    public string Username
    {
        get => _username;
        set { if (SetProperty(ref _username, value)) RaiseCanExecuteChanged(); }
    }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) RaiseCanExecuteChanged(); }
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

    public ICommand LoginCommand { get; }

    private bool CanLogin() =>
        !IsBusy && !string.IsNullOrWhiteSpace(Username) && Password.Length > 0;

    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _authenticationService.LoginAsync(new LoginRequest(Username, Password));
            Password = string.Empty;
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            LoginSucceeded?.Invoke();
        }
        finally
        {
            IsBusy = false;
            RaiseCanExecuteChanged();
        }
    }

    private void HandleException(Exception exception) =>
        ErrorMessage = UserFacingError.From(exception);

    private void RaiseCanExecuteChanged() => ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
}
