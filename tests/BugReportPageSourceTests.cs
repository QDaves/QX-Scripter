using System.Xml.Linq;
using Xunit;

namespace QX.Tests;

public sealed class BugReportPageSourceTests
{
    [Fact]
    public void bug_reports_use_a_main_window_page_without_a_dialog()
    {
        string root = RepositoryRoot();
        string navigation = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "MainWindow.Navigation.cs"));
        string window = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "MainWindow.xaml"));
        string layout = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "BugReportPage.xaml"));
        string page = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "BugReportPage.xaml.cs"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement report_button = XDocument.Parse(window)
            .Descendants(presentation + "ToggleButton")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "ReportBugButton");
        XDocument report_layout = XDocument.Parse(layout);
        XElement diagnostics = report_layout.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "DiagnosticsText");
        XElement include_logs = report_layout.Descendants(presentation + "CheckBox")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "IncludeLogsOption");

        Assert.Contains("GoTo(NavPage.BugReport)", navigation, StringComparison.Ordinal);
        Assert.Contains("NavPage.BugReport => BugReportView", navigation, StringComparison.Ordinal);
        Assert.Contains("<local:BugReportPage x:Name=\"BugReportView\"", window, StringComparison.Ordinal);
        Assert.Contains("BugReport.Collect(_log_path, _crash_log_path, now)", page, StringComparison.Ordinal);
        Assert.Contains("BugReport.CreateIssueUri(", page, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = true", page, StringComparison.Ordinal);
        Assert.Contains("_include_logs_preference ?? true", page, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnIncludeLogsChanged\"", layout, StringComparison.Ordinal);
        Assert.Contains("Property=\"IsKeyboardFocused\"", layout, StringComparison.Ordinal);
        Assert.Equal("{StaticResource SidebarNavToggle}", (string?)report_button.Attribute("Style"));
        Assert.DoesNotContain(report_button.Elements(), element => element.Name.LocalName == "ToggleButton.Style");
        Assert.Same(include_logs.Parent, diagnostics.Parent);
        Assert.DoesNotContain(diagnostics.Parent!.Elements(), element => element.Name == presentation + "Border");
        Assert.Contains("relevant {(logs.Included == 1 ? \"entry\" : \"entries\")} in the last 24 hours.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("selected from", page, StringComparison.Ordinal);
        Assert.DoesNotContain("application log entries found in the last 24 hours.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Access tokens, cookies and local profile paths", layout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "src", "QX.Ui", "BugReportDialog.xaml")));
        Assert.False(File.Exists(Path.Combine(root, "src", "QX.Ui", "BugReportDialog.xaml.cs")));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
