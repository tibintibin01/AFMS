using AFMS.Application.Models;

namespace AFMS.Application.Abstractions;

public interface IAccountableFormTypeService
{
    Task<IReadOnlyList<AccountableFormTypeDto>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<AccountableFormTypeDto> SaveAsync(SaveAccountableFormTypeRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IBarangayService
{
    Task<IReadOnlyList<BarangayDto>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<BarangayDto> SaveAsync(SaveBarangayRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface ICollectorService
{
    Task<IReadOnlyList<CollectorDto>> ListAsync(string? search, Guid? barangayId, bool? isActive, CancellationToken cancellationToken = default);
    Task<CollectorDto> SaveAsync(SaveCollectorRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserDto>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogDto>> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SystemSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditLogDto>> ListAsync(int limit = 250, CancellationToken cancellationToken = default);
    Task<AuditLogPage> SearchAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task RecordAsync(
        Domain.Enums.UserActivityType activityType,
        string description,
        string entityName,
        Guid entityId,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken = default,
        string? module = null,
        string? referenceNumber = null,
        string? reason = null);
}
