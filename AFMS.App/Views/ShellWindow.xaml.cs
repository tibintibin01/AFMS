using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AFMS.App.ViewModels;
using AFMS.Core;
using Serilog;

namespace AFMS.App.Views;

public partial class ShellWindow : Window
{
    private readonly ICurrentUser _session;
    private readonly ShellViewModel _viewModel;
    private readonly DispatcherTimer _idleTimer;
    private bool _expiring;

    public ShellWindow(ShellViewModel viewModel, ICurrentUser session)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _session = session;

        // Any typing or clicking counts as activity and pushes back the auto-lock.
        PreviewMouseDown += RecordActivity;
        PreviewKeyDown += RecordActivity;

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _idleTimer.Tick += CheckIdleTimeout;
        _idleTimer.Start();

        Closed += (_, _) =>
        {
            _idleTimer.Stop();
            PreviewMouseDown -= RecordActivity;
            PreviewKeyDown -= RecordActivity;
        };
    }

    private void RecordActivity(object sender, InputEventArgs e) => _session.Touch();

    private async void CheckIdleTimeout(object? sender, EventArgs e)
    {
        if (_expiring || !_session.HasExpired) return;

        _expiring = true;
        _idleTimer.Stop();

        try
        {
            MessageBox.Show(
                "AFMS locked itself after a period of inactivity. Sign in again to continue.",
                "Session locked", MessageBoxButton.OK, MessageBoxImage.Information);
            await _viewModel.HandleSessionExpiredAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not close an expired session cleanly");
            Close();
        }
    }
}
