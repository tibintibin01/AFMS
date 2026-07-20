using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.Application.Services;

public sealed class CurrentUserSession : ICurrentUserSession
{
    private readonly object _sync = new();
    private TimeSpan _timeout = TimeSpan.FromMinutes(30);
    public CurrentUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;
    public DateTimeOffset? LastActivityAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool HasExpired => User is not null && ExpiresAt <= DateTimeOffset.UtcNow;

    public void Begin(CurrentUser user, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_sync)
        {
            User = user;
            _timeout = timeout is { } value && value >= TimeSpan.FromMinutes(5) ? value : _timeout;
            TouchCore();
        }
    }

    public void Touch()
    {
        lock (_sync)
        {
            if (User is not null) TouchCore();
        }
    }

    public void ReplaceUser(CurrentUser user)
    {
        lock (_sync) User = user;
    }

    public void End()
    {
        lock (_sync) { User = null; LastActivityAt = null; ExpiresAt = null; }
    }

    private void TouchCore()
    {
        LastActivityAt = DateTimeOffset.UtcNow;
        ExpiresAt = LastActivityAt.Value.Add(_timeout);
    }
}
