using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AFMS.App.Services;
using AFMS.App.ViewModels;
using AFMS.App.Views;
using AFMS.Core;
using AFMS.Core.Domain;
using AFMS.Core.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AFMS.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private DispatcherTimer? _dailyBackupTimer;
    private bool _restartRequested;
    private bool _databaseReady;
    private Task? _shutdownTask;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RegisterGlobalExceptionHandlers();

        var paths = new ApplicationPaths();
        paths.EnsureDirectoriesExist();
        ConfigureLogging(paths);

        try
        {
            // A queued restore has to be applied before anything opens the database.
            BackupService.ApplyPendingRestore(paths);

            _host = BuildHost(e.Args, paths);
            await _host.StartAsync();

            if (!await PrepareDatabaseAsync(paths)) return;
            if (!await RunFirstRunSetupIfNeededAsync()) return;

            StartDailyBackupTimer();
            await RunSessionLoopAsync();

            await PrepareShutdownAsync();
            Shutdown();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "AFMS could not start");
            MessageBox.Show(
                "AFMS could not start. Details were written to the log folder under " +
                $"{paths.LogDirectory}.\n\n{exception.Message}",
                "AFMS could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            await PrepareShutdownAsync(showBackupWarning: false);
            Shutdown(-1);
        }
    }

    private static void ConfigureLogging(ApplicationPaths paths)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AFMS Inventory")
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, "afms-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31)
            .CreateLogger();
    }

    private static IHost BuildHost(string[] args, ApplicationPaths paths) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddInventoryCore($"Data Source={paths.DatabasePath}");
                services.AddSingleton<IApplicationPaths>(paths);
                services.AddSingleton<IUserDialogService, UserDialogService>();
                services.AddSingleton<ISignInPreferences, SignInPreferences>();

                // Replaces the empty provider registered by the core project.
                services.AddSingleton<IStarterDataProvider, JsonStarterDataProvider>();

                services.AddTransient<FirstRunViewModel>();
                services.AddTransient<SignInViewModel>();
                services.AddTransient<ChangePasswordViewModel>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<ItemsViewModel>();
                services.AddTransient<PeopleViewModel>();
                services.AddTransient<StockInViewModel>();
                services.AddTransient<ReleasesViewModel>();
                services.AddTransient<OutstandingViewModel>();
                services.AddTransient<HistoryViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();

                services.AddTransient<FirstRunWindow>();
                services.AddTransient<SignInWindow>();
                services.AddTransient<ChangePasswordWindow>();
            })
            .Build();

    private async Task<bool> PrepareDatabaseAsync(ApplicationPaths paths)
    {
        using var scope = _host!.Services.CreateScope();
        var services = scope.ServiceProvider;
        var backups = services.GetRequiredService<IBackupService>();

        // Take a safety copy before a schema upgrade touches existing records.
        var database = services.GetRequiredService<Core.Persistence.InventoryDbContext>();
        if (File.Exists(paths.DatabasePath) &&
            (await database.Database.GetPendingMigrationsAsync()).Any())
        {
            await backups.CreateAsync(BackupKind.BeforeUpgrade);
        }

        await services.GetRequiredService<IDatabaseSetup>().InitializeAsync();
        _databaseReady = true;

        await backups.WritePendingRestoreAuditAsync();
        await backups.RunDailyBackupIfDueAsync();
        return true;
    }

    private async Task<bool> RunFirstRunSetupIfNeededAsync()
    {
        using var scope = _host!.Services.CreateScope();
        var services = scope.ServiceProvider;

        if (!await services.GetRequiredService<IAuthenticationService>().NeedsFirstAccountAsync())
            return true;

        var window = services.GetRequiredService<FirstRunWindow>();
        if (window.ShowDialog() == true) return true;

        await PrepareShutdownAsync();
        Shutdown();
        return false;
    }

    private void StartDailyBackupTimer()
    {
        _dailyBackupTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _dailyBackupTimer.Tick += async (_, _) =>
        {
            try
            {
                using var scope = _host!.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IBackupService>()
                    .RunDailyBackupIfDueAsync();
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Scheduled backup failed");
            }
        };
        _dailyBackupTimer.Start();
    }

    /// <summary>
    /// Sign in, work, sign out, repeat. Each pass gets its own dependency scope so
    /// no data from a previous session is carried over.
    /// </summary>
    private async Task RunSessionLoopAsync()
    {
        while (true)
        {
            using var scope = _host!.Services.CreateScope();
            var services = scope.ServiceProvider;

            var signIn = services.GetRequiredService<SignInWindow>();
            if (signIn.ShowDialog() != true) break;

            var session = services.GetRequiredService<ICurrentUser>();
            if (session.User?.MustChangePassword == true)
            {
                var change = services.GetRequiredService<ChangePasswordWindow>();
                if (change.ShowDialog() != true)
                {
                    await services.GetRequiredService<IAuthenticationService>().SignOutAsync();
                    continue;
                }
            }

            var shellViewModel = services.GetRequiredService<ShellViewModel>();
            var shell = new ShellWindow(shellViewModel, session);

            void Restart()
            {
                _restartRequested = true;
                shell.Close();
            }

            shellViewModel.SignOutRequested += shell.Close;
            shellViewModel.RestartRequested += Restart;
            MainWindow = shell;

            shell.ShowDialog();

            shellViewModel.SignOutRequested -= shell.Close;
            shellViewModel.RestartRequested -= Restart;

            if (_restartRequested) break;

            // Still signed in means the window was closed with the X, so exit.
            if (session.IsSignedIn) break;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        if (_restartRequested && !string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true });
        }
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
        Log.Error(e.Exception, "Unhandled user-interface error");
        MessageBox.Show(
            "Something unexpected happened and AFMS will close to protect your records. " +
            "The details were written to the log folder.",
            "AFMS error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        ShutdownAfterFatalError();
    }

    private Task PrepareShutdownAsync(bool showBackupWarning = true) =>
        _shutdownTask ??= PrepareShutdownCoreAsync(showBackupWarning);

    private async Task PrepareShutdownCoreAsync(bool showBackupWarning)
    {
        _dailyBackupTimer?.Stop();

        if (_host is not null && _databaseReady)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                using var scope = _host.Services.CreateScope();
                var backups = scope.ServiceProvider.GetRequiredService<IBackupService>();
                if (!backups.HasPendingRestore)
                    await backups.CreateAsync(BackupKind.OnClosing, cancellationToken: timeout.Token);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Closing backup failed");
                if (showBackupWarning)
                {
                    MessageBox.Show(
                        "AFMS could not finish the automatic backup on closing. Your records are " +
                        "safe in the live database, but make a manual backup when you can.",
                        "Backup warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
        await PrepareShutdownAsync(showBackupWarning: false);
        Shutdown(-1);
    }
}
