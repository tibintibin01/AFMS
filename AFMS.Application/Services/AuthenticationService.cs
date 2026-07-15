using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class AuthenticationService(
    IUserRepository users,
    IAuditLogRepository auditLogs,
    IPasswordHasher passwordHasher,
    ICurrentUserSession session,
    IUnitOfWork unitOfWork) : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim().ToUpperInvariant();
        var user = await users.GetByUsernameAsync(normalizedUsername, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (user is null || !user.IsActive)
        {
            await RecordAuditAsync(null, UserActivityType.LoginFailed, $"Failed login for '{request.Username}'.", cancellationToken);
            return AuthenticationResult.Failure("Invalid username or password.");
        }

        if (user.LockoutEnd > now)
        {
            await RecordAuditAsync(user.Id, UserActivityType.LoginFailed, "Login blocked while account was locked.", cancellationToken);
            return AuthenticationResult.Failure("Account is temporarily locked. Please try again later.");
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
        session.Begin(new CurrentUser(user.Id, user.Username, user.DisplayName, roles));

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
            MachineName = Environment.MachineName
        }, cancellationToken);

        if (save)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
