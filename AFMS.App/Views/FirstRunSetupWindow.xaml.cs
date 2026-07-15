using System.Windows;
using System.Windows.Controls;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class FirstRunSetupWindow : Window
{
    private readonly FirstRunSetupViewModel _viewModel;

    public FirstRunSetupWindow(FirstRunSetupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.SetupCompleted += HandleSetupCompleted;
        Closed += (_, _) => _viewModel.SetupCompleted -= HandleSetupCompleted;
    }

    private void HandleSetupCompleted() => DialogResult = true;

    private void HandlePasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = ((PasswordBox)sender).Password;

    private void HandleConfirmPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.ConfirmPassword = ((PasswordBox)sender).Password;
}
