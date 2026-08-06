using ClosedXML.Excel;

namespace AFMS.Core.Files;

/// <summary>Writes the same report content as a filterable Excel sheet.</summary>
public sealed class ExcelReportWriter : IExcelReportWriter
{
    private const int HeaderRow = 8;

    public Task WriteAsync(
        ReportDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(document.Title));
        var columnCount = Math.Max(1, document.Columns.Count);

        sheet.Cell(1, 1).Value = document.OfficeName;
        sheet.Cell(2, 1).Value = string.Join(", ",
            new[] { document.MunicipalityName, document.ProvinceName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        sheet.Cell(3, 1).Value = document.Title;
        sheet.Cell(4, 1).Value = document.Subtitle;
        sheet.Cell(5, 1).Value =
            $"Ref: {document.ReportReference} | Generated {document.GeneratedAt.LocalDateTime:MMM d, yyyy h:mm tt} by {document.GeneratedBy}";

        for (var row = 1; row <= 5; row++)
        {
            sheet.Range(row, 1, row, columnCount).Merge();
            sheet.Range(row, 1, row, columnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Style.Font.FontSize = 14;
        sheet.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#173B5E");

        for (var index = 0; index < document.Columns.Count; index++)
            sheet.Cell(HeaderRow, index + 1).Value = document.Columns[index].Header;

        var header = sheet.Range(HeaderRow, 1, HeaderRow, columnCount);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#173B5E");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (var rowIndex = 0; rowIndex < document.Rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var columnIndex = 0; columnIndex < document.Columns.Count; columnIndex++)
            {
                var cell = sheet.Cell(HeaderRow + 1 + rowIndex, columnIndex + 1);
                var values = document.Rows[rowIndex].Values;
                SetValue(cell, columnIndex < values.Count ? values[columnIndex] : null);
                ApplyFormat(cell, document.Columns[columnIndex].Type);
            }
        }

        var lastRow = HeaderRow + document.Rows.Count;
        if (document.Rows.Count > 0 && document.Columns.Count > 0)
            sheet.Range(HeaderRow, 1, lastRow, document.Columns.Count).SetAutoFilter();
        sheet.SheetView.FreezeRows(HeaderRow);

        var totalsRow = lastRow + 2;
        foreach (var total in document.Totals)
        {
            sheet.Cell(totalsRow, 1).Value = total.Label;
            sheet.Cell(totalsRow, 1).Style.Font.Bold = true;
            sheet.Cell(totalsRow, 2).Value = total.Value;
            sheet.Cell(totalsRow, 2).Style.NumberFormat.Format = total.Format;
            totalsRow++;
        }

        totalsRow += 2;
        sheet.Cell(totalsRow, 1).Value = "Prepared by";
        sheet.Cell(totalsRow, 2).Value = document.PreparedBy;
        sheet.Cell(totalsRow + 1, 1).Value = "Certified correct by";
        sheet.Cell(totalsRow + 1, 2).Value = document.CertifiedBy;

        AutoSize(sheet, document);
        sheet.PageSetup.PageOrientation = document.Columns.Count > 6
            ? XLPageOrientation.Landscape
            : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Footer.Center.AddText($"{document.ReportReference} | Page &[Page] of &[Pages]");

        workbook.Properties.Title = document.Title;
        workbook.Properties.Author = document.OfficeName;
        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = string.Empty; break;
            case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); break;
            case DateTimeOffset moment: cell.Value = moment.LocalDateTime; break;
            case long number: cell.Value = number; break;
            case int number: cell.Value = number; break;
            case decimal number: cell.Value = number; break;
            case double number: cell.Value = number; break;
            case bool flag: cell.Value = flag; break;
            default: cell.Value = value.ToString() ?? string.Empty; break;
        }
    }

    private static void ApplyFormat(IXLCell cell, ReportValueType type)
    {
        switch (type)
        {
            case ReportValueType.Date: cell.Style.DateFormat.Format = "mmm d, yyyy"; break;
            case ReportValueType.DateTime: cell.Style.DateFormat.Format = "mmm d, yyyy h:mm AM/PM"; break;
            case ReportValueType.Integer: cell.Style.NumberFormat.Format = "#,##0"; break;
            case ReportValueType.Decimal: cell.Style.NumberFormat.Format = "#,##0.00"; break;
        }
    }

    private static void AutoSize(IXLWorksheet sheet, ReportDocument document)
    {
        for (var index = 0; index < document.Columns.Count; index++)
        {
            var width = document.Columns[index].Header.Length;
            foreach (var row in document.Rows)
            {
                var value = index < row.Values.Count ? row.Values[index] : null;
                width = Math.Max(width, value?.ToString()?.Length ?? 0);
            }

            sheet.Column(index + 1).Width = Math.Clamp(width + 2, 10, 46);
        }

        if (document.Columns.Count < 2) sheet.Column(2).Width = 26;
    }

    private static string SheetName(string value)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = string.Concat(value.Select(character => invalid.Contains(character) ? '-' : character));
        return clean.Length <= 31 ? clean : clean[..31];
    }
}
