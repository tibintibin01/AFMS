using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.App.ViewModels;
using AFMS.App.Views;
using AFMS.App.Services;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Files;
using AFMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AFMS.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private DispatcherTimer? _maintenanceTimer;
    private bool _restartRequested;
    private Task? _shutdownTask;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RegisterGlobalExceptionHandlers();

        var paths = new ApplicationPathService();
        paths.EnsureDirectoriesExist();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AFMS")
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, "afms-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31)
            .CreateLogger();

        try
        {
            BackupService.ApplyPendingRestore(paths);
            _host = Host.CreateDefaultBuilder(e.Args)
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddApplication();
                    services.AddInfrastructure($"Data Source={paths.DatabasePath}");
                    services.AddSingleton<IApplicationPathService>(paths);
                    services.AddTransient<FirstRunSetupViewModel>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<ChangePasswordViewModel>();
                    services.AddTransient<ShellViewModel>();
                    services.AddTransient<AccountableFormTypesViewModel>();
                    services.AddTransient<BarangaysViewModel>();
                    services.AddTransient<CollectorsViewModel>();
                    services.AddTransient<UserManagementViewModel>();
                    services.AddTransient<SystemSettingsViewModel>();
                    services.AddTransient<AuditLogViewModel>();
                    services.AddTransient<StockReceivingViewModel>();
                    services.AddTransient<InventoryViewModel>();
                    services.AddTransient<StockMovementHistoryViewModel>();
                    services.AddTransient<IssuanceViewModel>();
                    services.AddTransient<CollectorLedgerViewModel>();
                    services.AddTransient<OperationalTransactionsViewModel>();
                    services.AddTransient<ReportCenterViewModel>();
                    services.AddTransient<MaintenanceViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddSingleton<IUserDialogService, UserDialogService>();
                    services.AddSingleton<IInitialDataProvider, JsonInitialDataProvider>();
                    services.AddTransient<FirstRunSetupWindow>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<ChangePasswordWindow>();
                })
                .Build();

            await _host.StartAsync();
            using (var startupScope = _host.Services.CreateScope())
            {
                var startupServices = startupScope.ServiceProvider;
                var backupService = startupServices.GetRequiredService<IBackupService>();
                var database = startupServices.GetRequiredService<AfmsDbContext>();
                if (File.Exists(paths.DatabasePath) && (await database.Database.GetPendingMigrationsAsync()).Any())
                    await backupService.CreateAsync(AFMS.Domain.Enums.BackupKind.PreMigration);
                await startupServices.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
                await backupService.CompletePendingRestoreAuditAsync();
                await backupService.RunScheduledBackupIfDueAsync();

                if (await startupServices.GetRequiredService<IFirstRunService>().RequiresAdministratorSetupAsync())
                {
                    var setupWindow = startupServices.GetRequiredService<FirstRunSetupWindow>();
                    if (setupWindow.ShowDialog() != true)
                    {
                        await PrepareShutdownAsync();
                        Shutdown();
                        return;
                    }
                }
            }
            _maintenanceTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _maintenanceTimer.Tick += async (_, _) =>
            {
                try
                {
                    using var maintenanceScope = _host.Services.CreateScope();
                    await maintenanceScope.ServiceProvider.GetRequiredService<IBackupService>().RunScheduledBackupIfDueAsync();
                }
                catch (Exception exception) { Log.Error(exception, "Scheduled AFMS backup failed"); }
            };
            _maintenanceTimer.Start();

            while (true)
            {
                using var sessionScope = _host.Services.CreateScope();
                var services = sessionScope.ServiceProvider;
                var loginWindow = services.GetRequiredService<LoginWindow>();
                if (loginWindow.ShowDialog() != true)
                {
                    break;
                }

                var session = services.GetRequiredService<ICurrentUserSession>();
                if (session.User?.MustChangePassword == true)
                {
                    var changePasswordWindow = services.GetRequiredService<ChangePasswordWindow>();
                    if (changePasswordWindow.ShowDialog() != true)
                    {
                        await services.GetRequiredService<IAuthenticationService>().LogoutAsync();
                        continue;
                    }
                }

                var shellViewModel = services.GetRequiredService<ShellViewModel>();
                var shellWindow = new ShellWindow(shellViewModel, session);
                shellViewModel.LogoutRequested += shellWindow.Close;
                void Restart() { _restartRequested = true; shellWindow.Close(); }
                shellViewModel.RestartRequested += Restart;
                MainWindow = shellWindow;
                shellWindow.ShowDialog();
                shellViewModel.LogoutRequested -= shellWindow.Close;
                shellViewModel.RestartRequested -= Restart;

                if (_restartRequested) break;

                if (services.GetRequiredService<ICurrentUserSession>().IsAuthenticated)
                {
                    break;
                }
            }

            await PrepareShutdownAsync();
            Shutdown();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "AFMS failed during startup");
            MessageBox.Show(
                "AFMS could not start safely. Technical details were written to the application log.",
                "AFMS startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await PrepareShutdownAsync(false);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        if (_restartRequested && !string.IsNullOrWhiteSpace(Environment.ProcessPath))
            Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true });
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled application-domain exception");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled user-interface exception");
        MessageBox.Show(
            "An unexpected error occurred. Details were recorded in the AFMS log. The application will close to protect your data.",
            "AFMS error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        ShutdownAfterFatalError();
    }

    private Task PrepareShutdownAsync(bool showBackupWarning = true) =>
        _shutdownTask ??= PrepareShutdownCoreAsync(showBackupWarning);

    private async Task PrepareShutdownCoreAsync(bool showBackupWarning)
    {
        _maintenanceTimer?.Stop();
        if (_host is not null)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                using var shutdownScope = _host.Services.CreateScope();
                var backups = shutdownScope.ServiceProvider.GetRequiredService<IBackupService>();
                if (!backups.HasPendingRestore)
                    await backups.CreateAsync(AFMS.Domain.Enums.BackupKind.ApplicationClosing, cancellationToken: timeout.Token);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Automatic closing backup failed");
                if (showBackupWarning)
                    MessageBox.Show("AFMS could not complete the automatic closing backup. Your data remains in the live database; create a manual backup before shutting down Windows.",
                        "Closing backup warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(10));
            _host.Dispose();
            _host = null;
        }
        await Log.CloseAndFlushAsync();
    }

    private async void ShutdownAfterFatalError()
    {
        await PrepareShutdownAsync(false);
        Shutdown(-1);
    }
}
