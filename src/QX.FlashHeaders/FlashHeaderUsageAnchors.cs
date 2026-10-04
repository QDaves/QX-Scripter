using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public sealed partial class FlashHeaderNameResolver
{
    const int CallbackSearchWindow = 16;

    void ResolveByUsage(FlashHeaderMap map)
    {
        if (_database is null || _swf is null)
            return;
        FlashHeaderDefinition[] pending = [.. map.Incoming.Concat(map.Outgoing).Where(definition =>
            string.IsNullOrEmpty(definition.Name) && definition.TypeDefinitions.Count == 1)];
        if (pending.Length == 0)
            return;

        IReadOnlyDictionary<FlashHeaderDefinition, IReadOnlyList<string>> anchors = CollectUsageAnchors(map);
        var taken = new HashSet<(FlashMessageDirection, string)>(
            map.Incoming.Concat(map.Outgoing)
                .Where(definition => !string.IsNullOrEmpty(definition.Name))
                .Select(definition => (definition.Direction, definition.Name!)));
        foreach (FlashHeaderDefinition definition in pending)
        {
            if (!anchors.TryGetValue(definition, out IReadOnlyList<string>? keys))
                continue;
            string[] names = [.. keys
                .Select(key => _database.TryResolve(key, out string? name) ? name : null)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)];
            if (names.Length != 1 || !taken.Add((definition.Direction, names[0])))
                continue;
            definition.Name = names[0];
            definition.NameSource = NameSource.Usage;
        }
    }

    /// <summary>
    /// The readable places every message class is used from, as signature database keys:
    /// <c>handler3:in:{namespace}:{handler}</c> for the handlers an incoming event is built with,
    /// and <c>site3:out:{namespace}.{class}.{method}#{n}</c> for the methods that build an
    /// outgoing composer, where <c>n</c> orders the composers a method builds by first use.
    /// Places whose names are obfuscated are left out, because they change with every build.
    /// </summary>
    public IReadOnlyDictionary<FlashHeaderDefinition, IReadOnlyList<string>> CollectUsageAnchors(FlashHeaderMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var anchors = new Dictionary<FlashHeaderDefinition, IReadOnlyList<string>>();
        if (_swf is null)
            return anchors;

        var definitions = new Dictionary<ASInstance, FlashHeaderDefinition>();
        foreach (FlashHeaderDefinition definition in map.Incoming.Concat(map.Outgoing))
        {
            if (definition.TypeDefinitions.Count == 1)
                definitions.TryAdd(definition.TypeDefinitions[0].Instance, definition);
        }
        var simple_names = new HashSet<string>(
            definitions.Values.Select(definition => definition.Class),
            StringComparer.Ordinal);
        var found = new Dictionary<FlashHeaderDefinition, HashSet<string>>();

        using IDisposable identities = Avm2MethodAnalyzer.CacheRuntimeIdentities();
        foreach (ABCFile abc in _swf.AbcFiles)
        {
            foreach (ASMethodBody body in abc.MethodBodies)
            {
                ASCode code;
                try
                {
                    code = body.ParseCode();
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    continue;
                }

                string? owner = Owner(body);
                var composers = new List<FlashHeaderDefinition>();
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i] is not ConstructPropIns construct ||
                        construct.PropertyName?.Name is not { } name ||
                        !simple_names.Contains(name) ||
                        Target(construct, abc, definitions) is not { } definition)
                    {
                        continue;
                    }

                    string? key;
                    if (definition.Direction == FlashMessageDirection.Incoming)
                    {
                        string? callback = Callback(code, i, name);
                        string space = Namespace(body);
                        key = IsObfuscatedPath(space) || IsObfuscated(callback)
                            ? null
                            : $"handler3:in:{space}:{callback}";
                    }
                    else
                    {
                        if (!composers.Contains(definition))
                            composers.Add(definition);
                        key = owner is null
                            ? null
                            : $"site3:out:{owner}#{composers.IndexOf(definition)}";
                    }

                    if (key is null)
                        continue;
                    if (!found.TryGetValue(definition, out HashSet<string>? keys))
                        found[definition] = keys = new HashSet<string>(StringComparer.Ordinal);
                    keys.Add(key);
                }
            }
        }

        foreach ((FlashHeaderDefinition definition, HashSet<string> keys) in found)
            anchors[definition] = [.. keys.Order(StringComparer.Ordinal)];
        return anchors;
    }

    FlashHeaderDefinition? Target(
        ConstructPropIns construct,
        ABCFile abc,
        IReadOnlyDictionary<ASInstance, FlashHeaderDefinition> definitions)
    {
        IReadOnlyList<Avm2TypeDefinition> types;
        try
        {
            types = _types.ResolveTypes(construct.PropertyName, abc);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return null;
        }
        return types.Count == 1 && definitions.TryGetValue(types[0].Instance, out FlashHeaderDefinition? definition)
            ? definition
            : null;
    }

    static string? Callback(ASCode code, int construct_index, string class_name)
    {
        int start = -1;
        for (int i = construct_index - 1; i >= Math.Max(0, construct_index - CallbackSearchWindow); i--)
        {
            if (code[i] is FindPropStrictIns strict && strict.PropertyName?.Name == class_name ||
                code[i] is FindPropertyIns find && find.PropertyName?.Name == class_name)
            {
                start = i;
                break;
            }
        }
        if (start < 0)
            return null;
        for (int i = start + 1; i < construct_index; i++)
        {
            string? name = code[i] switch
            {
                GetLexIns lex => lex.TypeName?.Name,
                GetPropertyIns property => property.PropertyName?.Name,
                _ => null
            };
            if (name is not null)
                return name;
        }
        return null;
    }

    static string? Owner(ASMethodBody body)
    {
        string space = Namespace(body);
        string? type = body.Method.Container?.QName?.Name;
        string method = body.Method.Trait?.QName?.Name ?? (body.Method.Container is ASClass ? "cinit" : "ctor");
        if (IsObfuscatedPath(space) || IsObfuscated(type) || IsObfuscated(method))
            return null;
        return $"{space}.{type}.{method}";
    }

    static string Namespace(ASMethodBody body) => body.Method.Container?.QName?.Namespace?.Name ?? "";

    static bool IsObfuscatedPath(string path) =>
        path.Length == 0 || path.Split('.').Any(segment => segment.StartsWith("_-", StringComparison.Ordinal));
}
