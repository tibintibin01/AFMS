using AFMS.Application.Abstractions;
using AFMS.Infrastructure.Files;
using AFMS.Infrastructure.Persistence;
using AFMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IDatabaseOperationCoordinator, DatabaseOperationCoordinator>();
        services.AddDbContext<AfmsDbContext>(options =>
            options.UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(AfmsDbContext).Assembly.FullName)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AfmsDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAccountableFormTypeRepository, AccountableFormTypeRepository>();
        services.AddScoped<IBarangayRepository, BarangayRepository>();
        services.AddScoped<ICollectorRepository, CollectorRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IReferenceNumberSequenceRepository, ReferenceNumberSequenceRepository>();
        services.AddScoped<ITransactionRunner, TransactionRunner>();
        services.AddScoped<IStockReceiptRepository, StockReceiptRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IIssuanceRepository, IssuanceRepository>();
        services.AddScoped<ICollectorAccountabilityRepository, CollectorAccountabilityRepository>();
        services.AddScoped<IOperationalTransactionRepository, OperationalTransactionRepository>();
        services.AddScoped<IReportDataRepository, ReportDataRepository>();
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddSingleton<IReportPdfWriter, ReportPdfWriter>();
        services.AddSingleton<IReportExcelWriter, ReportExcelWriter>();
        services.AddSingleton<IInitialDataProvider, EmptyInitialDataProvider>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IApplicationPathService, ApplicationPathService>();
        services.AddSingleton<IIssuancePdfWriter, IssuancePdfWriter>();
        services.AddScoped<ITransactionAttachmentService, TransactionAttachmentService>();
        return services;
    }
}
