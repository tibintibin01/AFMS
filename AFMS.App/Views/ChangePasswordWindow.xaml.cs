using System.Windows;
using System.Windows.Controls;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.PasswordChanged += HandlePasswordChanged;
        Closed += (_, _) => _viewModel.PasswordChanged -= HandlePasswordChanged;
    }

    private void HandlePasswordChanged() => DialogResult = true;

    private void OnCurrentChanged(object sender, RoutedEventArgs e) =>
        _viewModel.CurrentPassword = ((PasswordBox)sender).Password;

    private void OnNewChanged(object sender, RoutedEventArgs e) =>
        _viewModel.NewPassword = ((PasswordBox)sender).Password;

    private void OnConfirmChanged(object sender, RoutedEventArgs e) =>
        _viewModel.ConfirmPassword = ((PasswordBox)sender).Password;
}
