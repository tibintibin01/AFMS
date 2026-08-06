using AFMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFMS.Core.Persistence;

/// <summary>
/// Runs once at start up: applies pending migrations, installs the append-only
/// audit guards, makes sure the default settings exist, and imports starter
/// records the first time only.
/// </summary>
public sealed class DatabaseSetup(
    InventoryDbContext context,
    IStarterDataProvider starterData,
    ILogger<DatabaseSetup> logger) : IDatabaseSetup
{
    private const string StarterVersionKey = "StarterDataVersion";

    private const string AuditGuardSql =
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
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Preparing the inventory database");

        await context.Database.MigrateAsync(cancellationToken);

        // Recreated every start so the guards survive a restore from an older backup.
        await context.Database.ExecuteSqlRawAsync(AuditGuardSql, cancellationToken);

        // The defaults are written before the starter import runs, because the
        // import looks settings up in the database to decide what is missing.
        // Leaving them unsaved would make it insert a second row for the same key.
        await EnsureDefaultSettingsAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await ImportStarterDataAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inventory database ready");
    }

    private async Task EnsureDefaultSettingsAsync(CancellationToken cancellationToken)
    {
        var existing = await context.SystemSettings
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);
        var known = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value, description) in SettingsCatalog.Defaults)
        {
            if (known.Contains(key)) continue;
            context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
        }
    }

    private async Task ImportStarterDataAsync(CancellationToken cancellationToken)
    {
        var data = await starterData.GetAsync(cancellationToken);
        if (data is null) return;

        var marker = await context.SystemSettings
            .SingleOrDefaultAsync(setting => setting.Key == StarterVersionKey, cancellationToken);
        if (marker?.Value == data.Version)
            return;

        var itemCodes = (await context.Items.Select(item => item.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recipientCodes = (await context.Recipients.Select(person => person.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var settingKeys = (await context.SystemSettings.Select(setting => setting.Key).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedItems = 0;
        foreach (var item in data.Items)
        {
            var code = item.Code.Trim();
            if (code.Length == 0 || itemCodes.Contains(code)) continue;
            context.Items.Add(new Item
            {
                Code = code,
                Name = item.Name.Trim(),
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "pcs" : item.Unit.Trim(),
                Category = item.Category?.Trim(),
                TracksSerials = item.TracksSerials,
                ReorderLevel = Math.Max(0, item.ReorderLevel)
            });
            itemCodes.Add(code);
            addedItems++;
        }

        var addedRecipients = 0;
        foreach (var person in data.Recipients)
        {
            var code = person.Code.Trim();
            if (code.Length == 0 || recipientCodes.Contains(code)) continue;
            context.Recipients.Add(new Recipient
            {
                Code = code,
                Name = person.Name.Trim(),
                Position = person.Position?.Trim(),
                Office = person.Office?.Trim()
            });
            recipientCodes.Add(code);
            addedRecipients++;
        }

        // Starter settings never overwrite a value the user already edited.
        foreach (var (key, value) in data.Settings)
        {
            var trimmedKey = key.Trim();
            if (trimmedKey.Length == 0) continue;
            if (settingKeys.Contains(trimmedKey))
            {
                var current = await context.SystemSettings
                    .SingleOrDefaultAsync(setting => setting.Key == trimmedKey, cancellationToken);
                if (current is not null && string.IsNullOrWhiteSpace(current.Value))
                    current.Value = value?.Trim() ?? string.Empty;
                continue;
            }

            context.SystemSettings.Add(new SystemSetting
            {
                Key = trimmedKey,
                Value = value?.Trim() ?? string.Empty,
                Description = "Starter value; editable in Settings."
            });
            settingKeys.Add(trimmedKey);
        }

        if (marker is null)
            context.SystemSettings.Add(new SystemSetting
            {
                Key = StarterVersionKey,
                Value = data.Version,
                Description = "Starter records already imported. Do not edit."
            });
        else
            marker.Value = data.Version;

        logger.LogInformation(
            "Imported starter data {Version}: {ItemCount} items, {RecipientCount} people",
            data.Version, addedItems, addedRecipients);
    }
}

/// <summary>Setting keys and their defaults, in one place so nothing drifts.</summary>
public static class SettingsCatalog
{
    public const string OfficeName = "OfficeName";
    public const string MunicipalityName = "MunicipalityName";
    public const string ProvinceName = "ProvinceName";
    public const string HeadOfOffice = "HeadOfOffice";
    public const string PreparedBy = "PreparedBy";
    public const string CertifiedBy = "CertifiedBy";
    public const string BackupFolder = "BackupFolder";
    public const string ReportFolder = "ReportFolder";
    public const string SessionTimeoutMinutes = "SessionTimeoutMinutes";
    public const string DailyBackupEnabled = "DailyBackupEnabled";
    public const string BackupRetentionDays = "BackupRetentionDays";
    public const string LastDailyBackupOn = "LastDailyBackupOn";

    public static readonly (string Key, string Value, string Description)[] Defaults =
    [
        (OfficeName, "Municipal Treasury Office", "Printed on every report and release slip."),
        (MunicipalityName, "", "Municipality name printed on report headers."),
        (ProvinceName, "", "Province name printed on report headers."),
        (HeadOfOffice, "", "Head of office shown on printed forms."),
        (PreparedBy, "", "Default name on the 'Prepared by' signature line."),
        (CertifiedBy, "", "Default name on the 'Certified correct by' signature line."),
        (BackupFolder, "", "Where backups are written. Blank uses the default AFMS folder."),
        (ReportFolder, "", "Where exported reports are saved. Blank uses the default AFMS folder."),
        (SessionTimeoutMinutes, "30", "Minutes of inactivity before the app locks itself."),
        (DailyBackupEnabled, "true", "Take one automatic backup per day."),
        (BackupRetentionDays, "60", "Delete automatic backups older than this many days."),
        (LastDailyBackupOn, "", "Internal marker for the daily backup. Do not edit.")
    ];
}
