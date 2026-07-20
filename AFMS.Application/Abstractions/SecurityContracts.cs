using AFMS.Application.Models;
using AFMS.Domain.Enums;

namespace AFMS.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> RefreshCurrentUserAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public interface IFirstRunService
{
    Task<bool> RequiresAdministratorSetupAsync(CancellationToken cancellationToken = default);
    Task<AuthenticationResult> CreateAdministratorAsync(
        CreateAdministratorRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICurrentUserSession
{
    CurrentUser? User { get; }
    bool IsAuthenticated { get; }
    DateTimeOffset? LastActivityAt { get; }
    DateTimeOffset? ExpiresAt { get; }
    bool HasExpired { get; }
    void Begin(CurrentUser user, TimeSpan? timeout = null);
    void Touch();
    void ReplaceUser(CurrentUser user);
    void End();
}

public interface IAuthorizationService
{
    bool HasPermission(SystemPermission permission);
    void EnsurePermission(SystemPermission permission);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
