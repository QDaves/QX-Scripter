using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace Qx.Scripting.Hosting;

/// <summary>Represents an assembly whose exported types are listed in an <see cref="ApiTypeCatalog"/>.</summary>
/// <param name="Name">The simple name of the assembly.</param>
/// <param name="Version">The assembly version, or <see langword="null"/> when it has none.</param>
/// <param name="TypeCount">The number of public types the assembly exports.</param>
public sealed record ApiAssembly(string Name, string? Version, int TypeCount);

/// <summary>Represents a single <c>&lt;param&gt;</c> entry read from the generated XML documentation.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Text">The parameter description as flattened text.</param>
public sealed record ApiDocParameter(string Name, string Text);

/// <summary>
/// Represents a single <c>&lt;exception&gt;</c> entry read from the generated XML documentation.
/// </summary>
/// <param name="Type">
/// The documented exception type, written as a cross reference is: without its namespace when
/// scripts import that namespace, and fully qualified otherwise.
/// </param>
/// <param name="Text">The condition under which the exception is thrown as flattened text, or an empty string.</param>
public sealed record ApiDocException(string Type, string Text);

/// <summary>
/// Represents the documentation attached to a catalog type or member, read from the XML
/// documentation file the compiler emits next to the assembly.
/// </summary>
/// <remarks>
/// Every part is optional; the whole record is absent when the member carries no documentation or
/// the assembly has no XML file. All text arrives with whitespace collapsed to single spaces and
/// with cross references replaced by the name of their target. That name leaves out a namespace
/// scripts import, so <c>Qx.Scripting.ScriptGlobals.Walk(System.Int32,System.Int32)</c> reads
/// <c>ScriptGlobals.Walk(int, int)</c>, while a target in any other namespace keeps its full name.
/// Parts that are <see langword="null"/> are left out when the record is serialized to JSON.
/// </remarks>
/// <param name="Summary">The summary text, or <see langword="null"/> when there is none.</param>
/// <param name="Returns">The returns text, or <see langword="null"/> when there is none.</param>
/// <param name="Remarks">The remarks text, or <see langword="null"/> when there are none.</param>
/// <param name="Parameters">The documented parameters, or <see langword="null"/> when none are documented.</param>
/// <param name="Exceptions">The documented exceptions, or <see langword="null"/> when none are documented.</param>
public sealed record ApiDoc(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Summary = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Returns = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Remarks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ApiDocParameter>? Parameters = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ApiDocException>? Exceptions = null);

