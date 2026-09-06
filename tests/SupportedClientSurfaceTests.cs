using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using Xunit;

namespace QX.Tests;

public sealed class SupportedClientSurfaceTests
{
    static readonly string Repository = FindRepository();

    [Fact]
    public void ClientTypeContainsOnlyFlashAndUnity()
    {
        string path = Path.Combine(Repository, "src", "QX.Wire", "ClientType.cs");
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(File.ReadAllText(path))
            .GetCompilationUnitRoot();
        EnumDeclarationSyntax declaration = Assert.Single(root.DescendantNodes().OfType<EnumDeclarationSyntax>());

        Assert.Equal(
            ["None", "Unity", "Flash", "All"],
            declaration.Members.Select(member => member.Identifier.ValueText).ToArray());
        Assert.Equal(
            ["0", "1", "2", "3"],
            declaration.Members.Select(member => member.EqualsValue?.Value.ToString() ?? "").ToArray());
    }

    [Fact]
    public void ProductionContainsNoRemovedHotelClientSurface()
    {
        Regex removed_client = new(
            @"\bClientType\s*\.\s*(?:Origins|Shockwave|Modern)\b|""SHOCKWAVE""",
            RegexOptions.CultureInvariant);
        string[] violations = Directory
            .EnumerateFiles(Path.Combine(Repository, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => removed_client.IsMatch(File.ReadAllText(path)))
            .Select(Relative)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void WireSurfaceContainsNoRemovedDialectCodec()
    {
        string wire = Path.Combine(Repository, "src", "QX.Wire");
        Assert.False(File.Exists(Path.Combine(wire, "Messages", "B64.cs")));
        Assert.False(File.Exists(Path.Combine(wire, "Messages", "VL64.cs")));

        Regex removed_api = new(
            @"\b(?:Read|Write|Replace)(?:B64|VL64|Content)\b",
            RegexOptions.CultureInvariant);
        string[] violations = Directory
            .EnumerateFiles(wire, "*.cs", SearchOption.AllDirectories)
            .Where(path => removed_api.IsMatch(File.ReadAllText(path)))
            .Select(Relative)
            .ToArray();

        Assert.Empty(violations);
    }

    static string Relative(string path) =>
        Path.GetRelativePath(Repository, path).Replace('\\', '/');

    static string FindRepository()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("QX repository was not found.");
    }
}
