using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Abstractions;

public interface IDatabaseOperationCoordinator
{
    Task<IAsyncDisposable> EnterWriteAsync(CancellationToken cancellationToken = default);
    Task<IAsyncDisposable> EnterMaintenanceAsync(CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<BackupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<BackupHistoryDto> CreateAsync(BackupKind kind, string? destinationFolder = null, CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateAsync(string backupPath, CancellationToken cancellationToken = default);
    Task ScheduleRestoreAsync(string backupPath, string reason, CancellationToken cancellationToken = default);
    Task<bool> RunScheduledBackupIfDueAsync(CancellationToken cancellationToken = default);
    Task CompletePendingRestoreAuditAsync(CancellationToken cancellationToken = default);
    bool HasPendingRestore { get; }
}

public interface IFiscalYearRepository
{
    Task<IReadOnlyList<FiscalYear>> ListAsync(CancellationToken cancellationToken = default);
    Task<FiscalYear?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FiscalYear?> FindForDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<bool> YearExistsAsync(int year, CancellationToken cancellationToken = default);
    Task AddAsync(FiscalYear year, CancellationToken cancellationToken = default);
    Task<FiscalYearCloseCheck> CheckCloseAsync(FiscalYear year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FiscalYearSnapshot>> BuildSnapshotAsync(FiscalYear year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FiscalYearCarryForward>> BuildCarryForwardAsync(FiscalYear year, CancellationToken cancellationToken = default);
    Task AddSnapshotsAsync(IEnumerable<FiscalYearSnapshot> rows, CancellationToken cancellationToken = default);
    Task AddCarryForwardsAsync(IEnumerable<FiscalYearCarryForward> rows, CancellationToken cancellationToken = default);
}

public interface IFiscalYearService
{
    Task<IReadOnlyList<FiscalYearDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<FiscalYearDto> CreateAsync(int year, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<FiscalYearCloseCheck> CheckCloseAsync(Guid id, CancellationToken cancellationToken = default);
    Task RequestCloseAsync(Guid id, string reason, bool allowDocumentedExceptions, CancellationToken cancellationToken = default);
    Task ApproveCloseAsync(Guid id, string comments, CancellationToken cancellationToken = default);
    Task RequestReopenAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task ApproveReopenAsync(Guid id, string comments, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FiscalYearSnapshotDto>> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IFiscalPeriodGuard
{
    Task EnsureOpenAsync(DateOnly transactionDate, CancellationToken cancellationToken = default);
}
