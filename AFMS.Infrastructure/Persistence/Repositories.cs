using AFMS.Application.Abstractions;
using AFMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Infrastructure.Persistence;

internal sealed class UserRepository(AfmsDbContext context) : IUserRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(cancellationToken);

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(
                user => user.Username.ToUpper() == normalizedUsername,
                cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        context.Users.AddAsync(user, cancellationToken).AsTask();
}

internal sealed class RoleRepository(AfmsDbContext context) : IRoleRepository
{
    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        context.Roles.SingleOrDefaultAsync(role => role.Name == name, cancellationToken);
}

internal sealed class AuditLogRepository(AfmsDbContext context) : IAuditLogRepository
{
    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        context.AuditLogs.AddAsync(auditLog, cancellationToken).AsTask();
}
