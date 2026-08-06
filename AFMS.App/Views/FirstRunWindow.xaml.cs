using System.Windows;
using System.Windows.Controls;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class FirstRunWindow : Window
{
    private readonly FirstRunViewModel _viewModel;

    public FirstRunWindow(FirstRunViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.AccountCreated += HandleAccountCreated;
        Closed += (_, _) => _viewModel.AccountCreated -= HandleAccountCreated;
    }

    private void HandleAccountCreated() => DialogResult = true;

    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = ((PasswordBox)sender).Password;

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.ConfirmPassword = ((PasswordBox)sender).Password;
}
