using AFMS.Core.Domain;

namespace AFMS.Core;

// ---------------------------------------------------------------------------
// Sign-in and session
// ---------------------------------------------------------------------------

public sealed record SignInRequest(string Username, string Password);

public sealed record SignInResult(bool Succeeded, string? ErrorMessage)
{
    public static SignInResult Success() => new(true, null);
    public static SignInResult Failure(string message) => new(false, message);
}

public sealed record SignedInUser(Guid Id, string Username, string DisplayName, bool MustChangePassword);

public sealed record CreateFirstAccountRequest(string DisplayName, string Username, string Password);

// ---------------------------------------------------------------------------
// Items
// ---------------------------------------------------------------------------

public sealed record ItemDto(
    Guid Id,
    string Code,
    string Name,
    string Unit,
    string? Category,
    bool TracksSerials,
    long ReorderLevel,
    string? Remarks,
    bool IsActive,
    long QuantityIn,
    long QuantityOut,
    long OnHand)
{
    public bool IsLow => ReorderLevel > 0 && OnHand <= ReorderLevel;
}

public sealed record SaveItemRequest(
    Guid? Id,
    string Code,
    string Name,
    string Unit,
    string? Category,
    bool TracksSerials,
    long ReorderLevel,
    string? Remarks,
    bool IsActive);

/// <summary>Lightweight item entry for pick lists on the stock-in and release forms.</summary>
public sealed record ItemOption(Guid Id, string Code, string Name, string Unit, bool TracksSerials, long OnHand)
{
    public string Display => $"{Code} - {Name}";
}

// ---------------------------------------------------------------------------
// Recipients
// ---------------------------------------------------------------------------

public sealed record RecipientDto(
    Guid Id,
    string Code,
    string Name,
    string? Position,
    string? Office,
    string? ContactNumber,
    string? Remarks,
    bool IsActive,
    long OutstandingQuantity);

public sealed record SaveRecipientRequest(
    Guid? Id,
    string Code,
    string Name,
    string? Position,
    string? Office,
    string? ContactNumber,
    string? Remarks,
    bool IsActive);

public sealed record RecipientOption(Guid Id, string Code, string Name)
{
    public string Display => $"{Code} - {Name}";
}

// ---------------------------------------------------------------------------
// Stock in
// ---------------------------------------------------------------------------

public sealed record StockInDto(
    Guid Id,
    string ReferenceNumber,
    DateOnly EntryDate,
    StockInSource Source,
    string? SourceName,
    string? DocumentNumber,
    Guid? ReturnedByRecipientId,
    string? ReturnedByRecipientName,
    string? ReceivedBy,
    string? Remarks,
    RecordStatus Status,
    string? VoidReason,
    string RecordedBy,
    IReadOnlyList<StockInLineDto> Lines)
{
    public long TotalQuantity => Lines.Sum(line => line.Quantity);
    public string SourceLabel => Source switch
    {
        StockInSource.Purchase => "Purchase / delivery",
        StockInSource.Transferred => "Transferred in",
        StockInSource.Returned => "Returned by recipient",
        StockInSource.BeginningBalance => "Beginning balance",
        StockInSource.Adjustment => "Adjustment",
        _ => Source.ToString()
    };
}

public sealed record StockInLineDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    long Quantity,
    long? SerialFrom,
    long? SerialTo,
    string? Remarks)
{
    public string SerialLabel => SerialFrom is { } from && SerialTo is { } to
        ? (from == to ? from.ToString() : $"{from}-{to}")
        : "-";
}

public sealed record SaveStockInRequest(
    Guid? Id,
    DateOnly EntryDate,
    StockInSource Source,
    string? SourceName,
    string? DocumentNumber,
    Guid? ReturnedByRecipientId,
    string? ReceivedBy,
    string? Remarks,
    IReadOnlyList<StockLineInput> Lines);

/// <summary>A line typed into the stock-in or release form before it is saved.</summary>
public sealed record StockLineInput(
    Guid ItemId,
    long Quantity,
    long? SerialFrom,
    long? SerialTo,
    string? Remarks);

// ---------------------------------------------------------------------------
// Releases
// ---------------------------------------------------------------------------

public sealed record ReleaseDto(
    Guid Id,
    string ReferenceNumber,
    DateOnly ReleaseDate,
    Guid RecipientId,
    string RecipientCode,
    string RecipientName,
    string? Purpose,
    string ReleasedBy,
    string ReceivedBy,
    string? Remarks,
    RecordStatus Status,
    string? VoidReason,
    string RecordedBy,
    IReadOnlyList<ReleaseLineDto> Lines)
{
    public long TotalQuantity => Lines.Sum(line => line.Quantity);
}

public sealed record ReleaseLineDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    long Quantity,
    long? SerialFrom,
    long? SerialTo,
    string? Remarks)
{
    public string SerialLabel => SerialFrom is { } from && SerialTo is { } to
        ? (from == to ? from.ToString() : $"{from}-{to}")
        : "-";
}

public sealed record SaveReleaseRequest(
    Guid? Id,
    DateOnly ReleaseDate,
    Guid RecipientId,
    string? Purpose,
    string ReleasedBy,
    string ReceivedBy,
    string? Remarks,
    IReadOnlyList<StockLineInput> Lines);

