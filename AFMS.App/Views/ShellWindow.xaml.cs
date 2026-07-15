using System.Windows;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
