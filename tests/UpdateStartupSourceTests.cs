using Xunit;

namespace QX.Tests;

public sealed class UpdateStartupSourceTests
{
    [Fact]
    public void desktop_checks_without_blocking_and_defers_hidden_notices()
    {
        string root = RepositoryRoot();
        string main = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "MainWindow.xaml.cs"));
        string updates = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "MainWindow.Updates.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "src", "QX.Ui", "UiSettings.cs"));

        Assert.Contains("StartUpdateCheck();", main, StringComparison.Ordinal);
        Assert.Contains("Activated += (_, _) => TryShowUpdateNotice();", main, StringComparison.Ordinal);
        Assert.Contains("Observe(CheckForUpdateAsync);", updates, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource.CreateLinkedTokenSource(_cts.Token)", updates, StringComparison.Ordinal);
        Assert.Contains("!IsVisible || WindowState == WindowState.Minimized", updates, StringComparison.Ordinal);
        Assert.Contains("LastNotifiedRelease", settings, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", updates, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait()", updates, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
