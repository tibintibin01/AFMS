using System.Windows;
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
        Loaded += (_, _) => CurrentBox.FocusInput();
    }

    private void HandlePasswordChanged() => DialogResult = true;
}
