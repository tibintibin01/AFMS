using System.Globalization;
using System.Text;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.Infrastructure.Files;

public sealed class IssuancePdfWriter : IIssuancePdfWriter
{
    public async Task WriteAsync(IssuanceReceiptDocument document, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("PDF output folder is invalid."));
        var pageLines = document.Lines.Chunk(16).Select(chunk => (IReadOnlyList<IssuanceReceiptLine>)chunk).ToList();
        if (pageLines.Count == 0) pageLines.Add([]);
        var contents = pageLines.Select((lines, index) => BuildPage(document, lines, index + 1, pageLines.Count)).ToList();
        await File.WriteAllBytesAsync(outputPath, BuildPdf(contents), cancellationToken);
    }

    private static string BuildPage(IssuanceReceiptDocument document, IReadOnlyList<IssuanceReceiptLine> lines, int pageNumber, int pageCount)
    {
        var content = new StringBuilder();
        Text(content, 297, 812, "REPUBLIC OF THE PHILIPPINES", 10, true, true);
        Text(content, 297, 797, $"PROVINCE OF {document.Province.ToUpperInvariant()}", 9, false, true);
        Text(content, 297, 783, $"MUNICIPALITY OF {document.Municipality.ToUpperInvariant()}", 9, false, true);
        Text(content, 297, 765, document.OfficeName.ToUpperInvariant(), 13, true, true);
        if (!string.IsNullOrWhiteSpace(document.OfficeAddress)) Text(content, 297, 751, document.OfficeAddress, 8, false, true);
        Line(content, 45, 740, 550, 740, 1.2f);
        Text(content, 297, 719, "ACCOUNTABLE FORMS ISSUANCE RECEIPT", 14, true, true);
        Text(content, 45, 695, $"Reference Number: {document.ReferenceNumber}", 9, true);
        Text(content, 390, 695, $"Date: {document.DateIssued:MMMM dd, yyyy}", 9);
        Text(content, 45, 677, $"Collector: {document.Collector}", 9);
        Text(content, 390, 677, $"Barangay/Area: {document.Barangay}", 9);
        Text(content, 45, 659, $"Purpose: {document.Purpose}", 9);

        const float top = 634;
        const float rowHeight = 22;
        Line(content, 45, top, 550, top, 0.8f); Line(content, 45, top - rowHeight, 550, top - rowHeight, 0.8f);
        foreach (var x in new[] { 45f, 220f, 270f, 325f, 410f, 495f, 550f }) Line(content, x, top, x, top - rowHeight * (lines.Count + 1), 0.5f);
        Text(content, 51, top - 15, "FORM TYPE", 8, true); Text(content, 228, top - 15, "YEAR", 8, true);
        Text(content, 278, top - 15, "BOOKLET", 8, true); Text(content, 333, top - 15, "BEGINNING", 8, true);
        Text(content, 418, top - 15, "ENDING", 8, true); Text(content, 505, top - 15, "QTY", 8, true);
        for (var index = 0; index < lines.Count; index++)
        {
            var yTop = top - rowHeight * (index + 1); var y = yTop - 15; var item = lines[index];
            Line(content, 45, yTop - rowHeight, 550, yTop - rowHeight, 0.4f);
            Text(content, 51, y, Truncate(item.FormType, 29), 8); Text(content, 231, y, item.SeriesYear.ToString(CultureInfo.InvariantCulture), 8);
            Text(content, 288, y, item.BookletNumber.ToString(CultureInfo.InvariantCulture), 8);
            Text(content, 334, y, item.BeginningSerial.ToString("N0", CultureInfo.InvariantCulture), 8);
            Text(content, 419, y, item.EndingSerial.ToString("N0", CultureInfo.InvariantCulture), 8);
            Text(content, 508, y, item.Quantity.ToString("N0", CultureInfo.InvariantCulture), 8);
        }

        var tableBottom = top - rowHeight * (lines.Count + 1);
        Text(content, 45, tableBottom - 22, $"Remarks: {document.Remarks}", 8);
        if (pageNumber == pageCount)
        {
            var signatureY = Math.Min(tableBottom - 82, 205);
            Signature(content, 50, signatureY, document.PreparedBy, "Prepared By");
            Signature(content, 310, signatureY, document.IssuedBy, "Issued By");
            Signature(content, 50, signatureY - 75, document.ApprovedBy, "Approved By");
            Signature(content, 310, signatureY - 75, document.ReceivedBy, "Received By Collector");
        }
        Text(content, 297, 35, $"AFMS - Page {pageNumber} of {pageCount}", 7, false, true);
        return content.ToString();
    }

    private static byte[] BuildPdf(IReadOnlyList<string> pageContents)
    {
        var objects = new SortedDictionary<int, string>();
        var pageNumbers = Enumerable.Range(0, pageContents.Count).Select(index => 5 + index * 2).ToList();
        objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[2] = $"<< /Type /Pages /Kids [{string.Join(' ', pageNumbers.Select(number => $"{number} 0 R"))}] /Count {pageNumbers.Count} >>";
        objects[3] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
        objects[4] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>";
        for (var index = 0; index < pageContents.Count; index++)
        {
            var pageNumber = pageNumbers[index]; var contentNumber = pageNumber + 1;
            var bytes = Encoding.ASCII.GetByteCount(pageContents[index]);
            objects[pageNumber] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentNumber} 0 R >>";
            objects[contentNumber] = $"<< /Length {bytes} >>\nstream\n{pageContents[index]}endstream";
        }

        using var stream = new MemoryStream();
        void Write(string value) { var bytes = Encoding.ASCII.GetBytes(value); stream.Write(bytes); }
        Write("%PDF-1.4\n%AFMS\n");
        var offsets = new Dictionary<int, long>();
        foreach (var (number, value) in objects)
        {
            offsets[number] = stream.Position; Write($"{number} 0 obj\n{value}\nendobj\n");
        }
        var xref = stream.Position; var max = objects.Keys.Max();
        Write($"xref\n0 {max + 1}\n0000000000 65535 f \n");
        for (var number = 1; number <= max; number++) Write($"{offsets[number]:0000000000} 00000 n \n");
        Write($"trailer\n<< /Size {max + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static void Text(StringBuilder content, float x, float y, string? value, float size, bool bold = false, bool centered = false)
    {
        value = ToPdfText(value ?? string.Empty);
        var approximateWidth = value.Length * size * 0.52f;
        if (centered) x -= approximateWidth / 2;
        content.AppendFormat(CultureInfo.InvariantCulture, "BT /{0} {1:0.##} Tf {2:0.##} {3:0.##} Td ({4}) Tj ET\n", bold ? "F2" : "F1", size, x, y, value);
    }

    private static void Line(StringBuilder content, float x1, float y1, float x2, float y2, float width) =>
        content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##} w {1:0.##} {2:0.##} m {3:0.##} {4:0.##} l S\n", width, x1, y1, x2, y2);

    private static void Signature(StringBuilder content, float x, float y, string value, string label)
    {
        Text(content, x + 100, y + 18, value, 9, true, true); Line(content, x, y + 13, x + 200, y + 13, 0.5f); Text(content, x + 100, y, label, 8, false, true);
    }

    private static string ToPdfText(string value) => new string(value.Normalize().Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 3)] + "...";
}
