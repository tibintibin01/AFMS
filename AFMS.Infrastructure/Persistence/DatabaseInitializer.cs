using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AFMS.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    AfmsDbContext context,
    IInitialDataProvider initialDataProvider,
    IAuditLogRepository auditLogs,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    private const string InitialDataVersionSettingKey = "InitialDataSeedVersion";
    private static readonly IReadOnlyDictionary<string, (string Description, SystemPermission[] Permissions)> DefaultRoles =
        new Dictionary<string, (string, SystemPermission[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["Administrator"] = ("Full system administration access.", Enum.GetValues<SystemPermission>()),
            ["Accountable Forms Custodian"] = ("Manages accountable forms, collectors, and stock custody.",
            [SystemPermission.ViewDashboard, SystemPermission.ManageFormTypes, SystemPermission.ManageCollectors,
             SystemPermission.ManageBarangays, SystemPermission.ReceiveStock, SystemPermission.IssueStock,
             SystemPermission.RecordUsage, SystemPermission.TransferStock, SystemPermission.ReturnStock,
             SystemPermission.ManageExceptions, SystemPermission.ViewReports, SystemPermission.ExportReports,
             SystemPermission.ManageFiscalYears, SystemPermission.ViewInventory, SystemPermission.ViewStockMovements,
             SystemPermission.ViewIssuances, SystemPermission.ViewCollectorAccountability]),
            ["Encoder"] = ("Records authorized operational data.",
            [SystemPermission.ViewDashboard, SystemPermission.RecordUsage, SystemPermission.ViewReports]),
            ["Supervisor"] = ("Reviews and approves accountable-form operations.",
            [SystemPermission.ViewDashboard, SystemPermission.ReceiveStock, SystemPermission.IssueStock,
             SystemPermission.RecordUsage, SystemPermission.TransferStock, SystemPermission.ReturnStock,
             SystemPermission.ManageExceptions, SystemPermission.ApproveTransactions, SystemPermission.ViewReports,
             SystemPermission.ExportReports, SystemPermission.ViewInventory, SystemPermission.ViewStockMovements,
             SystemPermission.ViewIssuances, SystemPermission.ViewCollectorAccountability,
             SystemPermission.ManageFiscalYears, SystemPermission.CloseFiscalYear,
             SystemPermission.ApproveFiscalYearReopen]),
            ["Auditor or Viewer"] = ("Read-only reporting and audit-log access.",
            [SystemPermission.ViewDashboard, SystemPermission.ViewReports, SystemPermission.ExportReports,
             SystemPermission.ViewAuditLogs, SystemPermission.ViewInventory, SystemPermission.ViewStockMovements,
             SystemPermission.ViewIssuances, SystemPermission.ViewCollectorAccountability])
        };

    private static readonly string[] LegacyRoleNames = ["TreasuryStaff", "Collector", "Auditor"];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing AFMS database");
        await context.Database.MigrateAsync(cancellationToken);
        var upgradedAuditRecords = await AuditIntegrity.UpgradeLegacyChainAsync(context, cancellationToken);
        if (upgradedAuditRecords > 0)
            logger.LogWarning("Upgraded {AuditEntryCount} retained legacy audit records to the protected integrity chain", upgradedAuditRecords);
        var auditEntries = await context.AuditLogs.AsNoTracking().ToListAsync(cancellationToken);
        AuditIntegrity.Verify(auditEntries);
        logger.LogInformation("Verified the AFMS audit integrity chain ({AuditEntryCount} records)", auditEntries.Count);
        if (upgradedAuditRecords > 0)
        {
            await auditLogs.AddAsync(new AuditLog
            {
                ActivityType = UserActivityType.RecordUpdated,
                Description = "Legacy audit history upgraded to the protected integrity chain.",
                EntityName = "AuditLog",
                Module = "Security",
                MachineName = Environment.MachineName,
                NewValues = JsonSerializer.Serialize(new { RecordsProtected = upgradedAuditRecords })
            }, cancellationToken);
        }
        await BackfillAccountabilityRangesAsync(cancellationToken);

        var existingRoles = await context.Roles.Include(role => role.Permissions).ToListAsync(cancellationToken);
        foreach (var definition in DefaultRoles)
        {
            var role = existingRoles.FirstOrDefault(item => item.Name.Equals(definition.Key, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                role = new Role { Name = definition.Key, Description = definition.Value.Description };
                context.Roles.Add(role);
                existingRoles.Add(role);
            }
            else
            {
                role.Description = definition.Value.Description;
                role.IsActive = true;
            }

            var desiredPermissions = definition.Value.Permissions.ToHashSet();
            foreach (var existingPermission in role.Permissions)
                existingPermission.IsActive = desiredPermissions.Contains(existingPermission.Permission);

            foreach (var permission in desiredPermissions.Where(permission => role.Permissions.All(item => item.Permission != permission)))
            {
                var rolePermission = new RolePermission { Role = role, RoleId = role.Id, Permission = permission };
                context.RolePermissions.Add(rolePermission);
            }
        }

        foreach (var legacyRole in existingRoles.Where(role => LegacyRoleNames.Contains(role.Name, StringComparer.OrdinalIgnoreCase)))
        {
            legacyRole.IsActive = false;
        }

        await SeedConfiguredInitialDataAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("AFMS database initialization completed");
    }

    private async Task SeedConfiguredInitialDataAsync(CancellationToken cancellationToken)
    {
        var data = await initialDataProvider.GetAsync(cancellationToken);
        if (data is null) return;

        ValidateInitialData(data);
        var seedMarker = await context.SystemSettings
            .SingleOrDefaultAsync(setting => setting.Key == InitialDataVersionSettingKey, cancellationToken);
        if (seedMarker?.Value == data.Version)
        {
            logger.LogInformation("Initial master data version {Version} is already loaded", data.Version);
            return;
        }

        var existingSettings = await context.SystemSettings
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);
        var settingKeys = existingSettings.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in data.SystemSettings.Where(item => !settingKeys.Contains(item.Key)))
        {
            context.SystemSettings.Add(new SystemSetting
            {
                Key = key.Trim(),
                Value = value?.Trim() ?? string.Empty,
                Description = "Configured initial value; editable in Administration."
            });
            settingKeys.Add(key);
        }

        var existingBarangays = await context.Barangays
            .Select(item => new { item.Code, item.Name })
            .ToListAsync(cancellationToken);
        var barangayCodes = existingBarangays.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var barangayNames = existingBarangays.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var addedBarangays = 0;
        foreach (var item in data.Barangays.Where(item =>
                     !barangayCodes.Contains(item.Code) && !barangayNames.Contains(item.Name)))
        {
            context.Barangays.Add(new Barangay
            {
                Code = item.Code.Trim(),
                Name = item.Name.Trim(),
                Municipality = item.Municipality.Trim(),
                Province = item.Province.Trim(),
                Remarks = NormalizeOptional(item.Remarks)
            });
            barangayCodes.Add(item.Code);
            barangayNames.Add(item.Name);
            addedBarangays++;
        }

        var existingFormTypes = await context.AccountableFormTypes.ToListAsync(cancellationToken);
        var addedFormTypes = 0;
        var updatedFormTypes = 0;
        foreach (var item in data.AccountableFormTypes)
        {
            var existing = existingFormTypes.FirstOrDefault(value =>
                value.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.PreviousCode) && value.Code.Equals(item.PreviousCode, StringComparison.OrdinalIgnoreCase)));
            if (existing is null)
            {
                existing = new AccountableFormType
                {
                    Code = item.Code.Trim(),
                    Name = item.Name.Trim(),
                    Category = NormalizeOptional(item.Category),
                    DefaultReceiptsPerBooklet = item.ReceiptsPerBooklet,
                    Remarks = NormalizeOptional(item.Remarks)
                };
                context.AccountableFormTypes.Add(existing);
                existingFormTypes.Add(existing);
                addedFormTypes++;
            }
            else
            {
                var codeConflict = existingFormTypes.Any(value => value.Id != existing.Id && value.Code.Equals(item.Code.Trim(), StringComparison.OrdinalIgnoreCase));
                if (codeConflict) throw new InvalidDataException($"Configured form code '{item.Code}' is already assigned to another record.");
                existing.Code = item.Code.Trim();
                existing.Name = item.Name.Trim();
                existing.DefaultReceiptsPerBooklet = item.ReceiptsPerBooklet;
                if (item.Category is not null) existing.Category = NormalizeOptional(item.Category);
                if (item.Remarks is not null) existing.Remarks = NormalizeOptional(item.Remarks);
                updatedFormTypes++;
            }
        }

        var existingCollectors = await context.Collectors.ToListAsync(cancellationToken);
        var addedCollectors = 0;
        var updatedCollectors = 0;
        foreach (var item in data.Collectors)
        {
            var existing = existingCollectors.FirstOrDefault(value => value.FullName.Equals(item.FullName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new Collector
                {
                    Code = item.CollectorNumber.Trim(),
                    FullName = item.FullName.Trim(),
                    BarangayId = null,
                    Remarks = NormalizeOptional(item.Remarks)
                };
                context.Collectors.Add(existing);
                existingCollectors.Add(existing);
                addedCollectors++;
            }
            else
            {
                var codeConflict = existingCollectors.Any(value => value.Id != existing.Id && value.Code.Equals(item.CollectorNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                if (codeConflict) throw new InvalidDataException($"Configured collector number '{item.CollectorNumber}' is already assigned to another record.");
                existing.Code = item.CollectorNumber.Trim();
                existing.FullName = item.FullName.Trim();
                if (item.Remarks is not null) existing.Remarks = NormalizeOptional(item.Remarks);
                updatedCollectors++;
            }
        }

        if (seedMarker is null)
        {
            context.SystemSettings.Add(new SystemSetting
            {
                Key = InitialDataVersionSettingKey,
                Value = data.Version,
                Description = "Tracks the configurable initial-data version already imported."
            });
        }
        else
        {
            seedMarker.Value = data.Version;
            seedMarker.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await auditLogs.AddAsync(new AuditLog
        {
            ActivityType = UserActivityType.RecordCreated,
            Description = "Configured initial master data imported.",
            EntityName = "InitialData",
            MachineName = Environment.MachineName,
            NewValues = JsonSerializer.Serialize(new
            {
                data.Version,
                BarangaysAdded = addedBarangays,
                FormTypesAdded = addedFormTypes,
                FormTypesUpdated = updatedFormTypes,
                CollectorsAdded = addedCollectors,
                CollectorsUpdated = updatedCollectors
            })
        }, cancellationToken);

        logger.LogInformation(
            "Loaded initial master data {Version}: {Barangays} barangays added, {FormTypes} form types added, {FormTypesUpdated} updated, {Collectors} collectors added, {CollectorsUpdated} updated",
            data.Version, addedBarangays, addedFormTypes, updatedFormTypes, addedCollectors, updatedCollectors);
    }

    private async Task BackfillAccountabilityRangesAsync(CancellationToken cancellationToken)
    {
        var booklets = await context.AccountableFormBooklets
            .Include(x => x.AccountabilityRanges)
            .Include(x => x.IssuanceItems).ThenInclude(x => x.Issuance)
            .Where(x => !x.AccountabilityRanges.Any())
            .ToListAsync(cancellationToken);
        foreach (var booklet in booklets)
        {
            var issuanceId = booklet.IssuanceItems.Where(x => x.Issuance.Status == TransactionStatus.Finalized)
                .OrderByDescending(x => x.Issuance.FinalizedAt).Select(x => (Guid?)x.IssuanceId).FirstOrDefault();
            var range = new AccountabilityRange
            {
                AccountableFormBookletId = booklet.Id,
                BeginningSerial = booklet.BeginningSerial,
                EndingSerial = booklet.EndingSerial,
                Status = booklet.Status,
                CurrentCollectorId = booklet.CurrentCollectorId,
                CurrentBarangayId = booklet.CurrentBarangayId,
                StorageLocation = booklet.StorageLocation,
                SourceIssuanceId = issuanceId
            };
            booklet.AccountabilityRanges.Add(range);
            context.AccountabilityRanges.Add(range);
        }
        if (booklets.Count > 0) logger.LogInformation("Created current-accountability ranges for {Count} existing booklets", booklets.Count);
    }

    private static void ValidateInitialData(InitialDataSet data)
    {
        if (string.IsNullOrWhiteSpace(data.Version))
            throw new InvalidDataException("Initial-data Version is required.");

        ValidateUniqueRequired(data.Barangays.Select(item => item.Code), "barangay code");
        ValidateUniqueRequired(data.Barangays.Select(item => item.Name), "barangay name");
        ValidateUniqueRequired(data.AccountableFormTypes.Select(item => item.Code), "accountable-form code");
        ValidateUniqueRequired(data.Collectors.Select(item => item.CollectorNumber), "collector identifier");

        if (data.Barangays.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                                       string.IsNullOrWhiteSpace(item.Municipality) ||
                                       string.IsNullOrWhiteSpace(item.Province)))
            throw new InvalidDataException("Every configured barangay requires a name, municipality, and province.");

        if (data.AccountableFormTypes.Any(item => string.IsNullOrWhiteSpace(item.Name) || item.ReceiptsPerBooklet <= 0))
            throw new InvalidDataException("Every configured accountable-form type requires a name and a positive booklet size.");

        if (data.Collectors.Any(item => string.IsNullOrWhiteSpace(item.FullName)))
            throw new InvalidDataException("Every configured collector requires a full name.");
    }

    private static void ValidateUniqueRequired(IEnumerable<string> values, string label)
    {
        var normalized = values.Select(value => value?.Trim() ?? string.Empty).ToList();
        if (normalized.Any(string.IsNullOrWhiteSpace) || normalized.Count != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidDataException($"Configured {label} values must be present and unique.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
