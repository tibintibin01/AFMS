using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class SystemSettingsService(
    ISystemSettingRepository repository,
    IAuthorizationService authorization,
    IAuditService audit,
    IUnitOfWork unitOfWork) : ISystemSettingsService
{
    private static readonly (string Key, Func<SystemSettingsDto, string> Get, Action<SystemSettingsDto, string> Set)[] Definitions =
    [
        ("MunicipalityName", value => value.MunicipalityName, (value, text) => value.MunicipalityName = text),
        ("ProvinceName", value => value.ProvinceName, (value, text) => value.ProvinceName = text),
        ("OfficeName", value => value.OfficeName, (value, text) => value.OfficeName = text),
        ("OfficeAddress", value => value.OfficeAddress, (value, text) => value.OfficeAddress = text),
        ("MunicipalTreasurerName", value => value.MunicipalTreasurerName, (value, text) => value.MunicipalTreasurerName = text),
        ("PreparedBy", value => value.PreparedBy, (value, text) => value.PreparedBy = text),
        ("CheckedBy", value => value.CheckedBy, (value, text) => value.CheckedBy = text),
        ("CertifiedCorrectBy", value => value.CertifiedCorrectBy, (value, text) => value.CertifiedCorrectBy = text),
        ("DefaultFiscalYear", value => value.DefaultFiscalYear, (value, text) => value.DefaultFiscalYear = text),
        ("ReferenceNumberPrefixes", value => value.ReferenceNumberPrefixes, (value, text) => value.ReferenceNumberPrefixes = text),
        ("DatabaseBackupFolder", value => value.DatabaseBackupFolder, (value, text) => value.DatabaseBackupFolder = text),
        ("ReportOutputFolder", value => value.ReportOutputFolder, (value, text) => value.ReportOutputFolder = text),
        ("DailyBackupEnabled", value => value.DailyBackupEnabled.ToString(), (value, text) => value.DailyBackupEnabled = bool.TryParse(text, out var parsed) && parsed),
        ("DailyBackupTime", value => value.DailyBackupTime, (value, text) => value.DailyBackupTime = text),
        ("BackupRetentionDays", value => value.BackupRetentionDays.ToString(), (value, text) => value.BackupRetentionDays = int.TryParse(text, out var parsed) ? parsed : 30),
        ("RecentBackupWarningHours", value => value.RecentBackupWarningHours.ToString(), (value, text) => value.RecentBackupWarningHours = int.TryParse(text, out var parsed) ? parsed : 48),
        ("SessionTimeoutMinutes", value => value.SessionTimeoutMinutes.ToString(), (value, text) => value.SessionTimeoutMinutes = int.TryParse(text, out var parsed) ? parsed : 30)
    ];

    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageSystemSettings);
        var settings = new SystemSettingsDto();
        var stored = (await repository.ListAsync(cancellationToken)).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            if (stored.TryGetValue(definition.Key, out var value)) definition.Set(settings, value);
        }
        return settings;
    }

    public async Task SaveAsync(SystemSettingsDto settings, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageSystemSettings);
        if (!TimeOnly.TryParse(settings.DailyBackupTime, out _)) throw new ApplicationValidationException(["Daily backup time must be a valid time."]);
        if (settings.BackupRetentionDays is < 1 or > 3650) throw new ApplicationValidationException(["Backup retention must be between 1 and 3650 days."]);
        if (settings.RecentBackupWarningHours is < 1 or > 8760) throw new ApplicationValidationException(["Recent-backup warning must be between 1 and 8760 hours."]);
        if (settings.SessionTimeoutMinutes is < 5 or > 480) throw new ApplicationValidationException(["Session timeout must be between 5 and 480 minutes."]);
        var changes = new List<object>();
        foreach (var definition in Definitions)
        {
            var value = definition.Get(settings).Trim();
            var entity = await repository.GetByKeyAsync(definition.Key, cancellationToken);
            if (entity is null)
            {
                entity = new SystemSetting { Key = definition.Key, Value = value, Description = $"AFMS setting: {definition.Key}" };
                await repository.AddAsync(entity, cancellationToken);
                changes.Add(new { definition.Key, OldValue = (string?)null, NewValue = value });
            }
            else if (!string.Equals(entity.Value, value, StringComparison.Ordinal))
            {
                changes.Add(new { definition.Key, OldValue = entity.Value, NewValue = value });
                entity.Value = value;
            }
        }
        if (changes.Count == 0) return;
        await audit.RecordAsync(UserActivityType.SystemSettingChanged, "System settings changed.", nameof(SystemSetting), Guid.Empty,
            null, changes, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
