using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class SettingsService(
    InventoryDbContext context,
    ICurrentUser session,
    IAuditService audit,
    IApplicationPaths paths) : ISettingsService
{
    public async Task<OfficeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await ReadAllAsync(cancellationToken);

        return new OfficeSettings(
            OfficeName: Read(values, SettingsCatalog.OfficeName, "Municipal Treasury Office"),
            MunicipalityName: Read(values, SettingsCatalog.MunicipalityName, string.Empty),
            ProvinceName: Read(values, SettingsCatalog.ProvinceName, string.Empty),
            HeadOfOffice: Read(values, SettingsCatalog.HeadOfOffice, string.Empty),
            PreparedBy: Read(values, SettingsCatalog.PreparedBy, string.Empty),
            CertifiedBy: Read(values, SettingsCatalog.CertifiedBy, string.Empty),
            BackupFolder: Fallback(Read(values, SettingsCatalog.BackupFolder, string.Empty), paths.DefaultBackupDirectory),
            ReportFolder: Fallback(Read(values, SettingsCatalog.ReportFolder, string.Empty), paths.DefaultReportDirectory),
            SessionTimeoutMinutes: ReadInt(values, SettingsCatalog.SessionTimeoutMinutes, 30, 5, 480),
            DailyBackupEnabled: ReadBool(values, SettingsCatalog.DailyBackupEnabled, true),
            BackupRetentionDays: ReadInt(values, SettingsCatalog.BackupRetentionDays, 60, 7, 3650));
    }

    public async Task SaveAsync(OfficeSettings settings, CancellationToken cancellationToken = default)
    {
        session.Require();

        var errors = new ErrorList();
        errors.AddIf(string.IsNullOrWhiteSpace(settings.OfficeName), "Office name is required.");
        errors.AddIf(settings.SessionTimeoutMinutes is < 5 or > 480,
            "Auto-lock must be between 5 and 480 minutes.");
        errors.AddIf(settings.BackupRetentionDays is < 7 or > 3650,
            "Backup retention must be between 7 and 3650 days.");
        ValidateFolder(errors, settings.BackupFolder, "Backup folder");
        ValidateFolder(errors, settings.ReportFolder, "Report folder");
        errors.ThrowIfAny();

        await WriteAsync(SettingsCatalog.OfficeName, settings.OfficeName.Trim(), cancellationToken);
        await WriteAsync(SettingsCatalog.MunicipalityName, settings.MunicipalityName?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.ProvinceName, settings.ProvinceName?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.HeadOfOffice, settings.HeadOfOffice?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.PreparedBy, settings.PreparedBy?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.CertifiedBy, settings.CertifiedBy?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.BackupFolder, settings.BackupFolder?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.ReportFolder, settings.ReportFolder?.Trim() ?? string.Empty, cancellationToken);
        await WriteAsync(SettingsCatalog.SessionTimeoutMinutes, settings.SessionTimeoutMinutes.ToString(), cancellationToken);
        await WriteAsync(SettingsCatalog.DailyBackupEnabled, settings.DailyBackupEnabled ? "true" : "false", cancellationToken);
        await WriteAsync(SettingsCatalog.BackupRetentionDays, settings.BackupRetentionDays.ToString(), cancellationToken);

        await audit.RecordAsync(
            ActivityType.SettingsUpdated, "Office settings updated.",
            nameof(SystemSetting), null, null,
            new { settings.OfficeName, settings.SessionTimeoutMinutes, settings.DailyBackupEnabled },
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateFolder(ErrorList errors, string? folder, string label)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        try
        {
            _ = Path.GetFullPath(folder);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"{label} is not a valid folder path.");
        }
    }

    private async Task<Dictionary<string, string>> ReadAllAsync(CancellationToken cancellationToken) =>
        await context.SystemSettings.AsNoTracking()
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

    private async Task WriteAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await context.SystemSettings.SingleOrDefaultAsync(
            entry => entry.Key == key, cancellationToken);
        if (setting is null)
            context.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        else
            setting.Value = value;
    }

    private static string Read(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback, int min, int max) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
