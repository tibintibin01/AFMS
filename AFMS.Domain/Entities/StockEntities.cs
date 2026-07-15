using AFMS.Domain.Common;
using AFMS.Domain.Enums;

namespace AFMS.Domain.Entities;

public sealed class StockReceipt : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public string? Source { get; set; }
    public string? Remarks { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public Guid FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public ICollection<StockReceiptItem> Items { get; set; } = new List<StockReceiptItem>();
}

public sealed class StockReceiptItem : BaseEntity
{
    public Guid StockReceiptId { get; set; }
    public StockReceipt StockReceipt { get; set; } = null!;
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    public ICollection<AccountableFormBooklet> Booklets { get; set; } = new List<AccountableFormBooklet>();
}

public sealed class AccountableFormBooklet : BaseEntity
{
    public Guid StockReceiptItemId { get; set; }
    public StockReceiptItem StockReceiptItem { get; set; } = null!;
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public AccountableFormStatus Status { get; set; } = AccountableFormStatus.Available;
    public Guid? CurrentCollectorId { get; set; }
    public Collector? CurrentCollector { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}

public sealed class StockMovement : BaseEntity
{
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public StockMovementType MovementType { get; set; }
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public Guid? FromCollectorId { get; set; }
    public Collector? FromCollector { get; set; }
    public Guid? ToCollectorId { get; set; }
    public Collector? ToCollector { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Remarks { get; set; }
}
