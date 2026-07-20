using AFMS.Domain.Entities;
using AFMS.Application.Models;

namespace AFMS.Application.Abstractions;

public interface IUserRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string normalizedUsername, Guid? excludingId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> ListAsync(Guid? userId, int limit, CancellationToken cancellationToken = default);
    Task<AuditLogPage> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);
}

public interface IAccountableFormTypeRepository
{
    Task<IReadOnlyList<AccountableFormType>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<AccountableFormType?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AccountableFormType entity, CancellationToken cancellationToken = default);
}

public interface IBarangayRepository
{
    Task<IReadOnlyList<Barangay>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<Barangay?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string normalizedName, Guid? excludingId, CancellationToken cancellationToken = default);
    Task AddAsync(Barangay entity, CancellationToken cancellationToken = default);
}

public interface ICollectorRepository
{
    Task<IReadOnlyList<Collector>> ListAsync(string? search, Guid? barangayId, bool? isActive, CancellationToken cancellationToken = default);
    Task<Collector?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NumberExistsAsync(string normalizedNumber, Guid? excludingId, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Collector entity, CancellationToken cancellationToken = default);
}

public interface ISystemSettingRepository
{
    Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken = default);
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task AddAsync(SystemSetting setting, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