/// <summary>Represents a short reference to a type in an <see cref="ApiTypeCatalog"/>.</summary>
/// <param name="Name">
/// The display name in C# syntax, with generic parameters and, for a nested type, the declaring
/// type, such as <c>List&lt;T&gt;</c> or <c>Outer.Inner</c>.
/// </param>
/// <param name="FullName">
/// The namespace qualified name with nested types joined by dots; generic types keep their arity
/// suffix, such as <c>`1</c>.
/// </param>
/// <param name="Kind">
/// The type kind: <c>class</c>, <c>static class</c>, <c>struct</c>, <c>interface</c>,
/// <c>enum</c> or <c>delegate</c>.
/// </param>
/// <param name="Assembly">The simple name of the assembly that exports the type.</param>
/// <param name="Namespace">The namespace, or <see langword="null"/> for a type in the global namespace.</param>
/// <param name="Imported">
/// <see langword="true"/> if a script can name the type without a using directive, because its
/// namespace is one of <see cref="ScriptEngine.Imports"/>; otherwise, <see langword="false"/>.
/// </param>
/// <param name="Using">
/// The using directive a script adds to name the type, such as <c>using Qx.Game.Protocol;</c>,
/// or <see langword="null"/> when the type is imported.
/// </param>
/// <param name="Documentation">The XML documentation of the type, or <see langword="null"/> when none is available.</param>
public sealed record ApiTypeReference(
    string Name,
    string FullName,
    string Kind,
    string Assembly,
    string? Namespace,
    bool Imported,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Using,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

/// <summary>Represents a public member of a type in an <see cref="ApiTypeCatalog"/>.</summary>
/// <param name="Kind">
/// The member kind: <c>constructor</c>, <c>property</c>, <c>method</c>, <c>event</c>,
/// <c>field</c>, or <c>value</c> for an enum member.
/// </param>
/// <param name="Name">The member name; for a constructor, the display name of the type.</param>
/// <param name="Signature">The member declaration in C# syntax, including default parameter values.</param>
/// <param name="IsStatic">
/// <see langword="true"/> if the member is static, which enum values always are; otherwise,
/// <see langword="false"/>.
/// </param>
/// <param name="DeclaredBy">
/// The full name of the type that declares the member, or an empty string when it is unknown.
/// </param>
/// <param name="Obsolete">
/// The message of the member's <see cref="ObsoleteAttribute"/>, an empty string when the attribute
/// carries none, or <see langword="null"/> when the member is not obsolete.
/// </param>
/// <param name="Documentation">The XML documentation of the member, or <see langword="null"/> when none is available.</param>
public sealed record ApiMember(
    string Kind,
    string Name,
    string Signature,
    bool IsStatic,
    string DeclaredBy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Obsolete,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

/// <summary>Represents the full description of a type in an <see cref="ApiTypeCatalog"/>, including its public members.</summary>
/// <param name="Name">The display name in C# syntax.</param>
/// <param name="FullName">The namespace qualified name with nested types joined by dots.</param>
/// <param name="Kind">
/// The type kind: <c>class</c>, <c>static class</c>, <c>struct</c>, <c>interface</c>,
/// <c>enum</c> or <c>delegate</c>.
/// </param>
/// <param name="Assembly">The simple name of the assembly that exports the type.</param>
/// <param name="Namespace">The namespace, or <see langword="null"/> for a type in the global namespace.</param>
/// <param name="Imported">
/// <see langword="true"/> if a script can name the type without a using directive, because its
/// namespace is one of <see cref="ScriptEngine.Imports"/>; otherwise, <see langword="false"/>.
/// </param>
/// <param name="Using">
/// The using directive a script adds to name the type, such as <c>using Qx.Game.Protocol;</c>,
/// or <see langword="null"/> when the type is imported.
/// </param>
/// <param name="Signature">
/// The type declaration in C# syntax, with its base type, interfaces and generic constraints.
/// </param>
/// <param name="BaseType">
/// The display name of the base type, or <see langword="null"/> when it is <see cref="object"/>,
/// <see cref="ValueType"/>, <see cref="Enum"/> or <see cref="MulticastDelegate"/>.
/// </param>
/// <param name="Interfaces">The display names of every interface the type implements, sorted.</param>
/// <param name="Obsolete">
/// The message of the type's <see cref="ObsoleteAttribute"/>, an empty string when the attribute
/// carries none, or <see langword="null"/> when the type is not obsolete.
/// </param>
/// <param name="Members">
/// The public constructors, properties, methods, events and fields, or the values of an enum,
/// ordered by kind and then by name. Interfaces also list the members of the interfaces they
/// inherit. A lookup with a member filter lists only the members whose name contains it, and a
/// lookup that answers with <paramref name="MemberIndex"/> lists none.
/// </param>
/// <param name="MemberIndex">
/// The distinct names of the members that are not obsolete, sorted, when the lookup gave no member
/// filter and the type has more public members than one answer should carry; otherwise,
/// <see langword="null"/>.
/// </param>
/// <param name="Documentation">The XML documentation of the type, or <see langword="null"/> when none is available.</param>
public sealed record ApiTypeDetails(
    string Name,
    string FullName,
    string Kind,
    string Assembly,
    string? Namespace,
    bool Imported,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Using,
    string Signature,
    string? BaseType,
    IReadOnlyList<string> Interfaces,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Obsolete,
    IReadOnlyList<ApiMember> Members,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? MemberIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

/// <summary>Represents the result of looking up a type by name in an <see cref="ApiTypeCatalog"/>.</summary>
/// <param name="Query">The name as it was passed to the lookup.</param>
/// <param name="Type">
/// The details of the matching type, or <see langword="null"/> when the name is ambiguous or
/// unknown.
/// </param>
/// <param name="Ambiguous">
/// <see langword="true"/> if the name matched more than one type and none of them won;
/// otherwise, <see langword="false"/>.
/// </param>
/// <param name="NotFound">
/// <see langword="true"/> if no type matched the name; otherwise, <see langword="false"/>.
/// </param>
/// <param name="Candidates">
/// The candidate types: every matching type sorted by full name when the name is ambiguous, up to
/// 15 types whose name contains the query when nothing matched, and none on a match.
/// </param>
/// <param name="AlsoMatches">
/// The nested types that share the short name of the top-level type that won the lookup, sorted
/// by full name, or none. Each one is still found by its display or full name.
/// </param>
/// <param name="Hint">
/// How to read the rest of the type when the answer leaves members out, or <see langword="null"/>
/// when it does not.
/// </param>
public sealed record ApiTypeLookup(
    string Query,
    ApiTypeDetails? Type,
    bool Ambiguous,
    bool NotFound,
    IReadOnlyList<ApiTypeReference> Candidates,
    IReadOnlyList<ApiTypeReference> AlsoMatches,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hint = null);

/// <summary>Represents one page of the results of an <see cref="ApiTypeCatalog"/> search.</summary>
/// <typeparam name="T">The type of the results.</typeparam>
/// <param name="Items">The results on the page, in rank order.</param>
/// <param name="Total">The number of results across every page.</param>
/// <param name="NextOffset">
/// The offset of the next page, or <see langword="null"/> when this page holds the last result.
/// </param>
public sealed record ApiSearchPage<T>(
    IReadOnlyList<T> Items,
    int Total,
    int? NextOffset);

/// <summary>
/// Represents a member found by <see cref="ApiTypeCatalog.SearchMembers(string, string?, int, int, bool)"/>,
/// together with the type it is listed on.
/// </summary>
/// <param name="Type">The full name of the type the member is listed on.</param>
/// <param name="DeclaredBy">
/// The full name of the type that declares the member, which differs from
/// <paramref name="Type"/> for inherited interface members.
/// </param>
/// <param name="Kind">
/// The member kind: <c>constructor</c>, <c>property</c>, <c>method</c>, <c>event</c>,
/// <c>field</c>, or <c>value</c> for an enum member.
/// </param>
/// <param name="Name">The member name; for a constructor, the display name of the type.</param>
/// <param name="Signature">The member declaration in C# syntax.</param>
/// <param name="IsStatic">
/// <see langword="true"/> if the member is static; otherwise, <see langword="false"/>.
/// </param>
/// <param name="Imported">
/// <see langword="true"/> if a script can name the type the member is listed on without a using
/// directive; otherwise, <see langword="false"/>. Members of <see cref="ScriptGlobals"/> are in
/// scope by their bare name.
/// </param>
/// <param name="Using">
/// The using directive a script adds to name the type the member is listed on, or
/// <see langword="null"/> when that type is imported.
/// </param>
/// <param name="Documentation">The XML documentation of the member, or <see langword="null"/> when none is available.</param>
public sealed record ApiMemberReference(
    string Type,
    string DeclaredBy,
    string Kind,
    string Name,
    string Signature,
    bool IsStatic,
    bool Imported,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Using,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApiDoc? Documentation = null);

/// <summary>
/// Provides search and lookup over the public types of a set of assemblies, with their members
/// and XML documentation.
/// </summary>
/// <remarks>
/// <para>
/// Types that cannot be loaded are skipped. The member index behind
/// <see cref="SearchMembers(string, string?, int, int, bool)"/> is built on its first call and
/// reused afterwards, and the order of every search is fixed, so consecutive pages never repeat
/// or skip a result.
/// </para>
/// <para>
/// Searches rank the closest name match first. Among equally close matches
/// <see cref="ScriptGlobals"/> comes first, then the namespaces scripts import, then every other
/// namespace. The host namespaces, which compose QX rather than serve scripts, and types marked
/// <see cref="EditorBrowsableState.Never"/> come after every other match. Searches leave out
/// types and members marked <see cref="ObsoleteAttribute"/>; <see cref="GetType(string, string?)"/>
/// still describes them, with their message. The <see cref="ObsoleteAttribute"/> the compiler
/// puts on a ref struct, or on a constructor of a type with required members, only keeps older
/// compilers away and does not count; one the author put there does.
/// </para>
/// </remarks>
public sealed class ApiTypeCatalog
{
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private const int DefaultTypeLimit = 50;
    private const int DefaultMemberLimit = 60;
    private const int MaximumLimit = 500;
    private const int MemberIndexThreshold = 150;

    private static readonly HashSet<string> imported_namespaces = new(ScriptEngine.Imports, StringComparer.Ordinal);
    private static readonly string[] host_namespaces =
    [
        "Qx.Scripting.Hosting",
        "Qx.Interception.GEarth",
        "Qx.Diagnostics",
        "Qx.Game.Rules"
    ];
    private static readonly Dictionary<string, string> compiler_markers = new(StringComparer.Ordinal)
    {
        [CompilerFeatureRequiredAttribute.RefStructs] =
            "Types with embedded references are not supported in this version of your compiler.",
        [CompilerFeatureRequiredAttribute.RequiredMembers] =
            "Constructors of types with required members are not supported in this version of your compiler."
    };

    private readonly IReadOnlyList<CatalogType> _types;
    private readonly Lazy<IReadOnlyList<IndexedMember>> _memberIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiTypeCatalog"/> class over the assemblies
    /// listed in <see cref="ScriptEngine.ReferenceAssemblies"/>.
    /// </summary>
    public ApiTypeCatalog()
        : this(ScriptEngine.ReferenceAssemblies)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiTypeCatalog"/> class over the specified
    /// assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The assemblies whose exported types are listed. <see langword="null"/> entries and
    /// duplicates are ignored.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is <see langword="null"/>.</exception>
    public ApiTypeCatalog(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var types = new List<CatalogType>();
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
                    types.Add(new CatalogType(type, name, AudienceOf(type), ObsoleteMessage(type)));
            }
            catalogAssemblies.Add(new ApiAssembly(name, assembly.GetName().Version?.ToString(), exported.Length));
        }

        _types = types
            .OrderBy(entry => TypeName(entry.Type), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => FullName(entry.Type), StringComparer.Ordinal)
            .ToArray();
        Assemblies = catalogAssemblies;
        _memberIndex = new Lazy<IReadOnlyList<IndexedMember>>(
            BuildMemberIndex,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Gets the assemblies in the catalog, ordered by name.</summary>
    public IReadOnlyList<ApiAssembly> Assemblies { get; }

    /// <summary>
    /// Reads the compiler generated XML documentation for a type, method, constructor, property,
    /// event or field.
    /// </summary>
    /// <remarks>
    /// The documentation file is looked up next to the declaring assembly, then in the application
    /// base directory, and parsed once per assembly. A missing, unreadable or malformed file yields
    /// <see langword="null"/> rather than an exception.
    /// </remarks>
    /// <param name="member">The member to read the documentation for.</param>
    /// <returns>The documentation, or <see langword="null"/> when the member is undocumented.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="member"/> is <see langword="null"/>.</exception>
    public static ApiDoc? DocumentationFor(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Describe(member);
    }

    internal static string? ObsoleteMessage(MemberInfo member) =>
        member.GetCustomAttribute<ObsoleteAttribute>(false) is { } obsolete && !IsCompilerMarker(member, obsolete)
            ? obsolete.Message ?? ""
            : null;

    internal static bool IsImported(string? @namespace) =>
        @namespace is null || imported_namespaces.Contains(@namespace);

    internal static string? UsingFor(string? @namespace) =>
        IsImported(@namespace) ? null : $"using {@namespace};";

    /// <summary>
    /// Searches the catalog for types whose display name or full name contains the query.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive, and blank filters count as <see langword="null"/>. Types marked
    /// <see cref="ObsoleteAttribute"/> are left out.
    /// </remarks>
    /// <param name="query">The text to look for in the type names, or <see langword="null"/> to list every type.</param>
    /// <param name="assembly">
    /// The text the assembly name must contain, or <see langword="null"/> for every assembly.
    /// </param>
    /// <param name="limit">
    /// The maximum number of results on the page. Zero or less uses the default of 50, and the
    /// value is capped at 500.
    /// </param>
    /// <param name="offset">The number of ranked results to skip. A negative value counts as zero.</param>
    /// <returns>
    /// The page of matching types: exact name matches first, then name prefix matches, then the
    /// rest, each ranked as the class remarks describe and then sorted by name.
    /// </returns>
    public ApiSearchPage<ApiTypeReference> SearchTypes(
        string? query = null,
        string? assembly = null,
        int limit = DefaultTypeLimit,
        int offset = 0)
    {
        string? normalizedQuery = NormalizeOptional(query);
        string? normalizedAssembly = NormalizeOptional(assembly);
        IEnumerable<CatalogType> source = _types.Where(entry => entry.Obsolete is null);

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

        return Page(Ranked(source, normalizedQuery), limit, offset, DefaultTypeLimit, Reference);
    }

    /// <summary>
    /// Looks up a type by its full name, falling back to its short or display name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is trimmed, <c>+</c> becomes <c>.</c> and <c>global::</c> is removed. Matching is
    /// case-insensitive, and full name matches take precedence over short name matches. When
    /// several types share the short name and exactly one of them is a top-level type, that type
    /// wins and the nested ones are listed in <see cref="ApiTypeLookup.AlsoMatches"/>.
    /// </para>
    /// <para>
    /// Without a member filter a type with more than 150 public members answers with
    /// <see cref="ApiTypeDetails.MemberIndex"/> instead of its members. The index leaves obsolete
    /// members out; a member filter still lists them with their message.
    /// </para>
    /// </remarks>
    /// <param name="name">The full name, short name or display name of the type.</param>
    /// <param name="member">
    /// The text a member name must contain, case-insensitively, to be listed, or
    /// <see langword="null"/> to list every member.
    /// </param>
    /// <returns>
    /// The lookup result: the type details on a match, the candidates when the name is ambiguous,
    /// or up to 15 suggestions when nothing matched.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    public ApiTypeLookup GetType(string name, string? member = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string query = NormalizeTypeQuery(name);

        List<CatalogType> matches = _types
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

        CatalogType[] top_level = [.. matches.Where(entry => !entry.Type.IsNested)];
        if (matches.Count == 1 || (matches.Count > 1 && top_level.Length == 1))
        {
            CatalogType found = matches.Count == 1 ? matches[0] : top_level[0];
            ApiTypeReference[] also_matches =
            [
                .. matches
                    .Where(entry => entry.Type != found.Type)
                    .Select(Reference)
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
            ];
            return Found(name, found, NormalizeOptional(member), also_matches);
        }

        if (matches.Count > 1)
        {
            return new ApiTypeLookup(
                name,
                null,
                true,
                false,
                matches.Select(Reference).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray(),
                []);
        }

        ApiTypeReference[] suggestions = Ranked(
                _types.Where(entry =>
                    TypeName(entry.Type).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    FullName(entry.Type).Contains(query, StringComparison.OrdinalIgnoreCase)),
                query)
            .Take(15)
            .Select(Reference)
            .ToArray();
        return new ApiTypeLookup(name, null, false, true, suggestions, []);
    }

    /// <summary>
    /// Searches the public members of every catalog type by member name, signature or type name.
    /// </summary>
    /// <remarks>
    /// The member index is built on the first call and reused afterwards. Members marked
    /// <see cref="ObsoleteAttribute"/>, and every member of an obsolete type, are left out.
    /// </remarks>
    /// <param name="query">
    /// The text to look for, case-insensitively, in the member name, the signature and the full
    /// name of the type the member is listed on.
    /// </param>
    /// <param name="kind">
    /// The member kind to restrict to, such as <c>method</c> or <c>property</c>, or
    /// <see langword="null"/> for every kind.
    /// </param>
    /// <param name="limit">
    /// The maximum number of results on the page. Zero or less uses the default of 60, and the
    /// value is capped at 500.
    /// </param>
    /// <param name="offset">The number of ranked results to skip. A negative value counts as zero.</param>
    /// <param name="includeGenerated">
    /// <see langword="true"/> to include the methods the compiler generates for records, such as
    /// <c>Equals</c>, <c>GetHashCode</c>, <c>ToString</c> and <c>Deconstruct</c>; otherwise,
    /// <see langword="false"/>. Properties are always included.
    /// </param>
    /// <returns>
    /// The page of matching members: exact name matches first, then name prefix and name substring
    /// matches, then signature and type name matches, each ranked as the class remarks describe.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="query"/> is <see langword="null"/>, empty or whitespace.</exception>
    public ApiSearchPage<ApiMemberReference> SearchMembers(
        string query,
        string? kind = null,
        int limit = DefaultMemberLimit,
        int offset = 0,
        bool includeGenerated = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        string needle = query.Trim();
        string? normalized_kind = NormalizeOptional(kind);
        IndexedMember[] matches = _memberIndex.Value
            .Where(entry => includeGenerated || !entry.Generated)
            .Where(entry => normalized_kind is null ||
                string.Equals(entry.Member.Kind, normalized_kind, StringComparison.OrdinalIgnoreCase))
            .Where(entry =>
                entry.Member.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                entry.Member.Signature.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                entry.Member.Type.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Audience is Audience.Host)
            .ThenBy(entry => MemberMatchRank(entry.Member, needle))
            .ThenBy(entry => entry.Audience)
            .ThenBy(entry => entry.Member.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Member.Kind, StringComparer.Ordinal)
            .ThenBy(entry => entry.Member.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Page(matches, limit, offset, DefaultMemberLimit, entry => entry.Member);
    }

    private IReadOnlyList<IndexedMember> BuildMemberIndex()
    {
        var members = new List<IndexedMember>();
        foreach (CatalogType entry in _types)
        {
            if (entry.Obsolete is not null)
                continue;
            string type_name = FullName(entry.Type);
            bool imported = IsImported(entry.Type.Namespace);
            string? directive = UsingFor(entry.Type.Namespace);
            foreach ((ApiMember member, MemberInfo source) in MemberEntries(entry.Type))
            {
                if (member.Obsolete is not null)
                    continue;
                members.Add(new IndexedMember(
                    new ApiMemberReference(
                        type_name,
                        member.DeclaredBy,
                        member.Kind,
                        member.Name,
                        member.Signature,
                        member.IsStatic,
                        imported,
                        directive,
                        member.Documentation),
                    entry.Audience,
                    source is MethodInfo method && method.IsDefined(typeof(CompilerGeneratedAttribute), false)));
            }
        }
        return members;
    }

    private static ApiTypeLookup Found(
        string query,
        CatalogType entry,
        string? member,
        IReadOnlyList<ApiTypeReference> also_matches)
    {
        ApiTypeDetails details = Details(entry);
        if (member is not null)
        {
            ApiMember[] members =
            [
                .. details.Members.Where(value => value.Name.Contains(member, StringComparison.OrdinalIgnoreCase))
            ];
            return new ApiTypeLookup(
                query,
                details with { Members = members },
                false,
                false,
                [],
                also_matches,
                members.Length == 0 ? $"No member name of {details.Name} contains '{member}'." : null);
        }

        if (details.Members.Count <= MemberIndexThreshold)
            return new ApiTypeLookup(query, details, false, false, [], also_matches);

        string[] names =
        [
            .. details.Members
                .Where(value => value.Obsolete is null)
                .Select(value => value.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.OrdinalIgnoreCase)
        ];
        return new ApiTypeLookup(
            query,
            details with { Members = [], MemberIndex = names },
            false,
            false,
            [],
            also_matches,
            $"{details.Name} has {details.Members.Count} public members, so only the names of those that are not obsolete are listed. " +
            "Look the type up again with a member filter to read the signatures and documentation of the members whose name contains it.");
    }

    private static CatalogType[] Ranked(IEnumerable<CatalogType> source, string? query) =>
        source
            .OrderBy(entry => entry.Audience is Audience.Host)
            .ThenBy(entry => MatchRank(entry.Type, query))
            .ThenBy(entry => entry.Audience)
            .ThenBy(entry => TypeName(entry.Type), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => FullName(entry.Type), StringComparer.Ordinal)
            .ToArray();

    private static ApiSearchPage<TResult> Page<TSource, TResult>(
        IReadOnlyList<TSource> matches,
        int limit,
        int offset,
        int fallback,
        Func<TSource, TResult> select)
    {
        int start = Math.Max(offset, 0);
        TResult[] items = [.. matches.Skip(start).Take(NormalizeLimit(limit, fallback)).Select(select)];
        int end = start + items.Length;
        return new ApiSearchPage<TResult>(items, matches.Count, end < matches.Count ? end : null);
    }

    private static ApiTypeReference Reference(CatalogType entry) =>
        new(
            TypeName(entry.Type),
            FullName(entry.Type),
            ReflectionFormat.TypeKind(entry.Type),
            entry.Assembly,
            entry.Type.Namespace,
            IsImported(entry.Type.Namespace),
            UsingFor(entry.Type.Namespace),
            Describe(entry.Type));

    private static ApiTypeDetails Details(CatalogType entry)
    {
        Type type = entry.Type;
        Type? baseType = MeaningfulBaseType(type.BaseType) ? type.BaseType : null;
        return new ApiTypeDetails(
            TypeName(type),
            FullName(type),
            ReflectionFormat.TypeKind(type),
            entry.Assembly,
            type.Namespace,
            IsImported(type.Namespace),
            UsingFor(type.Namespace),
            ReflectionFormat.TypeDeclaration(type),
            baseType is null ? null : ReflectionFormat.FriendlyName(baseType),
            type.GetInterfaces()
                .Select(value => ReflectionFormat.FriendlyName(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            entry.Obsolete,
            [.. MemberEntries(type).Select(value => value.Member)],
            null,
            Describe(type));
    }

    private static ApiDoc? Describe(MemberInfo? member) => Describe(member, 0);

    /// <summary>
    /// Finds a member's documentation and follows <c>inheritdoc</c> to the overridden member or
    /// the implemented interface member, which is what the compiler leaves unresolved.
    /// </summary>
    private static ApiDoc? Describe(MemberInfo? member, int depth)
    {
        const int max_depth = 8;
        if (member is null || depth > max_depth)
            return null;
        Assembly? assembly = (member as Type ?? member.DeclaringType)?.Assembly;
        if (assembly is null)
            return null;
        XmlDocSet docs = XmlDocSet.ForAssembly(assembly);
        if (docs.IsEmpty)
            return null;
        string id = ReflectionFormat.DocumentationId(member);
        ApiDoc? own = docs.Find(id);
        if (own?.Summary is not null || !docs.Inherits(id, out string? source))
            return own;
        if (source is not null)
            return docs.Find(source) ?? own;
        return Inherited(member)
            .Select(inherited => Describe(inherited, depth + 1))
            .FirstOrDefault(doc => doc?.Summary is not null) ?? own;
    }

    private static IEnumerable<MemberInfo> Inherited(MemberInfo member) => member switch
    {
        Type type => [.. new[] { type.BaseType }.OfType<Type>(), .. type.GetInterfaces()],
        MethodInfo method => Overridden(method),
        PropertyInfo property => (property.GetMethod ?? property.SetMethod) is { } accessor
            ? Overridden(accessor).Select(Owner).OfType<PropertyInfo>()
            : [],
        EventInfo @event => @event.AddMethod is { } adder
            ? Overridden(adder).Select(Owner).OfType<EventInfo>()
            : [],
        _ => []
    };

    private static IEnumerable<MethodInfo> Overridden(MethodInfo method)
    {
        MethodInfo definition = method.GetBaseDefinition();
        if (definition != method)
            yield return definition;
        if (method.DeclaringType is not { IsInterface: false } type)
            yield break;
        foreach (Type contract in type.GetInterfaces())
        {
            InterfaceMapping map = type.GetInterfaceMap(contract);
            int index = Array.IndexOf(map.TargetMethods, method);
            if (index >= 0)
                yield return map.InterfaceMethods[index];
        }
    }

    private static MemberInfo? Owner(MethodInfo accessor)
    {
        const BindingFlags every = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Type? type = accessor.DeclaringType;
        return (MemberInfo?)type?.GetProperties(every).FirstOrDefault(property => property.GetMethod == accessor || property.SetMethod == accessor)
            ?? type?.GetEvents(every).FirstOrDefault(@event => @event.AddMethod == accessor);
    }

    private static IReadOnlyList<(ApiMember Member, MemberInfo Source)> MemberEntries(Type type)
    {
        if (type.IsEnum)
        {
            return Enum.GetNames(type)
                .Select(name => type.GetField(name, BindingFlags.Public | BindingFlags.Static)!)
                .Select(field => (new ApiMember(
                    "value",
                    field.Name,
                    ReflectionFormat.EnumValue(type, field.Name),
                    true,
                    FullName(type),
                    ObsoleteMessage(field),
                    Describe(field)), (MemberInfo)field))
                .ToArray();
        }

        var members = new List<(ApiMember Member, MemberInfo Source)>();
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
            .OrderBy(entry => MemberRank(entry.Member.Kind))
            .ThenBy(entry => entry.Member.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Member.Signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Add(
        ICollection<(ApiMember Member, MemberInfo Source)> members,
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
            members.Add((new ApiMember(
                kind,
                name,
                signature,
                isStatic,
                declaredBy is null ? "" : FullName(declaredBy),
                ObsoleteMessage(source),
                Describe(source)), source));
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

    private static Audience AudienceOf(Type type)
    {
        if (type == typeof(ScriptGlobals))
            return Audience.Globals;
        if (IsHost(type))
            return Audience.Host;
        return IsImported(type.Namespace) ? Audience.Imported : Audience.Public;
    }

    private static bool IsHost(Type type)
    {
        for (Type? current = type; current is not null; current = current.DeclaringType)
        {
            if (current.GetCustomAttribute<EditorBrowsableAttribute>(false)?.State is EditorBrowsableState.Never)
                return true;
        }
        return type.Namespace is { } name &&
            host_namespaces.Any(host => name == host || name.StartsWith(host + ".", StringComparison.Ordinal));
    }

    private static bool IsCompilerMarker(MemberInfo member, ObsoleteAttribute obsolete) =>
        member.GetCustomAttributes<CompilerFeatureRequiredAttribute>(false).Any(feature =>
            compiler_markers.TryGetValue(feature.FeatureName, out string? marker) &&
            string.Equals(marker, obsolete.Message, StringComparison.Ordinal));

    private enum Audience
    {
        Globals,
        Imported,
        Public,
        Host
    }

    private sealed record CatalogType(Type Type, string Assembly, Audience Audience, string? Obsolete);

    private sealed record IndexedMember(ApiMemberReference Member, Audience Audience, bool Generated);
}

/// <summary>
/// Contains the parsed contents of one compiler generated XML documentation file, keyed by
/// ECMA-334 documentation comment identifier.
/// </summary>
/// <remarks>
/// Instances are immutable and cached per assembly; every failure path (no file, unreadable file,
/// malformed XML) collapses to <see cref="Empty"/> so that documentation is never able to break
/// catalog construction.
/// </remarks>
internal sealed class XmlDocSet
{
    public static readonly XmlDocSet Empty = new(
        new Dictionary<string, ApiDoc>(0, StringComparer.Ordinal),
        new Dictionary<string, string?>(0, StringComparer.Ordinal));

    private static readonly ConcurrentDictionary<Assembly, Lazy<XmlDocSet>> Cache = new();

    private readonly IReadOnlyDictionary<string, ApiDoc> _entries;
    private readonly IReadOnlyDictionary<string, string?> _inheriting;

    private XmlDocSet(IReadOnlyDictionary<string, ApiDoc> entries, IReadOnlyDictionary<string, string?> inheriting)
    {
        _entries = entries;
        _inheriting = inheriting;
    }

    public bool IsEmpty => _entries.Count == 0 && _inheriting.Count == 0;

    /// <summary>
    /// Gets whether the entry for <paramref name="id"/> takes its text from <c>inheritdoc</c>, and
    /// the documentation id it names, or <see langword="null"/> when it inherits implicitly.
    /// </summary>
    public bool Inherits(string id, out string? source) => _inheriting.TryGetValue(id, out source);

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
    /// Parses an XML documentation file, or returns <see cref="Empty"/> when the path is null, the
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
    /// Locates the XML documentation file for an assembly.
    /// </summary>
    /// <remarks>
    /// Prefers the file next to the loaded module and falls back to the application base
    /// directory, which is where a self-extracting single-file host places bundled content.
    /// Returns <see langword="null"/> when the assembly has no physical file and no matching file
    /// exists next to the host.
    /// </remarks>
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
        var inheriting = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (XElement entry in entries)
        {
            string? id = entry.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (entry.Element("inheritdoc") is { } inheritdoc)
                inheriting[id.Trim()] = inheritdoc.Attribute("cref")?.Value.Trim();
            if (Describe(entry) is { } doc)
                parsed.TryAdd(id.Trim(), doc);
        }

        return parsed.Count == 0 && inheriting.Count == 0 ? Empty : new XmlDocSet(parsed, inheriting);
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
        if (trimmed.Length <= 2 || trimmed[1] != ':' || !char.IsLetter(trimmed[0]))
            return trimmed;
        string target = trimmed[2..];
        return trimmed[0] switch
        {
            'T' => CrefType(target),
            'M' or 'P' or 'F' or 'E' => CrefMember(target),
            _ => target
        };
    }

    private static string CrefMember(string target)
    {
        int open = target.IndexOf('(');
        string path = open < 0 ? target : target[..open];
        int dot = path.LastIndexOf('.');
        if (dot < 0)
            return target;
        string owner = CrefType(path[..dot]);
        string name = path[(dot + 1)..];
        string member = name == "#ctor" ? owner : $"{owner}.{name}";
        if (open < 0)
            return member;
        int close = target.LastIndexOf(')');
        return close < open
            ? target
            : $"{member}({string.Join(", ", CrefArguments(target[(open + 1)..close]).Select(CrefType))})";
    }

    private static string CrefType(string text)
    {
        int open = text.IndexOf('{');
        if (open < 0)
        {
            int suffix = text.IndexOfAny(['[', '*', '@']);
            return suffix < 0 ? CrefName(text) : CrefName(text[..suffix]) + CrefSuffix(text[suffix..]);
        }

        int close = Closing(text, open);
        if (close < 0 || (close + 1 < text.Length && text[close + 1] == '.'))
            return text;
        string generic = text[..open];
        string[] arguments = [.. CrefArguments(text[(open + 1)..close]).Select(CrefType)];
        string formatted = generic == "System.Nullable" && arguments.Length == 1
            ? arguments[0] + "?"
            : $"{CrefName(generic)}<{string.Join(", ", arguments)}>";
        return formatted + CrefSuffix(text[(close + 1)..]);
    }

    private static string CrefName(string name)
    {
        if (ReflectionFormat.Alias(name) is { } keyword)
            return keyword;
        int dot = name.LastIndexOf('.');
        return dot > 0 && ApiTypeCatalog.IsImported(name[..dot]) ? name[(dot + 1)..] : name;
    }

    private static string CrefSuffix(string suffix) =>
        suffix.Replace("@", "", StringComparison.Ordinal).Replace("0:", "", StringComparison.Ordinal);

    private static IEnumerable<string> CrefArguments(string text)
    {
        int depth = 0;
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '{' or '[':
                    depth++;
                    break;
                case '}' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return text[start..index];
                    start = index + 1;
                    break;
            }
        }
        yield return text[start..];
    }

    private static int Closing(string text, int open)
    {
        int depth = 0;
        for (int index = open; index < text.Length; index++)
        {
            if (text[index] == '{')
                depth++;
            else if (text[index] == '}' && --depth == 0)
                return index;
        }
        return -1;
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
