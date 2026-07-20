using AFMS.Domain.Enums;

namespace AFMS.Application.Models;

public sealed record StockReceiptItemInput(int NumberOfBooklets, int ReceiptsPerBooklet, long BeginningSerial, long EndingSerial)
{
    public long TotalReceiptQuantity => EndingSerial >= BeginningSerial ? EndingSerial - BeginningSerial + 1 : 0;
}

public sealed record SaveStockReceiptDraftRequest(
    Guid? Id,
    DateOnly DateReceived,
    Guid AccountableFormTypeId,
    string? SourceOrIssuingOffice,
    string? SourceDocumentNumber,
    int SeriesYear,
    string? StorageLocation,
    string? ReceivedBy,
    string? Remarks,
    IReadOnlyList<StockReceiptItemInput> Items);

public sealed record StockReceiptItemDto(Guid Id, int NumberOfBooklets, int ReceiptsPerBooklet, long BeginningSerial, long EndingSerial, long TotalReceiptQuantity);

public sealed record StockReceiptDto(
    Guid Id,
    string ReferenceNumber,
    DateOnly DateReceived,
    Guid AccountableFormTypeId,
    string FormType,
    string? SourceOrIssuingOffice,
    string? SourceDocumentNumber,
    int SeriesYear,
    string? StorageLocation,
    string? ReceivedBy,
    string EncodedBy,
    string? Remarks,
    TransactionStatus Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? FinalizedAt,
    IReadOnlyList<StockReceiptItemDto> Items);

public sealed record InventoryFilter(
    string? Search = null,
    Guid? AccountableFormTypeId = null,
    int? SeriesYear = null,
    AccountableFormStatus? Status = null,
    Guid? CollectorId = null,
    Guid? BarangayId = null,
    DateOnly? ReceivedFrom = null,
    DateOnly? ReceivedTo = null,
    long? SerialNumber = null,
    bool AvailableOnly = false,
    bool OutstandingOnly = false);

public sealed record InventoryBookletDto(
    Guid Id,
    string FormType,
    int SeriesYear,
    int BookletNumber,
    long BeginningSerial,
    long EndingSerial,
    long TotalQuantity,
    long UsedQuantity,
    long RemainingQuantity,
    string CurrentHolder,
    string Barangay,
    DateOnly DateReceived,
    DateOnly? DateIssued,
    AccountableFormStatus Status,
    string? StorageLocation,
    DateTimeOffset? LastMovementDate,
    string? Remarks);

public sealed record StockMovementDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    StockMovementType MovementType,
    string ReferenceNumber,
    string FormType,
    long BeginningSerial,
    long EndingSerial,
    long Quantity,
    string? Source,
    string? Destination,
    string User,
    string? Remarks);

public sealed record UsageRangeSummary(long BeginningSerial, long EndingSerial);
public sealed record ExceptionSummary(long SerialNumber, ExceptionType Type, string Details, DateTimeOffset ReportedAt);

public sealed record InventoryDetailDto(
    InventoryBookletDto Booklet,
    string ReceivingReference,
    DateOnly ReceivingDate,
    string? ReceivingSource,
    string? SourceDocumentNumber,
    string CurrentAccountability,
    IReadOnlyList<StockMovementDto> Movements,
    IReadOnlyList<UsageRangeSummary> UsageRanges,
    IReadOnlyList<ExceptionSummary> Exceptions);

public sealed record MovementHistoryFilter(
    string? Search = null,
    StockMovementType? MovementType = null,
    Guid? AccountableFormTypeId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    long? SerialNumber = null);

public sealed record LookupOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record StockFormTypeLookup(Guid Id, string Code, string Name, int ReceiptsPerBooklet);
public sealed record StockLookup(Guid Id, string Name, bool IsActive = true);
