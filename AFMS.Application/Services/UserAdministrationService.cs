using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class UserAdministrationService(
    IUserRepository users,
    IRoleRepository roles,
    ICollectorRepository collectors,
    IAuditLogRepository auditLogs,
    IAuthorizationService authorization,
    ICurrentUserSession session,
    IPasswordHasher passwordHasher,
    IAuditService audit,
    IUnitOfWork unitOfWork) : IUserAdministrationService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        var userList = await users.ListAsync(search, isActive, cancellationToken);
        var collectorList = await collectors.ListAsync(null, null, null, cancellationToken);
        return userList.Select(user => Map(user, collectorList.FirstOrDefault(collector => collector.UserId == user.Id))).ToList();
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        return (await roles.ListAsync(cancellationToken)).Select(role => new RoleDto(role.Id, role.Name, role.Description)).ToList();
    }

    public async Task CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        var errors = CredentialValidator.ValidateUsername(request.Username)
            .Concat(CredentialValidator.ValidatePassword(request.TemporaryPassword)).ToList();
        if (string.IsNullOrWhiteSpace(request.FullName)) errors.Add("Full name is required.");
        if (await users.UsernameExistsAsync(request.Username.Trim().ToUpperInvariant(), null, cancellationToken)) errors.Add("Username already exists.");
        var role = await roles.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null) errors.Add("A valid role is required.");
        var collector = await GetAvailableCollectorAsync(request.LinkedCollectorId, null, cancellationToken, errors);
        if (errors.Count > 0) throw new ApplicationValidationException(errors);

        var user = new User
        {
            Username = request.Username.Trim(),
            DisplayName = request.FullName.Trim(),
            PasswordHash = passwordHasher.Hash(request.TemporaryPassword),
            MustChangePassword = request.MustChangePassword
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role! });
        if (collector is not null) collector.UserId = user.Id;
        await users.AddAsync(user, cancellationToken);
        await audit.RecordAsync(UserActivityType.AccountCreated, "System user created.", nameof(User), user.Id, null,
            new { user.Username, user.DisplayName, Role = role!.Name, Collector = collector?.FullName, user.IsActive, user.MustChangePassword }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        var user = await users.GetByIdAsync(request.Id, cancellationToken) ?? throw new InvalidOperationException("User was not found.");
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ApplicationValidationException(["Full name is required."]);
        var role = await roles.GetByIdAsync(request.RoleId, cancellationToken) ?? throw new ApplicationValidationException(["A valid role is required."]);
        var collectorList = await collectors.ListAsync(null, null, null, cancellationToken);
        var oldCollector = collectorList.FirstOrDefault(collector => collector.UserId == user.Id);
        var errors = new List<string>();
        var newCollector = await GetAvailableCollectorAsync(request.LinkedCollectorId, user.Id, cancellationToken, errors);
        if (errors.Count > 0) throw new ApplicationValidationException(errors);
        var oldValues = new { user.DisplayName, Roles = user.UserRoles.Where(item => item.IsActive && item.Role.IsActive).Select(item => item.Role.Name).ToArray(), Collector = oldCollector?.FullName, user.IsActive, user.MustChangePassword };
        var wasAdministrator = IsActiveAdministrator(user);
        var remainsAdministrator = request.IsActive && role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase);
        if (session.User?.Id == user.Id && !remainsAdministrator)
            throw new ApplicationValidationException(["You cannot deactivate or remove the Administrator role from the account currently signed in."]);
        if (wasAdministrator && !remainsAdministrator && await users.CountActiveAdministratorsAsync(cancellationToken) <= 1)
            throw new ApplicationValidationException(["AFMS must retain at least one active Administrator account."]);
        var securityChanged = user.IsActive != request.IsActive || user.MustChangePassword != request.MustChangePassword ||
            !oldValues.Roles.Contains(role.Name, StringComparer.OrdinalIgnoreCase);

        user.DisplayName = request.FullName.Trim(); user.IsActive = request.IsActive; user.MustChangePassword = request.MustChangePassword;
        if (securityChanged) user.SecurityVersion = checked(user.SecurityVersion + 1);
        if (oldCollector is not null && oldCollector.Id != newCollector?.Id) oldCollector.UserId = null;
        if (newCollector is not null) newCollector.UserId = user.Id;
        foreach (var userRole in user.UserRoles) userRole.IsActive = userRole.RoleId == role.Id;
        if (user.UserRoles.All(item => item.RoleId != role.Id)) user.UserRoles.Add(new UserRole { User = user, Role = role });

        await audit.RecordAsync(UserActivityType.AccountUpdated, "System user updated.", nameof(User), user.Id, oldValues,
            new { user.DisplayName, Role = role.Name, Collector = newCollector?.FullName, user.IsActive, user.MustChangePassword }, cancellationToken);
        if (!oldValues.Roles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
        {
            await audit.RecordAsync(UserActivityType.RoleAssigned, "User role assigned.", nameof(User), user.Id,
                new { Roles = oldValues.Roles }, new { Role = role.Name }, cancellationToken);
            await audit.RecordAsync(UserActivityType.PermissionChanged, "Effective permissions changed through role assignment.", nameof(User), user.Id,
                new { Roles = oldValues.Roles }, new { Role = role.Name }, cancellationToken, "User Administration", user.Username);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        var errors = CredentialValidator.ValidatePassword(request.TemporaryPassword);
        if (errors.Count > 0) throw new ApplicationValidationException(errors);
        var user = await users.GetByIdAsync(request.UserId, cancellationToken) ?? throw new InvalidOperationException("User was not found.");
        user.PasswordHash = passwordHasher.Hash(request.TemporaryPassword);
        user.MustChangePassword = request.ForceChange;
        user.FailedLoginCount = 0; user.LockoutEnd = null;
        user.SecurityVersion = checked(user.SecurityVersion + 1);
        await audit.RecordAsync(UserActivityType.PasswordReset, "User password reset.", nameof(User), user.Id, null,
            new { user.MustChangePassword }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        if (!isActive && session.User?.Id == id)
            throw new ApplicationValidationException(["You cannot deactivate the account currently signed in."]);
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("User was not found.");
        if (user.IsActive == isActive) return;
        if (!isActive && IsActiveAdministrator(user) && await users.CountActiveAdministratorsAsync(cancellationToken) <= 1)
            throw new ApplicationValidationException(["AFMS must retain at least one active Administrator account."]);
        var old = new { user.IsActive }; user.IsActive = isActive;
        user.SecurityVersion = checked(user.SecurityVersion + 1);
        await audit.RecordAsync(isActive ? UserActivityType.Activated : UserActivityType.Deactivated,
            isActive ? "User account activated." : "User account deactivated.", nameof(User), user.Id, old, new { user.IsActive }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageUsers);
        return (await auditLogs.ListAsync(userId, 100, cancellationToken)).Select(AuditService.Map).ToList();
    }

    private async Task<Collector?> GetAvailableCollectorAsync(Guid? collectorId, Guid? userId, CancellationToken cancellationToken, ICollection<string> errors)
    {
        if (collectorId is null) return null;
        var collector = await collectors.GetAsync(collectorId.Value, cancellationToken);
        if (collector is null) errors.Add("Linked collector was not found.");
        else if (collector.UserId is not null && collector.UserId != userId) errors.Add("Linked collector is already assigned to another user.");
        return collector;
    }

    private static UserDto Map(User user, Collector? collector)
    {
        var role = user.UserRoles.FirstOrDefault(item => item.IsActive && item.Role.IsActive)?.Role;
        return new UserDto(user.Id, user.Username, user.DisplayName, collector?.Id, collector?.FullName, role?.Id,
            role?.Name ?? "Unassigned", user.IsActive, user.CreatedAt, user.LastLoginAt, user.FailedLoginCount, user.MustChangePassword);
    }

    private static bool IsActiveAdministrator(User user) => user.IsActive && user.UserRoles.Any(userRole =>
        userRole.IsActive && userRole.Role.IsActive && userRole.Role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase));
}
