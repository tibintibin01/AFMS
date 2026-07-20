using AFMS.Domain.Enums;

namespace AFMS.Application.Models;

public sealed record BackupOptions(
    string BackupFolder,
    bool DailyBackupEnabled,
    TimeOnly DailyBackupTime,
    int RetentionDays,
    int RecentBackupWarningHours,
    bool UsesRecommendedSeparateLocation,
    string LocationWarning);

public sealed record BackupHistoryDto(
    string FilePath,
    string FileName,
    BackupKind Kind,
    DateTimeOffset CreatedAt,
    long FileSize,
    bool IsValid,
    string ValidationMessage,
    string CreatedBy,
    string Sha256);

public sealed record BackupStatusDto(
    BackupOptions Options,
    DateTimeOffset? LastSuccessfulBackup,
    bool IsBackupOverdue,
    bool HasPendingRestore,
    IReadOnlyList<BackupHistoryDto> History);

public sealed record BackupValidationResult(bool IsValid, string Message, string? DatabaseVersion = null);

public sealed record FiscalYearDto(
    Guid Id,
    int Year,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalYearWorkflowStatus Status,
    bool IsClosed,
    DateTimeOffset? CloseRequestedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ReopenRequestedAt,
    DateTimeOffset? ReopenedAt,
    string? ClosureReason,
    string? ReopenReason,
    string? SnapshotReference,
    string? ValidationIssues);

public sealed record FiscalYearCloseCheck(
    int DraftTransactions,
    int UnresolvedExceptions,
    int InventoryDiscrepancies,
    int InvalidBalances,
    int UnapprovedTransactions,
    IReadOnlyList<string> Issues)
{
    public bool CanCloseWithoutExceptions => Issues.Count == 0;
}

public sealed record FiscalYearSnapshotDto(
    string FormType,
    long BeginningQuantity,
    long Additions,
    long Usage,
    long Exceptions,
    long EndingQuantity,
    long OutstandingQuantity);

public sealed record AuditLogFilter(
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? UserId = null,
    UserActivityType? ActivityType = null,
    string? Module = null,
    int Page = 1,
    int PageSize = 100);

public sealed record AuditLogPage(IReadOnlyList<AuditLogDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
