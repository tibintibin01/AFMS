using System.Windows;
using System.Windows.Controls;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView() => InitializeComponent();
    private void HandlePasswordChanged(object sender, RoutedEventArgs e) { if (DataContext is UserManagementViewModel viewModel) viewModel.Password = ((PasswordBox)sender).Password; }
    private void HandleConfirmPasswordChanged(object sender, RoutedEventArgs e) { if (DataContext is UserManagementViewModel viewModel) viewModel.ConfirmPassword = ((PasswordBox)sender).Password; }
}
