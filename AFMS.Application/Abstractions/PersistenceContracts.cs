using AFMS.Domain.Entities;

namespace AFMS.Application.Abstractions;

public interface IUserRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
