using AFMS.Domain.Common;
using AFMS.Domain.Enums;

namespace AFMS.Domain.Entities;

public sealed class User : BaseEntity
{
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; }
    public int SecurityVersion { get; set; } = 1;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public sealed class Role : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

public sealed class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public SystemPermission Permission { get; set; }
}

public sealed class UserRole : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public sealed class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public UserActivityType ActivityType { get; set; }
    public required string Description { get; set; }
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? Module { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Reason { get; set; }
    public string? MachineName { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PreviousHash { get; set; }
    public string? IntegrityHash { get; set; }
}
