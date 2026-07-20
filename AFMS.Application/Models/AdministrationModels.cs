using AFMS.Domain.Enums;

namespace AFMS.Application.Models;

public sealed record AccountableFormTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    int ReceiptsPerBooklet,
    string? SerialNumberPrefix,
    int? SerialNumberLength,
    bool RequiresIndividualReceiptTracking,
    bool IsActive,
    string? Remarks,
    bool HasTransactionHistory);

public sealed record SaveAccountableFormTypeRequest(
    Guid? Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    int ReceiptsPerBooklet,
    string? SerialNumberPrefix,
    int? SerialNumberLength,
    bool RequiresIndividualReceiptTracking,
    bool IsActive,
    string? Remarks);

public sealed record BarangayDto(
    Guid Id,
    string Code,
    string Name,
    string Municipality,
    string Province,
    bool IsActive,
    string? Remarks);

public sealed record SaveBarangayRequest(
    Guid? Id,
    string Code,
    string Name,
    string Municipality,
    string Province,
    bool IsActive,
    string? Remarks);

public sealed record CollectorDto(
    Guid Id,
    string CollectorNumber,
    string FullName,
    string? EmployeeNumber,
    string? Position,
    string? OfficeOrDepartment,
    Guid? BarangayId,
    string? BarangayName,
    string? ContactNumber,
    string? EmailAddress,
    string? AppointmentReference,
    bool IsActive,
    string? Remarks,
    bool HasTransactionHistory);

public sealed record SaveCollectorRequest(
    Guid? Id,
    string CollectorNumber,
    string FullName,
    string? EmployeeNumber,
    string? Position,
    string? OfficeOrDepartment,
    Guid? BarangayId,
    string? ContactNumber,
    string? EmailAddress,
    string? AppointmentReference,
    bool IsActive,
    string? Remarks);

public sealed record RoleDto(Guid Id, string Name, string? Description);

public sealed record UserDto(
    Guid Id,
    string Username,
    string FullName,
    Guid? LinkedCollectorId,
    string? LinkedCollectorName,
    Guid? RoleId,
    string RoleName,
    bool IsActive,
    DateTimeOffset DateCreated,
    DateTimeOffset? LastLogin,
    int FailedLoginCount,
    bool MustChangePassword);

public sealed record CreateUserRequest(
    string Username,
    string FullName,
    Guid? LinkedCollectorId,
    Guid RoleId,
    string TemporaryPassword,
    bool MustChangePassword);

public sealed record UpdateUserRequest(
    Guid Id,
    string FullName,
    Guid? LinkedCollectorId,
    Guid RoleId,
    bool IsActive,
    bool MustChangePassword);

public sealed record ResetPasswordRequest(Guid UserId, string TemporaryPassword, bool ForceChange);

public sealed record AuditLogDto(
    DateTimeOffset OccurredAt,
    string Username,
    UserActivityType ActivityType,
    string Description,
    string? EntityName,
    Guid? EntityId,
    string? OldValues,
    string? NewValues,
    string? MachineName = null,
    string? Module = null,
    string? ReferenceNumber = null,
    string? Reason = null,
    string? IntegrityHash = null);

public sealed class SystemSettingsDto
{
    public string MunicipalityName { get; set; } = string.Empty;
    public string ProvinceName { get; set; } = string.Empty;
    public string OfficeName { get; set; } = "Municipal Treasury Office";
    public string OfficeAddress { get; set; } = string.Empty;
    public string MunicipalTreasurerName { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string CheckedBy { get; set; } = string.Empty;
    public string CertifiedCorrectBy { get; set; } = string.Empty;
    public string DefaultFiscalYear { get; set; } = string.Empty;
    public string ReferenceNumberPrefixes { get; set; } = string.Empty;
    public string DatabaseBackupFolder { get; set; } = string.Empty;
    public string ReportOutputFolder { get; set; } = string.Empty;
    public bool DailyBackupEnabled { get; set; } = true;
    public string DailyBackupTime { get; set; } = "17:00";
    public int BackupRetentionDays { get; set; } = 30;
    public int RecentBackupWarningHours { get; set; } = 48;
    public int SessionTimeoutMinutes { get; set; } = 30;
}
