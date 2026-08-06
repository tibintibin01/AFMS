using AFMS.Core.Domain;

namespace AFMS.Core;

/// <summary>Who is signed in right now, plus inactivity tracking.</summary>
public interface ICurrentUser
{
    SignedInUser? User { get; }
    bool IsSignedIn { get; }
    bool HasExpired { get; }
    DateTimeOffset? ExpiresAt { get; }

    void Begin(SignedInUser user, TimeSpan? timeout = null);
    void Replace(SignedInUser user);
    void Touch();
    void End();

    /// <summary>Returns the signed-in user or throws if the session ended or expired.</summary>
    SignedInUser Require();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IAuthenticationService
{
    Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<SignInResult> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>True when no account exists yet, so the app must ask for the first one.</summary>
    Task<bool> NeedsFirstAccountAsync(CancellationToken cancellationToken = default);
    Task<SignInResult> CreateFirstAccountAsync(CreateFirstAccountRequest request, CancellationToken cancellationToken = default);
}

public interface IItemService
{
    Task<IReadOnlyList<ItemDto>> ListAsync(string? search = null, bool? isActive = null, bool onlyLow = false, CancellationToken cancellationToken = default);
    Task<ItemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemOption>> ListOptionsAsync(CancellationToken cancellationToken = default);
    Task<ItemDto> SaveAsync(SaveItemRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListUnitsAsync(CancellationToken cancellationToken = default);
}

public interface IRecipientService
{
    Task<IReadOnlyList<RecipientDto>> ListAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<RecipientDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipientOption>> ListOptionsAsync(CancellationToken cancellationToken = default);
    Task<RecipientDto> SaveAsync(SaveRecipientRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IStockInService
{
    Task<IReadOnlyList<StockInDto>> ListAsync(string? search = null, DateOnly? from = null, DateOnly? to = null, bool includeVoided = true, CancellationToken cancellationToken = default);
    Task<StockInDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StockInDto> SaveAsync(SaveStockInRequest request, CancellationToken cancellationToken = default);
    Task VoidAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

public interface IReleaseService
{
    Task<IReadOnlyList<ReleaseDto>> ListAsync(string? search = null, Guid? recipientId = null, DateOnly? from = null, DateOnly? to = null, bool includeVoided = true, CancellationToken cancellationToken = default);
    Task<ReleaseDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReleaseDto> SaveAsync(SaveReleaseRequest request, CancellationToken cancellationToken = default);
    Task VoidAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a serial range before saving and returns a message when it is already
    /// out with someone. Lets the form warn while the user is still typing.
    /// </summary>
    Task<string?> CheckSerialAvailabilityAsync(Guid itemId, long from, long to, Guid? excludingReleaseId = null, CancellationToken cancellationToken = default);
}

public interface IAccountabilityService
{
    Task<IReadOnlyList<OutstandingByRecipient>> ListOutstandingAsync(Guid? recipientId = null, CancellationToken cancellationToken = default);
    Task<OutstandingByRecipient?> GetForRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default);
}

public interface IHistoryService
{
    Task<IReadOnlyList<HistoryEntry>> ListAsync(HistoryFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockCardEntry>> GetStockCardAsync(Guid itemId, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardSummary> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task RecordAsync(ActivityType activity, string description, string? entityName = null, Guid? entityId = null, string? referenceNumber = null, object? details = null, CancellationToken cancellationToken = default);
    Task<AuditPage> SearchAsync(AuditFilter filter, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<OfficeSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(OfficeSettings settings, CancellationToken cancellationToken = default);
}

public interface IReferenceNumberService
{
    /// <summary>Produces the next number for a prefix and year, for example IN-2026-0001.</summary>
    Task<string> NextAsync(string prefix, int year, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<ReportDocument> BuildAsync(ReportRequest request, CancellationToken cancellationToken = default);
    Task<string> ExportPdfAsync(ReportDocument document, string? folder = null, CancellationToken cancellationToken = default);
    Task<string> ExportExcelAsync(ReportDocument document, string? folder = null, CancellationToken cancellationToken = default);

    /// <summary>Writes the signed release slip a recipient keeps as proof.</summary>
    Task<string> ExportReleaseSlipAsync(Guid releaseId, string? folder = null, CancellationToken cancellationToken = default);
}

public interface IReportWriter
{
    Task WriteAsync(ReportDocument document, string outputPath, CancellationToken cancellationToken = default);
}

public interface IPdfReportWriter : IReportWriter;

public interface IExcelReportWriter : IReportWriter;

public interface IBackupService
{
    bool HasPendingRestore { get; }
    Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<BackupFileDto> CreateAsync(BackupKind kind, string? destinationFolder = null, CancellationToken cancellationToken = default);
    Task<BackupCheckResult> CheckAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>Queues a restore that is applied on the next start, before the database opens.</summary>
    Task ScheduleRestoreAsync(string backupPath, string reason, CancellationToken cancellationToken = default);
    Task<bool> RunDailyBackupIfDueAsync(CancellationToken cancellationToken = default);
    Task WritePendingRestoreAuditAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationPaths
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string LogDirectory { get; }
    string DefaultBackupDirectory { get; }
    string DefaultReportDirectory { get; }
    void EnsureDirectoriesExist();
}

public interface IDatabaseSetup
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Optional starter records imported once on a brand new database.</summary>
public interface IStarterDataProvider
{
    Task<StarterData?> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record StarterData(
    string Version,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<StarterItem> Items,
    IReadOnlyList<StarterRecipient> Recipients);

public sealed record StarterItem(string Code, string Name, string Unit, string? Category, bool TracksSerials, long ReorderLevel);

public sealed record StarterRecipient(string Code, string Name, string? Position, string? Office);
