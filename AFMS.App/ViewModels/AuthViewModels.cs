using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>First launch: create the one account that will use this computer.</summary>
public sealed class FirstRunViewModel : PageViewModelBase
{
    private readonly IAuthenticationService _auth;
    private string _displayName = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;

    public FirstRunViewModel(IAuthenticationService auth)
    {
        _auth = auth;
        CreateAccountCommand = new AsyncRelayCommand(
            CreateAsync, CanCreate, exception => ReportError(Services.UserFacingError.From(exception)));
    }

    public event Action? AccountCreated;

    public string DisplayName
    {
        get => _displayName;
        set { if (SetProperty(ref _displayName, value)) Refresh(); }
    }

    public string Username
    {
        get => _username;
        set { if (SetProperty(ref _username, value)) Refresh(); }
    }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) Refresh(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { if (SetProperty(ref _confirmPassword, value)) Refresh(); }
    }

    public ICommand CreateAccountCommand { get; }

    private bool CanCreate() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        !string.IsNullOrWhiteSpace(Username) &&
        Password.Length > 0 &&
        ConfirmPassword.Length > 0;

    private async Task CreateAsync()
    {
        if (Password != ConfirmPassword)
        {
            ReportError("The two passwords do not match.");
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _auth.CreateFirstAccountAsync(
                new CreateFirstAccountRequest(DisplayName, Username, Password));
            if (!result.Succeeded)
            {
                ReportError(result.ErrorMessage ?? "The account could not be created.");
                return;
            }

            Password = ConfirmPassword = string.Empty;
            AccountCreated?.Invoke();
        });
    }

    private void Refresh() => ((AsyncRelayCommand)CreateAccountCommand).RaiseCanExecuteChanged();
}

public sealed class SignInViewModel : PageViewModelBase
{
    private readonly IAuthenticationService _auth;
    private string _username = string.Empty;
    private string _password = string.Empty;

    public SignInViewModel(IAuthenticationService auth)
    {
        _auth = auth;
        SignInCommand = new AsyncRelayCommand(
            SignInAsync, CanSignIn, exception => ReportError(Services.UserFacingError.From(exception)));
    }

    public event Action? SignedIn;

    public string Username
    {
        get => _username;
        set { if (SetProperty(ref _username, value)) Refresh(); }
    }

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) Refresh(); }
    }

    public ICommand SignInCommand { get; }

    private bool CanSignIn() =>
        !IsBusy && !string.IsNullOrWhiteSpace(Username) && Password.Length > 0;

    private async Task SignInAsync()
    {
        IsBusy = true;
        Message = null;
        try
        {
            var result = await _auth.SignInAsync(new SignInRequest(Username, Password));
            Password = string.Empty;

            if (!result.Succeeded)
            {
                ReportError(result.ErrorMessage ?? "Sign-in failed.");
                return;
            }

            SignedIn?.Invoke();
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    private void Refresh() => ((AsyncRelayCommand)SignInCommand).RaiseCanExecuteChanged();
}

public sealed class ChangePasswordViewModel : PageViewModelBase
{
    private readonly IAuthenticationService _auth;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;

    public ChangePasswordViewModel(IAuthenticationService auth)
    {
        _auth = auth;
        ChangeCommand = new AsyncRelayCommand(
            ChangeAsync, CanChange, exception => ReportError(Services.UserFacingError.From(exception)));
    }

    public event Action? PasswordChanged;

    public string CurrentPassword
    {
        get => _currentPassword;
        set { if (SetProperty(ref _currentPassword, value)) Refresh(); }
    }

    public string NewPassword
    {
        get => _newPassword;
        set { if (SetProperty(ref _newPassword, value)) Refresh(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { if (SetProperty(ref _confirmPassword, value)) Refresh(); }
    }

    public ICommand ChangeCommand { get; }

    private bool CanChange() =>
        !IsBusy && CurrentPassword.Length > 0 && NewPassword.Length > 0 && ConfirmPassword.Length > 0;

    private async Task ChangeAsync()
    {
        if (NewPassword != ConfirmPassword)
        {
            ReportError("The two new passwords do not match.");
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _auth.ChangePasswordAsync(CurrentPassword, NewPassword);
            if (!result.Succeeded)
            {
                ReportError(result.ErrorMessage ?? "The password could not be changed.");
                return;
            }

            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
            PasswordChanged?.Invoke();
        });
    }

    private void Refresh() => ((AsyncRelayCommand)ChangeCommand).RaiseCanExecuteChanged();
}
