using AFMS.Core;
using AFMS.Core.Domain;
using AFMS.Core.Files;
using AFMS.Core.Persistence;
using AFMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Tests;

/// <summary>
/// The documentation guarantees: history cannot be rewritten, and the sign-in
/// rules hold. These are the parts a treasury office would be asked to prove.
/// </summary>
public sealed class AuditAndAuthTests
{
    [Fact]
    public async Task Every_recorded_action_leaves_an_audit_entry()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");
        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 30);

        var page = await app.Audit.SearchAsync(new AuditFilter());
        var activities = page.Entries.Select(entry => entry.Activity).ToList();

        Assert.Contains(ActivityType.ItemCreated, activities);
        Assert.Contains(ActivityType.RecipientCreated, activities);
        Assert.Contains(ActivityType.StockInRecorded, activities);
        Assert.Contains(ActivityType.ReleaseRecorded, activities);
        Assert.All(page.Entries, entry => Assert.Equal("tester", entry.User));
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_edited_through_the_app()
    {
        await using var app = await InventoryFixture.CreateAsync();
        await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");

        var entry = await app.Context.AuditLogs.FirstAsync();
        entry.Description = "tampered";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.Context.SaveChangesAsync());
        Assert.Contains("cannot be changed", error.Message);
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_deleted_even_with_raw_sql()
    {
        await using var app = await InventoryFixture.CreateAsync();
        await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");

        // The database triggers are installed by DatabaseSetup, so apply them here.
        await new DatabaseSetupHarness(app).InstallGuardsAsync();

        var error = await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => app.Context.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs"));
        Assert.Contains("cannot be deleted", error.Message);
    }

    [Fact]
    public async Task Audit_search_filters_by_text_and_activity()
    {
        await using var app = await InventoryFixture.CreateAsync();
        await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        await app.AddItemAsync("PEN", "Ballpen black", unit: "box");

        var byText = await app.Audit.SearchAsync(new AuditFilter(SearchText: "Ballpen"));
        Assert.Single(byText.Entries);

        var byActivity = await app.Audit.SearchAsync(new AuditFilter(Activity: ActivityType.ItemCreated));
        Assert.Equal(2, byActivity.Entries.Count);
    }

    [Fact]
    public async Task The_sync_save_path_is_closed_off()
    {
        await using var app = await InventoryFixture.CreateAsync();

        // Guards against a future change accidentally blocking the UI thread.
        Assert.Throws<NotSupportedException>(() => app.Context.SaveChanges());
    }

    [Fact]
    public async Task First_account_setup_enforces_the_password_policy()
    {
        await using var app = await AuthFixture.CreateAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.Auth.CreateFirstAccountAsync(
                new CreateFirstAccountRequest("Office Secretary", "secretary", "short")));

        Assert.Contains(error.Errors, message => message.Contains("at least 12 characters"));
    }

    [Fact]
    public async Task A_first_account_can_sign_in_and_a_wrong_password_cannot()
    {
        await using var app = await AuthFixture.CreateAsync();
        Assert.True(await app.Auth.NeedsFirstAccountAsync());

        var created = await app.Auth.CreateFirstAccountAsync(
            new CreateFirstAccountRequest("Office Secretary", "secretary", "Treasury#2026x"));
        Assert.True(created.Succeeded);
        Assert.False(await app.Auth.NeedsFirstAccountAsync());

        var wrong = await app.Auth.SignInAsync(new SignInRequest("secretary", "WrongPassword1!"));
        Assert.False(wrong.Succeeded);

        var right = await app.Auth.SignInAsync(new SignInRequest("secretary", "Treasury#2026x"));
        Assert.True(right.Succeeded);
        Assert.Equal("Office Secretary", app.Session.User!.DisplayName);
    }

    [Fact]
    public async Task An_unknown_username_gives_the_same_message_as_a_wrong_password()
    {
        await using var app = await AuthFixture.CreateAsync();
        await app.Auth.CreateFirstAccountAsync(
            new CreateFirstAccountRequest("Office Secretary", "secretary", "Treasury#2026x"));

        var unknown = await app.Auth.SignInAsync(new SignInRequest("nobody", "Treasury#2026x"));
        var wrongPassword = await app.Auth.SignInAsync(new SignInRequest("secretary", "Nope#123456x"));

        Assert.Equal(unknown.ErrorMessage, wrongPassword.ErrorMessage);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        await using var app = await AuthFixture.CreateAsync();
        await app.Auth.CreateFirstAccountAsync(
            new CreateFirstAccountRequest("Office Secretary", "secretary", "Treasury#2026x"));

        for (var attempt = 1; attempt <= 5; attempt++)
            await app.Auth.SignInAsync(new SignInRequest("secretary", "Wrong#123456x"));

        // Even the correct password is refused while the lockout stands.
        var blocked = await app.Auth.SignInAsync(new SignInRequest("secretary", "Treasury#2026x"));
        Assert.False(blocked.Succeeded);
        Assert.Contains("Too many incorrect attempts", blocked.ErrorMessage!);
    }

    [Fact]
    public async Task Username_matching_ignores_letter_case()
    {
        await using var app = await AuthFixture.CreateAsync();
        await app.Auth.CreateFirstAccountAsync(
            new CreateFirstAccountRequest("Office Secretary", "secretary", "Treasury#2026x"));

        var result = await app.Auth.SignInAsync(new SignInRequest("SECRETARY", "Treasury#2026x"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_session_expires_once_the_inactivity_window_passes()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero));
        var session = new CurrentUser(clock);
        session.Begin(new SignedInUser(Guid.NewGuid(), "tester", "Tester", false), TimeSpan.FromMinutes(30));

        clock.Advance(TimeSpan.FromMinutes(29));
        Assert.False(session.HasExpired);

        // Any activity pushes the deadline back out.
        session.Touch();
        clock.Advance(TimeSpan.FromMinutes(29));
        Assert.False(session.HasExpired);

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True(session.HasExpired);
        Assert.Throws<UnauthorizedAccessException>(() => session.Require());
    }

    [Fact]
    public void A_too_short_timeout_is_ignored()
    {
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var session = new CurrentUser(clock);

        // A one-tick window would lock the user out mid-task.
        session.Begin(new SignedInUser(Guid.NewGuid(), "tester", "Tester", false), TimeSpan.FromTicks(1));

        clock.Advance(TimeSpan.FromMinutes(29));
        Assert.False(session.HasExpired);
    }

    [Fact]
    public async Task Work_is_refused_when_nobody_is_signed_in()
    {
        await using var app = await InventoryFixture.CreateAsync();
        app.Session.End();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => app.Items.ListAsync());
    }
}

