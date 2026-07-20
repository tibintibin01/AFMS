using AFMS.Domain.Enums;

namespace AFMS.Application.Models;

public sealed record AvailableBookletFilter(Guid? FormTypeId = null, int? SeriesYear = null, string? StorageLocation = null, long? BeginningSerial = null, long? EndingSerial = null);

public sealed record AvailableBookletDto(Guid Id, Guid BookletId, Guid FormTypeId, string FormType, int SeriesYear, int BookletNumber,
    long BeginningSerial, long EndingSerial, long Quantity, string? StorageLocation, DateOnly DateReceived);

public sealed record SaveIssuanceDraftRequest(
    Guid? Id,
    DateOnly DateIssued,
    Guid CollectorId,
    Guid? BarangayId,
    string? CollectionArea,
    string Purpose,
    string ReleasedBy,
    string ReceivedBy,
    string? Remarks,
    IReadOnlyList<Guid> BookletIds);

public sealed record IssuanceItemDto(Guid Id, Guid BookletId, string FormType, int BookletNumber, long BeginningSerial, long EndingSerial, long Quantity, int SeriesYear, string? StorageLocation);

public sealed record IssuanceDto(
    Guid Id,
    string ReferenceNumber,
    DateOnly DateIssued,
    Guid CollectorId,
    string Collector,
    Guid? BarangayId,
    string BarangayOrArea,
    string Purpose,
    string ReleasedBy,
    string ApprovedBy,
    string ReceivedBy,
    string? Remarks,
    TransactionStatus Status,
    ApprovalStatus ApprovalStatus,
    string? RejectionReason,
    IReadOnlyList<IssuanceItemDto> Items);

public sealed record CollectorLedgerFilter(
    Guid CollectorId,
    Guid? FormTypeId = null,
    Guid? BarangayId = null,
    int? Month = null,
    int? Year = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    bool OutstandingOnly = false);

public enum AccountabilityTransactionType
{
    BeginningBalance = 1,
    Issuance = 2,
    TransferIn = 3,
    TransferOut = 4,
    Usage = 5,
    Return = 6,
    CancelledOrSpoiled = 7,
    Adjustment = 8,
    Reversal = 9
}

public sealed record CollectorLedgerEntryDto(
    DateTimeOffset Date,
    string ReferenceNumber,
    AccountabilityTransactionType TransactionType,
    string FormType,
    long BeginningSerial,
    long EndingSerial,
    long QuantityIn,
    long QuantityOut,
    long RunningBalance,
    string Barangay,
    string Status,
    string? Remarks);

public sealed record AccountabilitySummaryDto(string Label, long Quantity);

public sealed record CollectorAccountabilityDetailDto(
    Guid CollectorId,
    string CollectorNumber,
    string FullName,
    string? EmployeeNumber,
    string? Position,
    string? OfficeOrDepartment,
    bool IsActive,
    long TotalCurrentAccountability,
    IReadOnlyList<InventoryBookletDto> OutstandingForms,
    IReadOnlyList<InventoryBookletDto> FullyUsedForms,
    IReadOnlyList<InventoryBookletDto> ReturnedForms,
    IReadOnlyList<CollectorLedgerEntryDto> TransactionHistory,
    IReadOnlyList<AccountabilitySummaryDto> ByFormType,
    IReadOnlyList<AccountabilitySummaryDto> ByBarangay);

public sealed record IssuanceReceiptLine(string FormType, int SeriesYear, int BookletNumber, long BeginningSerial, long EndingSerial, long Quantity);

public sealed record IssuanceReceiptDocument(
    string Province,
    string Municipality,
    string OfficeName,
    string OfficeAddress,
    string ReferenceNumber,
    DateOnly DateIssued,
    string Collector,
    string Barangay,
    string Purpose,
    string Remarks,
    string PreparedBy,
    string IssuedBy,
    string ApprovedBy,
    string ReceivedBy,
    IReadOnlyList<IssuanceReceiptLine> Lines);
