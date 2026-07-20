using System.Windows;
using System.Windows.Controls;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;
    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent(); _viewModel = viewModel; DataContext = viewModel;
        _viewModel.PasswordChanged += HandlePasswordChanged;
        Closed += (_, _) => _viewModel.PasswordChanged -= HandlePasswordChanged;
    }
    private void HandlePasswordChanged() => DialogResult = true;
    private void HandleCurrentPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.CurrentPassword = ((PasswordBox)sender).Password;
    private void HandleNewPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.NewPassword = ((PasswordBox)sender).Password;
    private void HandleConfirmPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.ConfirmPassword = ((PasswordBox)sender).Password;
}
