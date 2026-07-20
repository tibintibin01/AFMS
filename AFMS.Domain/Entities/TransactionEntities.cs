using AFMS.Domain.Common;
using AFMS.Domain.Enums;

namespace AFMS.Domain.Entities;

public interface IReversibleTransaction
{
    Guid Id { get; }
    string ReferenceNumber { get; }
    Guid EncodedByUserId { get; }
    TransactionStatus Status { get; set; }
}

public interface IApprovalWorkflowTransaction : IReversibleTransaction
{
    Guid? SubmittedByUserId { get; set; }
    DateTimeOffset? SubmittedAt { get; set; }
    Guid? ApprovedByUserId { get; set; }
    DateTimeOffset? ApprovedAt { get; set; }
    Guid? RejectedByUserId { get; set; }
    DateTimeOffset? DecisionAt { get; set; }
    string? DecisionComments { get; set; }
    ApprovalStatus ApprovalStatus { get; set; }
}

public sealed class Issuance : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly IssuedDate { get; set; }
    public Guid CollectorId { get; set; }
    public Collector Collector { get; set; } = null!;
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public string? CollectionArea { get; set; }
    public required string Purpose { get; set; }
    public required string ReleasedBy { get; set; }
    public required string ReceivedBy { get; set; }
    public Guid IssuedByUserId { get; set; }
    public User IssuedByUser { get; set; } = null!;
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? Remarks { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionComments { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
    public string? RejectionReason { get; set; }
    public ICollection<IssuanceItem> Items { get; set; } = new List<IssuanceItem>();
}

public sealed class IssuanceItem : BaseEntity
{
    public Guid IssuanceId { get; set; }
    public Issuance Issuance { get; set; } = null!;
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public int SeriesYear { get; set; }
    public bool IsReservationActive { get; set; } = true;
}

public sealed class UsageReport : BaseEntity, IReversibleTransaction
{
    public required string ReferenceNumber { get; set; }
    public Guid CollectorId { get; set; }
    public Collector Collector { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly SubmittedDate { get; set; }
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public DateOnly ReportingDate { get; set; }
    public decimal? AmountCollected { get; set; }
    public string? SourceReportNumber { get; set; }
    public Guid EncodedByUserId { get; set; }
    public User EncodedByUser { get; set; } = null!;
    public string? Remarks { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public ICollection<UsageRange> Ranges { get; set; } = new List<UsageRange>();
}

public sealed class UsageRange : BaseEntity
{
    public Guid UsageReportId { get; set; }
    public UsageReport UsageReport { get; set; } = null!;
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public string? GapExplanation { get; set; }
}

public sealed class Transfer : BaseEntity, IApprovalWorkflowTransaction
{
    public required string ReferenceNumber { get; set; }
    public DateOnly TransferDate { get; set; }
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public Guid FromCollectorId { get; set; }
    public Collector FromCollector { get; set; } = null!;
    public Guid ToCollectorId { get; set; }
    public Collector ToCollector { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public required string Reason { get; set; }
    public required string ReleasedBy { get; set; }
    public required string ReceivedBy { get; set; }
    public Guid EncodedByUserId { get; set; }
    public User EncodedByUser { get; set; } = null!;
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionComments { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? Remarks { get; set; }
}

public sealed class ReturnTransaction : BaseEntity, IApprovalWorkflowTransaction
{
    public required string ReferenceNumber { get; set; }
    public DateOnly ReturnDate { get; set; }
    public Guid CollectorId { get; set; }
    public Collector Collector { get; set; } = null!;
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public Guid? OriginalIssuanceId { get; set; }
    public Issuance? OriginalIssuance { get; set; }
    public required string PhysicalCondition { get; set; }
    public ReturnDisposition Disposition { get; set; }
    public required string ReceivedBy { get; set; }
    public Guid EncodedByUserId { get; set; }
    public User EncodedByUser { get; set; } = null!;
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionComments { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public required string Reason { get; set; }
    public string? Remarks { get; set; }
}

public sealed class FormException : BaseEntity, IApprovalWorkflowTransaction
{
    public required string ReferenceNumber { get; set; }
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public Guid? CollectorId { get; set; }
    public Collector? Collector { get; set; }
    public long SerialNumber { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public ExceptionType ExceptionType { get; set; }
    public required string Details { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly DateDiscovered { get; set; }
    public string? SupportingDocumentReference { get; set; }
    public required string ActionTaken { get; set; }
    public ExceptionResolutionStatus ResolutionStatus { get; set; } = ExceptionResolutionStatus.Open;
    public Guid EncodedByUserId { get; set; }
    public User EncodedByUser { get; set; } = null!;
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionComments { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
    public string? Remarks { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
}

public sealed class TransactionAttachment : BaseEntity
{
    public required string OriginalFileName { get; set; }
    public required string StoredFileName { get; set; }
    public required string FileType { get; set; }
    public long FileSize { get; set; }
    public DateTimeOffset UploadDate { get; set; } = DateTimeOffset.UtcNow;
    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public AttachmentTransactionType RelatedTransactionType { get; set; }
    public Guid RelatedTransactionId { get; set; }
}
