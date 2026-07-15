using AFMS.Application.Abstractions;
using AFMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFMS.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    AfmsDbContext context,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    private static readonly (string Name, string Description)[] DefaultRoles =
    [
        ("Administrator", "Full system administration access."),
        ("TreasuryStaff", "Municipal Treasury Office operational access."),
        ("Collector", "Collector self-service access."),
        ("Auditor", "Read-only audit and reporting access.")
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing AFMS database");
        await context.Database.MigrateAsync(cancellationToken);

        var existingRoleNames = await context.Roles
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);
        foreach (var (name, description) in DefaultRoles.Where(role => !existingRoleNames.Contains(role.Name)))
        {
            context.Roles.Add(new Role { Name = name, Description = description });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("AFMS database initialization completed");
    }
}
