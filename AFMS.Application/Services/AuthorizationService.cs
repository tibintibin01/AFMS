using AFMS.Application.Abstractions;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class AuthorizationService(ICurrentUserSession session) : IAuthorizationService
{
    public bool IsInRole(string roleName) =>
        session.User?.Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase) == true;

    public bool Can(UserActivityType activityType)
    {
        if (!session.IsAuthenticated)
        {
            return false;
        }

        return activityType switch
        {
            UserActivityType.AccountCreated or
            UserActivityType.AccountUpdated or
            UserActivityType.RecordDeleted => IsInRole("Administrator"),
            _ => true
        };
    }
}
