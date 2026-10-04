using System.Reflection;

namespace Qx.Scripting.Hosting;

/// <summary>
/// Specifies the glyph the editor draws next to a member, matching the completion list's own glyphs.
/// </summary>
public enum ScriptApiGlyph
{
    /// <summary>The glyph for a member that returns nothing.</summary>
    Keyword,

    /// <summary>The glyph for a value type such as <c>int</c>, <c>long</c>, <c>bool</c> or a struct.</summary>
    Structure,

    /// <summary>The glyph for a reference type such as <c>string</c>, a model or a manager.</summary>
    Class,

    /// <summary>The glyph for an interface, which is what most collections come back as.</summary>
    Interface,

    /// <summary>The glyph for an enum.</summary>
    Enum,

    /// <summary>The glyph for a delegate.</summary>
    Delegate
}

/// <summary>Specifies what a script-facing member is, which is how the API browser groups them.</summary>
public enum ScriptApiKind
{
    /// <summary>A property that exposes a live piece of game state, such as the room, the inventory or a manager.</summary>
    State,

    /// <summary>A method to call that does or fetches something.</summary>
    Action,

    /// <summary>A method whose name starts with <c>On</c> and that takes a callback to run later.</summary>
    Event
}

/// <summary>
/// Represents one member a script can write without any using directive or qualification.
/// </summary>
/// <param name="Name">The bare name, which is what a search matches first.</param>
/// <param name="Signature">
/// The C# declaration as it compiles: the <c>static</c> modifier, the return type with its
/// nullability, type parameters and their constraints, and parameters with their modifiers and
/// default values, or for a property its type and accessors.
/// </param>
/// <param name="Insert">
/// The text to put into the editor: the property name, or the method call with empty brackets and
/// a leading <c>await</c> when the method returns a task.
/// </param>
/// <param name="CaretOffset">
/// The caret position within <paramref name="Insert"/>: after a property name, or between the
/// brackets of a method call.
/// </param>
/// <param name="Kind">The kind of member, which decides the group the browser shows it under.</param>
/// <param name="Group">The subsystem the member belongs to, derived from a keyword in its name, or <c>General</c>.</param>
/// <param name="Summary">The first sentence of the member's summary, or an empty string when it has none.</param>
/// <param name="Returns">The first sentence of the member's returns text, or an empty string when it has none.</param>
/// <param name="ReturnType">The return type, or the property type, as it would be written.</param>
/// <param name="ReturnFilter">
/// The return type without its generic arguments, which is what a type filter offers: every
/// <c>Task&lt;T&gt;</c> belongs under one <c>Task</c> rather than under a hundred separate ones.
/// </param>
/// <param name="Glyph">The completion glyph for the return type.</param>
/// <param name="Parameters">
/// The parameter list in brackets for a method, written as in <paramref name="Signature"/>,
/// <c>()</c> when it takes none, and an empty string for a property. It keeps overloads of the
/// same name apart.
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
    /// <summary>Gets whether the member has a parameter list, so the row can leave the brackets out when it has none.</summary>
    /// <remarks>
    /// Every method has a parameter list, even one that takes no arguments, so this is
    /// <see langword="false"/> only for properties.
    /// </remarks>
    public bool HasParameters => Parameters.Length > 0;

    /// <summary>Gets whether the member carries a one-line description, so the row can leave the space out.</summary>
    public bool HasSummary => Summary.Length > 0;

    /// <summary>
    /// Gets whether a search term appears in the name, signature, parameters, group or summary.
    /// </summary>
    /// <param name="term">The term, matched without regard to case. An empty term matches every member.</param>
    /// <returns><see langword="true"/> if the term matches; otherwise, <see langword="false"/>.</returns>
    public bool Matches(string term) =>
        term.Length == 0 ||
        Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Signature.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Parameters.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Group.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        Summary.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets how well a search term fits the member, so the closest match sorts first.</summary>
    /// <param name="term">The term, matched without regard to case.</param>
    /// <returns>
    /// 0 for an exact name match, 1 for a name prefix, 2 for a name substring or an empty term,
    /// 3 for a group match, and 4 otherwise.
    /// </returns>
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
/// Provides every member a script can reach without writing a using directive, read from
/// <see cref="ScriptGlobals"/> itself so it can never drift from what actually compiles.
/// </summary>
/// <remarks>
/// <para>
/// It lists the public properties and methods declared on <see cref="ScriptGlobals"/>, static
/// ones included, which is the same surface the completion list offers, but readable in one go
/// and searchable. Members with the same signature are listed once, and members marked
/// <see cref="ObsoleteAttribute"/> are left out.
/// </para>
/// <para>
/// The list is built on first use and held, because reflecting over the whole globals surface is
/// not free and it cannot change while the process runs.
/// </para>
/// </remarks>
public static class ScriptApiCatalog
{
    private static readonly Lazy<IReadOnlyList<ScriptApiMember>> Members = new(Build);

