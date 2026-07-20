using AFMS.Domain.Enums;

namespace AFMS.Application.Models;

public sealed record OwnedRangeDto(Guid BookletId, string FormCode, string FormName, int SeriesYear,
    long BookletBeginning, long BookletEnding, long BeginningSerial, long EndingSerial,
    string CollectorName, string? BarangayName, AccountableFormStatus Status)
{
    public long Quantity => EndingSerial - BeginningSerial + 1;
    public string Display => $"{FormCode} | {BeginningSerial:N0}-{EndingSerial:N0} ({CollectorName})";
}

public sealed record UsageRangeInput(Guid BookletId, long BeginningSerial, long EndingSerial, string? GapExplanation = null);
public sealed record SaveUsageReportRequest(Guid? Id, Guid CollectorId, Guid FormTypeId, DateOnly PeriodStart,
    DateOnly PeriodEnd, DateOnly ReportingDate, decimal? AmountCollected, string? SourceReportNumber,
    string? Remarks, IReadOnlyList<UsageRangeInput> Ranges);
public sealed record UsageReportDto(Guid Id, string ReferenceNumber, string Collector, string FormType,
    DateOnly ReportingDate, long Quantity, decimal? AmountCollected, TransactionStatus Status, string? Remarks);

public sealed record SaveTransferRequest(Guid? Id, DateOnly TransferDate, Guid FromCollectorId, Guid ToCollectorId,
    Guid? BarangayId, Guid BookletId, long BeginningSerial, long EndingSerial, string Reason,
    string ReleasedBy, string ReceivedBy, string? Remarks);
public sealed record TransferDto(Guid Id, string ReferenceNumber, DateOnly Date, string FromCollector,
    string ToCollector, string FormType, long BeginningSerial, long EndingSerial, long Quantity,
    TransactionStatus Status, ApprovalStatus ApprovalStatus);

public sealed record SaveReturnRequest(Guid? Id, DateOnly ReturnDate, Guid CollectorId, Guid BookletId,
    long BeginningSerial, long EndingSerial, string PhysicalCondition, string Reason,
    string ReceivedBy, ReturnDisposition Disposition, string? Remarks);
public sealed record ReturnDto(Guid Id, string ReferenceNumber, DateOnly Date, string Collector, string FormType,
    long BeginningSerial, long EndingSerial, long Quantity, ReturnDisposition Disposition,
    TransactionStatus Status, ApprovalStatus ApprovalStatus);

public sealed record SaveExceptionRequest(Guid? Id, DateOnly DateDiscovered, ExceptionType IncidentType,
    Guid CollectorId, Guid BookletId, long BeginningSerial, long EndingSerial, string Details,
    string? SupportingDocumentReference, string ActionTaken, string? Remarks);
public sealed record FormExceptionDto(Guid Id, string ReferenceNumber, DateOnly Date, ExceptionType Type,
    string Collector, string FormType, long BeginningSerial, long EndingSerial, long Quantity,
    TransactionStatus Status, ApprovalStatus ApprovalStatus, ExceptionResolutionStatus ResolutionStatus);

public sealed record AddAttachmentRequest(AttachmentTransactionType TransactionType, Guid TransactionId, string SourcePath);
public sealed record AttachmentDto(Guid Id, string OriginalFileName, string FileType, long FileSize, DateTimeOffset UploadDate);
