using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qx.Scripting.Hosting;

internal static class UsingHints
{
    private static readonly Lazy<FrozenDictionary<string, string[]>> type_namespaces = new(Index);

    public static string? For(Diagnostic diagnostic)
    {
        if (MissingType(diagnostic) is not { } name || !type_namespaces.Value.TryGetValue(name, out string[]? namespaces))
            return null;
        if (diagnostic.Id == "CS0234")
            return $"'{name}' is in {string.Join(" or ", namespaces)}";

        string[] usings = [.. namespaces.Select(ApiTypeCatalog.UsingFor).OfType<string>()];
        return usings.Length == 0 ? null : "add " + string.Join(" or ", usings);
    }

    private static string? MissingType(Diagnostic diagnostic) =>
        (diagnostic.Id, diagnostic.Location.SourceTree?.GetRoot().FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)) switch
        {
            ("CS0103", SimpleNameSyntax { Parent: MemberAccessExpressionSyntax access } receiver) when access.Expression == receiver =>
                receiver.Identifier.ValueText,
            ("CS0234" or "CS0246", SimpleNameSyntax type) => type.Identifier.ValueText,
            ("CS0234", MemberAccessExpressionSyntax access) => access.Name.Identifier.ValueText,
            _ => null
        };

    private static FrozenDictionary<string, string[]> Index()
    {
        var namespaces = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var pending = new Stack<INamespaceSymbol>();
        pending.Push(CSharpScript.Create(string.Empty, ScriptEngine.Options, typeof(ScriptGlobals)).GetCompilation().GlobalNamespace);
        while (pending.TryPop(out INamespaceSymbol? current))
        {
            foreach (INamespaceSymbol child in current.GetNamespaceMembers())
                pending.Push(child);
            if (current.IsGlobalNamespace)
                continue;

            string name = current.ToDisplayString();
            foreach (INamedTypeSymbol type in current.GetTypeMembers())
            {
                if (type.DeclaredAccessibility != Accessibility.Public || !type.CanBeReferencedByName)
                    continue;
                if (!namespaces.TryGetValue(type.Name, out SortedSet<string>? declaring))
                    namespaces[type.Name] = declaring = new SortedSet<string>(StringComparer.Ordinal);
                declaring.Add(name);
            }
        }
        return namespaces.ToFrozenDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }
}
