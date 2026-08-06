using AFMS.Core;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFMS.Tests;

/// <summary>
/// Start-up behaviour. This is the code that runs before the user sees anything,
/// so a failure here means the app will not open at all.
/// </summary>
public sealed class DatabaseSetupTests : IAsyncDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"afms-setup-{Guid.NewGuid():N}.db");

    private InventoryDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;
        return new InventoryDbContext(options);
    }

    private async Task RunSetupAsync(IStarterDataProvider starter)
    {
        await using var context = NewContext();
        var setup = new DatabaseSetup(context, starter, NullLogger<DatabaseSetup>.Instance);
        await setup.InitializeAsync();
    }

    [Fact]
    public async Task Setup_creates_the_database_and_the_default_settings()
    {
        await RunSetupAsync(new NoStarter());

        await using var context = NewContext();
        var keys = await context.SystemSettings.Select(setting => setting.Key).ToListAsync();

        Assert.Contains(SettingsCatalog.OfficeName, keys);
        Assert.Contains(SettingsCatalog.SessionTimeoutMinutes, keys);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Regression test. Starter settings overlap the built-in defaults, and an
    /// earlier version inserted a second row for the same key and crashed on start.
    /// </summary>
    [Fact]
    public async Task Starter_settings_that_repeat_a_default_key_do_not_break_start_up()
    {
        var starter = new FixedStarter(new StarterData(
            "test-1",
            new Dictionary<string, string>
            {
                [SettingsCatalog.OfficeName] = "Municipal Treasury Office",
                [SettingsCatalog.MunicipalityName] = "Dipaculao",
                [SettingsCatalog.ProvinceName] = "Aurora"
            },
            [new StarterItem("AF51", "Local receipt", "booklet", "Accountable form", true, 5)],
            [new StarterRecipient("AU", "Marigold", "Clerk", "Treasury")]));

        await RunSetupAsync(starter);

        await using var context = NewContext();
        var settings = await context.SystemSettings.ToListAsync();

        Assert.Single(settings, setting => setting.Key == SettingsCatalog.OfficeName);
        // A default with no value gets filled in from the starter file.
        Assert.Equal("Dipaculao",
            settings.Single(setting => setting.Key == SettingsCatalog.MunicipalityName).Value);
        Assert.Single(await context.Items.ToListAsync());
        Assert.Single(await context.Recipients.ToListAsync());
    }

    [Fact]
    public async Task Running_setup_twice_does_not_duplicate_anything()
    {
        var starter = new FixedStarter(new StarterData(
            "test-1",
            new Dictionary<string, string> { [SettingsCatalog.MunicipalityName] = "Dipaculao" },
            [new StarterItem("AF51", "Local receipt", "booklet", null, true, 5)],
            [new StarterRecipient("AU", "Marigold", null, null)]));

        await RunSetupAsync(starter);
        await RunSetupAsync(starter);

        await using var context = NewContext();
        Assert.Single(await context.Items.ToListAsync());
        Assert.Single(await context.Recipients.ToListAsync());

        var keys = await context.SystemSettings.Select(setting => setting.Key).ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Setup_installs_the_append_only_audit_guards()
    {
        await RunSetupAsync(new NoStarter());

        await using var context = NewContext();
        var triggers = await context.Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'trigger'")
            .ToListAsync();

        Assert.Contains("TR_AuditLogs_NoUpdate", triggers);
        Assert.Contains("TR_AuditLogs_NoDelete", triggers);
    }

    [Fact]
    public async Task An_edited_setting_is_not_overwritten_by_a_later_starter_import()
    {
        await RunSetupAsync(new FixedStarter(new StarterData(
            "test-1",
            new Dictionary<string, string> { [SettingsCatalog.MunicipalityName] = "Dipaculao" },
            [], [])));

        // The user corrects the value in the app.
        await using (var context = NewContext())
        {
            var setting = await context.SystemSettings
                .SingleAsync(entry => entry.Key == SettingsCatalog.MunicipalityName);
            setting.Value = "Corrected By Hand";
            await context.SaveChangesAsync();
        }

        // A newer starter file must not undo that.
        await RunSetupAsync(new FixedStarter(new StarterData(
            "test-2",
            new Dictionary<string, string> { [SettingsCatalog.MunicipalityName] = "Dipaculao" },
            [], [])));

        await using var verify = NewContext();
        var final = await verify.SystemSettings
            .SingleAsync(entry => entry.Key == SettingsCatalog.MunicipalityName);
        Assert.Equal("Corrected By Hand", final.Value);
    }

    public async ValueTask DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Yield();
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

    private sealed class NoStarter : IStarterDataProvider
    {
        public Task<StarterData?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<StarterData?>(null);
    }

    private sealed class FixedStarter(StarterData data) : IStarterDataProvider
    {
        public Task<StarterData?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<StarterData?>(data);
    }
}
