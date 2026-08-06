using System.Security.Cryptography;
using System.Text.Json;
using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFMS.Core.Files;

/// <summary>
/// Copies the live SQLite database to a dated .afmsbak file, checks that the copy
/// really opens and holds the expected tables, and can queue a restore that is
/// applied on the next start before the database is opened.
/// </summary>
public sealed class BackupService(
    InventoryDbContext context,
    IApplicationPaths paths,
    ISettingsService settings,
    ICurrentUser session,
    IAuditService audit,
    ILogger<BackupService> logger) : IBackupService
{
    private const string PendingRestoreFile = "pending-restore.json";
    private const string CompletedRestoreFile = "completed-restore.json";
    private const string BackupExtension = ".afmsbak";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed record RestoreRequest(
        string BackupPath,
        string Reason,
        string RequestedBy,
        DateTimeOffset RequestedAt,
        string SafetyBackupPath);

    private sealed record BackupManifest(
        BackupKind Kind,
        DateTimeOffset CreatedAt,
        long SizeBytes,
        string CreatedBy,
        string Sha256,
        string SchemaVersion);

    public bool HasPendingRestore => File.Exists(Path.Combine(paths.DataDirectory, PendingRestoreFile));

    public async Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        session.Require();
        var options = await settings.GetAsync(cancellationToken);
        var history = await ReadHistoryAsync(options.BackupFolder, cancellationToken);
        var last = history.Where(file => file.IsValid).Select(file => (DateTimeOffset?)file.CreatedAt).Max();

        return new BackupStatus(
            LastBackupAt: last,
            BackupOverdue: last is null || DateTimeOffset.Now - last > TimeSpan.FromHours(36),
            HasPendingRestore: HasPendingRestore,
            History: history);
    }

    public async Task<BackupFileDto> CreateAsync(
        BackupKind kind,
        string? destinationFolder = null,
        CancellationToken cancellationToken = default)
    {
        // Automatic backups run without a signed-in user, so only manual ones check the session.
        if (kind == BackupKind.Manual) session.Require();

        var options = await settings.GetAsync(cancellationToken);
        var folder = string.IsNullOrWhiteSpace(destinationFolder)
            ? options.BackupFolder
            : Path.GetFullPath(destinationFolder);
        Directory.CreateDirectory(folder);

        var now = DateTimeOffset.Now;
        var author = session.User?.Username ?? "Automatic";
        var target = UniquePath(Path.Combine(folder, $"AFMS-{now:yyyyMMdd-HHmmss}-{kind}{BackupExtension}"));

        try
        {
            // SQLite's own backup API produces a consistent copy even while the
            // database is open, which a plain file copy cannot guarantee.
            await using (var source = new SqliteConnection(
                             $"Data Source={paths.DatabasePath};Mode=ReadOnly;Pooling=False"))
            await using (var destination = new SqliteConnection(
                             $"Data Source={target};Mode=ReadWriteCreate;Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);

                // Fold the write-ahead log into the file itself. Without this the
                // backup is spread across .afmsbak plus -wal and -shm side files,
                // and copying only the .afmsbak to a USB drive would lose data.
                await using var checkpoint = destination.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode = DELETE;";
                await checkpoint.ExecuteNonQueryAsync(cancellationToken);
            }

            // The connections are closed by now, so the emptied side files can go.
            SqliteConnection.ClearAllPools();
            TryDelete(target + "-wal");
            TryDelete(target + "-shm");
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            TryDelete(target);
            TryDelete(target + "-wal");
            TryDelete(target + "-shm");
            logger.LogError(exception, "Backup failed");
            throw new IOException(
                "The backup could not be created. Check the folder and that there is free disk space.", exception);
        }

        var check = await CheckFileAsync(target, cancellationToken);
        if (!check.IsValid)
        {
            TryDelete(target);
            throw new InvalidDataException($"The new backup did not pass its check: {check.Message}");
        }

        var info = new FileInfo(target);
        var hash = await Sha256Async(target, cancellationToken);
        var manifest = new BackupManifest(kind, now, info.Length, author, hash, check.Message);
        await File.WriteAllTextAsync(
            ManifestPath(target), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);

        await ApplyRetentionAsync(folder, options.BackupRetentionDays, cancellationToken);

        if (session.User is not null)
        {
            await audit.RecordAsync(
                ActivityType.BackupCreated,
                $"{kind} backup created ({info.Name}).",
                "Backup", null, info.Name,
                new { info.Name, info.Length, Sha256 = hash },
                cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("{Kind} backup written to {Path}", kind, target);
        return new BackupFileDto(target, info.Name, kind, now, info.Length, true, check.Message);
    }

    public Task<BackupCheckResult> CheckAsync(string backupPath, CancellationToken cancellationToken = default) =>
        CheckFileAsync(backupPath, cancellationToken);

    public async Task ScheduleRestoreAsync(
        string backupPath,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var user = session.Require();

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw new ValidationException("Give a reason of at least 10 characters for restoring an older copy.");

        var check = await CheckFileAsync(backupPath, cancellationToken);
        if (!check.IsValid)
            throw new InvalidDataException(check.Message);

        // Take a safety copy of the current data first, so a restore is reversible.
        var safety = await CreateAsync(BackupKind.BeforeRestore, cancellationToken: cancellationToken);

        var request = new RestoreRequest(
            Path.GetFullPath(backupPath), reason.Trim(), user.Username, DateTimeOffset.Now, safety.FilePath);
        var pendingPath = Path.Combine(paths.DataDirectory, PendingRestoreFile);
        await File.WriteAllTextAsync(
            pendingPath, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);

        await audit.RecordAsync(
            ActivityType.RestoreRequested,
            "Restore scheduled; it is applied the next time the app starts.",
            "Backup", null, Path.GetFileName(backupPath),
            new { Backup = Path.GetFileName(backupPath), Safety = safety.FileName, Reason = request.Reason },
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RunDailyBackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        var options = await settings.GetAsync(cancellationToken);
        if (!options.DailyBackupEnabled || HasPendingRestore) return false;

        var marker = await context.SystemSettings.SingleOrDefaultAsync(
            setting => setting.Key == SettingsCatalog.LastDailyBackupOn, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (DateOnly.TryParse(marker?.Value, out var last) && last >= today)
            return false;

        await CreateAsync(BackupKind.Daily, cancellationToken: cancellationToken);

        if (marker is null)
            context.SystemSettings.Add(new SystemSetting
            {
                Key = SettingsCatalog.LastDailyBackupOn,
                Value = today.ToString("O"),
                Description = "Internal marker for the daily backup. Do not edit."
            });
        else
            marker.Value = today.ToString("O");

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Applies a queued restore. Called before the database is opened, so it runs
    /// as a plain file swap with no EF Core involvement.
    /// </summary>
    public static void ApplyPendingRestore(IApplicationPaths paths, ILogger? logger = null)
    {
        var pendingPath = Path.Combine(paths.DataDirectory, PendingRestoreFile);
        if (!File.Exists(pendingPath)) return;

        try
        {
            var request = JsonSerializer.Deserialize<RestoreRequest>(File.ReadAllText(pendingPath));
            if (request is null || !File.Exists(request.BackupPath))
            {
                File.Delete(pendingPath);
                return;
            }

            File.Copy(request.BackupPath, paths.DatabasePath, overwrite: true);

            // Remove stale side files so SQLite does not replay a journal that
            // belonged to the database we just replaced.
            TryDelete(paths.DatabasePath + "-wal");
            TryDelete(paths.DatabasePath + "-shm");

            File.Move(pendingPath, Path.Combine(paths.DataDirectory, CompletedRestoreFile), overwrite: true);
            logger?.LogWarning("Database restored from {Path}", request.BackupPath);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Restore could not be applied");
            throw;
        }
    }

    /// <summary>Writes the audit entry for a restore that was applied during start up.</summary>
    public async Task WritePendingRestoreAuditAsync(CancellationToken cancellationToken = default)
    {
        var completedPath = Path.Combine(paths.DataDirectory, CompletedRestoreFile);
        if (!File.Exists(completedPath)) return;

        try
        {
            var request = JsonSerializer.Deserialize<RestoreRequest>(
                await File.ReadAllTextAsync(completedPath, cancellationToken));
            if (request is not null)
            {
                await audit.RecordAsync(
                    ActivityType.RestoreCompleted,
                    $"Database restored from {Path.GetFileName(request.BackupPath)}.",
                    "Backup", null, Path.GetFileName(request.BackupPath),
                    new { request.Reason, request.RequestedBy, request.RequestedAt },
                    cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            TryDelete(completedPath);
        }
    }

    private static async Task<BackupCheckResult> CheckFileAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            return new BackupCheckResult(false, "That backup file could not be found.");

        try
        {
            await using var connection = new SqliteConnection(
                $"Data Source={Path.GetFullPath(backupPath)};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(cancellationToken);

            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                return new BackupCheckResult(false, $"The file is damaged: {result}");

            var required = new[] { "Users", "Items", "Recipients", "StockIns", "Releases", "__EFMigrationsHistory" };
            await using var tables = connection.CreateCommand();
            tables.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name IN " +
                "('Users','Items','Recipients','StockIns','Releases','__EFMigrationsHistory');";
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await tables.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken)) found.Add(reader.GetString(0));
            }

            if (required.Any(name => !found.Contains(name)))
                return new BackupCheckResult(false, "That file is a database but not an AFMS inventory backup.");

            await using var version = connection.CreateCommand();
            version.CommandText =
                "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;";
            var schema = Convert.ToString(await version.ExecuteScalarAsync(cancellationToken)) ?? "unknown";
            return new BackupCheckResult(true, schema);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new BackupCheckResult(false, "That file is damaged, in use, or not an AFMS backup.");
        }
    }

    private async Task<IReadOnlyList<BackupFileDto>> ReadHistoryAsync(
        string folder,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder)) return [];

        var files = new List<BackupFileDto>();
        foreach (var path in Directory.EnumerateFiles(folder, $"*{BackupExtension}"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            var manifest = await ReadManifestAsync(path, cancellationToken);

            files.Add(new BackupFileDto(
                path,
                info.Name,
                manifest?.Kind ?? GuessKind(info.Name),
                manifest?.CreatedAt ?? info.CreationTime,
                info.Length,
                manifest is not null,
                manifest is null ? "No manifest; verify before restoring." : "Checked when created."));
        }

        return files.OrderByDescending(file => file.CreatedAt).ToList();
    }

    private static async Task<BackupManifest?> ReadManifestAsync(string backupPath, CancellationToken cancellationToken)
    {
        var manifestPath = ManifestPath(backupPath);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            return JsonSerializer.Deserialize<BackupManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken));
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Deletes automatic backups past the retention window. Manual copies are always kept.</summary>
    private async Task ApplyRetentionAsync(string folder, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.Now.AddDays(-retentionDays);
        foreach (var path in Directory.EnumerateFiles(folder, $"*{BackupExtension}"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await ReadManifestAsync(path, cancellationToken);
            var kind = manifest?.Kind ?? GuessKind(Path.GetFileName(path));
            if (kind is BackupKind.Manual or BackupKind.BeforeRestore) continue;

            var created = manifest?.CreatedAt ?? new FileInfo(path).CreationTime;
            if (created >= cutoff) continue;

            TryDelete(path);
            TryDelete(ManifestPath(path));
        }
    }

    private static BackupKind GuessKind(string fileName) =>
        Enum.GetValues<BackupKind>().FirstOrDefault(
            kind => fileName.Contains(kind.ToString(), StringComparison.OrdinalIgnoreCase),
            BackupKind.Manual);

    private static string ManifestPath(string backupPath) => backupPath + ".json";

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 2; index < 1000; index++)
        {
            var candidate = Path.Combine(folder, $"{name}-{index}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        throw new IOException("Too many backups share the same name in that folder.");
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A locked leftover file is not worth failing the operation over.
        }
    }
}
