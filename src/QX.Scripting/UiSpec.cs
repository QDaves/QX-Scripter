using System.Globalization;
using System.Text.RegularExpressions;

namespace Qx.Scripting;

public enum UiFieldKind
{
    Int,
    Number,
    String,
    Text,
    Bool,
    Select,
    File,
    Slider,
    Color
}

/// <summary>How prominent a button is.</summary>
public enum UiButtonStyle
{
    /// <summary>The ordinary outlined button.</summary>
    Normal,
    /// <summary>The filled button. The first declared button is this unless it says otherwise.</summary>
    Primary,
    /// <summary>Text only, for secondary actions that should not compete.</summary>
    Quiet,
    /// <summary>Tinted for something destructive.</summary>
    Danger
}

/// <summary>How a row distributes the space its children do not claim.</summary>
public enum UiRowAlign
{
    /// <summary>Children keep their natural width and sit to the left.</summary>
    Start,
    /// <summary>Children keep their natural width and sit in the middle.</summary>
    Center,
    /// <summary>Children keep their natural width and sit to the right.</summary>
    End,
    /// <summary>Children share the row evenly unless one of them sets a width.</summary>
    Stretch
}

/// <summary>
/// The attributes a directive carried, as written.
/// </summary>
/// <remarks>
/// Kept as text so an unknown attribute is preserved rather than dropped: the parser is shared with
/// a renderer that may learn about it later, and refusing what it does not yet understand would
/// make the two versions incompatible for no gain.
/// </remarks>
public sealed class UiAttributes
{
    private readonly Dictionary<string, string> _values;

    internal UiAttributes(Dictionary<string, string> values) => _values = values;

    /// <summary>An empty set, for directives that carried none.</summary>
    public static UiAttributes Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Every attribute, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>The attribute as text, or <see langword="null"/> when it was not written.</summary>
    /// <param name="name">The attribute name.</param>
    public string? Text(string name) => _values.GetValueOrDefault(name);

