using AFMS.Domain.Common;

namespace AFMS.Domain.Entities;

public sealed class Collector : BaseEntity
{
    public required string Code { get; set; }
    public required string FullName { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? Position { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
}

public sealed class Barangay : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<Collector> Collectors { get; set; } = new List<Collector>();
}

public sealed class AccountableFormType : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? FormNumber { get; set; }
    public string? Description { get; set; }
    public int DefaultReceiptsPerBooklet { get; set; }
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
    public ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
}
