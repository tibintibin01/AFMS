using AFMS.Core;
using AFMS.Core.Domain;
using AFMS.Core.Files;
using AFMS.Core.Persistence;
using AFMS.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFMS.Tests;

/// <summary>
/// Backups are the only safety net for records that exist on one computer, so the
/// file has to be complete on its own and has to open again.
/// </summary>
public sealed class BackupTests : IAsyncDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"afms-backup-{Guid.NewGuid():N}");

    private sealed class TestPaths(string root) : IApplicationPaths
    {
        public string DataDirectory { get; } = root;
        public string DatabasePath { get; } = Path.Combine(root, "inventory.db");
        public string LogDirectory { get; } = Path.Combine(root, "Logs");
        public string DefaultBackupDirectory { get; } = Path.Combine(root, "Backups");
        public string DefaultReportDirectory { get; } = Path.Combine(root, "Reports");

        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(DefaultBackupDirectory);
            Directory.CreateDirectory(DefaultReportDirectory);
        }
    }

    private async Task<(BackupService Backups, InventoryDbContext Context, TestPaths Paths)> ArrangeAsync()
    {
        var paths = new TestPaths(_root);
        paths.EnsureDirectoriesExist();

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={paths.DatabasePath}")
            .Options;
        var context = new InventoryDbContext(options);
        await context.Database.MigrateAsync();

        var user = new User
        {
            Username = "tester",
            DisplayName = "Test Secretary",
            PasswordHash = "not-used"
        };
        context.Users.Add(user);
        context.Items.Add(new Item { Code = "PAPER", Name = "Bond paper", Unit = "ream" });
        await context.SaveChangesAsync();

        var session = new CurrentUser();
        session.Begin(new SignedInUser(user.Id, user.Username, user.DisplayName, false));

        var audit = new AuditService(context, session);
        var settings = new SettingsService(context, session, audit, paths);
        var backups = new BackupService(
            context, paths, settings, session, audit, NullLogger<BackupService>.Instance);

        return (backups, context, paths);
    }

    [Fact]
    public async Task A_backup_is_a_single_self_contained_file()
    {
        var (backups, context, _) = await ArrangeAsync();
        await using var _context = context;

        var backup = await backups.CreateAsync(BackupKind.Manual);

        Assert.True(File.Exists(backup.FilePath));
        Assert.True(backup.SizeBytes > 0);

        // Leftover write-ahead files would mean the .afmsbak alone is incomplete,
        // which matters as soon as it is copied to a USB drive.
        Assert.False(File.Exists(backup.FilePath + "-wal"), "a stray -wal file was left behind");
        Assert.False(File.Exists(backup.FilePath + "-shm"), "a stray -shm file was left behind");
    }

    [Fact]
    public async Task A_new_backup_passes_its_own_check()
    {
        var (backups, context, _) = await ArrangeAsync();
        await using var _context = context;

        var backup = await backups.CreateAsync(BackupKind.Manual);
        var check = await backups.CheckAsync(backup.FilePath);

        Assert.True(check.IsValid, check.Message);
    }

    [Fact]
    public async Task The_backup_really_holds_the_records()
    {
        var (backups, context, _) = await ArrangeAsync();
        await using var _context = context;

        var backup = await backups.CreateAsync(BackupKind.Manual);

        // Open the backup as a database in its own right and read the data back.
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={backup.FilePath}")
            .Options;
        await using var restored = new InventoryDbContext(options);

        Assert.Single(await restored.Items.ToListAsync());
        Assert.Equal("PAPER", (await restored.Items.SingleAsync()).Code);
        Assert.Single(await restored.Users.ToListAsync());
    }

    [Fact]
    public async Task A_file_that_is_not_a_backup_is_rejected()
    {
        var (backups, context, paths) = await ArrangeAsync();
        await using var _context = context;

        var bogus = Path.Combine(paths.DefaultBackupDirectory, "not-a-backup.afmsbak");
        await File.WriteAllTextAsync(bogus, "this is just text");

        var check = await backups.CheckAsync(bogus);

        Assert.False(check.IsValid);
    }

    [Fact]
    public async Task A_missing_file_is_reported_rather_than_throwing()
    {
        var (backups, context, _) = await ArrangeAsync();
        await using var _context = context;

        var check = await backups.CheckAsync(Path.Combine(_root, "nope.afmsbak"));

        Assert.False(check.IsValid);
        Assert.Contains("could not be found", check.Message);
    }

    [Fact]
    public async Task A_restore_needs_a_written_reason()
    {
        var (backups, context, _) = await ArrangeAsync();
        await using var _context = context;

        var backup = await backups.CreateAsync(BackupKind.Manual);

        await Assert.ThrowsAsync<ValidationException>(
            () => backups.ScheduleRestoreAsync(backup.FilePath, "oops"));
    }

    [Fact]
    public async Task Scheduling_a_restore_queues_it_and_takes_a_safety_copy_first()
    {
        var (backups, context, paths) = await ArrangeAsync();
        await using var _context = context;

        var backup = await backups.CreateAsync(BackupKind.Manual);
        Assert.False(backups.HasPendingRestore);

        await backups.ScheduleRestoreAsync(backup.FilePath, "Recovering from an encoding mistake.");

        Assert.True(backups.HasPendingRestore);
        Assert.Contains(
            Directory.EnumerateFiles(paths.DefaultBackupDirectory, "*.afmsbak"),
            file => file.Contains("BeforeRestore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_daily_backup_runs_once_a_day()
    {
        var (backups, context, _) = await ArrangeAsync();
        await using var _context = context;

        Assert.True(await backups.RunDailyBackupIfDueAsync());
        Assert.False(await backups.RunDailyBackupIfDueAsync());
    }

    public async ValueTask DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Yield();
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Harmless leftover in the temp folder.
        }
    }
}
