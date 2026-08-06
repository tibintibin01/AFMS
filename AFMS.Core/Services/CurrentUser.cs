namespace AFMS.Core.Services;

/// <summary>
/// Holds the signed-in user for the life of the process. The app is single user,
/// so one instance is shared; the lock keeps the inactivity timer and the UI
/// thread from reading a half-updated session.
/// </summary>
public sealed class CurrentUser(TimeProvider? clock = null) : ICurrentUser
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _sync = new();
    private TimeSpan _timeout = TimeSpan.FromMinutes(30);

    public SignedInUser? User { get; private set; }

    public bool IsSignedIn => User is not null;

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool HasExpired => User is not null && ExpiresAt <= _clock.GetUtcNow();

    public void Begin(SignedInUser user, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_sync)
        {
            User = user;
            // A too-short window would lock the user out mid-task, so ignore it.
            if (timeout is { } value && value >= TimeSpan.FromMinutes(1))
                _timeout = value;
            Extend();
        }
    }

    public void Replace(SignedInUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_sync) User = user;
    }

    public void Touch()
    {
        lock (_sync)
        {
            if (User is not null) Extend();
        }
    }

    public void End()
    {
        lock (_sync)
        {
            User = null;
            ExpiresAt = null;
        }
    }

    public SignedInUser Require()
    {
        lock (_sync)
        {
            if (User is null)
                throw new UnauthorizedAccessException("Sign in to continue.");
            if (ExpiresAt <= _clock.GetUtcNow())
                throw new UnauthorizedAccessException(
                    "The session ended after a period of inactivity. Sign in again.");
            Extend();
            return User;
        }
    }

    private void Extend() => ExpiresAt = _clock.GetUtcNow().Add(_timeout);
}
