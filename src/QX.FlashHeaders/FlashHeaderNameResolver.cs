using System.Text.RegularExpressions;
using Flazzy.ABC;

namespace Qx.Headers.Flash;

public sealed partial class FlashHeaderNameResolver
{
    readonly Avm2CallTargetResolver _types;
    readonly SignatureDatabase? _database;

    public FlashHeaderNameResolver(SwfInfo swf, SignatureDatabase? database = null)
    {
        _database = database;
        _types = new Avm2CallTargetResolver(
            swf.DeclaringScopes,
            swf.AuthenticatedHarmanTransform);
    }

    public void Apply(FlashHeaderMap map, bool overwrite = true)
    {
        using IDisposable identities =
            Avm2MethodAnalyzer.CacheRuntimeIdentities();
        foreach (FlashHeaderDefinition definition in map.Incoming)
            Resolve(definition, incoming: true, overwrite);
        foreach (FlashHeaderDefinition definition in map.Outgoing)
            Resolve(definition, incoming: false, overwrite);
    }

    void Resolve(FlashHeaderDefinition definition, bool incoming, bool overwrite)
    {
        ASInstance? instance = definition.TypeDefinitions.Count == 1
            ? definition.TypeDefinitions[0].Instance
            : null;
        definition.Signature = instance is null
            ? null
            : incoming
                ? StructuralSignature.ForIncoming(instance, _types)
                : StructuralSignature.ForOutgoing(instance);

        if (!overwrite && !string.IsNullOrEmpty(definition.Name))
            return;

        if (!IsObfuscated(definition.Class))
        {
            definition.Name = Strip(definition.Class);
            definition.NameSource = NameSource.ClassName;
            return;
        }

        string? own = PrivateName(instance);
        if (own != null)
        {
            definition.Name = Strip(own);
            definition.NameSource = NameSource.PrivateNamespace;
            return;
        }

        string? constructor = ConstructorName(instance, incoming);
        if (constructor != null)
        {
            if (incoming)
            {
                string? parser_name = PrivateName(ParserOf(instance));
                if (parser_name != null)
                {
                    parser_name = Strip(parser_name);
                    if (!parser_name.Equals(constructor, StringComparison.OrdinalIgnoreCase))
                        definition.SemanticAliases = [parser_name];
                }
            }
            definition.Name = constructor;
            definition.NameSource = NameSource.ConstructorName;
            return;
        }

        if (incoming)
        {
            string? viaParser = PrivateName(ParserOf(instance));
            if (viaParser != null)
            {
                definition.Name = Strip(viaParser);
                definition.NameSource = NameSource.ParserNamespace;
                return;
            }
        }

        if (_database != null &&
            _database.TryResolve(ClassSignature(definition), out string? class_name))
        {
            definition.Name = class_name;
            definition.NameSource = NameSource.ClassSignature;
            return;
        }

        if (_database != null && definition.Signature is not null &&
            _database.TryResolve(definition.Signature, out string? hashed))
        {
            definition.Name = hashed;
            definition.NameSource = NameSource.StructureHash;
        }
    }

    public static string ClassSignature(FlashHeaderDefinition definition)
    {
        string direction = definition.Direction == MessageDirection.Incoming ? "in" : "out";
        return $"class3:{direction}:{definition.Qualified}";
    }

    public string? Signature(FlashHeaderDefinition d, bool incoming)
    {
        using IDisposable identities =
            Avm2MethodAnalyzer.CacheRuntimeIdentities();
        ASInstance? inst = d.TypeDefinitions.Count == 1
            ? d.TypeDefinitions[0].Instance
            : null;
        if (inst == null) return null;
        return incoming
            ? StructuralSignature.ForIncoming(inst, _types)
            : StructuralSignature.ForOutgoing(inst);
    }

    ASInstance? ParserOf(ASInstance? evt) =>
        evt is null ? null : ParserBindingResolver.Resolve(evt, _types);

    static string? PrivateName(ASInstance? inst)
    {
        if (inst == null) return null;
        foreach (ASTrait trait in inst.Traits)
        {
            ASNamespace? ns = trait.QName?.Namespace;
            if (ns?.Kind != NamespaceKind.Private ||
                ns.NameIndex < 0 ||
                ns.NameIndex >= ns.Pool.Strings.Count)
            {
                continue;
            }
            string? tail = Tail(ns.RuntimeName);
            if (!IsObfuscated(tail)) return tail;
        }
        return null;
    }

    static string? ConstructorName(ASInstance? instance, bool incoming)
    {
        if (instance is null)
            return null;
        string? name;
        try
        {
            name = instance.Constructor?.Name;
        }
        catch
        {
            return null;
        }
        return ConstructorName(name, incoming);
    }

    static string? ConstructorName(string? name, bool incoming)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        string suffix = incoming ? "MessageEvent" : "MessageComposer";
        if (!name.EndsWith(suffix, StringComparison.Ordinal))
            return null;
        string semantic_name = Strip(name);
        return IsObfuscated(semantic_name) ? null : semantic_name;
    }

    static string? Tail(string? fqn)
    {
        if (string.IsNullOrEmpty(fqn)) return null;
        Match m = TrailingIdentifier().Match(fqn);
        return m.Success ? m.Value : null;
    }

    public static string Strip(string name)
    {
        string previous;
        do
        {
            previous = name;
            name = SuffixPattern().Replace(name, "");
        }
        while (name != previous && name.Length > 0);
        return name.Length == 0 ? previous : name;
    }

    static bool IsObfuscated(string? s) => s == null || s.StartsWith("_-") || s.Length < 3;

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex TrailingIdentifier();

    [GeneratedRegex(@"(?:Message)?(?:Composer|Event|Parser)$")]
    private static partial Regex SuffixPattern();
}
