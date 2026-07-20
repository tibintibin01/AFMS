using System.Xml.Linq;

namespace AFMS.Tests.Application;

public sealed class WpfBindingSourceTests
{
    [Fact]
    public void AuditLog_ReadOnlyPagingProperties_UseOneWayBindings()
    {
        var solutionDirectory = FindSolutionDirectory();
        var document = XDocument.Load(Path.Combine(solutionDirectory, "AFMS.App", "Views", "AuditLogView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var runBindings = document.Descendants(presentation + "Run")
            .Select(element => element.Attribute("Text")?.Value)
            .Where(value => value is not null)
            .ToList();

        Assert.Contains("{Binding Page, Mode=OneWay}", runBindings);
        Assert.Contains("{Binding TotalPages, Mode=OneWay}", runBindings);
    }

    [Fact]
    public void FiscalYearEntry_UsesReadableYearAndDateWidths()
    {
        var document = LoadView("MaintenanceView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var year = document.Descendants(presentation + "TextBox")
            .Single(element => element.Attribute("Text")?.Value.Contains("NewYear", StringComparison.Ordinal) == true);
        var dates = document.Descendants(presentation + "DatePicker")
            .Where(element => element.Attribute("SelectedDate")?.Value.Contains("StartDate", StringComparison.Ordinal) == true ||
                              element.Attribute("SelectedDate")?.Value.Contains("EndDate", StringComparison.Ordinal) == true)
            .ToList();

        Assert.True(ReadWidth(year) >= 90);
        Assert.Equal(2, dates.Count);
        Assert.All(dates, date => Assert.True(ReadWidth(date) >= 150));
    }

    [Fact]
    public void InventoryGrid_KeepsHeadersReadable_AndScrollsHorizontally()
    {
        var document = LoadView("InventoryView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dataGrid = document.Descendants(presentation + "DataGrid").Single();
        Assert.Equal("Auto", dataGrid.Attribute("HorizontalScrollBarVisibility")?.Value);

        var requiredWidths = new Dictionary<string, double>
        {
            ["Year"] = 70, ["Booklet"] = 90, ["Beginning serial"] = 110,
            ["Ending serial"] = 105, ["Remaining"] = 90, ["Current holder"] = 140,
            ["Date received"] = 115, ["Date issued"] = 105, ["Storage location"] = 130,
            ["Last movement"] = 150
        };
        var columns = document.Descendants(presentation + "DataGridTextColumn")
            .ToDictionary(element => element.Attribute("Header")?.Value ?? string.Empty);
        foreach (var (header, minimumWidth) in requiredWidths)
            Assert.True(ReadWidth(columns[header]) >= minimumWidth, $"{header} must be at least {minimumWidth} pixels wide.");
    }

    private static XDocument LoadView(string fileName) =>
        XDocument.Load(Path.Combine(FindSolutionDirectory(), "AFMS.App", "Views", fileName));

    private static double ReadWidth(XElement element) =>
        double.TryParse(element.Attribute("Width")?.Value, out var width) ? width : 0;

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFMS.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the AFMS solution directory.");
    }
}
