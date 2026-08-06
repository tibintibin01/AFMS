namespace AFMS.Core.Domain;

/// <summary>Fields shared by every stored record.</summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Left unset on purpose. Entity Framework fills this in when the record is
    /// added; pre-filling it would make EF mistake a brand new child row for an
    /// existing one and issue an UPDATE that matches nothing.
    /// </summary>
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A person who can sign in to the app.</summary>
public sealed class User : BaseEntity
{
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public bool MustChangePassword { get; set; }
    public int FailedSignInCount { get; set; }
    public DateTimeOffset? LockedOutUntil { get; set; }
    public DateTimeOffset? LastSignInAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Anything tracked in the inventory: a ream of bond paper, a box of ballpens,
/// or an accountable form booklet such as AF51.
/// </summary>
public sealed class Item : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>How the item is counted: pcs, box, ream, booklet.</summary>
    public required string Unit { get; set; }

    public string? Category { get; set; }

    /// <summary>
    /// True for accountable forms and anything else with printed serial numbers.
    /// When set, stock-in and release lines can carry a serial range and the app
    /// blocks releasing the same serial twice.
    /// </summary>
    public bool TracksSerials { get; set; }

    /// <summary>Warn on the dashboard when quantity on hand drops to this level or below. Zero disables the warning.</summary>
    public long ReorderLevel { get; set; }

    public string? Remarks { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A person items are released to.</summary>
public sealed class Recipient : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Position { get; set; }
    public string? Office { get; set; }
    public string? ContactNumber { get; set; }
    public string? Remarks { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One incoming delivery, transfer, return, beginning balance, or adjustment.</summary>
public sealed class StockIn : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly EntryDate { get; set; }
    public StockInSource Source { get; set; } = StockInSource.Purchase;

    /// <summary>Supplier or supplying office. Blank for beginning balances and adjustments.</summary>
    public string? SourceName { get; set; }

    /// <summary>Delivery receipt, purchase order, invoice, or memo number.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>
    /// Who handed the stock back. Required when <see cref="Source"/> is
    /// <see cref="StockInSource.Returned"/>; this is what clears their accountability.
    /// </summary>
    public Guid? ReturnedByRecipientId { get; set; }
    public Recipient? ReturnedByRecipient { get; set; }

    public string? ReceivedBy { get; set; }
    public string? Remarks { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Recorded;
    public string? VoidReason { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }

    public Guid RecordedByUserId { get; set; }
    public User RecordedByUser { get; set; } = null!;

    public ICollection<StockInLine> Lines { get; set; } = new List<StockInLine>();
}

/// <summary>One item and quantity on an incoming entry.</summary>
public sealed class StockInLine : BaseEntity
{
    public Guid StockInId { get; set; }
    public StockIn StockIn { get; set; } = null!;

    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public long Quantity { get; set; }

    /// <summary>Set together with <see cref="SerialTo"/> for serial-tracked items.</summary>
    public long? SerialFrom { get; set; }
    public long? SerialTo { get; set; }

    public string? Remarks { get; set; }

    /// <summary>The serial range for this line, or null when the item is not serial tracked.</summary>
    public SerialRange? Serials =>
        SerialFrom is { } from && SerialTo is { } to ? new SerialRange(from, to) : null;
}

/// <summary>One outgoing release to a person.</summary>
public sealed class Release : BaseEntity
{
    public required string ReferenceNumber { get; set; }
    public DateOnly ReleaseDate { get; set; }

    public Guid RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;

    public string? Purpose { get; set; }

    /// <summary>Name printed on the "released by" signature line.</summary>
    public required string ReleasedBy { get; set; }

    /// <summary>Name printed on the "received by" signature line.</summary>
    public required string ReceivedBy { get; set; }

    public string? Remarks { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Recorded;
    public string? VoidReason { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }

    public Guid RecordedByUserId { get; set; }
    public User RecordedByUser { get; set; } = null!;

    public ICollection<ReleaseLine> Lines { get; set; } = new List<ReleaseLine>();
}

/// <summary>One item and quantity on a release.</summary>
public sealed class ReleaseLine : BaseEntity
{
    public Guid ReleaseId { get; set; }
    public Release Release { get; set; } = null!;

    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public long Quantity { get; set; }

    public long? SerialFrom { get; set; }
    public long? SerialTo { get; set; }

    public string? Remarks { get; set; }

    public SerialRange? Serials =>
        SerialFrom is { } from && SerialTo is { } to ? new SerialRange(from, to) : null;
}

/// <summary>
/// Append-only record of what happened and when. Enforced by database triggers,
/// so entries cannot be edited or deleted even outside the app.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UserId { get; set; }

    /// <summary>Stored alongside <see cref="UserId"/> so history stays readable if an account is renamed.</summary>
    public string? Username { get; set; }

    public ActivityType Activity { get; set; }
    public required string Description { get; set; }
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? ReferenceNumber { get; set; }

    /// <summary>Optional JSON snapshot of what changed.</summary>
    public string? Details { get; set; }

    public string? MachineName { get; set; }
}

/// <summary>Editable configuration such as office name and signatories.</summary>
public sealed class SystemSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
}

/// <summary>Backs the automatic reference numbers, one counter per prefix and year.</summary>
public sealed class ReferenceSequence : BaseEntity
{
    public required string Prefix { get; set; }
    public int Year { get; set; }
    public long LastNumber { get; set; }
}
