using AFMS.Application.Models;
using AFMS.Domain.Entities;

namespace AFMS.Application.Abstractions;

public sealed record ReportDataSnapshot(
    IReadOnlyList<AccountableFormBooklet> Booklets,
    IReadOnlyList<AccountabilityRange> AccountabilityRanges,
    IReadOnlyList<StockMovement> Movements,
    IReadOnlyList<StockReceipt> StockReceipts,
    IReadOnlyList<Issuance> Issuances,
    IReadOnlyList<UsageReport> UsageReports,
    IReadOnlyList<Transfer> Transfers,
    IReadOnlyList<ReturnTransaction> Returns,
    IReadOnlyList<FormException> Exceptions,
    IReadOnlyList<Collector> Collectors,
    IReadOnlyList<AuditLog> AuditLogs);

public interface IReportDataRepository
{
    Task<ReportDataSnapshot> LoadAsync(ReportFilter filter, CancellationToken cancellationToken = default);
}

public interface IReportCatalog
{
    IReadOnlyList<ReportDefinition> Definitions { get; }
    ReportDefinition Get(ReportType type);
}

public interface IReportQueryService
{
    Task<ReportDocument> GenerateAsync(ReportType type, ReportFilter filter, CancellationToken cancellationToken = default);
}

public interface IReportPdfWriter
{
    Task WriteAsync(ReportDocument document, string outputPath, CancellationToken cancellationToken = default);
}

public interface IReportExcelWriter
{
    Task WriteAsync(ReportDocument document, string outputPath, CancellationToken cancellationToken = default);
}

public interface IReportCenterService
{
    IReadOnlyList<ReportDefinition> GetCatalog();
    Task<ReportDocument> PreviewAsync(ReportType type, ReportFilter filter, CancellationToken cancellationToken = default);
    Task<string> ExportPdfAsync(ReportType type, ReportFilter filter, string? outputFolder = null, CancellationToken cancellationToken = default);
    Task<string> ExportExcelAsync(ReportType type, ReportFilter filter, string? outputFolder = null, CancellationToken cancellationToken = default);
    Task RecordPrintAsync(ReportDocument document, CancellationToken cancellationToken = default);
}
