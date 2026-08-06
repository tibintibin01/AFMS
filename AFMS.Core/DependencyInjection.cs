using AFMS.Core.Files;
using AFMS.Core.Persistence;
using AFMS.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Core;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything the app needs. SQLite is chosen only here, so moving
    /// to another database later would not touch any screen or service.
    /// </summary>
    public static IServiceCollection AddInventoryCore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(InventoryDbContext).Assembly.FullName)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        // One session for the whole process: this is a single-operator desktop app.
        services.AddSingleton<ICurrentUser, CurrentUser>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IApplicationPaths, ApplicationPaths>();
        services.AddSingleton<IPdfReportWriter, PdfReportWriter>();
        services.AddSingleton<IExcelReportWriter, ExcelReportWriter>();

        services.AddScoped<IDatabaseSetup, DatabaseSetup>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IReferenceNumberService, ReferenceNumberService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IRecipientService, RecipientService>();
        services.AddScoped<IStockInService, StockInService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IAccountabilityService, AccountabilityService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IReportService, ReportService>();

        // Replaced by the WPF layer, which reads starter-data.json.
        services.AddSingleton<IStarterDataProvider, NoStarterData>();

        return services;
    }
}

internal sealed class NoStarterData : IStarterDataProvider
{
    public Task<StarterData?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<StarterData?>(null);
}
