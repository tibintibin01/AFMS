using AFMS.Domain.Common;
using AFMS.Domain.Enums;

namespace AFMS.Domain.Entities;

public sealed class Collector : BaseEntity
{
    public required string Code { get; set; }
    public required string FullName { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? Position { get; set; }
    public string? OfficeOrDepartment { get; set; }
    public string? ContactNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? AppointmentReference { get; set; }
    public string? Remarks { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
}

public sealed class Barangay : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Municipality { get; set; }
    public required string Province { get; set; }
    public string? Remarks { get; set; }
    public ICollection<Collector> Collectors { get; set; } = new List<Collector>();
}

public sealed class AccountableFormType : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? FormNumber { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int DefaultReceiptsPerBooklet { get; set; }
    public string? SerialNumberPrefix { get; set; }
    public int? SerialNumberLength { get; set; }
    public bool RequiresIndividualReceiptTracking { get; set; }
    public string? Remarks { get; set; }
}

public sealed class SystemSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
}

public sealed class FiscalYear : BaseEntity
{
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsClosed { get; set; }
    public FiscalYearWorkflowStatus WorkflowStatus { get; set; } = FiscalYearWorkflowStatus.Open;
    public Guid? CloseRequestedByUserId { get; set; }
    public DateTimeOffset? CloseRequestedAt { get; set; }
    public string? CloseRequestReason { get; set; }
    public string? CloseValidationIssues { get; set; }
    public bool CloseWithDocumentedExceptions { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }
    public Guid? ReopenRequestedByUserId { get; set; }
    public DateTimeOffset? ReopenRequestedAt { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }
    public string? ReopenReason { get; set; }
    public string? SnapshotReference { get; set; }
    public ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
    public ICollection<FiscalYearSnapshot> Snapshots { get; set; } = new List<FiscalYearSnapshot>();
    public ICollection<FiscalYearCarryForward> CarryForwards { get; set; } = new List<FiscalYearCarryForward>();
}

public sealed class FiscalYearSnapshot : BaseEntity
{
    public Guid FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public long BeginningQuantity { get; set; }
    public long Additions { get; set; }
    public long Usage { get; set; }
    public long Exceptions { get; set; }
    public long EndingQuantity { get; set; }
    public long OutstandingQuantity { get; set; }
}

public sealed class FiscalYearCarryForward : BaseEntity
{
    public Guid FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public int TargetYear { get; set; }
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public Guid? CollectorId { get; set; }
    public Collector? Collector { get; set; }
    public AccountableFormStatus Status { get; set; }
}