    /// <summary>Gets every member, ordered by group and then by name.</summary>
    public static IReadOnlyList<ScriptApiMember> All => Members.Value;

    /// <summary>Gets the groups that have members, in alphabetical order.</summary>
    public static IReadOnlyList<string> Groups =>
        [.. All.Select(m => m.Group).Distinct().OrderBy(g => g, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Gets the return types worth filtering by, the most common first.
    /// </summary>
    /// <remarks>
    /// A type that only one member returns is not a filter, it is that member; those are left out
    /// so the row stays short enough to read. Types returned equally often are sorted by name.
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
    /// Searches the members for a term, closest match first.
    /// </summary>
    /// <param name="term">
    /// The text that was typed, trimmed and matched without regard to case; <see langword="null"/>
    /// or empty matches every member.
    /// </param>
    /// <param name="kind">The kind to restrict to, or <see langword="null"/> for every kind.</param>
    /// <param name="group">
    /// The group to restrict to, compared without regard to case, or <see langword="null"/> for
    /// every group.
    /// </param>
    /// <param name="returnType">
    /// The return type filter to restrict to, one of <see cref="ReturnTypes"/> compared exactly,
    /// or <see langword="null"/> for every return type.
    /// </param>
    /// <returns>The matching members, ordered by <see cref="ScriptApiMember.Rank(string)"/> and then by name.</returns>
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
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        Type globals = typeof(ScriptGlobals);
        var members = new List<ScriptApiMember>();

        foreach (PropertyInfo property in globals.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length > 0 || ApiTypeCatalog.ObsoleteMessage(property) is not null)
                continue;
            ApiDoc? doc = ApiTypeCatalog.DocumentationFor(property);
            members.Add(new ScriptApiMember(
                property.Name,
                ReflectionFormat.Property(property),
                property.Name,
                property.Name.Length,
                ScriptApiKind.State,
                GroupOf(property),
                First(doc?.Summary),
                First(doc?.Returns),
                ReflectionFormat.PropertyType(property),
                Bare(property.PropertyType),
                GlyphOf(property.PropertyType),
                ""));
        }

        foreach (MethodInfo method in globals.GetMethods(flags))
        {
            if (method.IsSpecialName || method.DeclaringType != globals || ApiTypeCatalog.ObsoleteMessage(method) is not null)
                continue;

            ApiDoc? doc = ApiTypeCatalog.DocumentationFor(method);
            bool awaited = method.ReturnType.Name.StartsWith("Task", StringComparison.Ordinal);
            string insert = $"{(awaited ? "await " : "")}{method.Name}()";
            members.Add(new ScriptApiMember(
                method.Name,
                ReflectionFormat.Method(method),
                insert,
                insert.Length - 1,
                method.Name.StartsWith("On", StringComparison.Ordinal) && method.GetParameters().Length > 0
                    ? ScriptApiKind.Event
                    : ScriptApiKind.Action,
                GroupOf(method),
                First(doc?.Summary),
                First(doc?.Returns),
                ReflectionFormat.ReturnType(method),
                Bare(method.ReturnType),
                GlyphOf(method.ReturnType),
                ReflectionFormat.ParameterList(method)));
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
        ("Movement", "Movement"),
        ("VariableFx", "Variable Fx"),
        ("Keyboard", "Platform"),
        ("Os", "Platform"),
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
        ("WallItem", "Room"),
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
        string name = ReflectionFormat.FriendlyName(Nullable.GetUnderlyingType(type) ?? type);
        int generic = name.IndexOf('<', StringComparison.Ordinal);
        return generic > 0 ? name[..generic] : name;
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
}
