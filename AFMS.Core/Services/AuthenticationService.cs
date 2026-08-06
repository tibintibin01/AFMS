using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class AuthenticationService(
    InventoryDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentUser session,
    IAuditService audit) : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Verified when the username is unknown so a wrong username and a wrong
    // password take roughly the same time to answer.
    private const string DecoyHash = "$2a$12$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    private const string GenericFailure = "Incorrect username or password.";

    public async Task<bool> NeedsFirstAccountAsync(CancellationToken cancellationToken = default) =>
        !await context.Users.AnyAsync(cancellationToken);

    public async Task<SignInResult> CreateFirstAccountAsync(
        CreateFirstAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await context.Users.AnyAsync(cancellationToken))
            return SignInResult.Failure("An account already exists on this computer. Sign in instead.");

        var errors = new ErrorList();
        errors.AddIf(string.IsNullOrWhiteSpace(request.DisplayName), "Full name is required.");
        errors.AddRange(CredentialRules.ValidateUsername(request.Username));
        errors.AddRange(CredentialRules.ValidatePassword(request.Password));
        if (errors.HasErrors) errors.ThrowIfAny();

        var user = new User
        {
            Username = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            MustChangePassword = false
        };
        context.Users.Add(user);

        await audit.RecordAsync(
            ActivityType.AccountCreated,
            $"Account '{user.Username}' created during first-time setup.",
            nameof(User), user.Id, user.Username,
            cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return SignInResult.Success();
    }

    public async Task<SignInResult> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var now = DateTimeOffset.UtcNow;

        // Username has a NOCASE collation, so a plain comparison matches any casing.
        var user = await context.Users.SingleOrDefaultAsync(
            candidate => candidate.Username == username, cancellationToken);

        if (user is null || !user.IsActive)
        {
            BurnTime(request.Password);
            await audit.RecordAsync(
                ActivityType.SignInFailed,
                $"Sign-in failed for '{username}'.",
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return SignInResult.Failure(GenericFailure);
        }

        if (user.LockedOutUntil > now)
        {
            await audit.RecordAsync(
                ActivityType.SignInFailed,
                "Sign-in blocked while the account was locked.",
                nameof(User), user.Id, user.Username,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            var minutes = Math.Max(1, (int)Math.Ceiling((user.LockedOutUntil.Value - now).TotalMinutes));
            return SignInResult.Failure(
                $"Too many incorrect attempts. Try again in about {minutes} minute{(minutes == 1 ? "" : "s")}.");
        }

        if (!passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            user.FailedSignInCount++;
            var locked = user.FailedSignInCount >= MaxFailedAttempts;
            if (locked)
            {
                user.LockedOutUntil = now.Add(LockoutDuration);
                user.FailedSignInCount = 0;
            }

            await audit.RecordAsync(
                ActivityType.SignInFailed,
                locked
                    ? "Incorrect password; the account was locked for 15 minutes."
                    : "Incorrect password.",
                nameof(User), user.Id, user.Username,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return SignInResult.Failure(locked
                ? "Too many incorrect attempts. The account is locked for 15 minutes."
                : GenericFailure);
        }

        user.FailedSignInCount = 0;
        user.LockedOutUntil = null;
        user.LastSignInAt = now;

        var timeout = await ReadSessionTimeoutAsync(cancellationToken);
        session.Begin(
            new SignedInUser(user.Id, user.Username, user.DisplayName, user.MustChangePassword),
            timeout);

        await audit.RecordAsync(
            ActivityType.SignedIn, "Signed in.",
            nameof(User), user.Id, user.Username,
            cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return SignInResult.Success();
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (session.User is { } current)
        {
            await audit.RecordAsync(
                ActivityType.SignedOut, "Signed out.",
                nameof(User), current.Id, current.Username,
                cancellationToken: cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        session.End();
    }

    public async Task<SignInResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (session.User is not { } current)
            return SignInResult.Failure("Sign in first.");

        var passwordErrors = CredentialRules.ValidatePassword(newPassword);
        if (passwordErrors.Count > 0)
            return SignInResult.Failure(string.Join(Environment.NewLine, passwordErrors));

        var user = await context.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == current.Id, cancellationToken);
        if (user is null || !user.IsActive)
            return SignInResult.Failure("This account is no longer available.");

        if (!passwordHasher.Verify(currentPassword ?? string.Empty, user.PasswordHash))
            return SignInResult.Failure("The current password is incorrect.");
        if (passwordHasher.Verify(newPassword, user.PasswordHash))
            return SignInResult.Failure("The new password must be different from the current one.");

        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.FailedSignInCount = 0;

        await audit.RecordAsync(
            ActivityType.PasswordChanged, "Password changed.",
            nameof(User), user.Id, user.Username,
            cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        session.Replace(current with { MustChangePassword = false });
        return SignInResult.Success();
    }

    private void BurnTime(string? password)
    {
        try
        {
            _ = passwordHasher.Verify(password ?? string.Empty, DecoyHash);
        }
        catch
        {
            // Only here to even out response time; the outcome is discarded.
        }
    }

    private async Task<TimeSpan> ReadSessionTimeoutAsync(CancellationToken cancellationToken)
    {
        var setting = await context.SystemSettings.SingleOrDefaultAsync(
            entry => entry.Key == SettingsCatalog.SessionTimeoutMinutes, cancellationToken);
        var minutes = int.TryParse(setting?.Value, out var configured)
            ? Math.Clamp(configured, 5, 480)
            : 30;
        return TimeSpan.FromMinutes(minutes);
    }
}
