using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.App.Commands;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserSession _session;
    private object _currentPage;
    private string _currentSection = "Dashboard";
    private string? _errorMessage;

    public ShellViewModel(
        IAuthenticationService authenticationService,
        IAuthorizationService authorization,
        ICurrentUserSession session,
        AccountableFormTypesViewModel formTypes,
        BarangaysViewModel barangays,
        CollectorsViewModel collectors,
        UserManagementViewModel users,
        SystemSettingsViewModel settings,
        AuditLogViewModel auditLog,
        StockReceivingViewModel stockReceiving,
        InventoryViewModel inventory,
        StockMovementHistoryViewModel movementHistory,
        IssuanceViewModel issuance,
        CollectorLedgerViewModel collectorLedger,
        OperationalTransactionsViewModel operationalTransactions,
        ReportCenterViewModel reportCenter,
        MaintenanceViewModel maintenance,
        DashboardViewModel dashboard,
        IAuditService audit,
        IUnitOfWork unitOfWork)
    {
        _authenticationService = authenticationService; _authorization = authorization;
        _audit = audit; _unitOfWork = unitOfWork; _session = session;
        DisplayName = session.User?.DisplayName ?? "AFMS User";
        RoleSummary = session.User is null ? string.Empty : string.Join(" • ", session.User.Roles.OrderBy(role => role));

        UpdatePermissionProperties();

        DashboardCommand = new AsyncRelayCommand(() => NavigateAsync(dashboard, "Dashboard", dashboard.InitializeAsync));
        AccountableFormsCommand = new AsyncRelayCommand(() => NavigateAsync(formTypes, "Form Types", formTypes.InitializeAsync));
        BarangaysCommand = new AsyncRelayCommand(() => NavigateAsync(barangays, "Barangays", barangays.InitializeAsync));
        CollectorsCommand = new AsyncRelayCommand(() => NavigateAsync(collectors, "Collectors", collectors.InitializeAsync));
        StockReceivingCommand = new AsyncRelayCommand(() => NavigateAsync(stockReceiving, "Receive Stock", stockReceiving.InitializeAsync));
        InventoryCommand = new AsyncRelayCommand(() => NavigateAsync(inventory, "Stock Inventory", inventory.InitializeAsync));
        StockMovementsCommand = new AsyncRelayCommand(() => NavigateAsync(movementHistory, "Movement History", movementHistory.InitializeAsync));
        IssuanceCommand = new AsyncRelayCommand(() => NavigateAsync(issuance, "Issue to Collectors", issuance.InitializeAsync));
        CollectorLedgerCommand = new AsyncRelayCommand(() => NavigateAsync(collectorLedger, "Collector Ledger", collectorLedger.InitializeAsync));
        TransactionsCommand = new AsyncRelayCommand(() => NavigateAsync(operationalTransactions, "Usage, Transfers & Returns", operationalTransactions.InitializeAsync));
        ReportsCommand = new AsyncRelayCommand(() => NavigateAsync(reportCenter, "Reports", reportCenter.InitializeAsync));
        UserManagementCommand = new AsyncRelayCommand(() => NavigateAsync(users, "Users", users.InitializeAsync));
        SystemSettingsCommand = new AsyncRelayCommand(() => NavigateAsync(settings, "System Settings", settings.InitializeAsync));
        AuditLogCommand = new AsyncRelayCommand(() => NavigateAsync(auditLog, "Audit Log", auditLog.InitializeAsync));
        MaintenanceCommand = new AsyncRelayCommand(() => NavigateAsync(maintenance, "Backup & Fiscal Years", maintenance.InitializeAsync));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync, onException: exception => ErrorMessage = Services.UserFacingError.From(exception));
        maintenance.RestartRequested += () => RestartRequested?.Invoke();

        _currentPage = dashboard;
        _ = dashboard.InitializeAsync();
    }

    public event Action? LogoutRequested;
    public event Action? RestartRequested;
    public string DisplayName { get; private set; }
    public string RoleSummary { get; private set; }
    public bool CanViewDashboard { get; private set; }
    public bool CanManageFormTypes { get; private set; }
    public bool CanManageBarangays { get; private set; }
    public bool CanManageCollectors { get; private set; }
    public bool CanManageUsers { get; private set; }
    public bool CanManageSettings { get; private set; }
    public bool CanViewAuditLogs { get; private set; }
    public bool CanViewTransactions { get; private set; }
    public bool CanReceiveStock { get; private set; }
    public bool CanViewInventory { get; private set; }
    public bool CanViewStockMovements { get; private set; }
    public bool CanViewIssuances { get; private set; }
    public bool CanViewCollectorAccountability { get; private set; }
    public bool CanViewReports { get; private set; }
    public bool CanMaintainSystem { get; private set; }
    public ICommand DashboardCommand { get; }
    public ICommand AccountableFormsCommand { get; }
    public ICommand BarangaysCommand { get; }
    public ICommand CollectorsCommand { get; }
    public ICommand TransactionsCommand { get; }
    public ICommand StockReceivingCommand { get; }
    public ICommand InventoryCommand { get; }
    public ICommand StockMovementsCommand { get; }
    public ICommand IssuanceCommand { get; }
    public ICommand CollectorLedgerCommand { get; }
    public ICommand ReportsCommand { get; }
    public ICommand UserManagementCommand { get; }
    public ICommand SystemSettingsCommand { get; }
    public ICommand AuditLogCommand { get; }
    public ICommand MaintenanceCommand { get; }
    public ICommand LogoutCommand { get; }
    public object CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }
    public string CurrentSection { get => _currentSection; private set => SetProperty(ref _currentSection, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    private async Task LogoutAsync() { await _authenticationService.LogoutAsync(); LogoutRequested?.Invoke(); }

    private async Task NavigateAsync(object page, string section, Func<Task> initialize)
    {
        CurrentPage = page;
        CurrentSection = section;
        await initialize();
    }

    public async Task HandleSessionExpiredAsync()
    {
        var user = _session.User;
        if (user is not null)
        {
            await _audit.RecordAsync(UserActivityType.SessionExpired, "Session ended after the configured inactivity timeout.",
                "UserSession", user.Id, null, null, module: "Security", referenceNumber: user.Username);
            await _unitOfWork.SaveChangesAsync();
        }
        await _authenticationService.LogoutAsync();
        LogoutRequested?.Invoke();
    }

    public async Task<bool> RefreshPermissionsAsync()
    {
        if (!await _authenticationService.RefreshCurrentUserAsync()) return false;
        DisplayName = _session.User?.DisplayName ?? "AFMS User";
        RoleSummary = _session.User is null ? string.Empty : string.Join(" • ", _session.User.Roles.OrderBy(role => role));
        RaisePropertyChanged(nameof(DisplayName)); RaisePropertyChanged(nameof(RoleSummary));
        UpdatePermissionProperties();
        return true;
    }
    public void CloseForAccountChange() => LogoutRequested?.Invoke();

    private void UpdatePermissionProperties()
    {
        CanViewDashboard = _authorization.HasPermission(SystemPermission.ViewDashboard);
        CanManageFormTypes = _authorization.HasPermission(SystemPermission.ManageFormTypes);
        CanManageBarangays = _authorization.HasPermission(SystemPermission.ManageBarangays);
        CanManageCollectors = _authorization.HasPermission(SystemPermission.ManageCollectors);
        CanManageUsers = _authorization.HasPermission(SystemPermission.ManageUsers);
        CanManageSettings = _authorization.HasPermission(SystemPermission.ManageSystemSettings);
        CanViewAuditLogs = _authorization.HasPermission(SystemPermission.ViewAuditLogs);
        CanViewTransactions = _authorization.HasPermission(SystemPermission.RecordUsage) || _authorization.HasPermission(SystemPermission.TransferStock) || _authorization.HasPermission(SystemPermission.ReturnStock) || _authorization.HasPermission(SystemPermission.ManageExceptions);
        CanReceiveStock = _authorization.HasPermission(SystemPermission.ReceiveStock);
        CanViewInventory = _authorization.HasPermission(SystemPermission.ViewInventory);
        CanViewStockMovements = _authorization.HasPermission(SystemPermission.ViewStockMovements);
        CanViewIssuances = _authorization.HasPermission(SystemPermission.ViewIssuances);
        CanViewCollectorAccountability = _authorization.HasPermission(SystemPermission.ViewCollectorAccountability);
        CanViewReports = _authorization.HasPermission(SystemPermission.ViewReports);
        CanMaintainSystem = _authorization.HasPermission(SystemPermission.BackupAndRestore) || _authorization.HasPermission(SystemPermission.ManageFiscalYears);
        foreach (var property in new[] { nameof(CanViewDashboard), nameof(CanManageFormTypes), nameof(CanManageBarangays), nameof(CanManageCollectors),
                     nameof(CanManageUsers), nameof(CanManageSettings), nameof(CanViewAuditLogs), nameof(CanViewTransactions), nameof(CanReceiveStock),
                     nameof(CanViewInventory), nameof(CanViewStockMovements), nameof(CanViewIssuances), nameof(CanViewCollectorAccountability),
                     nameof(CanViewReports), nameof(CanMaintainSystem) })
            RaisePropertyChanged(property);
    }
}
