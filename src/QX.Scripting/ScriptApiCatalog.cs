using System.Reflection;

namespace Qx.Scripting;

/// <summary>
/// What the editor draws next to a member, matching the completion list's own glyphs.
/// </summary>
public enum ScriptApiGlyph
{
    /// <summary>Returns nothing.</summary>
    Keyword,

    /// <summary>A value type: int, long, bool, a struct.</summary>
    Structure,

    /// <summary>A reference type: string, a model, a manager.</summary>
    Class,

    /// <summary>An interface, which is what most collections come back as.</summary>
    Interface,

    /// <summary>An enum.</summary>
    Enum,

    /// <summary>A delegate.</summary>
    Delegate
}

/// <summary>What a script-facing member is, which is how the browser groups them.</summary>
public enum ScriptApiKind
{
    /// <summary>A live piece of game state: the room, the inventory, a manager.</summary>
    State,

    /// <summary>Something to call that does or fetches something.</summary>
    Action,

    /// <summary>Something to subscribe to, which runs a callback later.</summary>
    Event
}

/// <summary>
/// One member a script can write without any using or qualification.
/// </summary>
/// <param name="Name">The bare name, which is what a search matches first.</param>
/// <param name="Signature">The whole declaration, return type included.</param>
/// <param name="Insert">What to put into the editor, with the caret where the arguments go.</param>
/// <param name="CaretOffset">How far into <paramref name="Insert"/> the caret belongs.</param>
/// <param name="Kind">Which group it belongs to.</param>
/// <param name="Group">The subsystem it belongs to, taken from the declaring file.</param>
/// <param name="Summary">The one-line documentation, empty when it carries none.</param>
/// <param name="Returns">What it gives back, empty when it carries none.</param>
/// <param name="ReturnType">The return type as it would be written.</param>
/// <param name="ReturnFilter">
/// The return type without its generic arguments, which is what a type filter offers: every
/// <c>Task&lt;T&gt;</c> belongs under one <c>Task</c> rather than under a hundred separate ones.
/// </param>
/// <param name="Glyph">Which completion glyph the return type draws.</param>
/// <param name="Parameters">
/// The parameter list in brackets, empty for anything that takes none. Without it two overloads
/// of the same name are one row twice over, which is exactly what they are not.
/// </param>
public sealed record ScriptApiMember(
    string Name,
    string Signature,
    string Insert,
    int CaretOffset,
    ScriptApiKind Kind,
    string Group,
    string Summary,
    string Returns,
    string ReturnType,
    string ReturnFilter,
    ScriptApiGlyph Glyph,
    string Parameters)
{
    /// <summary>Whether it takes arguments, so the row can leave the brackets out.</summary>
    public bool HasParameters => Parameters.Length > 0;

    /// <summary>Whether it carries a one-line description, so the row can leave the space out.</summary>
    public bool HasSummary => Summary.Length > 0;

    /// <summary>Whether a search term appears anywhere worth matching.</summary>
    /// <param name="term">The term, matched without regard to case.</param>
    public bool Matches(string term) =>
        term.Length == 0 ||
        Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Signature.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Parameters.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Group.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Summary.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>How well the term fits, so the closest match sorts first.</summary>
    /// <param name="term">The term, matched without regard to case.</param>
    public int Rank(string term)
    {
        if (term.Length == 0)
            return 2;
        if (Name.Equals(term, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (Group.Contains(term, StringComparison.OrdinalIgnoreCase))
            return 3;
        return 4;
    }
}

/// <summary>
/// Everything a script can reach without writing a using, read off <see cref="ScriptGlobals"/>
/// itself so it can never drift from what actually compiles.
/// </summary>
/// <remarks>
/// The same surface the completion list offers, but readable in one go and searchable. Built once
/// and held, because reflecting over the whole globals surface is not free and it cannot change
/// while the process runs.
/// </remarks>
public static class ScriptApiCatalog
{
    private static readonly Lazy<IReadOnlyList<ScriptApiMember>> Members = new(Build);

    /// <summary>Every member, ordered by group and then by name.</summary>
    public static IReadOnlyList<ScriptApiMember> All => Members.Value;

    /// <summary>The groups that have members, in the order they should be shown.</summary>
    public static IReadOnlyList<string> Groups =>
        [.. All.Select(m => m.Group).Distinct().OrderBy(g => g, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The return types worth filtering by, the most common first.
    /// </summary>
    /// <remarks>
    /// A type that only one member returns is not a filter, it is that member; those are left out
    /// so the row stays short enough to read.
    /// </remarks>
    public static IReadOnlyList<string> ReturnTypes =>
    [
        .. All
            .GroupBy(m => m.ReturnFilter, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
    ];

    /// <summary>
    /// The members matching a search term, closest first.
    /// </summary>
    /// <param name="term">What was typed; empty returns everything.</param>
    /// <param name="kind">Restrict to one kind, or null for all.</param>
    /// <param name="group">Restrict to one group, or null for all.</param>
    /// <param name="returnType">Restrict to one return type, or null for all.</param>
    public static IReadOnlyList<ScriptApiMember> Search(
        string? term,
        ScriptApiKind? kind = null,
        string? group = null,
        string? returnType = null)
    {
        string needle = (term ?? "").Trim();
        return
        [
            .. All
                .Where(m => kind is null || m.Kind == kind)
                .Where(m => group is null || string.Equals(m.Group, group, StringComparison.OrdinalIgnoreCase))
                .Where(m => returnType is null || string.Equals(m.ReturnFilter, returnType, StringComparison.Ordinal))
                .Where(m => m.Matches(needle))
                .OrderBy(m => m.Rank(needle))
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static IReadOnlyList<ScriptApiMember> Build()
    {
        Type globals = typeof(ScriptGlobals);
        var members = new List<ScriptApiMember>();

        foreach (PropertyInfo property in globals.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;
            ApiDoc? doc = ApiTypeCatalog.DocumentationFor(property);
            members.Add(new ScriptApiMember(
                property.Name,
                $"{Simple(property.PropertyType)} {property.Name}",
                property.Name,
                property.Name.Length,
                ScriptApiKind.State,
                GroupOf(property),
                First(doc?.Summary),
                First(doc?.Returns),
                Simple(property.PropertyType),
                Bare(property.PropertyType),
                GlyphOf(property.PropertyType),
                ""));
        }

        foreach (MethodInfo method in globals.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.IsSpecialName || method.DeclaringType != globals)
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            string arguments = string.Join(", ", parameters.Select(p =>
                p.HasDefaultValue
                    ? $"{Simple(p.ParameterType)} {p.Name} = {Literal(p.DefaultValue)}"
                    : $"{Simple(p.ParameterType)} {p.Name}"));

            ApiDoc? doc = ApiTypeCatalog.DocumentationFor(method);
            bool awaited = method.ReturnType.Name.StartsWith("Task", StringComparison.Ordinal);

            // Written as it would be called: awaited when it returns one, with the caret between
            // the brackets so the arguments can be typed straight away.
            string insert = $"{(awaited ? "await " : "")}{method.Name}()";
            members.Add(new ScriptApiMember(
                method.Name,
                $"{Simple(method.ReturnType)} {method.Name}({arguments})",
                insert,
                insert.Length - 1,
                method.Name.StartsWith("On", StringComparison.Ordinal) && parameters.Length > 0
                    ? ScriptApiKind.Event
                    : ScriptApiKind.Action,
                GroupOf(method),
                First(doc?.Summary),
                First(doc?.Returns),
                Simple(method.ReturnType),
                Bare(method.ReturnType),
                GlyphOf(method.ReturnType),
                $"({arguments})"));
        }

        return
        [
            .. members
                .GroupBy(m => m.Signature, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(m => m.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// Which subsystem a member belongs to.
    /// </summary>
    /// <remarks>
    /// Taken from the name rather than from the file it was declared in: the globals class is
    /// split across partials whose names are not available through reflection, and a member's own
    /// name is what a person searches by anyway.
    /// </remarks>
    private static string GroupOf(MemberInfo member)
    {
        string name = member.Name;
        foreach ((string prefix, string group) in Prefixes)
        {
            if (name.Contains(prefix, StringComparison.Ordinal))
                return group;
        }
        return "General";
    }

    private static readonly (string Prefix, string Group)[] Prefixes =
    [
        ("Achievement", "Achievements"),
        ("Earning", "Earnings"),
        ("Chest", "Wired chests"),
        ("Wired", "Wired"),
        ("Marketplace", "Marketplace"),
        ("Market", "Marketplace"),
        ("Catalog", "Catalog"),
        ("Shop", "Catalog"),
        ("Trade", "Trading"),
        ("Friend", "Friends"),
        ("Message", "Friends"),
        ("Forum", "Forums"),
        ("Quest", "Quests"),
        ("DailyTask", "Daily tasks"),
        ("Craft", "Crafting"),
        ("Gift", "Gifts"),
        ("Subscription", "Subscriptions"),
        ("Badge", "Badges"),
        ("Habbicon", "Habbicons"),
        ("Leaderboard", "Leaderboards"),
        ("Navigator", "Navigator"),
        ("Room", "Room"),
        ("Floor", "Room"),
        ("Wall", "Room"),
        ("Furni", "Room"),
        ("Pet", "Pets"),
        ("Bot", "Bots"),
        ("Group", "Groups"),
        ("Guild", "Groups"),
        ("Inventory", "Inventory"),
        ("Profile", "Profile"),
        ("User", "Users"),
        ("Avatar", "Users"),
        ("Ui", "Panel UI"),
        ("Poll", "Polls"),
        ("Send", "Packets"),
        ("Packet", "Packets"),
        ("Intercept", "Packets"),
        ("Log", "Output"),
        ("Delay", "Flow"),
        ("Wait", "Flow"),
        ("Run", "Flow")
    ];

    /// <summary>The type without its generic arguments, which is what a type filter groups by.</summary>
    private static string Bare(Type type)
    {
        if (type == typeof(void))
            return "void";
        Type? nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
            return Bare(nullable);
        string simple = Simple(type);
        int generic = simple.IndexOf('<', StringComparison.Ordinal);
        return generic > 0 ? simple[..generic] : simple;
    }

    /// <summary>
    /// Which completion glyph a return type draws, decided the way the editor decides it.
    /// </summary>
    private static ScriptApiGlyph GlyphOf(Type type)
    {
        if (type == typeof(void))
            return ScriptApiGlyph.Keyword;

        Type actual = Nullable.GetUnderlyingType(type) ?? type;
        if (actual.IsInterface)
            return ScriptApiGlyph.Interface;
        if (actual.IsEnum)
            return ScriptApiGlyph.Enum;
        if (typeof(Delegate).IsAssignableFrom(actual))
            return ScriptApiGlyph.Delegate;
        return actual.IsValueType ? ScriptApiGlyph.Structure : ScriptApiGlyph.Class;
    }

    private static string First(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        string one = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        int stop = one.IndexOf(". ", StringComparison.Ordinal);
        return stop > 0 ? one[..(stop + 1)] : one;
    }

    private static string Literal(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };

    private static string Simple(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(object)) return "object";

        Type? nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
            return Simple(nullable) + "?";

        if (!type.IsGenericType)
            return type.Name;

        string bare = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
        return $"{bare}<{string.Join(", ", type.GetGenericArguments().Select(Simple))}>";
    }
}
