using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AFMS.Application.Abstractions;
using Serilog;
using AFMS.App.ViewModels;

namespace AFMS.App.Views;

public partial class ShellWindow : Window
{
    private readonly ICurrentUserSession _session;
    private readonly ShellViewModel _viewModel;
    private readonly DispatcherTimer _sessionTimer;
    private bool _expiring;
    private DateTimeOffset _lastPermissionRefresh = DateTimeOffset.UtcNow;

    public ShellWindow(ShellViewModel viewModel, ICurrentUserSession session)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel; _session = session;
        PreviewMouseDown += RecordActivity; PreviewKeyDown += RecordActivity;
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _sessionTimer.Tick += CheckSession; _sessionTimer.Start();
        Closed += (_, _) => _sessionTimer.Stop();
    }

    private void RecordActivity(object sender, InputEventArgs e) => _session.Touch();

    private async void CheckSession(object? sender, EventArgs e)
    {
        if (_expiring) return;
        if (_session.HasExpired)
        {
            _expiring = true; _sessionTimer.Stop();
            MessageBox.Show("Your AFMS session expired after a period of inactivity. Sign in again to continue.",
                "Session expired", MessageBoxButton.OK, MessageBoxImage.Information);
            await _viewModel.HandleSessionExpiredAsync();
            return;
        }
        if (DateTimeOffset.UtcNow - _lastPermissionRefresh < TimeSpan.FromMinutes(5)) return;
        _lastPermissionRefresh = DateTimeOffset.UtcNow;
        try
        {
            if (await _viewModel.RefreshPermissionsAsync()) return;
            _expiring = true; _sessionTimer.Stop();
            MessageBox.Show("Your account is no longer active. Sign in with an authorized account to continue.",
                "Account access changed", MessageBoxButton.OK, MessageBoxImage.Warning);
            _viewModel.CloseForAccountChange();
        }
        catch (Exception exception) { Log.Error(exception, "Current-user permission refresh failed"); }
    }
}
