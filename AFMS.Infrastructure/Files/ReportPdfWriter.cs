using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace AFMS.Infrastructure.Files;

public sealed class ReportPdfWriter : IReportPdfWriter
{
    private const double Margin = 30;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly XColor Navy = XColor.FromArgb(22, 50, 79);
    private static readonly XColor Gold = XColor.FromArgb(214, 168, 75);
    private static readonly XColor Light = XColor.FromArgb(235, 241, 246);

    public async Task WriteAsync(ReportDocument document, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            using var pdf = new PdfDocument(); pdf.Info.Title = document.Title; pdf.Info.Author = document.OfficeName; pdf.Info.Subject = document.ReportReference;
            var pages = new List<PageState>(); var rows = document.Rows.Count == 0 ? [new ReportRow(["No records found for the selected filters."])] : document.Rows;
            var index = 0;
            while (index < rows.Count)
            {
                cancellationToken.ThrowIfCancellationRequested(); var state = NewPage(pdf, document); pages.Add(state); DrawTableHeader(state, document.Columns);
                while (index < rows.Count && state.Y + 22 < state.Bottom)
                {
                    DrawRow(state, document.Columns, rows[index], index % 2 == 1); state.Y += 20; index++;
                }
            }
            if (pages.Count == 0) pages.Add(NewPage(pdf, document));
            DrawSummaryAndSignatures(pdf, pages, document);
            for (var i = 0; i < pages.Count; i++) DrawFooter(pages[i], document, i + 1, pages.Count);
            pdf.Save(outputPath);
        }
        finally
        {
            WriteGate.Release();
        }
    }

    private static PageState NewPage(PdfDocument pdf, ReportDocument document)
    {
        var page = pdf.AddPage(); page.Size = PdfSharp.PageSize.A4; page.Orientation = document.Columns.Count > 7 ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
        var gfx = XGraphics.FromPdfPage(page); var width = page.Width.Point; var height = page.Height.Point;
        var state = new PageState(page, gfx, 130, height - 95); DrawHeader(state, document, width); return state;
    }

    private static void DrawHeader(PageState s, ReportDocument d, double width)
    {
        var center = XStringFormats.TopCenter; var bold = Font(10, XFontStyleEx.Bold); var normal = Font(9); var title = Font(14, XFontStyleEx.Bold);
        s.Gfx.DrawString("REPUBLIC OF THE PHILIPPINES", bold, XBrushes.Black, new XRect(Margin, 20, width - 2 * Margin, 15), center);
        s.Gfx.DrawString($"PROVINCE OF {d.Province.ToUpperInvariant()}", normal, XBrushes.Black, new XRect(Margin, 35, width - 2 * Margin, 15), center);
        s.Gfx.DrawString($"MUNICIPALITY OF {d.Municipality.ToUpperInvariant()}", normal, XBrushes.Black, new XRect(Margin, 50, width - 2 * Margin, 15), center);
        s.Gfx.DrawString(d.OfficeName.ToUpperInvariant(), bold, XBrushes.Black, new XRect(Margin, 65, width - 2 * Margin, 15), center);
        s.Gfx.DrawLine(new XPen(Gold, 2), Margin, 84, width - Margin, 84);
        s.Gfx.DrawString(d.Title, title, new XSolidBrush(Navy), new XRect(Margin, 91, width - 2 * Margin, 20), center);
        s.Gfx.DrawString($"Period: {d.ReportingPeriod}    |    Report Ref: {d.ReportReference}    |    Generated: {d.GeneratedAt:MMM d, yyyy h:mm tt}", Font(7.5), XBrushes.Black, new XRect(Margin, 113, width - 2 * Margin, 12), center);
    }

    private static void DrawTableHeader(PageState s, IReadOnlyList<ReportColumn> columns)
    {
        if (columns.Count == 0) return; var widths = Widths(s.Page.Width.Point, columns); var x = Margin;
        foreach (var (column, i) in columns.Select((c, i) => (c, i))) { s.Gfx.DrawRectangle(new XSolidBrush(Navy), x, s.Y, widths[i], 22); DrawCell(s.Gfx, column.Header, Font(7, XFontStyleEx.Bold), XBrushes.White, x + 3, s.Y + 5, widths[i] - 6, 14, column.Type); x += widths[i]; }
        s.Y += 22;
    }

    private static void DrawRow(PageState s, IReadOnlyList<ReportColumn> columns, ReportRow row, bool alternate)
    {
        if (columns.Count == 0) { s.Gfx.DrawString(row.Values.FirstOrDefault()?.ToString() ?? "", Font(9), XBrushes.Black, new XRect(Margin + 5, s.Y + 4, s.Page.Width.Point - 2 * Margin - 10, 14), XStringFormats.TopLeft); return; }
        var widths = Widths(s.Page.Width.Point, columns); var x = Margin;
        for (var i = 0; i < columns.Count; i++)
        {
            var brush = alternate ? new XSolidBrush(Light) : XBrushes.White; s.Gfx.DrawRectangle(brush, x, s.Y, widths[i], 20); s.Gfx.DrawRectangle(new XPen(XColors.LightGray, .4), x, s.Y, widths[i], 20);
            DrawCell(s.Gfx, Format(i < row.Values.Count ? row.Values[i] : null, columns[i].Type), Font(6.8), XBrushes.Black, x + 3, s.Y + 4, widths[i] - 6, 13, columns[i].Type); x += widths[i];
        }
    }

    private static void DrawSummaryAndSignatures(PdfDocument pdf, List<PageState> pages, ReportDocument d)
    {
        var s = pages[^1]; var required = 92 + d.Summaries.Count * 16;
        if (s.Y + required > s.Bottom) { s = NewPage(pdf, d); pages.Add(s); }
        s.Y += 8; foreach (var summary in d.Summaries) { s.Gfx.DrawString(summary.Label + ":", Font(8, XFontStyleEx.Bold), XBrushes.Black, new XRect(Margin, s.Y, 180, 14), XStringFormats.TopLeft); s.Gfx.DrawString(summary.Value.ToString(summary.Format), Font(8), XBrushes.Black, new XRect(Margin + 180, s.Y, 100, 14), XStringFormats.TopRight); s.Y += 15; }
        s.Y += 25; var usable = s.Page.Width.Point - 2 * Margin; var third = usable / 3;
        Signature(s.Gfx, d.PreparedBy, "Prepared By", Margin, s.Y, third - 10); Signature(s.Gfx, d.CheckedBy, "Checked By", Margin + third, s.Y, third - 10); Signature(s.Gfx, d.CertifiedCorrectBy, "Certified Correct By", Margin + 2 * third, s.Y, third - 10);
    }

    private static void Signature(XGraphics gfx, string name, string label, double x, double y, double width)
    {
        gfx.DrawLine(new XPen(XColors.Black, .7), x + 15, y + 25, x + width - 15, y + 25); gfx.DrawString(string.IsNullOrWhiteSpace(name) ? " " : name, Font(8, XFontStyleEx.Bold), XBrushes.Black, new XRect(x, y + 10, width, 14), XStringFormats.TopCenter); gfx.DrawString(label, Font(7), XBrushes.Black, new XRect(x, y + 28, width, 12), XStringFormats.TopCenter);
    }

    private static void DrawFooter(PageState s, ReportDocument d, int page, int total)
    {
        var y = s.Page.Height.Point - 43; s.Gfx.DrawLine(new XPen(Navy, .6), Margin, y, s.Page.Width.Point - Margin, y);
        s.Gfx.DrawString($"Generated by {d.GeneratedBy} | AFMS offline reporting", Font(7), XBrushes.Gray, new XRect(Margin, y + 7, 300, 12), XStringFormats.TopLeft);
        s.Gfx.DrawString($"Page {page} of {total}", Font(7, XFontStyleEx.Bold), XBrushes.Black, new XRect(s.Page.Width.Point - 150, y + 7, 120, 12), XStringFormats.TopRight);
    }

    private static double[] Widths(double pageWidth, IReadOnlyList<ReportColumn> columns)
    {
        var available = pageWidth - 2 * Margin; var total = columns.Sum(x => Math.Max(.5, x.Width)); return columns.Select(x => available * Math.Max(.5, x.Width) / total).ToArray();
    }
    private static void DrawCell(XGraphics gfx, string value, XFont font, XBrush brush, double x, double y, double width, double height, ReportValueType type)
    {
        var text = value; while (text.Length > 3 && gfx.MeasureString(text + "...", font).Width > width) text = text[..^1]; if (text != value) text += "...";
        var format = type is ReportValueType.Integer or ReportValueType.Decimal ? XStringFormats.TopRight : XStringFormats.TopLeft; gfx.DrawString(text, font, brush, new XRect(x, y, width, height), format);
    }
    private static string Format(object? value, ReportValueType type) => value switch { null => "", DateOnly d => d.ToString("MMM d, yyyy"), DateTimeOffset d => d.ToString("MMM d, yyyy h:mm tt"), decimal d => d.ToString("N2"), double d => d.ToString("N2"), long l => l.ToString("N0"), int i => i.ToString("N0"), _ => value.ToString() ?? "" };
    private static XFont Font(double size, XFontStyleEx style = XFontStyleEx.Regular) => new("Arial", size, style);
    private sealed class PageState(PdfPage page, XGraphics gfx, double y, double bottom) { public PdfPage Page { get; } = page; public XGraphics Gfx { get; } = gfx; public double Y { get; set; } = y; public double Bottom { get; } = bottom; }
}
