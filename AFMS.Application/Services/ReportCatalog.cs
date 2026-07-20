using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.Application.Services;

public sealed class ReportCatalog : IReportCatalog
{
    public IReadOnlyList<ReportDefinition> Definitions { get; } =
    [
        D(ReportType.StockInventory, ReportCategory.Inventory, "Accountable Forms Stock Inventory", "Complete current stock and accountability."),
        D(ReportType.AvailableForms, ReportCategory.Inventory, "Available Accountable Forms", "Forms available for issuance."),
        D(ReportType.InventoryByFormType, ReportCategory.Inventory, "Inventory by Form Type", "Current quantities summarized by form type."),
        D(ReportType.BeginningInventory, ReportCategory.Inventory, "Beginning Inventory", "Opening inventory before the reporting period."),
        D(ReportType.EndingInventory, ReportCategory.Inventory, "Ending Inventory", "Closing inventory through the reporting period."),
        D(ReportType.YearEndInventory, ReportCategory.Inventory, "Year-End Inventory", "Inventory balance at fiscal year end."),
        D(ReportType.StockMovement, ReportCategory.Inventory, "Stock Movement Report", "All stock movements in the selected period."),
        D(ReportType.InventoryByStorageLocation, ReportCategory.Inventory, "Inventory by Storage Location", "Available inventory grouped by location."),
        D(ReportType.IssuancePerCollector, ReportCategory.Collector, "Issuance per Collector", "Finalized issuances by collector."),
        D(ReportType.CollectorAccountabilityLedger, ReportCategory.Collector, "Collector Accountability Ledger", "Running collector accountability.", true),
        D(ReportType.OutstandingFormsPerCollector, ReportCategory.Collector, "Outstanding Forms per Collector", "Current outstanding accountable forms."),
        D(ReportType.UsedSoldReceipts, ReportCategory.Collector, "Used or Sold Receipt Report", "Finalized receipt usage by collector."),
        D(ReportType.UnusedFormsPerCollector, ReportCategory.Collector, "Unused Forms per Collector", "Unused serial ranges currently held."),
        D(ReportType.CollectorTransactionHistory, ReportCategory.Collector, "Collector Transaction History", "All collector stock movements."),
        D(ReportType.AccountabilityByBarangay, ReportCategory.Collector, "Accountability by Barangay", "Current accountability by barangay."),
        D(ReportType.CollectorYearEndAccountability, ReportCategory.Collector, "Collector Year-End Accountability", "Collector balances at year end."),
        D(ReportType.StockReceivingRegister, ReportCategory.Transaction, "Stock Receiving Register", "Finalized stock receipts."),
        D(ReportType.IssuanceRegister, ReportCategory.Transaction, "Issuance Register", "Finalized and reversed issuances."),
        D(ReportType.UsageRegister, ReportCategory.Transaction, "Usage Register", "Finalized and reversed usage reports."),
        D(ReportType.TransferRegister, ReportCategory.Transaction, "Transfer Register", "Finalized and reversed transfers."),
        D(ReportType.ReturnRegister, ReportCategory.Transaction, "Return Register", "Finalized and reversed returns."),
        D(ReportType.ReversalRegister, ReportCategory.Transaction, "Reversal Register", "All transaction reversals."),
        D(ReportType.SpoiledReceipts, ReportCategory.Exception, "Spoiled Receipts", "Finalized spoiled receipt incidents."),
        D(ReportType.CancelledReceipts, ReportCategory.Exception, "Cancelled Receipts", "Finalized cancelled or invalidated receipts."),
        D(ReportType.LostMissingForms, ReportCategory.Exception, "Lost or Missing Forms", "Lost and missing accountable forms."),
        D(ReportType.DamagedForms, ReportCategory.Exception, "Damaged Forms", "Damaged accountable forms."),
        D(ReportType.SerialNumberGap, ReportCategory.Exception, "Serial Number Gap Report", "Documented and detected serial gaps."),
        D(ReportType.InventoryVariance, ReportCategory.Exception, "Inventory Variance Report", "Booklet totals compared with current range totals."),
        D(ReportType.UnresolvedExceptions, ReportCategory.Exception, "Unresolved Exception Report", "Open and investigated incidents."),
        D(ReportType.AnnualConsolidated, ReportCategory.Consolidated, "Annual Consolidated Accountable Forms Report", "Annual balances and activity by form."),
        D(ReportType.MonthlyIssuanceUsage, ReportCategory.Consolidated, "Monthly Issuance and Usage Summary", "Monthly issue and usage quantities."),
        D(ReportType.InventoryByBarangay, ReportCategory.Consolidated, "Inventory by Barangay", "Current quantities by barangay and form."),
        D(ReportType.IssuedUsedRemaining, ReportCategory.Consolidated, "Issued versus Used versus Remaining", "Accountability activity by form."),
        D(ReportType.BalanceRollForward, ReportCategory.Consolidated, "Beginning Balance, Additions, Usage, Returns and Ending Balance", "Reconciled roll-forward by form."),
        D(ReportType.SummaryByFormType, ReportCategory.Consolidated, "Summary by Form Type", "Current range status totals by form."),
        D(ReportType.SummaryByCollector, ReportCategory.Consolidated, "Summary by Collector", "Current accountability totals by collector."),
        D(ReportType.AuditActivity, ReportCategory.Audit, "Audit Activity Report", "System activity and report/export audit history."),
        D(ReportType.StockReceivingForm, ReportCategory.Transaction, "Stock Receiving Form", "Printable receiving transaction.", requiresReference: true),
        D(ReportType.IssuanceForm, ReportCategory.Transaction, "Accountable Forms Issuance Receipt", "Printable issuance receipt.", requiresReference: true),
        D(ReportType.UsageForm, ReportCategory.Transaction, "Usage Report Form", "Printable usage transaction.", requiresReference: true),
        D(ReportType.TransferForm, ReportCategory.Transaction, "Accountable Forms Transfer Form", "Printable transfer transaction.", requiresReference: true),
        D(ReportType.ReturnForm, ReportCategory.Transaction, "Accountable Forms Return Form", "Printable return transaction.", requiresReference: true),
        D(ReportType.ExceptionForm, ReportCategory.Transaction, "Accountable Forms Exception Report", "Printable exception transaction.", requiresReference: true),
        D(ReportType.ReversalForm, ReportCategory.Transaction, "Transaction Reversal Record", "Printable reversal record.", requiresReference: true)
    ];

    public ReportDefinition Get(ReportType type) => Definitions.Single(x => x.Type == type);
    private static ReportDefinition D(ReportType type, ReportCategory category, string title, string description, bool requiresCollector = false, bool requiresReference = false) => new(type, category, title, description, requiresCollector, requiresReference);
}
