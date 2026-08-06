using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace AFMS.Core.Files;

/// <summary>
/// Draws a report as a printable A4 PDF with the office header and signature
/// lines the treasury office expects on filed documents.
/// </summary>
public sealed class PdfReportWriter : IPdfReportWriter
{
    private const double Margin = 32;

    // PDFsharp's font resolver and document model are not thread safe.
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private static readonly XColor Navy = XColor.FromArgb(23, 59, 94);
    private static readonly XColor Gold = XColor.FromArgb(217, 168, 62);
    private static readonly XColor Stripe = XColor.FromArgb(238, 243, 248);

    public async Task WriteAsync(
        ReportDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            using var pdf = new PdfDocument();
            pdf.Info.Title = document.Title;
            pdf.Info.Author = document.OfficeName;
            pdf.Info.Subject = document.ReportReference;

            var rows = document.Rows.Count == 0
                ? new List<ReportRow> { new(["No records for the selected filters."]) }
                : document.Rows.ToList();

            var pages = new List<Page>();
            var index = 0;
            while (index < rows.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = StartPage(pdf, document);
                pages.Add(page);
                DrawColumnHeaders(page, document.Columns);

                while (index < rows.Count && page.Y + 20 < page.Bottom)
                {
                    DrawRow(page, document.Columns, rows[index], index % 2 == 1);
                    page.Y += 18;
                    index++;
                }
            }

            if (pages.Count == 0) pages.Add(StartPage(pdf, document));

            DrawTotalsAndSignatures(pdf, pages, document);
            for (var number = 0; number < pages.Count; number++)
                DrawFooter(pages[number], document, number + 1, pages.Count);

            pdf.Save(outputPath);
        }
        finally
        {
            WriteGate.Release();
        }
    }

    private static Page StartPage(PdfDocument pdf, ReportDocument document)
    {
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        page.Orientation = document.Columns.Count > 6
            ? PdfSharp.PageOrientation.Landscape
            : PdfSharp.PageOrientation.Portrait;

        var gfx = XGraphics.FromPdfPage(page);
        var state = new Page(page, gfx, 132, page.Height.Point - 96);
        DrawHeader(state, document);
        return state;
    }

    private static void DrawHeader(Page page, ReportDocument document)
    {
        var width = page.Width;
        var center = XStringFormats.TopCenter;

        page.Gfx.DrawString("REPUBLIC OF THE PHILIPPINES", Font(9.5, XFontStyleEx.Bold), XBrushes.Black,
            new XRect(Margin, 22, width - 2 * Margin, 14), center);

        if (!string.IsNullOrWhiteSpace(document.ProvinceName))
            page.Gfx.DrawString($"PROVINCE OF {document.ProvinceName.ToUpperInvariant()}", Font(8.5), XBrushes.Black,
                new XRect(Margin, 36, width - 2 * Margin, 14), center);

        if (!string.IsNullOrWhiteSpace(document.MunicipalityName))
            page.Gfx.DrawString($"MUNICIPALITY OF {document.MunicipalityName.ToUpperInvariant()}", Font(8.5), XBrushes.Black,
                new XRect(Margin, 50, width - 2 * Margin, 14), center);

        page.Gfx.DrawString(document.OfficeName.ToUpperInvariant(), Font(10, XFontStyleEx.Bold), XBrushes.Black,
            new XRect(Margin, 65, width - 2 * Margin, 14), center);

        page.Gfx.DrawLine(new XPen(Gold, 2), Margin, 84, width - Margin, 84);

        page.Gfx.DrawString(document.Title, Font(14, XFontStyleEx.Bold), new XSolidBrush(Navy),
            new XRect(Margin, 91, width - 2 * Margin, 20), center);

        page.Gfx.DrawString(document.Subtitle, Font(8), XBrushes.Black,
            new XRect(Margin, 111, width - 2 * Margin, 12), center);

        page.Gfx.DrawString(
            $"Ref: {document.ReportReference}    |    Generated {document.GeneratedAt:MMM d, yyyy h:mm tt} by {document.GeneratedBy}",
            Font(7), XBrushes.Gray, new XRect(Margin, 122, width - 2 * Margin, 11), center);
    }

    private static void DrawColumnHeaders(Page page, IReadOnlyList<ReportColumn> columns)
    {
        if (columns.Count == 0) return;

        var widths = ColumnWidths(page.Width, columns);
        var x = Margin;
        for (var index = 0; index < columns.Count; index++)
        {
            page.Gfx.DrawRectangle(new XSolidBrush(Navy), x, page.Y, widths[index], 20);
            DrawCell(page.Gfx, columns[index].Header, Font(7, XFontStyleEx.Bold), XBrushes.White,
                x + 3, page.Y + 5, widths[index] - 6, columns[index].Type);
            x += widths[index];
        }

        page.Y += 20;
    }

    private static void DrawRow(Page page, IReadOnlyList<ReportColumn> columns, ReportRow row, bool stripe)
    {
        if (columns.Count == 0)
        {
            page.Gfx.DrawString(row.Values.FirstOrDefault()?.ToString() ?? string.Empty, Font(9), XBrushes.Black,
                new XRect(Margin + 4, page.Y + 3, page.Width - 2 * Margin - 8, 14), XStringFormats.TopLeft);
            return;
        }

        var widths = ColumnWidths(page.Width, columns);
        var x = Margin;
        for (var index = 0; index < columns.Count; index++)
        {
            page.Gfx.DrawRectangle(stripe ? new XSolidBrush(Stripe) : XBrushes.White, x, page.Y, widths[index], 18);
            page.Gfx.DrawRectangle(new XPen(XColors.LightGray, 0.4), x, page.Y, widths[index], 18);

            var value = index < row.Values.Count ? row.Values[index] : null;
            DrawCell(page.Gfx, FormatValue(value, columns[index].Type), Font(7), XBrushes.Black,
                x + 3, page.Y + 4, widths[index] - 6, columns[index].Type);
            x += widths[index];
        }
    }

    private static void DrawTotalsAndSignatures(PdfDocument pdf, List<Page> pages, ReportDocument document)
    {
        var page = pages[^1];
        var needed = 96 + document.Totals.Count * 15;
        if (page.Y + needed > page.Bottom)
        {
            page = StartPage(pdf, document);
            pages.Add(page);
        }

        page.Y += 10;
        foreach (var total in document.Totals)
        {
            page.Gfx.DrawString($"{total.Label}:", Font(8, XFontStyleEx.Bold), XBrushes.Black,
                new XRect(Margin, page.Y, 220, 13), XStringFormats.TopLeft);
            page.Gfx.DrawString(total.Value.ToString(total.Format), Font(8), XBrushes.Black,
                new XRect(Margin + 220, page.Y, 100, 13), XStringFormats.TopRight);
            page.Y += 14;
        }

        page.Y += 28;
        var usable = page.Width - 2 * Margin;
        var half = usable / 2;
        DrawSignature(page.Gfx, document.PreparedBy, "Prepared by", Margin, page.Y, half - 20);
        DrawSignature(page.Gfx, document.CertifiedBy, "Certified correct by", Margin + half, page.Y, half - 20);
    }

    private static void DrawSignature(XGraphics gfx, string name, string label, double x, double y, double width)
    {
        gfx.DrawString(string.IsNullOrWhiteSpace(name) ? " " : name.ToUpperInvariant(),
            Font(8.5, XFontStyleEx.Bold), XBrushes.Black,
            new XRect(x, y + 8, width, 13), XStringFormats.TopCenter);
        gfx.DrawLine(new XPen(XColors.Black, 0.7), x + 12, y + 24, x + width - 12, y + 24);
        gfx.DrawString(label, Font(7), XBrushes.Black,
            new XRect(x, y + 27, width, 11), XStringFormats.TopCenter);
    }

    private static void DrawFooter(Page page, ReportDocument document, int number, int total)
    {
        var y = page.Height - 44;
        page.Gfx.DrawLine(new XPen(Navy, 0.6), Margin, y, page.Width - Margin, y);
        page.Gfx.DrawString($"{document.ReportReference} | AFMS inventory records", Font(7), XBrushes.Gray,
            new XRect(Margin, y + 6, 320, 11), XStringFormats.TopLeft);
        page.Gfx.DrawString($"Page {number} of {total}", Font(7, XFontStyleEx.Bold), XBrushes.Black,
            new XRect(page.Width - Margin - 120, y + 6, 120, 11), XStringFormats.TopRight);
    }

    private static double[] ColumnWidths(double pageWidth, IReadOnlyList<ReportColumn> columns)
    {
        var available = pageWidth - 2 * Margin;
        var total = columns.Sum(column => Math.Max(0.5, column.Width));
        return columns.Select(column => available * Math.Max(0.5, column.Width) / total).ToArray();
    }

    private static void DrawCell(
        XGraphics gfx, string text, XFont font, XBrush brush,
        double x, double y, double width, ReportValueType type)
    {
        var display = text;
        while (display.Length > 3 && gfx.MeasureString(display + "...", font).Width > width)
            display = display[..^1];
        if (display != text) display += "...";

        var alignment = type is ReportValueType.Integer or ReportValueType.Decimal
            ? XStringFormats.TopRight
            : XStringFormats.TopLeft;
        gfx.DrawString(display, font, brush, new XRect(x, y, width, 13), alignment);
    }

    private static string FormatValue(object? value, ReportValueType type) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("MMM d, yyyy"),
        DateTimeOffset moment => moment.ToString("MMM d, yyyy h:mm tt"),
        decimal number => number.ToString("N2"),
        double number => number.ToString("N2"),
        long number => number.ToString("N0"),
        int number => number.ToString("N0"),
        _ => value.ToString() ?? string.Empty
    };

    private static XFont Font(double size, XFontStyleEx style = XFontStyleEx.Regular) => new("Arial", size, style);

    private sealed class Page(PdfPage page, XGraphics gfx, double y, double bottom)
    {
        public XGraphics Gfx { get; } = gfx;
        public double Y { get; set; } = y;
        public double Bottom { get; } = bottom;
        public double Width { get; } = page.Width.Point;
        public double Height { get; } = page.Height.Point;
    }
}
