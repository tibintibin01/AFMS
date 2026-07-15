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
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public sealed class Role : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
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
    public string? MachineName { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
