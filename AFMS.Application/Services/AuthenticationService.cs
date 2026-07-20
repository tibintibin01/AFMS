using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Application.Validation;

namespace AFMS.Application.Services;

public sealed class AuthenticationService(
    IUserRepository users,
    IAuditLogRepository auditLogs,
    ISystemSettingRepository settings,
    IPasswordHasher passwordHasher,
    ICurrentUserSession session,
    IUnitOfWork unitOfWork) : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string DummyPasswordHash = "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim().ToUpperInvariant();
        var user = await users.GetByUsernameAsync(normalizedUsername, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (user is null || !user.IsActive)
        {
            TryDummyVerification(request.Password);
            await RecordAuditAsync(null, UserActivityType.LoginFailed, $"Failed login for '{request.Username}'.", cancellationToken);
            return AuthenticationResult.Failure("Invalid username or password.");
        }

        if (user.LockoutEnd > now)
        {
            await RecordAuditAsync(user.Id, UserActivityType.LoginFailed, "Login blocked while account was locked.", cancellationToken);
            return AuthenticationResult.Failure("Invalid username or password. Try again later if repeated attempts were made.");
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            user.UpdatedAt = now;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEnd = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }

            await RecordAuditAsync(user.Id, UserActivityType.LoginFailed, "Invalid password.", cancellationToken, save: false);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Failure("Invalid username or password.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var roles = user.UserRoles
            .Where(userRole => userRole.IsActive && userRole.Role.IsActive)
            .Select(userRole => userRole.Role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = user.UserRoles
            .Where(userRole => userRole.IsActive && userRole.Role.IsActive)
            .SelectMany(userRole => userRole.Role.Permissions)
            .Where(permission => permission.IsActive)
            .Select(permission => permission.Permission)
            .ToHashSet();
        var timeoutSetting = await settings.GetByKeyAsync("SessionTimeoutMinutes", cancellationToken);
        var timeoutMinutes = int.TryParse(timeoutSetting?.Value, out var configuredTimeout) ? Math.Clamp(configuredTimeout, 5, 480) : 30;
        session.Begin(new CurrentUser(
            user.Id,
            user.Username,
            user.DisplayName,
            roles,
            permissions,
            user.MustChangePassword,
            user.SecurityVersion), TimeSpan.FromMinutes(timeoutMinutes));

        await RecordAuditAsync(user.Id, UserActivityType.LoginSucceeded, "User logged in.", cancellationToken, save: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthenticationResult.Success();
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (session.User is { } currentUser)
        {
            await RecordAuditAsync(currentUser.Id, UserActivityType.Logout, "User logged out.", cancellationToken);
        }

        session.End();
    }

    public async Task<AuthenticationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (session.User is not { } currentUser)
            return AuthenticationResult.Failure("No authenticated user session exists.");
        var passwordErrors = CredentialValidator.ValidatePassword(newPassword);
        if (passwordErrors.Count > 0) return AuthenticationResult.Failure(string.Join(Environment.NewLine, passwordErrors));
        var user = await users.GetByIdAsync(currentUser.Id, cancellationToken);
        if (user is null || !user.IsActive) return AuthenticationResult.Failure("The user account is not available.");
        if (!passwordHasher.Verify(currentPassword, user.PasswordHash)) return AuthenticationResult.Failure("Current password is incorrect.");
        if (passwordHasher.Verify(newPassword, user.PasswordHash)) return AuthenticationResult.Failure("New password must be different from the current password.");

        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.SecurityVersion = checked(user.SecurityVersion + 1);
        await RecordAuditAsync(user.Id, UserActivityType.PasswordChanged, "User changed password.", cancellationToken, save: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        session.Begin(currentUser with { MustChangePassword = false, SecurityVersion = user.SecurityVersion });
        return AuthenticationResult.Success();
    }

    public async Task<bool> RefreshCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (session.User is not { } current) return false;
        var user = await users.GetByIdAsync(current.Id, cancellationToken);
        if (user is null || !user.IsActive) { session.End(); return false; }
        if (user.SecurityVersion != current.SecurityVersion) { session.End(); return false; }
        var roles = user.UserRoles.Where(x => x.IsActive && x.Role.IsActive).Select(x => x.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = user.UserRoles.Where(x => x.IsActive && x.Role.IsActive).SelectMany(x => x.Role.Permissions)
            .Where(x => x.IsActive).Select(x => x.Permission).ToHashSet();
        session.ReplaceUser(current with { DisplayName = user.DisplayName, Roles = roles, Permissions = permissions, MustChangePassword = user.MustChangePassword });
        return true;
    }

    private void TryDummyVerification(string password)
    {
        try { _ = passwordHasher.Verify(password, DummyPasswordHash); }
        catch { /* Deliberately normalize authentication timing without affecting the response. */ }
    }

    private async Task RecordAuditAsync(
        Guid? userId,
        UserActivityType activityType,
        string description,
        CancellationToken cancellationToken,
        bool save = true)
    {
        await auditLogs.AddAsync(new AuditLog
        {
            UserId = userId,
            ActivityType = activityType,
            Description = description,
            Module = "Security",
            ReferenceNumber = userId?.ToString(),
            MachineName = Environment.MachineName
        }, cancellationToken);

        if (save)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