/// <summary>A database with no accounts, for testing first-run and sign-in.</summary>
internal sealed class AuthFixture : IAsyncDisposable
{
    private readonly string _databasePath;

    private AuthFixture(string databasePath, InventoryDbContext context)
    {
        _databasePath = databasePath;
        Context = context;
        Session = new CurrentUser();
        var audit = new AuditService(context, Session);
        Auth = new AuthenticationService(context, new BCryptPasswordHasher(), Session, audit);
    }

    public InventoryDbContext Context { get; }
    public CurrentUser Session { get; }
    public AuthenticationService Auth { get; }

    public static async Task<AuthFixture> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"afms-auth-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var context = new InventoryDbContext(options);
        await context.Database.MigrateAsync();
        return new AuthFixture(databasePath, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                var path = _databasePath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Harmless leftover.
            }
        }
    }
}

/// <summary>A clock the test drives by hand, so expiry does not depend on real waiting.</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}

/// <summary>Installs the append-only audit triggers the same way start up does.</summary>
internal sealed class DatabaseSetupHarness(InventoryFixture app)
{
    public Task InstallGuardsAsync() => app.Context.Database.ExecuteSqlRawAsync(
        """
        DROP TRIGGER IF EXISTS TR_AuditLogs_NoUpdate;
        DROP TRIGGER IF EXISTS TR_AuditLogs_NoDelete;

        CREATE TRIGGER TR_AuditLogs_NoUpdate
        BEFORE UPDATE ON AuditLogs
        BEGIN
            SELECT RAISE(ABORT, 'Audit entries cannot be changed.');
        END;

        CREATE TRIGGER TR_AuditLogs_NoDelete
        BEFORE DELETE ON AuditLogs
        BEGIN
            SELECT RAISE(ABORT, 'Audit entries cannot be deleted.');
        END;
        """);
}
