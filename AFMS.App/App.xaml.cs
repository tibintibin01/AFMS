using System.IO;
using System.Windows;
using System.Windows.Threading;
using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.App.ViewModels;
using AFMS.App.Views;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AFMS.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private IServiceScope? _applicationScope;

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
            _host = Host.CreateDefaultBuilder(e.Args)
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddApplication();
                    services.AddInfrastructure($"Data Source={paths.DatabasePath}");
                    services.AddSingleton<IApplicationPathService>(paths);
                    services.AddTransient<FirstRunSetupViewModel>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<ShellViewModel>();
                    services.AddTransient<FirstRunSetupWindow>();
                    services.AddTransient<LoginWindow>();
                })
                .Build();

            await _host.StartAsync();
            _applicationScope = _host.Services.CreateScope();
            var services = _applicationScope.ServiceProvider;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

            if (await services.GetRequiredService<IFirstRunService>().RequiresAdministratorSetupAsync())
            {
                var setupWindow = services.GetRequiredService<FirstRunSetupWindow>();
                if (setupWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            while (true)
            {
                var loginWindow = services.GetRequiredService<LoginWindow>();
                if (loginWindow.ShowDialog() != true)
                {
                    break;
                }

                var shellViewModel = services.GetRequiredService<ShellViewModel>();
                var shellWindow = new ShellWindow(shellViewModel);
                shellViewModel.LogoutRequested += shellWindow.Close;
                MainWindow = shellWindow;
                shellWindow.ShowDialog();
                shellViewModel.LogoutRequested -= shellWindow.Close;

                if (services.GetRequiredService<ICurrentUserSession>().IsAuthenticated)
                {
                    break;
                }
            }

            Shutdown();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "AFMS failed during startup");
            MessageBox.Show(
                $"AFMS could not start. Details were written to the application log.\n\n{exception.Message}",
                "AFMS startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _applicationScope?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
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
        Shutdown(-1);
    }
}
