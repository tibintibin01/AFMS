using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.Application.Services;

public sealed class CurrentUserSession : ICurrentUserSession
{
    public CurrentUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;

    public void Begin(CurrentUser user) => User = user ?? throw new ArgumentNullException(nameof(user));

    public void End() => User = null;
}
