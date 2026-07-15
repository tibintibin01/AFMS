using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.App.Commands;

namespace AFMS.App.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAuthorizationService _authorizationService;
    private PlaceholderPageViewModel _currentPage;
    private string? _errorMessage;

    public ShellViewModel(
        IAuthenticationService authenticationService,
        IAuthorizationService authorizationService,
        ICurrentUserSession session)
    {
        _authenticationService = authenticationService;
        _authorizationService = authorizationService;
        DisplayName = session.User?.DisplayName ?? "AFMS User";
        RoleSummary = session.User is null ? string.Empty : string.Join(" • ", session.User.Roles.OrderBy(role => role));

        DashboardCommand = NavigateCommand("Dashboard", "Treasury accountability at a glance", "Operational summaries and alerts will appear here as modules are implemented.");
        AccountableFormsCommand = NavigateCommand("Accountable Forms", "Official receipt and form inventory", "Stock receiving and accountable-form lifecycle features are intentionally reserved for the next phase.");
        TransactionsCommand = NavigateCommand("Transactions", "Issuance, usage, transfer, and returns", "Transaction entry and approval workflows will be implemented in a later phase.");
        CollectorsCommand = NavigateCommand("Collectors", "Collector records and accountability", "Collector maintenance will be added after the foundation is approved.");
        ReportsCommand = NavigateCommand("Reports", "Audit-ready treasury reporting", "Operational and statutory reports will be implemented in a later phase.");
        AdministrationCommand = NavigateCommand("Administration", "Users, roles, settings, and audit", "Administration modules will build on the authorization and audit foundation now in place.");
        LogoutCommand = new AsyncRelayCommand(LogoutAsync, onException: exception => ErrorMessage = exception.Message);

        _currentPage = new PlaceholderPageViewModel(
            "Dashboard",
            "Treasury accountability at a glance",
            "Operational summaries and alerts will appear here as modules are implemented.");
    }

    public event Action? LogoutRequested;
    public string DisplayName { get; }
    public string RoleSummary { get; }
    public bool CanAccessAdministration => _authorizationService.IsInRole("Administrator");
    public ICommand DashboardCommand { get; }
    public ICommand AccountableFormsCommand { get; }
    public ICommand TransactionsCommand { get; }
    public ICommand CollectorsCommand { get; }
    public ICommand ReportsCommand { get; }
    public ICommand AdministrationCommand { get; }
    public ICommand LogoutCommand { get; }

    public PlaceholderPageViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private ICommand NavigateCommand(string title, string subtitle, string description) =>
        new RelayCommand(() => CurrentPage = new PlaceholderPageViewModel(title, subtitle, description));

    private async Task LogoutAsync()
    {
        await _authenticationService.LogoutAsync();
        LogoutRequested?.Invoke();
    }
}

public sealed record PlaceholderPageViewModel(string Title, string Subtitle, string Description);
