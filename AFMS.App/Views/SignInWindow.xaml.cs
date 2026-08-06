using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class SignInWindow : Window
{
    private readonly SignInViewModel _viewModel;

    // Set while copying text between the masked and plain boxes, so their change
    // handlers do not trigger each other in a loop.
    private bool _syncing;

    public SignInWindow(SignInViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.SignedIn += HandleSignedIn;
        _viewModel.PasswordCleared += HandlePasswordCleared;
        _viewModel.PropertyChanged += HandleViewModelChanged;

        Closed += (_, _) =>
        {
            _viewModel.SignedIn -= HandleSignedIn;
            _viewModel.PasswordCleared -= HandlePasswordCleared;
            _viewModel.PropertyChanged -= HandleViewModelChanged;
        };

        // A remembered username means the password is what is actually needed.
        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_viewModel.Username)) UsernameBox.Focus();
            else PasswordBox.Focus();
        };
    }

    private void HandleSignedIn() => DialogResult = true;

    private void HandleViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Keep the button's enabled state honest if the password is cleared elsewhere.
        if (e.PropertyName == nameof(SignInViewModel.Password) &&
            _viewModel.Password.Length == 0 &&
            (PasswordBox.Password.Length > 0 || PasswordPlainBox.Text.Length > 0))
        {
            HandlePasswordCleared();
        }
    }

    /// <summary>Wipes both boxes after a sign-in attempt and puts the cursor back.</summary>
    private void HandlePasswordCleared()
    {
        _syncing = true;
        try
        {
            PasswordBox.Clear();
            PasswordPlainBox.Clear();
        }
        finally
        {
            _syncing = false;
        }

        if (_viewModel.ShowPassword) PasswordPlainBox.Focus();
        else PasswordBox.Focus();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _viewModel.Password = PasswordBox.Password;
    }

    private void OnPasswordPlainChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        _viewModel.Password = PasswordPlainBox.Text;
    }

    /// <summary>Swaps the visible box, carrying the text and caret across.</summary>
    private void OnRevealChanged(object sender, RoutedEventArgs e)
    {
        var reveal = RevealToggle.IsChecked == true;

        _syncing = true;
        try
        {
            if (reveal)
            {
                PasswordPlainBox.Text = PasswordBox.Password;
                PasswordPlainBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                PasswordBox.Password = PasswordPlainBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordPlainBox.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            _syncing = false;
        }

        if (reveal)
        {
            PasswordPlainBox.Focus();
            PasswordPlainBox.CaretIndex = PasswordPlainBox.Text.Length;
        }
        else
        {
            PasswordBox.Focus();
        }
    }
}
