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
    void Begin(CurrentUser user);
    void End();
}

public interface IAuthorizationService
{
    bool IsInRole(string roleName);
    bool Can(UserActivityType activityType);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
