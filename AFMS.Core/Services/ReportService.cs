using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

/// <summary>
/// Turns the recorded data into printable documents: what is on hand, one item's
/// stock card, what went out to a person, and what people still hold.
/// </summary>
public sealed class ReportService(
    InventoryDbContext context,
    ICurrentUser session,
    ISettingsService settings,
    IItemService items,
    IHistoryService history,
    IAccountabilityService accountability,
    IReleaseService releases,
    IAuditService audit,
    IPdfReportWriter pdfWriter,
    IExcelReportWriter excelWriter,
    IApplicationPaths paths) : IReportService
{
    public async Task<ReportDocument> BuildAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = session.Require();
        var office = await settings.GetAsync(cancellationToken);
        var now = DateTimeOffset.Now;
        var reference = $"RPT-{now:yyyyMMdd-HHmmss}";

        return request.Kind switch
        {
            ReportKind.StockOnHand => await BuildStockOnHandAsync(request, office, user, now, reference, cancellationToken),
            ReportKind.StockCard => await BuildStockCardAsync(request, office, user, now, reference, cancellationToken),
            ReportKind.ReleasesByRecipient => await BuildReleasesAsync(request, office, user, now, reference, cancellationToken),
            ReportKind.OutstandingAccountability => await BuildOutstandingAsync(request, office, user, now, reference, cancellationToken),
            ReportKind.InOutSummary => await BuildInOutSummaryAsync(request, office, user, now, reference, cancellationToken),
            _ => throw new ValidationException("That report is not available.")
        };
    }

    // -----------------------------------------------------------------------
    // Report builders
    // -----------------------------------------------------------------------

    private async Task<ReportDocument> BuildStockOnHandAsync(
        ReportRequest request, OfficeSettings office, SignedInUser user,
        DateTimeOffset now, string reference, CancellationToken cancellationToken)
    {
        var all = await items.ListAsync(cancellationToken: cancellationToken);
        var visible = all.Where(item => item.IsActive || item.OnHand != 0).ToList();

        var columns = new[]
        {
            new ReportColumn("Code", 1.0),
            new ReportColumn("Item", 3.0),
            new ReportColumn("Unit", 0.8),
            new ReportColumn("Category", 1.5),
            new ReportColumn("Received", 1.0, ReportValueType.Integer),
            new ReportColumn("Released", 1.0, ReportValueType.Integer),
            new ReportColumn("On hand", 1.0, ReportValueType.Integer),
            new ReportColumn("Status", 1.0)
        };

        var rows = visible
            .Select(item => new ReportRow(
            [
                item.Code, item.Name, item.Unit, item.Category ?? "-",
                item.QuantityIn, item.QuantityOut, item.OnHand,
                item.IsLow ? "LOW STOCK" : item.IsActive ? "OK" : "Hidden"
            ]))
            .ToList();

        return new ReportDocument(
            "STOCK ON HAND",
            $"As of {now:MMMM d, yyyy}",
            reference, office.OfficeName, office.MunicipalityName, office.ProvinceName,
            user.DisplayName, now, office.PreparedBy, office.CertifiedBy,
            columns, rows,
            [
                new ReportTotal("Items listed", visible.Count),
                new ReportTotal("Total pieces on hand", visible.Sum(item => Math.Max(0, item.OnHand))),
                new ReportTotal("Items at or below low-stock level", visible.Count(item => item.IsLow))
            ]);
    }

    private async Task<ReportDocument> BuildStockCardAsync(
        ReportRequest request, OfficeSettings office, SignedInUser user,
        DateTimeOffset now, string reference, CancellationToken cancellationToken)
    {
        if (request.ItemId is not { } itemId)
            throw new ValidationException("Choose an item for the stock card.");

        var item = await items.GetAsync(itemId, cancellationToken)
            ?? throw new ValidationException("That item no longer exists.");
        var card = await history.GetStockCardAsync(itemId, request.FromDate, request.ToDate, cancellationToken);

        var columns = new[]
        {
            new ReportColumn("Date", 1.1, ReportValueType.Date),
            new ReportColumn("Reference", 1.4),
            new ReportColumn("Particulars", 3.2),
            new ReportColumn("Serials", 1.4),
            new ReportColumn("In", 0.8, ReportValueType.Integer),
            new ReportColumn("Out", 0.8, ReportValueType.Integer),
            new ReportColumn("Balance", 1.0, ReportValueType.Integer)
        };

        var rows = card
            .Select(entry => new ReportRow(
            [
                entry.Date, entry.ReferenceNumber, entry.Particulars, entry.SerialLabel,
                entry.QuantityIn == 0 ? null : entry.QuantityIn,
                entry.QuantityOut == 0 ? null : entry.QuantityOut,
                entry.Balance
            ]))
            .ToList();

        return new ReportDocument(
            "STOCK CARD",
            $"{item.Code} - {item.Name} ({item.Unit}){DateRangeLabel(request)}",
            reference, office.OfficeName, office.MunicipalityName, office.ProvinceName,
            user.DisplayName, now, office.PreparedBy, office.CertifiedBy,
            columns, rows,
            [
                new ReportTotal("Total received", card.Sum(entry => entry.QuantityIn)),
                new ReportTotal("Total released", card.Sum(entry => entry.QuantityOut)),
                new ReportTotal("Balance on hand", item.OnHand)
            ]);
    }

    private async Task<ReportDocument> BuildReleasesAsync(
        ReportRequest request, OfficeSettings office, SignedInUser user,
        DateTimeOffset now, string reference, CancellationToken cancellationToken)
    {
        var filter = new HistoryFilter(
            RecipientId: request.RecipientId,
            ItemId: request.ItemId,
            Direction: MovementDirection.Out,
            FromDate: request.FromDate,
            ToDate: request.ToDate);
        var entries = await history.ListAsync(filter, cancellationToken);

        var recipientName = request.RecipientId is { } recipientId
            ? await context.Recipients.Where(person => person.Id == recipientId)
                .Select(person => person.Name).SingleOrDefaultAsync(cancellationToken) ?? "Unknown"
            : "All recipients";

        var columns = new[]
        {
            new ReportColumn("Date", 1.1, ReportValueType.Date),
            new ReportColumn("Reference", 1.4),
            new ReportColumn("Released to", 2.2),
            new ReportColumn("Item", 2.6),
            new ReportColumn("Unit", 0.7),
            new ReportColumn("Qty", 0.8, ReportValueType.Integer),
            new ReportColumn("Serials", 1.4),
            new ReportColumn("Purpose", 2.0)
        };

        var rows = entries
            .Select(entry => new ReportRow(
            [
                entry.Date, entry.ReferenceNumber, entry.PartyName,
                $"{entry.ItemCode} - {entry.ItemName}", entry.Unit,
                entry.Quantity, entry.SerialLabel, entry.Detail
            ]))
            .ToList();

        return new ReportDocument(
            "RELEASES TO RECIPIENTS",
            $"{recipientName}{DateRangeLabel(request)}",
            reference, office.OfficeName, office.MunicipalityName, office.ProvinceName,
            user.DisplayName, now, office.PreparedBy, office.CertifiedBy,
            columns, rows,
            [
                new ReportTotal("Release lines", entries.Count),
                new ReportTotal("Total pieces released", entries.Sum(entry => entry.Quantity))
            ]);
    }

    private async Task<ReportDocument> BuildOutstandingAsync(
        ReportRequest request, OfficeSettings office, SignedInUser user,
        DateTimeOffset now, string reference, CancellationToken cancellationToken)
    {
        var outstanding = await accountability.ListOutstandingAsync(request.RecipientId, cancellationToken);

        var columns = new[]
        {
            new ReportColumn("Person", 2.2),
            new ReportColumn("Position / office", 2.0),
            new ReportColumn("Item", 2.6),
            new ReportColumn("Unit", 0.7),
            new ReportColumn("Released", 0.9, ReportValueType.Integer),
            new ReportColumn("Returned", 0.9, ReportValueType.Integer),
            new ReportColumn("Still held", 0.9, ReportValueType.Integer),
            new ReportColumn("Serials still held", 1.8)
        };

        var rows = new List<ReportRow>();
        foreach (var person in outstanding)
        {
            foreach (var row in person.Items)
            {
                rows.Add(new ReportRow(
                [
                    person.RecipientName,
                    string.Join(" / ", new[] { person.Position, person.Office }
                        .Where(part => !string.IsNullOrWhiteSpace(part))),
                    $"{row.ItemCode} - {row.ItemName}", row.Unit,
                    row.Released, row.Returned, row.Outstanding, row.SerialsHeld
                ]));
            }
        }

        return new ReportDocument(
            "OUTSTANDING ACCOUNTABILITY",
            $"Items still in the hands of recipients as of {now:MMMM d, yyyy}",
            reference, office.OfficeName, office.MunicipalityName, office.ProvinceName,
            user.DisplayName, now, office.PreparedBy, office.CertifiedBy,
            columns, rows,
            [
                new ReportTotal("People holding items", outstanding.Count),
                new ReportTotal("Total pieces still out", outstanding.Sum(person => person.TotalQuantity))
            ]);
    }

    private async Task<ReportDocument> BuildInOutSummaryAsync(
        ReportRequest request, OfficeSettings office, SignedInUser user,
        DateTimeOffset now, string reference, CancellationToken cancellationToken)
    {
        var from = request.FromDate ?? new DateOnly(now.Year, now.Month, 1);
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.Today);

        var entries = await history.ListAsync(
            new HistoryFilter(ItemId: request.ItemId, FromDate: from, ToDate: to), cancellationToken);

        var grouped = entries
            .GroupBy(entry => new { entry.ItemCode, entry.ItemName, entry.Unit })
            .Select(group => new
            {
                group.Key.ItemCode,
                group.Key.ItemName,
                group.Key.Unit,
                In = group.Sum(entry => entry.QuantityIn),
                Out = group.Sum(entry => entry.QuantityOut)
            })
            .OrderBy(row => row.ItemName)
            .ToList();

        var columns = new[]
        {
            new ReportColumn("Code", 1.0),
            new ReportColumn("Item", 3.2),
            new ReportColumn("Unit", 0.8),
            new ReportColumn("Received", 1.1, ReportValueType.Integer),
            new ReportColumn("Released", 1.1, ReportValueType.Integer),
            new ReportColumn("Net change", 1.1, ReportValueType.Integer)
        };

        var rows = grouped
            .Select(row => new ReportRow(
                [row.ItemCode, row.ItemName, row.Unit, row.In, row.Out, row.In - row.Out]))
            .ToList();

        return new ReportDocument(
            "STOCK IN AND OUT SUMMARY",
            $"{from:MMMM d, yyyy} to {to:MMMM d, yyyy}",
            reference, office.OfficeName, office.MunicipalityName, office.ProvinceName,
            user.DisplayName, now, office.PreparedBy, office.CertifiedBy,
            columns, rows,
            [
                new ReportTotal("Items with activity", grouped.Count),
                new ReportTotal("Total received", grouped.Sum(row => row.In)),
                new ReportTotal("Total released", grouped.Sum(row => row.Out))
            ]);
    }

    // -----------------------------------------------------------------------
    // Export
    // -----------------------------------------------------------------------

    public Task<string> ExportPdfAsync(
        ReportDocument document, string? folder = null, CancellationToken cancellationToken = default) =>
        ExportAsync(document, folder, ".pdf", pdfWriter, cancellationToken);

    public Task<string> ExportExcelAsync(
        ReportDocument document, string? folder = null, CancellationToken cancellationToken = default) =>
        ExportAsync(document, folder, ".xlsx", excelWriter, cancellationToken);

    public async Task<string> ExportReleaseSlipAsync(
        Guid releaseId,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        var user = session.Require();
        var release = await releases.GetAsync(releaseId, cancellationToken)
            ?? throw new ValidationException("That release no longer exists.");
        var office = await settings.GetAsync(cancellationToken);
        var now = DateTimeOffset.Now;

        var columns = new[]
        {
            new ReportColumn("Item code", 1.2),
            new ReportColumn("Description", 3.4),
            new ReportColumn("Unit", 0.8),
            new ReportColumn("Quantity", 1.0, ReportValueType.Integer),
            new ReportColumn("Serial numbers", 1.8),
            new ReportColumn("Remarks", 1.8)
        };

        var rows = release.Lines
            .Select(line => new ReportRow(
            [
                line.ItemCode, line.ItemName, line.Unit,
                line.Quantity, line.SerialLabel, line.Remarks ?? string.Empty
            ]))
            .ToList();

        var subtitle =
            $"{release.ReferenceNumber}  |  Released to {release.RecipientName}  |  {release.ReleaseDate:MMMM d, yyyy}" +
            (string.IsNullOrWhiteSpace(release.Purpose) ? string.Empty : $"  |  {release.Purpose}") +
            (release.Status == RecordStatus.Voided ? "  |  *** VOIDED ***" : string.Empty);

        var document = new ReportDocument(
            "INVENTORY RELEASE SLIP",
            subtitle,
            release.ReferenceNumber,
            office.OfficeName, office.MunicipalityName, office.ProvinceName,
            user.DisplayName, now,
            // The slip carries the two names typed on the release itself, since
            // those are the people who actually handed over and received the items.
            release.ReleasedBy, release.ReceivedBy,
            columns, rows,
            [new ReportTotal("Total quantity released", release.TotalQuantity)]);

        return await ExportAsync(document, folder, ".pdf", pdfWriter, cancellationToken);
    }

    private async Task<string> ExportAsync(
        ReportDocument document,
        string? folder,
        string extension,
        IReportWriter writer,
        CancellationToken cancellationToken)
    {
        session.Require();

        var office = await settings.GetAsync(cancellationToken);
        var target = string.IsNullOrWhiteSpace(folder)
            ? (string.IsNullOrWhiteSpace(office.ReportFolder) ? paths.DefaultReportDirectory : office.ReportFolder)
            : folder;
        Directory.CreateDirectory(target);

        var fileName = $"{Slug(document.Title)}-{document.ReportReference}{extension}";
        var path = Path.Combine(target, fileName);

        await writer.WriteAsync(document, path, cancellationToken);

        await audit.RecordAsync(
            ActivityType.ReportExported,
            $"{document.Title} exported as {extension.TrimStart('.').ToUpperInvariant()}.",
            "Report", null, document.ReportReference,
            new { document.Title, File = fileName },
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return path;
    }

    private static string DateRangeLabel(ReportRequest request) =>
        (request.FromDate, request.ToDate) switch
        {
            ({ } from, { } to) => $"  |  {from:MMM d, yyyy} to {to:MMM d, yyyy}",
            ({ } from, null) => $"  |  from {from:MMM d, yyyy}",
            (null, { } to) => $"  |  up to {to:MMM d, yyyy}",
            _ => "  |  complete history"
        };

    private static string Slug(string value)
    {
        var cleaned = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var slug = new string(cleaned);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
