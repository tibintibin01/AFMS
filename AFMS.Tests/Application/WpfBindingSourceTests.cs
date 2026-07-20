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

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFMS.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the AFMS solution directory.");
    }
}
