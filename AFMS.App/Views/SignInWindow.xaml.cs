using System.Windows;
using System.Windows.Controls;
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
        Closed += (_, _) => _viewModel.SignedIn -= HandleSignedIn;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void HandleSignedIn() => DialogResult = true;

    // The password never lives in a bindable property on the view model until typed,
    // so it is passed across explicitly rather than through two-way binding.
    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = ((PasswordBox)sender).Password;
}
