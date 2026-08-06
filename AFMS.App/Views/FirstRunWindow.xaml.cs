using System.Windows;
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
        Loaded += (_, _) => NameBox.Focus();
    }

    private void HandleAccountCreated() => DialogResult = true;
}
