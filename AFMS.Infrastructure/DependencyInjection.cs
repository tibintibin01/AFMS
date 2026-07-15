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
        services.AddDbContext<AfmsDbContext>(options =>
            options.UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(AfmsDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AfmsDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IApplicationPathService, ApplicationPathService>();
        return services;
    }
}
