using AFMS.Domain.Common;
using AFMS.Domain.Enums;

namespace AFMS.Domain.Entities;

public sealed class StockReceipt : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public string? Source { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public int SeriesYear { get; set; }
    public string? StorageLocation { get; set; }
    public string? ReceivedBy { get; set; }
    public Guid EncodedByUserId { get; set; }
    public User EncodedByUser { get; set; } = null!;
    public string? Remarks { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public Guid? FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
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
    public int NumberOfBooklets { get; set; }
    public int ReceiptsPerBooklet { get; set; }
    public ICollection<AccountableFormBooklet> Booklets { get; set; } = new List<AccountableFormBooklet>();
}

public sealed class AccountableFormBooklet : BaseEntity
{
    public Guid StockReceiptItemId { get; set; }
    public StockReceiptItem StockReceiptItem { get; set; } = null!;
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public int SeriesYear { get; set; }
    public int BookletSequence { get; set; }
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long TotalQuantity { get; set; }
    public long UsedQuantity { get; set; }
    public long RemainingQuantity { get; set; }
    public AccountableFormStatus Status { get; set; } = AccountableFormStatus.Available;
    public Guid? CurrentCollectorId { get; set; }
    public Collector? CurrentCollector { get; set; }
    public Guid? CurrentBarangayId { get; set; }
    public Barangay? CurrentBarangay { get; set; }
    public DateOnly DateReceived { get; set; }
    public DateOnly? DateIssued { get; set; }
    public string? StorageLocation { get; set; }
    public string? Remarks { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
    public ICollection<UsageRange> UsageRanges { get; set; } = new List<UsageRange>();
    public ICollection<FormException> Exceptions { get; set; } = new List<FormException>();
    public ICollection<IssuanceItem> IssuanceItems { get; set; } = new List<IssuanceItem>();
    public ICollection<AccountabilityRange> AccountabilityRanges { get; set; } = new List<AccountabilityRange>();
}

/// <summary>Current ownership and disposition of a non-overlapping part of a physical booklet.</summary>
public sealed class AccountabilityRange : BaseEntity
{
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public AccountableFormStatus Status { get; set; }
    public Guid? CurrentCollectorId { get; set; }
    public Collector? CurrentCollector { get; set; }
    public Guid? CurrentBarangayId { get; set; }
    public Barangay? CurrentBarangay { get; set; }
    public string? StorageLocation { get; set; }
    public Guid? SourceIssuanceId { get; set; }
    public Issuance? SourceIssuance { get; set; }
    public string? LastTransactionType { get; set; }
    public Guid? LastTransactionId { get; set; }
    public AccountableFormStatus? PreviousStatus { get; set; }
    public Guid? PreviousCollectorId { get; set; }
    public Guid? PreviousBarangayId { get; set; }
    public string? PreviousStorageLocation { get; set; }
    public string? PreviousTransactionType { get; set; }
    public Guid? PreviousTransactionId { get; set; }
}

public sealed class StockMovement : BaseEntity
{
    public Guid AccountableFormBookletId { get; set; }
    public AccountableFormBooklet AccountableFormBooklet { get; set; } = null!;
    public Guid AccountableFormTypeId { get; set; }
    public AccountableFormType AccountableFormType { get; set; } = null!;
    public StockMovementType MovementType { get; set; }
    public required string ReferenceNumber { get; set; }
    public long BeginningSerial { get; set; }
    public long EndingSerial { get; set; }
    public long Quantity { get; set; }
    /// <summary>
    /// Signed change to the municipality's accountable-form inventory caused by
    /// this movement. Transfers, issuance, and return-to-stock are zero; receipt,
    /// consumption, exception disposition, and reversal movements carry their
    /// actual positive or negative quantity.
    /// </summary>
    public long InventoryQuantityDelta { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public Guid? FromCollectorId { get; set; }
    public Collector? FromCollector { get; set; }
    public Guid? ToCollectorId { get; set; }
    public Collector? ToCollector { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;
    /// <summary>
    /// Official business date of the underlying transaction. This is used for
    /// fiscal-period and report calculations; <see cref="OccurredAt"/> remains
    /// the immutable system posting timestamp.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Remarks { get; set; }
}

public sealed class ReferenceNumberSequence : BaseEntity
{
    public required string Prefix { get; set; }
    public int Year { get; set; }
    public long LastNumber { get; set; }
}
