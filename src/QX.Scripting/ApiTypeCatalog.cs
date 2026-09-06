using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace Qx.Scripting;

public sealed record ApiAssembly(string Name, string? Version, int TypeCount);

/// <summary>A single <c>&lt;param&gt;</c> entry lifted from the generated XML documentation.</summary>
public sealed record ApiDocParameter(string Name, string Text);

/// <summary>
/// A single <c>&lt;exception&gt;</c> entry lifted from the generated XML documentation.
/// <paramref name="Type"/> is the documented exception type with the doc-comment prefix
/// (<c>T:</c>) stripped.
/// </summary>
public sealed record ApiDocException(string Type, string Text);

/// <summary>
/// Documentation attached to a catalog type or member, read from the XML documentation file the
/// compiler emits next to the assembly. Every part is optional; the whole record is absent when
/// the member carries no documentation or the assembly has no XML file. All text arrives with
/// whitespace collapsed to single spaces.
/// </summary>
public sealed record ApiDoc(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Summary = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Returns = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Remarks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ApiDocParameter>? Parameters = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ApiDocException>? Exceptions = null);

public sealed record ApiTypeReference(
    string Name,
    string FullName,
    string Kind,
    string Assembly,
    string? Namespace,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

public sealed record ApiMember(
    string Kind,
    string Name,
    string Signature,
    bool IsStatic,
    string DeclaredBy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

public sealed record ApiTypeDetails(
    string Name,
    string FullName,
    string Kind,
    string Assembly,
    string? Namespace,
    string Signature,
    string? BaseType,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<ApiMember> Members,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

public sealed record ApiTypeLookup(
    string Query,
    ApiTypeDetails? Type,
    bool Ambiguous,
    bool NotFound,
    IReadOnlyList<ApiTypeReference> Candidates);

public sealed record ApiMemberReference(
    string Type,
    string DeclaredBy,
    string Kind,
    string Name,
    string Signature,
    bool IsStatic,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

public sealed class ApiTypeCatalog
{
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private const int DefaultTypeLimit = 50;
    private const int DefaultMemberLimit = 60;
    private const int MaximumLimit = 500;

    private readonly IReadOnlyList<(Type Type, string Assembly)> _types;
    private readonly Lazy<IReadOnlyList<ApiMemberReference>> _memberIndex;

    public ApiTypeCatalog()
        : this(ScriptEngine.ReferenceAssemblies)
    {
    }

    public ApiTypeCatalog(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var types = new List<(Type Type, string Assembly)>();
        var catalogAssemblies = new List<ApiAssembly>();

        foreach (Assembly assembly in assemblies
            .Where(assembly => assembly is not null)
            .DistinctBy(assembly => assembly.FullName)
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase))
        {
            Type[] exported = ExportedTypes(assembly);
            string name = assembly.GetName().Name ?? assembly.FullName ?? "?";
            foreach (Type type in exported)
            {
                if (!type.IsSpecialName)
                    types.Add((type, name));
            }
            catalogAssemblies.Add(new ApiAssembly(name, assembly.GetName().Version?.ToString(), exported.Length));
        }

        _types = types
            .OrderBy(entry => TypeName(entry.Type), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => FullName(entry.Type), StringComparer.Ordinal)
            .ToArray();
        Assemblies = catalogAssemblies;
        _memberIndex = new Lazy<IReadOnlyList<ApiMemberReference>>(
            BuildMemberIndex,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<ApiAssembly> Assemblies { get; }

    /// <summary>
    /// Reads the compiler-generated XML documentation for <paramref name="member"/> (a type,
    /// method, constructor, property, event or field). The documentation file is resolved next to
    /// the declaring assembly and parsed once per assembly; a missing, unreadable or malformed
    /// file yields <see langword="null"/> rather than an exception.
    /// </summary>
    /// <returns>The documentation, or <see langword="null"/> when the member is undocumented.</returns>
    public static ApiDoc? DocumentationFor(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Describe(member);
    }

    public IReadOnlyList<ApiTypeReference> SearchTypes(
        string? query = null,
        string? assembly = null,
        int limit = DefaultTypeLimit)
    {
        string? normalizedQuery = NormalizeOptional(query);
        string? normalizedAssembly = NormalizeOptional(assembly);
        IEnumerable<(Type Type, string Assembly)> source = _types;

        if (normalizedAssembly is not null)
        {
            source = source.Where(entry =>
                entry.Assembly.Contains(normalizedAssembly, StringComparison.OrdinalIgnoreCase));
        }

        if (normalizedQuery is not null)
        {
            source = source.Where(entry =>
                TypeName(entry.Type).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                FullName(entry.Type).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }

        return source
            .OrderBy(entry => MatchRank(entry.Type, normalizedQuery))
            .ThenBy(entry => TypeName(entry.Type), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => FullName(entry.Type), StringComparer.Ordinal)
            .Take(NormalizeLimit(limit, DefaultTypeLimit))
            .Select(Reference)
            .ToArray();
    }

    public ApiTypeLookup GetType(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string query = NormalizeTypeQuery(name);

        List<(Type Type, string Assembly)> matches = _types
            .Where(entry => string.Equals(FullName(entry.Type), query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            matches = _types
                .Where(entry =>
                    string.Equals(entry.Type.Name, query, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(TypeName(entry.Type), query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (matches.Count == 1)
            return new ApiTypeLookup(name, Details(matches[0]), false, false, []);

        if (matches.Count > 1)
        {
            return new ApiTypeLookup(
                name,
                null,
                true,
                false,
                matches.Select(Reference).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray());
        }

        ApiTypeReference[] suggestions = _types
            .Where(entry =>
                TypeName(entry.Type).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                FullName(entry.Type).Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => MatchRank(entry.Type, query))
            .ThenBy(entry => TypeName(entry.Type), StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .Select(Reference)
            .ToArray();
        return new ApiTypeLookup(name, null, false, true, suggestions);
    }

    public IReadOnlyList<ApiMemberReference> SearchMembers(
        string query,
        string? kind = null,
        int limit = DefaultMemberLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        string? normalizedKind = NormalizeOptional(kind);
        IEnumerable<ApiMemberReference> source = _memberIndex.Value;

        if (normalizedKind is not null)
        {
            source = source.Where(member =>
                string.Equals(member.Kind, normalizedKind, StringComparison.OrdinalIgnoreCase));
        }

        return source
            .Where(member =>
                member.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                member.Signature.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                member.Type.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(member => MemberMatchRank(member, query))
            .ThenBy(member => member.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.Kind, StringComparer.Ordinal)
            .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Take(NormalizeLimit(limit, DefaultMemberLimit))
            .ToArray();
    }

    private IReadOnlyList<ApiMemberReference> BuildMemberIndex()
    {
        var members = new List<ApiMemberReference>();
        foreach ((Type type, string assembly) in _types)
        {
            ApiTypeDetails details = Details((type, assembly));
            members.AddRange(details.Members.Select(member => new ApiMemberReference(
                details.FullName,
                member.DeclaredBy,
                member.Kind,
                member.Name,
                member.Signature,
                member.IsStatic,
                member.Documentation)));
        }
        return members;
    }

    private static ApiTypeReference Reference((Type Type, string Assembly) entry) =>
        new(
            TypeName(entry.Type),
            FullName(entry.Type),
            ReflectionFormat.TypeKind(entry.Type),
            entry.Assembly,
            entry.Type.Namespace,
            Describe(entry.Type));

    private static ApiTypeDetails Details((Type Type, string Assembly) entry)
    {
        Type type = entry.Type;
        Type? baseType = MeaningfulBaseType(type.BaseType) ? type.BaseType : null;
        return new ApiTypeDetails(
            TypeName(type),
            FullName(type),
            ReflectionFormat.TypeKind(type),
            entry.Assembly,
            type.Namespace,
            ReflectionFormat.TypeDeclaration(type),
            baseType is null ? null : ReflectionFormat.FriendlyName(baseType),
            type.GetInterfaces()
                .Select(value => ReflectionFormat.FriendlyName(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            Members(type),
            Describe(type));
    }

    private static ApiDoc? Describe(MemberInfo? member)
    {
        if (member is null)
            return null;
        Assembly? assembly = (member as Type ?? member.DeclaringType)?.Assembly;
        if (assembly is null)
            return null;
        XmlDocSet docs = XmlDocSet.ForAssembly(assembly);
        return docs.IsEmpty ? null : docs.Find(ReflectionFormat.DocumentationId(member));
    }

    private static IReadOnlyList<ApiMember> Members(Type type)
    {
        if (type.IsEnum)
        {
            return Enum.GetNames(type)
                .Select(name => new ApiMember(
                    "value",
                    name,
                    ReflectionFormat.EnumValue(type, name),
                    true,
                    FullName(type),
                    Describe(type.GetField(name, BindingFlags.Public | BindingFlags.Static))))
                .ToArray();
        }

        var members = new List<ApiMember>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        foreach (ConstructorInfo constructor in type.GetConstructors(MemberFlags))
        {
            Add(
                members,
                signatures,
                "constructor",
                TypeName(type),
                ReflectionFormat.Constructor(constructor),
                constructor.IsStatic,
                constructor.DeclaringType,
                constructor);
        }

        foreach (Type scope in TypeScopes(type))
        {
            foreach (PropertyInfo property in scope.GetProperties(MemberFlags))
            {
                MethodInfo? accessor = property.GetMethod ?? property.SetMethod;
                Add(
                    members,
                    signatures,
                    "property",
                    property.Name,
                    ReflectionFormat.Property(property),
                    accessor?.IsStatic == true,
                    property.DeclaringType,
                    property);
            }

            foreach (MethodInfo method in scope.GetMethods(MemberFlags))
            {
                if (method.IsSpecialName ||
                    method.DeclaringType == typeof(object) ||
                    method.Name.StartsWith('<'))
                    continue;
                Add(
                    members,
                    signatures,
                    "method",
                    method.Name,
                    ReflectionFormat.Method(method),
                    method.IsStatic,
                    method.DeclaringType,
                    method);
            }

            foreach (EventInfo @event in scope.GetEvents(MemberFlags))
            {
                MethodInfo? accessor = @event.AddMethod ?? @event.RemoveMethod;
                Add(
                    members,
                    signatures,
                    "event",
                    @event.Name,
                    ReflectionFormat.Event(@event),
                    accessor?.IsStatic == true,
                    @event.DeclaringType,
                    @event);
            }

            foreach (FieldInfo field in scope.GetFields(MemberFlags))
            {
                Add(
                    members,
                    signatures,
                    "field",
                    field.Name,
                    ReflectionFormat.Field(field),
                    field.IsStatic,
                    field.DeclaringType,
                    field);
            }
        }

        return members
            .OrderBy(member => MemberRank(member.Kind))
            .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.Signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Add(
        ICollection<ApiMember> members,
        ISet<string> signatures,
        string kind,
        string name,
        string signature,
        bool isStatic,
        Type? declaredBy,
        MemberInfo source)
    {
        if (signatures.Add($"{kind}:{signature}"))
        {
            members.Add(new ApiMember(
                kind,
                name,
                signature,
                isStatic,
                declaredBy is null ? "" : FullName(declaredBy),
                Describe(source)));
        }
    }

    private static IEnumerable<Type> TypeScopes(Type type)
    {
        yield return type;
        if (!type.IsInterface)
            yield break;
        foreach (Type inherited in type.GetInterfaces())
            yield return inherited;
    }

    private static Type[] ExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().Where(type => type.IsVisible).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string TypeName(Type type) => ReflectionFormat.FriendlyName(type);

    private static string FullName(Type type) => (type.FullName ?? type.Name).Replace('+', '.');

    private static string NormalizeTypeQuery(string query) =>
        query.Trim().Replace('+', '.').Replace("global::", "", StringComparison.Ordinal);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizeLimit(int limit, int fallback) =>
        Math.Clamp(limit <= 0 ? fallback : limit, 1, MaximumLimit);

    private static int MatchRank(Type type, string? query)
    {
        if (query is null)
            return 0;
        string name = TypeName(type);
        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static int MemberRank(string kind) =>
        kind switch
        {
            "constructor" => 0,
            "property" => 1,
            "method" => 2,
            "event" => 3,
            "field" => 4,
            _ => 5
        };

    private static int MemberMatchRank(ApiMemberReference member, string query)
    {
        if (string.Equals(member.Name, query, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (member.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (member.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (member.Signature.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (member.Type.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (member.Type.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 5;
        return 6;
    }

    private static bool MeaningfulBaseType(Type? type) =>
        type is not null &&
        type != typeof(object) &&
        type != typeof(ValueType) &&
        type != typeof(Enum) &&
        type != typeof(MulticastDelegate);
}

/// <summary>
/// The parsed contents of one compiler-generated XML documentation file, keyed by ECMA-334
/// documentation comment identifier. Instances are immutable and cached per assembly; every
/// failure path (no file, unreadable file, malformed XML) collapses to <see cref="Empty"/> so
/// that documentation is never able to break catalog construction.
/// </summary>
internal sealed class XmlDocSet
{
    public static readonly XmlDocSet Empty = new(new Dictionary<string, ApiDoc>(0, StringComparer.Ordinal));

    private static readonly ConcurrentDictionary<Assembly, Lazy<XmlDocSet>> Cache = new();

    private readonly IReadOnlyDictionary<string, ApiDoc> _entries;

    private XmlDocSet(IReadOnlyDictionary<string, ApiDoc> entries) => _entries = entries;

    public bool IsEmpty => _entries.Count == 0;

    public int Count => _entries.Count;

    public ApiDoc? Find(string id) =>
        id.Length != 0 && _entries.TryGetValue(id, out ApiDoc? doc) ? doc : null;

    /// <summary>
    /// Returns the documentation for <paramref name="assembly"/>, parsing the XML file on first
    /// use and reusing the parsed result for the lifetime of the process.
    /// </summary>
    public static XmlDocSet ForAssembly(Assembly assembly) =>
        Cache.GetOrAdd(
            assembly,
            static key => new Lazy<XmlDocSet>(
                () => Load(ResolvePath(key)),
                LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;

    /// <summary>
    /// Parses an XML documentation file. Returns <see cref="Empty"/> when the path is null, the
    /// file does not exist, cannot be read, or does not parse.
    /// </summary>
    public static XmlDocSet Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !FileExists(path))
            return Empty;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            using XmlReader reader = XmlReader.Create(path, settings);
            XDocument document = XDocument.Load(reader);
            return Parse(document);
        }
        catch
        {
            return Empty;
        }
    }

    /// <summary>
    /// Locates the XML documentation file for an assembly. Prefers the file next to the loaded
    /// module and falls back to the application base directory, which is where a self-extracting
    /// single-file host places bundled content. Returns <see langword="null"/> when the assembly
    /// has no physical file and no matching file exists next to the host.
    /// </summary>
    [UnconditionalSuppressMessage(
        "SingleFile",
        "IL3002",
        Justification = "The module path is probed only to find a sibling file; absence is handled.")]
    public static string? ResolvePath(Assembly assembly)
    {
        var candidates = new List<string>(2);
        try
        {
            string module = assembly.ManifestModule.FullyQualifiedName;
            if (module.Length > 0 && !module.StartsWith('<'))
                candidates.Add(module);
        }
        catch
        {
        }

        if (assembly.GetName().Name is { Length: > 0 } simple)
            candidates.Add(Path.Combine(AppContext.BaseDirectory, simple + ".dll"));

        foreach (string candidate in candidates)
        {
            try
            {
                string path = Path.ChangeExtension(candidate, ".xml");
                if (FileExists(path))
                    return path;
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }

    private static bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static XmlDocSet Parse(XDocument document)
    {
        IEnumerable<XElement>? entries = document.Root?.Element("members")?.Elements("member");
        if (entries is null)
            return Empty;

        var parsed = new Dictionary<string, ApiDoc>(StringComparer.Ordinal);
        foreach (XElement entry in entries)
        {
            string? id = entry.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (Describe(entry) is { } doc)
                parsed.TryAdd(id.Trim(), doc);
        }

        return parsed.Count == 0 ? Empty : new XmlDocSet(parsed);
    }

    private static ApiDoc? Describe(XElement entry)
    {
        string? summary = Text(entry.Element("summary"));
        string? returns = Text(entry.Element("returns"));
        string? remarks = Text(entry.Element("remarks"));

        ApiDocParameter[] parameters = entry.Elements("param")
            .Select(element => new ApiDocParameter(element.Attribute("name")?.Value?.Trim() ?? "", Text(element) ?? ""))
            .Where(parameter => parameter.Name.Length > 0 && parameter.Text.Length > 0)
            .ToArray();

        ApiDocException[] exceptions = entry.Elements("exception")
            .Select(element => new ApiDocException(Cref(element.Attribute("cref")?.Value) ?? "", Text(element) ?? ""))
            .Where(exception => exception.Type.Length > 0)
            .ToArray();

        if (summary is null &&
            returns is null &&
            remarks is null &&
            parameters.Length == 0 &&
            exceptions.Length == 0)
        {
            return null;
        }

        return new ApiDoc(
            summary,
            returns,
            remarks,
            parameters.Length == 0 ? null : parameters,
            exceptions.Length == 0 ? null : exceptions);
    }

    /// <summary>
    /// Flattens the inline markup of a documentation element into a single line: cross references
    /// become their target name, <c>paramref</c> and <c>typeparamref</c> become the referenced
    /// name, block elements become spaces, and every whitespace run collapses to one space.
    /// </summary>
    /// <returns>The flattened text, or <see langword="null"/> when the element is absent or blank.</returns>
    public static string? Text(XElement? element)
    {
        if (element is null)
            return null;
        var builder = new StringBuilder();
        Flatten(element, builder);
        return Collapse(builder);
    }

    private static void Flatten(XElement element, StringBuilder builder)
    {
        foreach (XNode node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child:
                    Inline(child, builder);
                    break;
            }
        }
    }

    private static void Inline(XElement element, StringBuilder builder)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                if (element.IsEmpty || element.Nodes().All(node => node is XText text && text.Value.Trim().Length == 0))
                {
                    builder.Append(' ');
                    builder.Append(
                        Cref(element.Attribute("cref")?.Value) ??
                        element.Attribute("langword")?.Value ??
                        element.Attribute("href")?.Value ??
                        "");
                    builder.Append(' ');
                }
                else
                {
                    Flatten(element, builder);
                }
                break;
            case "paramref":
            case "typeparamref":
                builder.Append(' ').Append(element.Attribute("name")?.Value ?? "").Append(' ');
                break;
            case "para":
            case "item":
            case "listheader":
            case "br":
                builder.Append(' ');
                Flatten(element, builder);
                builder.Append(' ');
                break;
            default:
                Flatten(element, builder);
                break;
        }
    }

    private static string? Cref(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string trimmed = value.Trim();
        return trimmed.Length > 2 && trimmed[1] == ':' && char.IsLetter(trimmed[0])
            ? trimmed[2..]
            : trimmed;
    }

    private static string? Collapse(StringBuilder source)
    {
        var builder = new StringBuilder(source.Length);
        bool pending = false;
        for (int index = 0; index < source.Length; index++)
        {
            char character = source[index];
            if (char.IsWhiteSpace(character))
            {
                pending = builder.Length > 0;
                continue;
            }
            if (pending)
            {
                if (!Tight(character) && !Opening(builder[^1]))
                    builder.Append(' ');
                pending = false;
            }
            builder.Append(character);
        }
        return builder.Length == 0 ? null : builder.ToString();
    }

    private static bool Tight(char character) =>
        character is '.' or ',' or ';' or ':' or ')' or ']' or '}' or '!' or '?';

    private static bool Opening(char character) =>
        character is '(' or '[' or '{';
}
