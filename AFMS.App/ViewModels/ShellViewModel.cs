using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>The main window: navigation, the current page, and signing out.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly IAuthenticationService _auth;
    private readonly ICurrentUser _session;
    private object _currentPage;
    private string _currentSection = "Dashboard";
    private string? _errorMessage;

    public ShellViewModel(
        IAuthenticationService auth,
        ICurrentUser session,
        DashboardViewModel dashboard,
        StockInViewModel stockIn,
        ReleasesViewModel releases,
        OutstandingViewModel outstanding,
        HistoryViewModel history,
        ReportsViewModel reports,
        ItemsViewModel items,
        PeopleViewModel people,
        SettingsViewModel settings)
    {
        _auth = auth;
        _session = session;

        DisplayName = session.User?.DisplayName ?? "AFMS user";

        DashboardCommand = Navigate(dashboard, "Dashboard", dashboard.InitializeAsync);
        StockInCommand = Navigate(stockIn, "Stock In", stockIn.InitializeAsync);
        ReleasesCommand = Navigate(releases, "Release", releases.InitializeAsync);
        OutstandingCommand = Navigate(outstanding, "Who Has What", outstanding.InitializeAsync);
        HistoryCommand = Navigate(history, "History", history.InitializeAsync);
        ReportsCommand = Navigate(reports, "Reports", reports.InitializeAsync);
        ItemsCommand = Navigate(items, "Items", items.InitializeAsync);
        PeopleCommand = Navigate(people, "People", people.InitializeAsync);
        SettingsCommand = Navigate(settings, "Settings", settings.InitializeAsync);

        SignOutCommand = new AsyncRelayCommand(
            SignOutAsync, onException: exception => ErrorMessage = UserFacingError.From(exception));

        settings.RestartRequested += () => RestartRequested?.Invoke();

        _currentPage = dashboard;
        _ = dashboard.InitializeAsync();
    }

    public event Action? SignOutRequested;
    public event Action? RestartRequested;

    public string DisplayName { get; }

    public ICommand DashboardCommand { get; }
    public ICommand StockInCommand { get; }
    public ICommand ReleasesCommand { get; }
    public ICommand OutstandingCommand { get; }
    public ICommand HistoryCommand { get; }
    public ICommand ReportsCommand { get; }
    public ICommand ItemsCommand { get; }
    public ICommand PeopleCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand SignOutCommand { get; }

    public object CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentSection
    {
        get => _currentSection;
        private set => SetProperty(ref _currentSection, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Called by the window when the inactivity timer runs out.</summary>
    public async Task HandleSessionExpiredAsync()
    {
        await _auth.SignOutAsync();
        SignOutRequested?.Invoke();
    }

    private ICommand Navigate(object page, string section, Func<Task> initialize) =>
        new AsyncRelayCommand(
            async () =>
            {
                CurrentPage = page;
                CurrentSection = section;
                ErrorMessage = null;
                await initialize();
            },
            onException: exception => ErrorMessage = UserFacingError.From(exception));

    private async Task SignOutAsync()
    {
        await _auth.SignOutAsync();
        SignOutRequested?.Invoke();
    }
}
