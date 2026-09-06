using System.Xml.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QX.Tests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void runtime_source_contains_no_numeric_hotel_header_binding()
    {
        string root = repository();
        Regex[] patterns =
        [
            new(@"new\s+Header\s*\(\s*Direction\.(?:In|Out)\s*,\s*(?:\([^\)]*\)\s*)?(?:0x[0-9a-fA-F]+|\d+)", RegexOptions.CultureInvariant),
            new(@"new\s+(?:Packet|HPacket)\s*\(\s*(?:0x[0-9a-fA-F]+|\d+)", RegexOptions.CultureInvariant),
            new(@"new\s*(?:[A-Za-z_][A-Za-z0-9_<>.]*\s*)?\([^;\r\n]*Direction\.(?:In|Out)[^;\r\n]*\b(?:0x[0-9a-fA-F]+|\d+)\b", RegexOptions.CultureInvariant),
            new(@"\b(?:header|header_id|headerId|HeaderId)\s*(?::|=)\s*(?:\([^\)]*\)\s*)?(?:0x[0-9a-fA-F]+|\d+)\b", RegexOptions.CultureInvariant),
            new(@"\.Id\s+switch\s*\{[\s\S]{0,600}?\b(?:0x[0-9a-fA-F]+|\d+)\s+when", RegexOptions.CultureInvariant)
        ];
        string[] violations = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "obj" or "bin" or "Flazzy"))
            .Where(path => patterns.Any(pattern => pattern.IsMatch(File.ReadAllText(path))))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Empty(violations);
    }

    [Fact]
    public void solution_contains_all_projects_and_production_references_stay_in_src()
    {
        string root = repository();
        string[] projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "obj" or "bin"))
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] listed = XDocument.Load(Path.Combine(root, "QX.slnx"))
            .Descendants("Project")
            .Select(project => Path.GetFullPath(Path.Combine(root, project.Attribute("Path")!.Value)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(projects, listed, StringComparer.OrdinalIgnoreCase);
        string source = Path.Combine(root, "src") + Path.DirectorySeparatorChar;
        foreach (string project in projects.Where(path => path.StartsWith(source, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (XElement reference in XDocument.Load(project).Descendants("ProjectReference"))
            {
                string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference.Attribute("Include")!.Value));
                Assert.StartsWith(source, target, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(target, projects, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static string repository()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