    /// <summary>The attribute as a number.</summary>
    /// <param name="name">The attribute name.</param>
    public double? Number(string name) =>
        _values.TryGetValue(name, out string? value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;

    /// <summary>
    /// The attribute as a switch. A bare attribute with no value counts as true, so
    /// <c>wrap</c> and <c>wrap=true</c> mean the same thing.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public bool? Flag(string name)
    {
        if (!_values.TryGetValue(name, out string? value))
            return null;
        if (value.Length == 0)
            return true;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}

public sealed record UiField(
    UiFieldKind Kind,
    string Name,
    string Label,
    string Default,
    IReadOnlyList<string> Options,
    double? Min,
    double? Max,
    string? Section = null,
    UiAttributes? Attributes = null)
{
    /// <summary>The attributes the directive carried; never null.</summary>
    public UiAttributes Attr => Attributes ?? UiAttributes.Empty;
}

public sealed record UiOutput(string Name, string Label, UiAttributes? Attributes = null)
{
    /// <inheritdoc cref="UiField.Attr"/>
    public UiAttributes Attr => Attributes ?? UiAttributes.Empty;

    /// <summary>The height the box asks for, or null for the renderer's own.</summary>
    public double? Height => Attr.Number("height");

    /// <summary>Whether long lines wrap instead of scrolling sideways.</summary>
    public bool Wrap => Attr.Flag("wrap") ?? false;

    /// <summary>Whether the text is drawn in the code font. On unless turned off.</summary>
    public bool Monospace => Attr.Flag("mono") ?? true;

    /// <summary>
    /// Whether the box carries its own clear and copy actions. On unless turned off, because a box
    /// a script fills is a box someone will want to empty or take away.
    /// </summary>
    public bool Toolbar => Attr.Flag("toolbar") ?? true;
}

public sealed record UiButton(string Name, string Label, UiAttributes? Attributes = null)
{
    /// <inheritdoc cref="UiField.Attr"/>
    public UiAttributes Attr => Attributes ?? UiAttributes.Empty;

    /// <summary>How prominent the button is, or null to let position decide.</summary>
    public UiButtonStyle? Style => Attr.Text("style")?.ToLowerInvariant() switch
    {
        "primary" => UiButtonStyle.Primary,
        "quiet" => UiButtonStyle.Quiet,
        "danger" => UiButtonStyle.Danger,
        "normal" => UiButtonStyle.Normal,
        _ => null
    };
}

/// <summary>A piece of a panel.</summary>
public abstract record UiNode
{
    /// <summary>The attributes the directive carried; never null.</summary>
    public UiAttributes Attr { get; init; } = UiAttributes.Empty;

    /// <summary>
    /// How much of a row's spare width this takes, relative to its siblings. Null means it keeps
    /// its natural width.
    /// </summary>
    public double? Grow => Attr.Number("grow");

    /// <summary>A fixed width in pixels, or null to size to content.</summary>
    public double? Width => Attr.Number("width");
}

/// <summary>An input.</summary>
public sealed record UiFieldNode(UiField Field) : UiNode;

/// <summary>A box a script writes lines into.</summary>
public sealed record UiOutputNode(UiOutput Output) : UiNode;

/// <summary>A button that starts a run.</summary>
public sealed record UiButtonNode(UiButton Button) : UiNode;

/// <summary>Static text.</summary>
public sealed record UiLabelNode(string Text) : UiNode;

/// <summary>A horizontal rule.</summary>
public sealed record UiSeparatorNode : UiNode;

/// <summary>Empty space.</summary>
public sealed record UiSpacerNode : UiNode
{
    /// <summary>How tall the gap is.</summary>
    public double Height => Attr.Number("height") ?? 12;
}

/// <summary>A progress bar the script drives.</summary>
public sealed record UiProgressNode(string Name, string Label) : UiNode;

/// <summary>A single line of text the script replaces as it goes.</summary>
/// <param name="Name">The line's name, for <c>Ui.Status</c>.</param>
/// <param name="Label">The caption beside it.</param>
/// <param name="Initial">
/// What it says before the script writes anything. The quoted text is the caption, so a starting
/// value is written as a default: <c>//@ui:status state "Stage" ="waiting"</c>.
/// </param>
public sealed record UiStatusNode(string Name, string Label, string Initial = "") : UiNode;

/// <summary>
/// A grid of rows a script fills as it goes.
/// </summary>
/// <remarks>
/// The columns come from the directive's bracket list, so a table is declared the way a select
/// declares its options. A row with more cells than there are columns keeps the extras out of
/// sight rather than dropping them, because a script that adds a column later should not have to
/// rewrite what it already wrote.
/// </remarks>
/// <param name="Name">The table's name, for <c>Ui.AddRow</c> and <c>Ui.Clear</c>.</param>
/// <param name="Label">The caption above it.</param>
/// <param name="Columns">The column headings.</param>
public sealed record UiTableNode(string Name, string Label, IReadOnlyList<string> Columns) : UiNode
{
    /// <summary>How tall the grid is.</summary>
    public double Height => Attr.Number("height") ?? 220;

    /// <summary>Whether a row can be selected, which a script reads back with <c>Ui.String</c>.</summary>
    public bool Selectable => Attr.Flag("selectable") ?? true;

    /// <summary>Whether the grid carries its own clear and copy actions.</summary>
    public bool Toolbar => Attr.Flag("toolbar") ?? true;
}

/// <summary>A heading with a rule, kept for panels written against the older grammar.</summary>
public sealed record UiSectionNode(string Title) : UiNode;

/// <summary>Children laid out side by side.</summary>
public sealed record UiRowNode : UiNode
{
    /// <summary>What sits in the row, left to right.</summary>
    public List<UiNode> Children { get; } = [];

    /// <summary>The gap between children.</summary>
    public double Gap => Attr.Number("gap") ?? 12;

    /// <summary>How the row distributes width its children do not claim.</summary>
    public UiRowAlign Align => Attr.Text("align")?.ToLowerInvariant() switch
    {
        "center" => UiRowAlign.Center,
        "end" or "right" => UiRowAlign.End,
        "stretch" => UiRowAlign.Stretch,
        _ => UiRowAlign.Start
    };
}

/// <summary>Children inside a titled box that can be folded away.</summary>
public sealed record UiGroupNode(string Title) : UiNode
{
    /// <summary>What the group holds.</summary>
    public List<UiNode> Children { get; } = [];

    /// <summary>Whether the group starts folded.</summary>
    public bool Collapsed => Attr.Flag("collapsed") ?? false;
}

public sealed partial class UiSpec
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// The panel as written, including its rows and groups.
    /// </summary>
    /// <remarks>
    /// <see cref="Fields"/>, <see cref="Outputs"/> and <see cref="Buttons"/> are the same things
    /// flattened, in declaration order. A renderer walks the tree; everything that only needs to
    /// look a name up reads the flat lists.
    /// </remarks>
    public List<UiNode> Nodes { get; } = [];

    public List<UiField> Fields { get; } = [];
    public List<UiOutput> Outputs { get; } = [];
    public List<UiButton> Buttons { get; } = [];

    /// <summary>Progress bars declared in the panel.</summary>
    public List<UiProgressNode> Progresses { get; } = [];

    /// <summary>Status lines declared in the panel.</summary>
    public List<UiStatusNode> Statuses { get; } = [];

    /// <summary>Tables declared in the panel.</summary>
    public List<UiTableNode> Tables { get; } = [];

    public bool HasUi =>
        Nodes.Count > 0 || Title.Length > 0 || Description.Length > 0;

    [GeneratedRegex(@"^\s*//\s*@ui:(?<key>\w+)\b\s*(?<rest>.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex("\"(?<v>[^\"]*)\"")]
    private static partial Regex QuotedRegex();

    [GeneratedRegex(@"\[(?<v>[^\]]*)\]")]
    private static partial Regex BracketRegex();

    [GeneratedRegex(@"(?:^|\s)=\s*(?:""(?<q>[^""]*)""|(?<b>[^\s]+))")]
    private static partial Regex DefaultRegex();

    [GeneratedRegex(@"\b(?<k>[A-Za-z_]\w*)\s*=\s*(?:""(?<q>[^""]*)""|(?<b>[^\s\]]+))")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"^(?<n>[A-Za-z_]\w*)")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"(?<k>[A-Za-z_]\w*)")]
    private static partial Regex BareFlagRegex();

    public static UiSpec Parse(string code)
    {
        var spec = new UiSpec();
        string? pendingSection = null;

        // Containers nest, so the parser keeps a stack and appends to whatever is open. An
        // unclosed row or group is closed by the end of the file rather than discarded, because a
        // panel that is missing its last line should still render.
        var open = new Stack<List<UiNode>>();
        open.Push(spec.Nodes);

        void Add(UiNode node) => open.Peek().Add(node);

        foreach (string line in code.Split('\n'))
        {
            Match m = DirectiveRegex().Match(line);
            if (!m.Success)
                continue;

            string key = m.Groups["key"].Value.ToLowerInvariant();
            string rest = m.Groups["rest"].Value.Trim();
            UiAttributes attributes = ParseAttributes(rest);

            switch (key)
            {
                case "title":
                    spec.Title = Unquote(rest);
                    break;

                case "desc" or "description":
                    spec.Description = Unquote(rest);
                    break;

                case "section":
                    // The older grammar attached a section to the next field. It now stands on its
                    // own as a heading, which is what it always looked like, and the pending value
                    // is still carried so a field's Section keeps reporting it.
                    pendingSection = Unquote(rest);
                    Add(new UiSectionNode(pendingSection));
                    break;

                case "row":
                {
                    var row = new UiRowNode { Attr = attributes };
                    Add(row);
                    open.Push(row.Children);
                    break;
                }

                case "endrow" or "end":
                    if (open.Count > 1)
                        open.Pop();
                    break;

                case "group":
                {
                    var group = new UiGroupNode(Unquote(StripAttributes(rest))) { Attr = attributes };
                    Add(group);
                    open.Push(group.Children);
                    break;
                }

                case "endgroup":
                    if (open.Count > 1)
                        open.Pop();
                    break;

                case "separator" or "divider":
                    Add(new UiSeparatorNode { Attr = attributes });
                    break;

                case "spacer" or "space":
                    Add(new UiSpacerNode { Attr = attributes });
                    break;

                case "label":
                    Add(new UiLabelNode(Unquote(StripAttributes(rest))) { Attr = attributes });
                    break;

                case "progress":
                    if (ParseNameLabel(rest) is var (pName, pLabel) && pName.Length > 0)
                    {
                        var progress = new UiProgressNode(pName, pLabel) { Attr = attributes };
                        Add(progress);
                        spec.Progresses.Add(progress);
                    }
                    break;

                case "status":
                    if (ParseNameLabel(rest) is var (sName, sLabel) && sName.Length > 0)
                    {
                        var status = new UiStatusNode(sName, sLabel, ParseDefault(rest))
                        {
                            Attr = attributes
                        };
                        Add(status);
                        spec.Statuses.Add(status);
                    }
                    break;

                case "table":
                    if (ParseNameLabel(rest) is var (tName, tLabel) && tName.Length > 0)
                    {
                        List<string> columns = [];
                        Match tBracket = BracketRegex().Match(StripAttributes(rest));
                        if (tBracket.Success)
                        {
                            columns = tBracket.Groups["v"].Value
                                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                .ToList();
                        }
                        var table = new UiTableNode(tName, tLabel, columns) { Attr = attributes };
                        Add(table);
                        spec.Tables.Add(table);
                    }
                    break;

                case "output" or "log":
                    if (ParseNameLabel(rest) is var (oName, oLabel) && oName.Length > 0)
                    {
                        var output = new UiOutput(oName, oLabel, attributes);
                        spec.Outputs.Add(output);
                        Add(new UiOutputNode(output) { Attr = attributes });
                    }
                    break;

                case "button":
                    if (ParseNameLabel(rest) is var (bName, bLabel) && bName.Length > 0)
                    {
                        var button = new UiButton(bName, bLabel, attributes);
                        spec.Buttons.Add(button);
                        Add(new UiButtonNode(button) { Attr = attributes });
                    }
                    break;

                case "int" or "number" or "string" or "text" or "bool" or "select" or "file" or "slider" or "color":
                    UiField? field = ParseField(key, rest, pendingSection, attributes);
                    if (field is not null)
                    {
                        spec.Fields.Add(field);
                        Add(new UiFieldNode(field) { Attr = attributes });
                        pendingSection = null;
                    }
                    break;
            }
        }

        return spec;
    }

    private static UiField? ParseField(string kind, string rest, string? section, UiAttributes attributes)
    {
        Match nameMatch = NameRegex().Match(rest);
        if (!nameMatch.Success)
            return null;
        string name = nameMatch.Groups["n"].Value;

        // Read from the line with its attributes taken out, so a quoted attribute value cannot be
        // mistaken for the label and a bracket inside one cannot be mistaken for an option list.
        // The default is left in place, because `="text"` carries no attribute name and has always
        // stood in for a label that was not written.
        string plain = StripAttributes(rest);

        Match label = QuotedRegex().Match(plain);
        string labelText = label.Success ? label.Groups["v"].Value : Humanize(name);

        List<string> options = [];
        Match bracket = BracketRegex().Match(plain);
        if (bracket.Success)
            options = bracket.Groups["v"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

        string def = ParseDefault(rest);

        double? min = attributes.Number("min");
        double? max = attributes.Number("max");

        UiFieldKind fieldKind = kind switch
        {
            "int" => UiFieldKind.Int,
            "number" => UiFieldKind.Number,
            "text" => UiFieldKind.Text,
            "bool" => UiFieldKind.Bool,
            "select" => UiFieldKind.Select,
            "file" => UiFieldKind.File,
            "slider" => UiFieldKind.Slider,
            "color" => UiFieldKind.Color,
            _ => UiFieldKind.String
        };

        if (fieldKind == UiFieldKind.Select && def.Length == 0 && options.Count > 0)
            def = options[0];

        return new UiField(fieldKind, name, labelText, def, options, min, max, section, attributes);
    }

    private static (string, string) ParseNameLabel(string rest)
    {
        Match nameMatch = NameRegex().Match(rest);
        if (!nameMatch.Success)
            return ("", "");
        string name = nameMatch.Groups["n"].Value;
        Match label = QuotedRegex().Match(StripAttributes(rest));
        return (name, label.Success ? label.Groups["v"].Value : Humanize(name));
    }

    /// <summary>The <c>=value</c> a directive carried, or an empty string.</summary>
    /// <param name="rest">The directive's text after its key.</param>
    private static string ParseDefault(string rest)
    {
        Match m = DefaultRegex().Match(rest);
        if (!m.Success)
            return "";
        return m.Groups["q"].Success ? m.Groups["q"].Value : m.Groups["b"].Value;
    }

    private static UiAttributes ParseAttributes(string rest)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttributeRegex().Matches(rest))
        {
            string key = m.Groups["k"].Value;
            values[key] = m.Groups["q"].Success ? m.Groups["q"].Value : m.Groups["b"].Value;
        }

        // A flag may be written bare, so `wrap` and `wrap=true` agree. Finding those means looking
        // at what is left once everything that is not a flag is out of the way: the quoted label,
        // the option list, the pairs matched above, and the directive's own name. Scanning the raw
        // line instead would take the name itself for a flag.
        string residue = QuotedRegex().Replace(rest, " ");
        residue = BracketRegex().Replace(residue, " ");
        residue = AttributeRegex().Replace(residue, " ");
        residue = NameRegex().Replace(residue.TrimStart(), " ");

        foreach (Match m in BareFlagRegex().Matches(residue))
        {
            string key = m.Groups["k"].Value;
            if (!values.ContainsKey(key))
                values[key] = "";
        }

        return values.Count == 0 ? UiAttributes.Empty : new UiAttributes(values);
    }

    /// <summary>
    /// Removes <c>key=value</c> pairs so what is left is the directive's own text.
    /// </summary>
    /// <remarks>
    /// Needed where a directive takes free text rather than a name, such as a group title, so that
    /// <c>//@ui:group "Options" collapsed=true</c> does not end up titled with its own attribute.
    /// </remarks>
    private static string StripAttributes(string rest) =>
        AttributeRegex().Replace(rest, "").Trim();

    private static string Unquote(string s)
    {
        s = s.Trim();
        return s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;
    }

    private static string Humanize(string name)
    {
        string spaced = Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ").Replace('_', ' ');
        return spaced.Length == 0 ? name : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
