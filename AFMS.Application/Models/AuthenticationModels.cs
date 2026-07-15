namespace AFMS.Application.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record CreateAdministratorRequest(string Username, string DisplayName, string Password);

public sealed record CurrentUser(Guid Id, string Username, string DisplayName, IReadOnlySet<string> Roles);

public sealed record AuthenticationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static AuthenticationResult Success() => new(true);
    public static AuthenticationResult Failure(string message) => new(false, message);
}
