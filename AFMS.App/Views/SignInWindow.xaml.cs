using System.Windows;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class SignInWindow : Window
{
    private readonly SignInViewModel _viewModel;

    public SignInWindow(SignInViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.SignedIn += HandleSignedIn;
        _viewModel.PasswordCleared += HandlePasswordCleared;

        Closed += (_, _) =>
        {
            _viewModel.SignedIn -= HandleSignedIn;
            _viewModel.PasswordCleared -= HandlePasswordCleared;
        };

        // A remembered username means the password is what is actually needed.
        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_viewModel.Username)) UsernameBox.Focus();
            else PasswordBox.FocusInput();
        };
    }

    private void HandleSignedIn() => DialogResult = true;

    /// <summary>
    /// The boxes empty themselves through the Password binding; this just puts the
    /// cursor back so a corrected password can be typed straight away.
    /// </summary>
    private void HandlePasswordCleared() => PasswordBox.FocusInput();
}
