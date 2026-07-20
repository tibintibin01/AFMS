using AFMS.Application.Abstractions;
using AFMS.Application.Services;
using AFMS.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SerialRangeService>();
        services.AddSingleton<BookletGenerationService>();
        services.AddSingleton<ICurrentUserSession, CurrentUserSession>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IFirstRunService, FirstRunService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditQueryService, AuditService>();
        services.AddScoped<IAccountableFormTypeService, AccountableFormTypeService>();
        services.AddScoped<IBarangayService, BarangayService>();
        services.AddScoped<ICollectorService, CollectorService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IReferenceNumberService, ReferenceNumberService>();
        services.AddScoped<IStockReceivingService, StockReceivingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockLookupService, StockLookupService>();
        services.AddScoped<IIssuanceService, IssuanceService>();
        services.AddScoped<ICollectorAccountabilityService, CollectorAccountabilityService>();
        services.AddScoped<IIssuanceReceiptService, IssuanceReceiptService>();
        services.AddScoped<IReportSettingsReader, ReportSettingsReader>();
        services.AddScoped<IOperationalTransactionService, OperationalTransactionService>();
        services.AddSingleton<IReportCatalog, ReportCatalog>();
        services.AddScoped<IReportQueryService, ReportQueryService>();
        services.AddScoped<IReportCenterService, ReportCenterService>();
        services.AddScoped<IFiscalYearService, FiscalYearService>();
        services.AddScoped<IFiscalPeriodGuard, FiscalPeriodGuard>();
        return services;
    }
}
