using AFMS.Domain.Enums;

namespace AFMS.Application.Models;

public enum ReportCategory { Inventory = 1, Collector = 2, Transaction = 3, Exception = 4, Consolidated = 5, Audit = 6 }

public enum ReportType
{
    StockInventory, AvailableForms, InventoryByFormType, BeginningInventory, EndingInventory, YearEndInventory, StockMovement, InventoryByStorageLocation,
    IssuancePerCollector, CollectorAccountabilityLedger, OutstandingFormsPerCollector, UsedSoldReceipts, UnusedFormsPerCollector, CollectorTransactionHistory, AccountabilityByBarangay, CollectorYearEndAccountability,
    StockReceivingRegister, IssuanceRegister, UsageRegister, TransferRegister, ReturnRegister, ReversalRegister,
    SpoiledReceipts, CancelledReceipts, LostMissingForms, DamagedForms, SerialNumberGap, InventoryVariance, UnresolvedExceptions,
    AnnualConsolidated, MonthlyIssuanceUsage, InventoryByBarangay, IssuedUsedRemaining, BalanceRollForward, SummaryByFormType, SummaryByCollector,
    AuditActivity,
    StockReceivingForm, IssuanceForm, UsageForm, TransferForm, ReturnForm, ExceptionForm, ReversalForm
}

public enum ReportValueType { Text, Integer, Decimal, Date, DateTime, Status }

public sealed record ReportDefinition(ReportType Type, ReportCategory Category, string Title, string Description, bool RequiresCollector = false, bool RequiresReference = false)
{
    public string Display => Title;
}

public sealed record ReportFilter(DateOnly? FromDate = null, DateOnly? ToDate = null, int? FiscalYear = null,
    Guid? FormTypeId = null, Guid? CollectorId = null, Guid? BarangayId = null,
    AccountableFormStatus? Status = null, string? ReferenceNumber = null,
    bool OutstandingOnly = false, bool IncludeDrafts = false, TransactionStatus? TransactionStatus = null);

public sealed record ReportColumn(string Key, string Header, ReportValueType Type = ReportValueType.Text, double Width = 1);
public sealed record ReportRow(IReadOnlyList<object?> Values);
public sealed record ReportSummary(string Label, decimal Value, string Format = "N0");

public sealed record ReportDocument(
    ReportType Type, string Title, string ReportReference, string ReportingPeriod,
    DateTimeOffset GeneratedAt, string GeneratedBy, string Province, string Municipality,
    string OfficeName, string OfficeAddress, string PreparedBy, string CheckedBy, string CertifiedCorrectBy,
    IReadOnlyList<ReportColumn> Columns, IReadOnlyList<ReportRow> Rows, IReadOnlyList<ReportSummary> Summaries,
    ReportFilter Filter)
{
    public bool IsEmpty => Rows.Count == 0;
}

public sealed record ReportGenerationResult(ReportDocument Document, string? OutputPath = null);
