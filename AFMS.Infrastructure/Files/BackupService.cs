using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AFMS.Infrastructure.Files;

public sealed class BackupService(
    IApplicationPathService paths, ISystemSettingRepository settings, IDatabaseOperationCoordinator coordinator,
    IAuthorizationService authorization, ICurrentUserSession session, IAuditService audit, IUnitOfWork unitOfWork,
    IUserRepository users, IAuditLogRepository auditLogs,
    ILogger<BackupService> logger) : IBackupService
{
    private const string PendingFile = "pending-restore.json";
    private const string CompletedFile = "completed-restore.json";
    private const string RestoreLedgerFile = "restore-ledger.jsonl";
    private static readonly object RestoreLedgerSync = new();
    private static readonly HashSet<string> BlockedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".msi", ".scr", ".dll", ".jar", ".hta" };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public bool HasPendingRestore => File.Exists(Path.Combine(paths.DataDirectory, PendingFile));

    public async Task<BackupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.BackupAndRestore);
        var options = await ReadOptionsAsync(cancellationToken); var history = await ReadHistoryAsync(options.BackupFolder, cancellationToken);
        var last = history.Where(x => x.IsValid).Select(x => (DateTimeOffset?)x.CreatedAt).Max();
        return new(options, last, last is null || DateTimeOffset.Now - last > TimeSpan.FromHours(options.RecentBackupWarningHours), HasPendingRestore, history);
    }

    public async Task<BackupHistoryDto> CreateAsync(BackupKind kind, string? destinationFolder = null, CancellationToken cancellationToken = default)
    {
        if (kind == BackupKind.Manual) authorization.EnsurePermission(SystemPermission.BackupAndRestore);
        var options = await ReadOptionsAsync(cancellationToken); var folder = string.IsNullOrWhiteSpace(destinationFolder) ? options.BackupFolder : Path.GetFullPath(destinationFolder);
        Directory.CreateDirectory(folder); var now = DateTimeOffset.Now; var user = session.User?.Username ?? "System";
        var baseName = $"AFMS-{now:yyyyMMdd-HHmmss}-{kind}"; var file = Unique(Path.Combine(folder, baseName + ".afmsbak"));
        var attachmentsArchive = AttachmentsArchivePath(file);
        long attachmentFileCount = 0;
        await using (await coordinator.EnterMaintenanceAsync(cancellationToken))
        {
            try
            {
                await using var source = new SqliteConnection($"Data Source={paths.DatabasePath};Mode=ReadOnly;Pooling=False");
                await using var target = new SqliteConnection($"Data Source={file};Mode=ReadWriteCreate;Pooling=False");
                await source.OpenAsync(cancellationToken); await target.OpenAsync(cancellationToken); source.BackupDatabase(target);
                var attachmentsFolder = Path.Combine(paths.DataDirectory, "Attachments");
                Directory.CreateDirectory(attachmentsFolder);
                attachmentFileCount = Directory.EnumerateFiles(attachmentsFolder, "*", SearchOption.AllDirectories).LongCount();
                ZipFile.CreateFromDirectory(attachmentsFolder, attachmentsArchive, CompressionLevel.Optimal, false);
            }
            catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
            {
                TryDelete(file); TryDelete(attachmentsArchive); logger.LogError(exception, "AFMS database and attachment backup failed");
                throw new IOException("AFMS could not create the database and attachment backup. Check the destination folder and available disk space.", exception);
            }
        }
        var validation = await ValidateDatabaseFileAsync(file, cancellationToken);
        if (!validation.IsValid) { TryDelete(file); TryDelete(attachmentsArchive); throw new InvalidDataException($"The new backup failed validation: {validation.Message}"); }
        var sha = await Sha256Async(file, cancellationToken); var attachmentsSha = await Sha256Async(attachmentsArchive, cancellationToken); var info = new FileInfo(file);
        var manifest = new BackupManifest(file, kind, now, info.Length, user, sha, validation.Message, validation.DatabaseVersion,
            attachmentsArchive, attachmentFileCount, attachmentsSha);
        await File.WriteAllTextAsync(ManifestPath(file), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        await ApplyRetentionAsync(folder, options.RetentionDays, cancellationToken);
        if (kind != BackupKind.PreMigration)
        {
            await audit.RecordAsync(UserActivityType.BackupCreated, $"{kind} database backup created.", "DatabaseBackup", Guid.NewGuid(), null,
                new { FileName = info.Name, info.Length, Sha256 = sha, validation.DatabaseVersion, AttachmentFileCount = attachmentFileCount, AttachmentsSha256 = attachmentsSha }, cancellationToken, "Backup and Restore", info.Name);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("AFMS {BackupKind} backup created at {BackupPath}", kind, file);
        return new(file, info.Name, kind, now, info.Length, true, validation.Message, user, sha);
    }

    public static async Task<BackupValidationResult> ValidateDatabaseFileAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath)) return new(false, "The selected backup file does not exist.");
        try
        {
            await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(backupPath)};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(cancellationToken);
            await using var integrity = connection.CreateCommand(); integrity.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) return new(false, $"SQLite integrity check failed: {result}");
            await using var tables = connection.CreateCommand();
            tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Users','SystemSettings','StockMovements','__EFMigrationsHistory');";
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase); await using var reader = await tables.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) found.Add(reader.GetString(0));
            var required = new[] { "Users", "SystemSettings", "StockMovements", "__EFMigrationsHistory" };
            if (required.Any(x => !found.Contains(x))) return new(false, "The file is a SQLite database but not a complete AFMS backup.");
            await using var version = connection.CreateCommand(); version.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;";
            return new(true, "Backup integrity and AFMS schema validation passed.", Convert.ToString(await version.ExecuteScalarAsync(cancellationToken)));
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new(false, "The backup is invalid, corrupted, inaccessible, or not an AFMS database.");
        }
    }

    public Task<BackupValidationResult> ValidateAsync(string backupPath, CancellationToken cancellationToken = default) =>
        ValidateWithManifestAsync(backupPath, cancellationToken);

    public async Task ScheduleRestoreAsync(string backupPath, string reason, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.BackupAndRestore);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10) throw new InvalidDataException("A restoration reason of at least 10 characters is required.");
        var validation = await ValidateAsync(backupPath, cancellationToken);
        if (!validation.IsValid) throw new InvalidDataException(validation.Message);
        var safety = await CreateAsync(BackupKind.PreRestore, cancellationToken: cancellationToken);
        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required to restore a database.");
        var backupSha = await Sha256Async(backupPath, cancellationToken);
        var request = new RestoreRequest(Path.GetFullPath(backupPath), reason.Trim(), currentUser.Username, DateTimeOffset.Now, safety.FilePath,
            currentUser.Id, backupSha, safety.Sha256);
        var pendingPath = Path.Combine(paths.DataDirectory, PendingFile);
        await File.WriteAllTextAsync(pendingPath, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);
        try { AppendRestoreLedger(paths, request, "Scheduled", "Restore request authorized; safety backup created."); }
        catch { TryDelete(pendingPath); throw; }
        await audit.RecordAsync(UserActivityType.RestoreRequested, "Database restoration scheduled for safe application restart.", "DatabaseRestore", Guid.NewGuid(), null,
            new { Backup = Path.GetFileName(backupPath), PreRestoreBackup = safety.FileName }, cancellationToken, "Backup and Restore", Path.GetFileName(backupPath), reason.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RunScheduledBackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        var options = await ReadOptionsAsync(cancellationToken); if (!options.DailyBackupEnabled || HasPendingRestore) return false;
        var now = DateTimeOffset.Now; if (TimeOnly.FromDateTime(now.DateTime) < options.DailyBackupTime) return false;
        var history = await ReadHistoryAsync(options.BackupFolder, cancellationToken);
        if (history.Any(x => x.IsValid && x.Kind == BackupKind.Scheduled && x.CreatedAt.Date == now.Date)) return false;
        await CreateAsync(BackupKind.Scheduled, cancellationToken: cancellationToken); return true;
    }

    public async Task CompletePendingRestoreAuditAsync(CancellationToken cancellationToken = default)
    {
        var completed = Path.Combine(paths.DataDirectory, CompletedFile); if (!File.Exists(completed)) return;
        var request = JsonSerializer.Deserialize<RestoreRequest>(await File.ReadAllTextAsync(completed, cancellationToken)) ?? throw new InvalidDataException("Restore completion record is invalid.");
        var requesterExists = request.RequestedByUserId is { } requesterId && await users.GetByIdAsync(requesterId, cancellationToken) is not null;
        await auditLogs.AddAsync(new AuditLog
        {
            UserId = requesterExists ? request.RequestedByUserId : null,
            ActivityType = UserActivityType.Restored,
            Description = $"Database restored and migrated successfully. Requested by {request.RequestedBy} at {request.RequestedAt:O}.",
            EntityName = "DatabaseRestore",
            EntityId = Guid.NewGuid(),
            Module = "Backup and Restore",
            ReferenceNumber = Path.GetFileName(request.BackupPath),
            Reason = request.Reason,
            MachineName = Environment.MachineName,
            NewValues = JsonSerializer.Serialize(new { Backup = Path.GetFileName(request.BackupPath), request.BackupSha256, request.PreRestoreBackup, request.PreRestoreSha256 })
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AppendRestoreLedger(paths, request, "Completed", "Restored database migrated and durable audit record created.");
        File.Delete(completed);
    }

    private async Task<BackupOptions> ReadOptionsAsync(CancellationToken token)
    {
        var values = (await settings.ListAsync(token)).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AFMS Backups");
        var folder = values.GetValueOrDefault("DatabaseBackupFolder"); folder = string.IsNullOrWhiteSpace(folder) ? defaultFolder : Path.GetFullPath(folder);
        var daily = !values.TryGetValue("DailyBackupEnabled", out var enabled) || !bool.TryParse(enabled, out var parsedEnabled) || parsedEnabled;
        var time = values.TryGetValue("DailyBackupTime", out var configuredTime) && TimeOnly.TryParse(configuredTime, out var parsedTime) ? parsedTime : new TimeOnly(17, 0);
        var retention = values.TryGetValue("BackupRetentionDays", out var configuredRetention) && int.TryParse(configuredRetention, out var parsedRetention) ? Math.Clamp(parsedRetention, 1, 3650) : 30;
        var warningHours = values.TryGetValue("RecentBackupWarningHours", out var configuredWarning) && int.TryParse(configuredWarning, out var parsedWarning) ? Math.Clamp(parsedWarning, 1, 8760) : 48;
        var separate = folder.StartsWith("\\", StringComparison.Ordinal) || !string.Equals(Path.GetPathRoot(folder), Path.GetPathRoot(paths.DatabasePath), StringComparison.OrdinalIgnoreCase);
        var warning = separate ? "Backup location is separate from the live database drive." : "For disaster recovery, select a different drive or a secured network location. Backups on this drive can be lost with the computer.";
        return new(folder, daily, time, retention, warningHours, separate, warning);
    }

    private static async Task<IReadOnlyList<BackupHistoryDto>> ReadHistoryAsync(string folder, CancellationToken token)
    {
        if (!Directory.Exists(folder)) return [];
        var result = new List<BackupHistoryDto>();
        foreach (var manifestPath in Directory.EnumerateFiles(folder, "*.afmsbak.json").OrderByDescending(File.GetCreationTimeUtc).Take(500))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, token));
                if (manifest is null) continue; var exists = File.Exists(manifest.FilePath);
                var attachmentsValid = manifest.AttachmentsArchive is not null && File.Exists(manifest.AttachmentsArchive) &&
                    string.Equals(await Sha256Async(manifest.AttachmentsArchive, token), manifest.AttachmentsSha256, StringComparison.OrdinalIgnoreCase);
                var hashMatches = exists && new FileInfo(manifest.FilePath).Length == manifest.FileSize && attachmentsValid &&
                    string.Equals(await Sha256Async(manifest.FilePath, token), manifest.Sha256, StringComparison.OrdinalIgnoreCase);
                result.Add(new(manifest.FilePath, Path.GetFileName(manifest.FilePath), manifest.Kind, manifest.CreatedAt, manifest.FileSize,
                    hashMatches, !exists ? "Backup file is missing." : hashMatches ? manifest.ValidationMessage : "Backup file hash or size does not match its manifest.", manifest.CreatedBy, manifest.Sha256));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            { result.Add(new(manifestPath, Path.GetFileName(manifestPath), BackupKind.Manual, File.GetCreationTimeUtc(manifestPath), 0, false, "Backup manifest or file is invalid or inaccessible.", "Unknown", string.Empty)); }
        }
        return result.OrderByDescending(x => x.CreatedAt).ToList();
    }

    private static async Task ApplyRetentionAsync(string folder, int retentionDays, CancellationToken token)
    {
        var cutoff = DateTimeOffset.Now.AddDays(-retentionDays);
        foreach (var manifestPath in Directory.EnumerateFiles(folder, "*.afmsbak.json"))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, token));
                if (manifest is null || manifest.Kind == BackupKind.PreRestore || manifest.CreatedAt >= cutoff) continue;
                TryDelete(manifest.FilePath); if (manifest.AttachmentsArchive is not null) TryDelete(manifest.AttachmentsArchive); TryDelete(manifestPath);
            }
            catch (JsonException) { /* Invalid manifests remain visible for administrator review. */ }
        }
    }

    private static string Unique(string path) { if (!File.Exists(path)) return path; return Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}-{Guid.NewGuid():N}.afmsbak"); }
    private static string ManifestPath(string backup) => backup + ".json";
    private static string AttachmentsArchivePath(string backup) => backup + ".attachments.zip";
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static async Task<string> Sha256Async(string path, CancellationToken token) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)); }
    internal sealed record RestoreRequest(string BackupPath, string Reason, string RequestedBy, DateTimeOffset RequestedAt, string PreRestoreBackup,
        Guid? RequestedByUserId = null, string? BackupSha256 = null, string? PreRestoreSha256 = null);
    private sealed record BackupManifest(string FilePath, BackupKind Kind, DateTimeOffset CreatedAt, long FileSize, string CreatedBy, string Sha256,
        string ValidationMessage, string? DatabaseVersion, string? AttachmentsArchive = null, long AttachmentFileCount = 0, string? AttachmentsSha256 = null);

    public static void ApplyPendingRestore(IApplicationPathService paths)
    {
        var pending = Path.Combine(paths.DataDirectory, PendingFile); if (!File.Exists(pending)) return;
        var request = JsonSerializer.Deserialize<RestoreRequest>(File.ReadAllText(pending)) ?? throw new InvalidDataException("The pending restore request is invalid.");
        var validation = ValidateWithManifestAsync(request.BackupPath, CancellationToken.None).GetAwaiter().GetResult();
        if (!validation.IsValid) throw new InvalidDataException($"Pending restore cancelled: {validation.Message}");
        SqliteConnection.ClearAllPools();
        TryDelete(paths.DatabasePath + "-wal"); TryDelete(paths.DatabasePath + "-shm");
        var temporary = paths.DatabasePath + ".restore"; File.Copy(request.BackupPath, temporary, true);
        var stagedValidation = ValidateDatabaseFileAsync(temporary).GetAwaiter().GetResult();
        var expectedSha = request.BackupSha256 ?? Sha256Async(request.BackupPath, CancellationToken.None).GetAwaiter().GetResult();
        if (!stagedValidation.IsValid || !string.Equals(Sha256Async(temporary, CancellationToken.None).GetAwaiter().GetResult(), expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(temporary);
            throw new InvalidDataException("The staged restore copy failed integrity or hash validation.");
        }
        var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(ManifestPath(request.BackupPath)))
            ?? throw new InvalidDataException("The restore manifest is invalid.");
        var attachmentArchive = manifest.AttachmentsArchive ?? throw new InvalidDataException("The restore does not contain an attachment archive.");
        var stagedAttachments = Path.Combine(paths.DataDirectory, $"Attachments.restore-{Guid.NewGuid():N}");
        try { ExtractAttachmentsArchive(attachmentArchive, stagedAttachments, manifest.AttachmentFileCount); }
        catch { if (Directory.Exists(stagedAttachments)) Directory.Delete(stagedAttachments, true); TryDelete(temporary); throw; }
        var attachments = Path.Combine(paths.DataDirectory, "Attachments");
        var previousAttachments = Path.Combine(paths.DataDirectory, $"Attachments.before-restore-{Guid.NewGuid():N}");
        var databaseRollback = paths.DatabasePath + ".before-restore";
        var previousAttachmentsMoved = false;
        var stagedAttachmentsMoved = false;
        try
        {
            if (Directory.Exists(attachments)) { Directory.Move(attachments, previousAttachments); previousAttachmentsMoved = true; }
            Directory.Move(stagedAttachments, attachments); stagedAttachmentsMoved = true;
            TryDelete(databaseRollback);
            if (File.Exists(paths.DatabasePath)) File.Replace(temporary, paths.DatabasePath, databaseRollback); else File.Move(temporary, paths.DatabasePath);
        }
        catch
        {
            if (stagedAttachmentsMoved && Directory.Exists(attachments)) Directory.Delete(attachments, true);
            if (previousAttachmentsMoved && Directory.Exists(previousAttachments)) Directory.Move(previousAttachments, attachments);
            if (Directory.Exists(stagedAttachments)) Directory.Delete(stagedAttachments, true);
            TryDelete(temporary);
            throw;
        }
        if (Directory.Exists(previousAttachments)) Directory.Delete(previousAttachments, true);
        TryDelete(databaseRollback);
        File.WriteAllText(Path.Combine(paths.DataDirectory, CompletedFile), JsonSerializer.Serialize(request, JsonOptions)); File.Delete(pending);
        AppendRestoreLedger(paths, request, "Applied", "Database file replaced; migration and completion audit are pending.");
    }

    private static async Task<BackupValidationResult> ValidateWithManifestAsync(string backupPath, CancellationToken cancellationToken)
    {
        var validation = await ValidateDatabaseFileAsync(backupPath, cancellationToken);
        if (!validation.IsValid) return validation;
        var manifestPath = ManifestPath(Path.GetFullPath(backupPath));
        if (!File.Exists(manifestPath)) return new(false, "The backup has no AFMS integrity manifest and cannot be trusted for restoration.");
        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken));
            if (manifest is null) return new(false, "The AFMS backup manifest is invalid.");
            var info = new FileInfo(backupPath);
            var sha = await Sha256Async(backupPath, cancellationToken);
            if (manifest.FileSize != info.Length || !string.Equals(manifest.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                return new(false, "The backup file was changed after it was created; its size or SHA-256 hash does not match the manifest.");
            if (!string.Equals(Path.GetFullPath(manifest.FilePath), Path.GetFullPath(backupPath), StringComparison.OrdinalIgnoreCase))
                return new(false, "The backup manifest does not identify the selected file.");
            var expectedAttachments = AttachmentsArchivePath(Path.GetFullPath(backupPath));
            if (manifest.AttachmentsArchive is null || !string.Equals(Path.GetFullPath(manifest.AttachmentsArchive), expectedAttachments, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(expectedAttachments) || string.IsNullOrWhiteSpace(manifest.AttachmentsSha256))
                return new(false, "The backup attachment archive is missing or does not match the manifest.");
            var attachmentSha = await Sha256Async(expectedAttachments, cancellationToken);
            if (!string.Equals(attachmentSha, manifest.AttachmentsSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, "The attachment archive was changed after the backup was created.");
            ValidateAttachmentsArchive(expectedAttachments, manifest.AttachmentFileCount);
            return validation;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidDataException or OverflowException)
        {
            return new(false, "The AFMS backup integrity manifest is invalid or inaccessible.");
        }
    }

    private static void ValidateAttachmentsArchive(string archivePath, long expectedFileCount)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        long fileCount = 0;
        long totalSize = 0;
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
        {
            fileCount++;
            if (fileCount > 10_000 || entry.Length <= 0 || entry.Length > 25L * 1024 * 1024)
                throw new InvalidDataException("The attachment archive contains an invalid number or size of files.");
            totalSize = checked(totalSize + entry.Length);
            if (totalSize > 2L * 1024 * 1024 * 1024)
                throw new InvalidDataException("The attachment archive is larger than the supported restore limit.");
            if (BlockedAttachmentExtensions.Contains(Path.GetExtension(entry.Name)))
                throw new InvalidDataException("The attachment archive contains a prohibited executable or script file.");
            EnsureSafeArchivePath(Path.GetTempPath(), entry.FullName);
        }
        if (fileCount != expectedFileCount) throw new InvalidDataException("The attachment archive file count does not match the backup manifest.");
    }

    private static void ExtractAttachmentsArchive(string archivePath, string destination, long expectedFileCount)
    {
        ValidateAttachmentsArchive(archivePath, expectedFileCount);
        Directory.CreateDirectory(destination);
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var target = EnsureSafeArchivePath(destination, entry.FullName);
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open(); using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static string EnsureSafeArchivePath(string destination, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName))
            throw new InvalidDataException("The attachment archive contains an invalid path.");
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, entryName.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The attachment archive contains an unsafe path.");
        return target;
    }

    private static void AppendRestoreLedger(IApplicationPathService applicationPaths, RestoreRequest request, string eventName, string details)
    {
        lock (RestoreLedgerSync)
        {
            var ledgerPath = Path.Combine(applicationPaths.DataDirectory, RestoreLedgerFile);
            string? previousHash = null;
            if (File.Exists(ledgerPath))
            {
                var lastLine = File.ReadLines(ledgerPath).LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
                if (lastLine is not null) previousHash = JsonSerializer.Deserialize<RestoreLedgerEntry>(lastLine)?.IntegrityHash;
            }
            var occurredAt = DateTimeOffset.UtcNow;
            var payload = string.Join('|', eventName, occurredAt.ToString("O"), request.RequestedByUserId, request.RequestedBy,
                request.RequestedAt.ToUniversalTime().ToString("O"), request.BackupPath, request.BackupSha256, request.PreRestoreBackup,
                request.PreRestoreSha256, request.Reason, details, Environment.MachineName, previousHash);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            var entry = new RestoreLedgerEntry(eventName, occurredAt, request.RequestedByUserId, request.RequestedBy, request.RequestedAt,
                request.BackupPath, request.BackupSha256, request.PreRestoreBackup, request.PreRestoreSha256, request.Reason, details,
                Environment.MachineName, previousHash, hash);
            File.AppendAllText(ledgerPath, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
        }
    }

    private sealed record RestoreLedgerEntry(string Event, DateTimeOffset OccurredAt, Guid? RequestedByUserId, string RequestedBy,
        DateTimeOffset RequestedAt, string BackupPath, string? BackupSha256, string PreRestoreBackup, string? PreRestoreSha256,
        string Reason, string Details, string MachineName, string? PreviousHash, string IntegrityHash);
}
