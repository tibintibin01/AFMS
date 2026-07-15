using AFMS.Domain.Common;
using AFMS.Domain.Enums;

namespace AFMS.Domain.Entities;

public sealed class Issuance : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly IssuedDate { get; set; }
    public Guid CollectorId { get; set; }
    public Collector Collector { get; set; } = null!;
    public Guid IssuedByUserId { get; set; }
    public User IssuedByUser { get; set; } = null!;
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? Remarks { get; set; }
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
}

public sealed class UsageReport : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public Guid CollectorId { get; set; }
    public Collector Collector { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly SubmittedDate { get; set; }
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
}

public sealed class Transfer : BaseEntity
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
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? Remarks { get; set; }
}

public sealed class ReturnTransaction : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly ReturnDate { get; set; }
    public Guid CollectorId { get; set; }
    public Collector Collector { get; set; } = null!;
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? Reason { get; set; }
}

public sealed class FormException : BaseEntity
{
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public Guid? CollectorId { get; set; }
    public Collector? Collector { get; set; }
    public long SerialNumber { get; set; }
    public ExceptionType ExceptionType { get; set; }
    public required string Details { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
}