// ---------------------------------------------------------------------------
// Outstanding accountability
// ---------------------------------------------------------------------------

/// <summary>What one person still holds, grouped by item.</summary>
public sealed record OutstandingByRecipient(
    Guid RecipientId,
    string RecipientCode,
    string RecipientName,
    string? Position,
    string? Office,
    IReadOnlyList<OutstandingItem> Items)
{
    public long TotalQuantity => Items.Sum(item => item.Outstanding);
    public bool HasAnything => Items.Count > 0;
}

public sealed record OutstandingItem(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    long Released,
    long Returned,
    long Outstanding,
    string SerialsHeld,
    DateOnly? LastReleasedOn,
    string? LastReference);

// ---------------------------------------------------------------------------
// History
// ---------------------------------------------------------------------------

public sealed record HistoryFilter(
    string? SearchText = null,
    Guid? ItemId = null,
    Guid? RecipientId = null,
    MovementDirection? Direction = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    bool IncludeVoided = false);

/// <summary>One line of the combined in/out history.</summary>
public sealed record HistoryEntry(
    Guid DocumentId,
    string ReferenceNumber,
    DateOnly Date,
    MovementDirection Direction,
    string ItemCode,
    string ItemName,
    string Unit,
    long Quantity,
    string SerialLabel,
    string PartyName,
    string Detail,
    RecordStatus Status,
    string RecordedBy)
{
    public string DirectionLabel => Direction == MovementDirection.In ? "IN" : "OUT";
    public long QuantityIn => Direction == MovementDirection.In ? Quantity : 0;
    public long QuantityOut => Direction == MovementDirection.Out ? Quantity : 0;
    public bool IsVoided => Status == RecordStatus.Voided;
}

/// <summary>One line of a single item's stock card, with the running balance.</summary>
public sealed record StockCardEntry(
    DateOnly Date,
    string ReferenceNumber,
    string Particulars,
    long QuantityIn,
    long QuantityOut,
    long Balance,
    string SerialLabel);

// ---------------------------------------------------------------------------
// Dashboard
// ---------------------------------------------------------------------------

public sealed record DashboardSummary(
    int ActiveItems,
    int ActiveRecipients,
    long TotalOnHand,
    int LowStockCount,
    long OutstandingQuantity,
    int PeopleHoldingItems,
    int StockInThisMonth,
    int ReleasesThisMonth,
    IReadOnlyList<ItemDto> LowStockItems,
    IReadOnlyList<HistoryEntry> RecentActivity);

// ---------------------------------------------------------------------------
// Audit
// ---------------------------------------------------------------------------

public sealed record AuditFilter(
    string? SearchText = null,
    ActivityType? Activity = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 100);

public sealed record AuditEntryDto(
    DateTimeOffset OccurredAt,
    string User,
    ActivityType Activity,
    string Description,
    string? EntityName,
    string? ReferenceNumber,
    string? Details,
    string? MachineName);

public sealed record AuditPage(IReadOnlyList<AuditEntryDto> Entries, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

// ---------------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------------

public sealed record OfficeSettings(
    string OfficeName,
    string MunicipalityName,
    string ProvinceName,
    string HeadOfOffice,
    string PreparedBy,
    string CertifiedBy,
    string BackupFolder,
    string ReportFolder,
    int SessionTimeoutMinutes,
    bool DailyBackupEnabled,
    int BackupRetentionDays);

// ---------------------------------------------------------------------------
// Backup
// ---------------------------------------------------------------------------

public sealed record BackupFileDto(
    string FilePath,
    string FileName,
    BackupKind Kind,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    bool IsValid,
    string Message)
{
    public string SizeLabel => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:N0} KB"
        : $"{SizeBytes / (1024.0 * 1024.0):N1} MB";
}

public sealed record BackupStatus(
    DateTimeOffset? LastBackupAt,
    bool BackupOverdue,
    bool HasPendingRestore,
    IReadOnlyList<BackupFileDto> History);

public sealed record BackupCheckResult(bool IsValid, string Message);

// ---------------------------------------------------------------------------
// Reports
// ---------------------------------------------------------------------------

public enum ReportKind
{
    StockOnHand = 1,
    StockCard = 2,
    ReleasesByRecipient = 3,
    OutstandingAccountability = 4,
    InOutSummary = 5
}

public sealed record ReportRequest(
    ReportKind Kind,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? ItemId = null,
    Guid? RecipientId = null);

public enum ReportValueType
{
    Text = 0,
    Integer = 1,
    Decimal = 2,
    Date = 3,
    DateTime = 4
}

public sealed record ReportColumn(string Header, double Width, ReportValueType Type = ReportValueType.Text);

public sealed record ReportRow(IReadOnlyList<object?> Values);

public sealed record ReportTotal(string Label, decimal Value, string Format = "#,##0");

public sealed record ReportDocument(
    string Title,
    string Subtitle,
    string ReportReference,
    string OfficeName,
    string MunicipalityName,
    string ProvinceName,
    string GeneratedBy,
    DateTimeOffset GeneratedAt,
    string PreparedBy,
    string CertifiedBy,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<ReportTotal> Totals);
