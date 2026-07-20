using AFMS.Application.Abstractions;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class AuthorizationService(ICurrentUserSession session) : IAuthorizationService
{
    public bool HasPermission(SystemPermission permission) =>
        !session.HasExpired && session.User?.Permissions.Contains(permission) == true;

    public void EnsurePermission(SystemPermission permission)
    {
        if (!HasPermission(permission))
        {
            throw new UnauthorizedAccessException(session.HasExpired
                ? "Your AFMS session expired because of inactivity. Sign in again."
                : "You are not authorized to perform this action.");
        }
        session.Touch();
    }
}
