using System.Text.RegularExpressions;
using Xunit;

namespace QX.Tests;

public sealed class VersioningSourceTests
{
    [Fact]
    public void desktop_and_cli_do_not_hardcode_gearth_product_versions()
    {
        string root = RepositoryRoot();
        string[] hosts =
        [
            Path.Combine(root, "src", "QX.Ui", "App.xaml.cs"),
            Path.Combine(root, "src", "QX.Ui", "MainWindow.xaml.cs"),
            Path.Combine(root, "src", "QX.App", "Program.cs"),
            Path.Combine(root, "src", "QX.App", "ApplicationCommands.cs")
        ];
        var literal_version = new Regex(
            "\\bVersion\\s*=\\s*\"[^\"]*\"",
            RegexOptions.CultureInvariant);

        Assert.All(hosts, path =>
            Assert.DoesNotMatch(literal_version, File.ReadAllText(path)));
    }

    [Fact]
    public void every_actions_checkout_fetches_complete_version_history()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            ".github",
            "workflows",
            "build.yml");
        string[] lines = File.ReadAllLines(path);
        int[] checkouts = lines
            .Select((line, index) => new { line, index })
            .Where(entry => entry.line.TrimStart().StartsWith(
                "uses: actions/checkout@",
                StringComparison.Ordinal))
            .Select(entry => entry.index)
            .ToArray();

        Assert.NotEmpty(checkouts);
        Assert.All(checkouts, checkout =>
        {
            int indentation = Indentation(lines[checkout]);
            int end = checkout + 1;
            while (end < lines.Length)
            {
                string candidate = lines[end];
                if (candidate.TrimStart().StartsWith("- ", StringComparison.Ordinal) &&
                    Indentation(candidate) < indentation)
                {
                    break;
                }
                end++;
            }

            Assert.Contains(
                lines[checkout..end],
                line => Regex.IsMatch(
                    line,
                    "^[ \\t]*fetch-depth:[ \\t]*0[ \\t]*$",
                    RegexOptions.CultureInvariant));
        });
    }

    private static int Indentation(string value) =>
        value.TakeWhile(character => character is ' ' or '\t').Count();

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
