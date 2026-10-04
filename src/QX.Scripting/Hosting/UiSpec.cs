using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Qx.Scripting.Hosting;

/// <summary>Specifies the kind of input a panel field directive declares.</summary>
public enum UiFieldKind
{
    /// <summary>A whole number input, declared with <c>//@ui:int</c>.</summary>
    Int,
    /// <summary>A decimal number input, declared with <c>//@ui:number</c>.</summary>
    Number,
    /// <summary>A single line text input, declared with <c>//@ui:string</c>.</summary>
    String,
    /// <summary>A multi-line text input, declared with <c>//@ui:text</c>.</summary>
    Text,
    /// <summary>A checkbox, declared with <c>//@ui:bool</c>.</summary>
    Bool,
    /// <summary>A choice from the bracket list of options, declared with <c>//@ui:select</c>.</summary>
    Select,
    /// <summary>A file picker that holds the chosen path, declared with <c>//@ui:file</c>.</summary>
    File,
    /// <summary>A slider limited to the <c>min</c> and <c>max</c> range, declared with <c>//@ui:slider</c>.</summary>
    Slider,
    /// <summary>A color input that holds a <c>#RRGGBB</c> value, declared with <c>//@ui:color</c>.</summary>
    Color
}

/// <summary>Specifies how prominent a panel button is.</summary>
public enum UiButtonStyle
{
    /// <summary>The ordinary outlined button.</summary>
    Normal,
    /// <summary>The filled button.</summary>
    /// <remarks>The first declared button uses this style unless it sets another one.</remarks>
    Primary,
    /// <summary>A text only button, for secondary actions that should not compete.</summary>
    Quiet,
    /// <summary>A tinted button, for something destructive.</summary>
    Danger
}

/// <summary>Specifies how a row distributes the space its children do not claim.</summary>
public enum UiRowAlign
{
    /// <summary>Children at their natural width, aligned to the left.</summary>
    Start,
    /// <summary>Children at their natural width, aligned to the middle.</summary>
    Center,
    /// <summary>Children at their natural width, aligned to the right.</summary>
    End,
    /// <summary>Children sharing the row evenly unless one of them sets a width.</summary>
    Stretch
}

/// <summary>
/// Represents the attributes a directive carried, as written.
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

    /// <summary>Gets an empty set, for directives that carried none.</summary>
    public static UiAttributes Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Gets every attribute, keyed case-insensitively.</summary>
    /// <remarks>A bare attribute written without a value is stored with an empty string.</remarks>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Gets the attribute as text.</summary>
    /// <param name="name">The attribute name, matched case-insensitively.</param>
    /// <returns>
    /// The value as written, an empty string for a bare attribute, or <see langword="null"/> when it
    /// was not written.
    /// </returns>
    public string? Text(string name) => _values.GetValueOrDefault(name);

    /// <summary>Gets the attribute as a number.</summary>
    /// <param name="name">The attribute name, matched case-insensitively.</param>
    /// <returns>
    /// The value parsed with the invariant culture, or <see langword="null"/> when it was not written
    /// or is not a number.
    /// </returns>
    public double? Number(string name) =>
        _values.TryGetValue(name, out string? value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;

    /// <summary>
    /// Gets the attribute as a switch.
    /// </summary>
    /// <remarks>
    /// A bare attribute with no value counts as true, so <c>wrap</c> and <c>wrap=true</c> mean the
    /// same thing.
    /// </remarks>
    /// <param name="name">The attribute name, matched case-insensitively.</param>
    /// <returns>
    /// <see langword="true"/> when the value is empty, <c>true</c> in any case or <c>1</c>;
    /// <see langword="false"/> for any other value; <see langword="null"/> when it was not written.
    /// </returns>
    public bool? Flag(string name)
    {
        if (!_values.TryGetValue(name, out string? value))
            return null;
        if (value.Length == 0)
            return true;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}

/// <summary>Represents an input field a panel declares.</summary>
/// <param name="Kind">The kind of input.</param>
/// <param name="Name">The name the script reads the value by.</param>
/// <param name="Label">The caption shown with the input, derived from the name when none was written.</param>
/// <param name="Default">
/// The starting value, or an empty string when none was written. A select field without one starts
/// on its first option.
/// </param>
/// <param name="Options">The bracket list of options that a select field offers, empty when none was written.</param>
/// <param name="Min">The lower bound from the <c>min</c> attribute, or <see langword="null"/> when it was not written.</param>
/// <param name="Max">The upper bound from the <c>max</c> attribute, or <see langword="null"/> when it was not written.</param>
/// <param name="Section">
/// The title of the preceding <c>//@ui:section</c> when this is the first field after it; otherwise,
/// <see langword="null"/>.
/// </param>
/// <param name="Attributes">The attributes the directive carried, or <see langword="null"/> for none.</param>
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
    /// <summary>Gets the attributes the directive carried, never <see langword="null"/>.</summary>
    public UiAttributes Attr => Attributes ?? UiAttributes.Empty;
}

/// <summary>Represents an output box a panel declares.</summary>
/// <param name="Name">The name the script writes to with <see cref="ScriptUi.Log(string, object)"/>.</param>
/// <param name="Label">The caption shown with the box.</param>
/// <param name="Attributes">The attributes the directive carried, or <see langword="null"/> for none.</param>
public sealed record UiOutput(string Name, string Label, UiAttributes? Attributes = null)
{
    /// <inheritdoc cref="UiField.Attr"/>
    public UiAttributes Attr => Attributes ?? UiAttributes.Empty;

    /// <summary>Gets the height in pixels the box asks for, or <see langword="null"/> for the renderer's own.</summary>
    public double? Height => Attr.Number("height");

    /// <summary>Gets whether long lines wrap instead of scrolling sideways.</summary>
    /// <remarks>Off unless the <c>wrap</c> attribute turns it on.</remarks>
    public bool Wrap => Attr.Flag("wrap") ?? false;

    /// <summary>Gets whether the text is drawn in the code font.</summary>
    /// <remarks>On unless <c>mono=false</c> turns it off.</remarks>
    public bool Monospace => Attr.Flag("mono") ?? true;

    /// <summary>
    /// Gets whether the box carries its own clear and copy actions.
    /// </summary>
    /// <remarks>
    /// On unless <c>toolbar=false</c> turns it off, because a box a script fills is a box someone
    /// will want to empty or take away.
    /// </remarks>
    public bool Toolbar => Attr.Flag("toolbar") ?? true;
}

/// <summary>Represents a button a panel declares.</summary>
/// <param name="Name">
/// The name that <see cref="ScriptUi.OnClick(string, Func{Task})"/> and
/// <see cref="ScriptUi.Clicked(string)"/> refer to.
/// </param>
/// <param name="Label">The caption on the button.</param>
/// <param name="Attributes">The attributes the directive carried, or <see langword="null"/> for none.</param>
public sealed record UiButton(string Name, string Label, UiAttributes? Attributes = null)
{
    /// <inheritdoc cref="UiField.Attr"/>
    public UiAttributes Attr => Attributes ?? UiAttributes.Empty;

    /// <summary>Gets how prominent the button is, or <see langword="null"/> to let its position decide.</summary>
    /// <remarks>
    /// Read from the <c>style</c> attribute; a value other than <c>normal</c>, <c>primary</c>,
    /// <c>quiet</c> or <c>danger</c> counts as not written.
    /// </remarks>
    public UiButtonStyle? Style => Attr.Text("style")?.ToLowerInvariant() switch
    {
        "primary" => UiButtonStyle.Primary,
        "quiet" => UiButtonStyle.Quiet,
        "danger" => UiButtonStyle.Danger,
        "normal" => UiButtonStyle.Normal,
        _ => null
    };
}

/// <summary>Represents a piece of a panel.</summary>
public abstract record UiNode
{
    /// <summary>Gets the attributes the directive carried, never <see langword="null"/>.</summary>
    public UiAttributes Attr { get; init; } = UiAttributes.Empty;

    /// <summary>
    /// Gets how much of a row's spare width the node takes, relative to its siblings.
    /// </summary>
    /// <remarks><see langword="null"/> means it keeps its natural width.</remarks>
    public double? Grow => Attr.Number("grow");

    /// <summary>Gets a fixed width in pixels, or <see langword="null"/> to size to content.</summary>
    public double? Width => Attr.Number("width");
}

/// <summary>Represents an input field in the panel tree.</summary>
/// <param name="Field">The field the directive declared.</param>
public sealed record UiFieldNode(UiField Field) : UiNode;

/// <summary>Represents a box a script writes lines into.</summary>
/// <param name="Output">The output box the directive declared.</param>
public sealed record UiOutputNode(UiOutput Output) : UiNode;

/// <summary>Represents a button in the panel tree.</summary>
/// <remarks>
/// Pressing it calls the handlers registered with <see cref="ScriptUi.OnClick(string, Func{Task})"/>,
/// or starts a run when the script registered none.
/// </remarks>
/// <param name="Button">The button the directive declared.</param>
public sealed record UiButtonNode(UiButton Button) : UiNode;

/// <summary>Represents static text.</summary>
/// <param name="Text">The text shown.</param>
public sealed record UiLabelNode(string Text) : UiNode;

/// <summary>Represents a horizontal rule.</summary>
public sealed record UiSeparatorNode : UiNode;

/// <summary>Represents empty space.</summary>
public sealed record UiSpacerNode : UiNode
{
    /// <summary>Gets how tall the gap is in pixels, 12 unless the <c>height</c> attribute sets it.</summary>
    public double Height => Attr.Number("height") ?? 12;
}

/// <summary>Represents a progress bar the script drives.</summary>
/// <param name="Name">The bar's name, for <see cref="ScriptUi.Progress(string, double)"/>.</param>
/// <param name="Label">The caption beside it.</param>
public sealed record UiProgressNode(string Name, string Label) : UiNode;

/// <summary>Represents a single line of text the script replaces as it goes.</summary>
/// <param name="Name">The line's name, for <c>Ui.Status</c>.</param>
/// <param name="Label">The caption beside it.</param>
/// <param name="Initial">
/// The text shown before the script writes anything. The quoted text is the caption, so a starting
/// value is written as a default: <c>//@ui:status state "Stage" ="waiting"</c>.
/// </param>
public sealed record UiStatusNode(string Name, string Label, string Initial = "") : UiNode;

/// <summary>
/// Represents a grid of rows a script fills as it goes.
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
    /// <summary>Gets how tall the grid is in pixels, 220 unless the <c>height</c> attribute sets it.</summary>
    public double Height => Attr.Number("height") ?? 220;

    /// <summary>Gets whether a row can be selected, which a script reads back with <c>Ui.String</c>.</summary>
    /// <remarks>
    /// On unless <c>selectable=false</c> turns it off. The selected row reads back as its cells
    /// joined with tabs.
    /// </remarks>
    public bool Selectable => Attr.Flag("selectable") ?? true;

    /// <summary>Gets whether the grid carries its own clear and copy actions.</summary>
    /// <remarks>On unless <c>toolbar=false</c> turns it off.</remarks>
    public bool Toolbar => Attr.Flag("toolbar") ?? true;
}

/// <summary>Represents a heading with a rule, kept for panels written against the older grammar.</summary>
/// <param name="Title">The heading text.</param>
public sealed record UiSectionNode(string Title) : UiNode;

/// <summary>Represents a row that lays out its children side by side.</summary>
public sealed record UiRowNode : UiNode
{
    /// <summary>Gets what sits in the row, left to right.</summary>
    public List<UiNode> Children { get; } = [];

    /// <summary>Gets the gap between children in pixels, 12 unless the <c>gap</c> attribute sets it.</summary>
    public double Gap => Attr.Number("gap") ?? 12;

    /// <summary>Gets how the row distributes width its children do not claim.</summary>
    /// <remarks>
    /// Read from the <c>align</c> attribute, where <c>right</c> means the same as <c>end</c>. A
    /// missing or unknown value gives <see cref="UiRowAlign.Start"/>.
    /// </remarks>
    public UiRowAlign Align => Attr.Text("align")?.ToLowerInvariant() switch
    {
        "center" => UiRowAlign.Center,
        "end" or "right" => UiRowAlign.End,
        "stretch" => UiRowAlign.Stretch,
        _ => UiRowAlign.Start
    };
}

/// <summary>Represents a titled box of children that can be folded away.</summary>
/// <param name="Title">The title shown on the box.</param>
public sealed record UiGroupNode(string Title) : UiNode
{
    /// <summary>Gets what the group holds.</summary>
    public List<UiNode> Children { get; } = [];

    /// <summary>Gets whether the group starts folded.</summary>
    public bool Collapsed => Attr.Flag("collapsed") ?? false;
}

/// <summary>Represents how the script's own output is shown under its panel.</summary>
/// <param name="Collapsed"><see langword="true"/> when it starts folded; otherwise, <see langword="false"/>.</param>
/// <param name="Height">The starting height in pixels, or <see langword="null"/> for the default.</param>
public sealed record UiConsole(bool Collapsed, double? Height);

/// <summary>Represents how the panel's content sits on its page.</summary>
/// <param name="Centered"><see langword="true"/> when the content is centered; <see langword="false"/> when it is left-aligned.</param>
/// <param name="Width">
/// The widest the content grows in pixels, <see langword="null"/> for the default and
/// <see cref="double.PositiveInfinity"/> to fill the page.
/// </param>
public sealed record UiLayout(bool Centered, double? Width);

/// <summary>Describes a panel directive, for editors that offer it while a script is written.</summary>
/// <param name="Key">The name written after <c>//@ui:</c>.</param>
/// <param name="Syntax">The directive as it is written, with its arguments.</param>
/// <param name="Summary">What the directive declares.</param>
/// <param name="Aliases">Other names the parser accepts for it, empty when there are none.</param>
public sealed record UiDirective(string Key, string Syntax, string Summary, IReadOnlyList<string> Aliases);

internal sealed record UiUnknownDirective(string Key, int Offset);

/// <summary>Represents the panel a script declares with <c>//@ui:</c> directives.</summary>
public sealed partial class UiSpec
{
    /// <summary>Gets or sets the panel title from <c>//@ui:title</c>, or an empty string when none was written.</summary>
    public string Title { get; set; } = "";
    /// <summary>
    /// Gets or sets the panel description from <c>//@ui:desc</c> or <c>//@ui:description</c>, or an
    /// empty string when none was written.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the script only works with its panel, as <c>//@ui:required</c> declares.
    /// </summary>
    /// <remarks>
    /// Such a script always runs in panel mode: starting it from the editor opens the panel, and a
    /// run without a panel is refused instead of finishing at once with default values.
    /// </remarks>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets how the script's own output is shown under the panel, as <c>//@ui:console</c>
    /// declares.
    /// </summary>
    /// <remarks><see langword="null"/> when the panel hides it.</remarks>
    public UiConsole? Console { get; set; }

    /// <summary>
    /// Gets or sets where the panel's content sits, as <c>//@ui:layout</c> declares.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> means the default: left-aligned at the theme's width.
    /// </remarks>
    public UiLayout? Layout { get; set; }

    /// <summary>
    /// Gets the panel as written, including its rows and groups.
    /// </summary>
    /// <remarks>
    /// <see cref="Fields"/>, <see cref="Outputs"/> and <see cref="Buttons"/> are the same things
    /// flattened, in declaration order. A renderer walks the tree; everything that only needs to
    /// look a name up reads the flat lists.
    /// </remarks>
    public List<UiNode> Nodes { get; } = [];

    /// <summary>Gets every input field in declaration order, flattened out of rows and groups.</summary>
    public List<UiField> Fields { get; } = [];
    /// <summary>Gets every output box in declaration order, flattened out of rows and groups.</summary>
    public List<UiOutput> Outputs { get; } = [];
    /// <summary>Gets every button in declaration order, flattened out of rows and groups.</summary>
    public List<UiButton> Buttons { get; } = [];

    /// <summary>Gets the progress bars declared in the panel.</summary>
    public List<UiProgressNode> Progresses { get; } = [];

    /// <summary>Gets the status lines declared in the panel.</summary>
    public List<UiStatusNode> Statuses { get; } = [];

    /// <summary>Gets the tables declared in the panel.</summary>
    public List<UiTableNode> Tables { get; } = [];

    private delegate void DirectiveReader(PanelBuilder panel, string rest, UiAttributes attributes);

    private static readonly (UiDirective Directive, DirectiveReader Read)[] grammar =
    [
        (new("title", "//@ui:title Text", "The panel title.", []),
            static (panel, rest, _) => panel.Spec.Title = Unquote(rest)),
        (new("desc", "//@ui:desc Text", "A line of text under the title.", ["description"]),
            static (panel, rest, _) => panel.Spec.Description = Unquote(rest)),
        (new("required", "//@ui:required", "The script only works with its panel. Runs without one are refused.", []),
            static (panel, _, _) => panel.Spec.Required = true),
        (new("console", "//@ui:console [collapsed] [height=N]", "Shows the script's own output under the panel.", []),
            static (panel, rest, _) =>
            {
                UiAttributes options = ParseAttributes(rest, named: false);
                panel.Spec.Console = new UiConsole(options.Flag("collapsed") is true, options.Number("height"));
            }),
        (new("layout", "//@ui:layout [center] [width=N | full]", "Places the content. Left-aligned at 720 pixels by default.", []),
            static (panel, rest, _) =>
            {
                UiAttributes options = ParseAttributes(rest, named: false);
                double? width = options.Flag("full") is true
                    ? double.PositiveInfinity
                    : options.Number("width") is double value && value > 0 ? value : null;
                panel.Spec.Layout = new UiLayout(options.Flag("center") is true, width);
            }),
        (new("string", "//@ui:string name \"Label\" =\"default\"", "A single line text input, read as string.", []),
            Field(UiFieldKind.String)),
        (new("text", "//@ui:text name \"Label\"", "A multi-line text input, read as string.", []),
            Field(UiFieldKind.Text)),
        (new("int", "//@ui:int name \"Label\" =5 min=0 max=10", "A whole number input, read as int.", []),
            Field(UiFieldKind.Int)),
        (new("number", "//@ui:number name \"Label\" =1.5", "A decimal number input, read as double.", []),
            Field(UiFieldKind.Number)),
        (new("slider", "//@ui:slider name \"Label\" =50 min=0 max=100", "A slider limited to min and max, read as double.", []),
            Field(UiFieldKind.Slider)),
        (new("bool", "//@ui:bool name \"Label\" =true", "A checkbox, read as bool.", []),
            Field(UiFieldKind.Bool)),
        (new("select", "//@ui:select name \"Label\" [A,B,C] =A", "A choice from the bracket list, read as string. Starts on the first option without a default.", []),
            Field(UiFieldKind.Select)),
        (new("file", "//@ui:file name \"Label\"", "A file picker, read as the chosen path.", []),
            Field(UiFieldKind.File)),
        (new("color", "//@ui:color name \"Label\" =\"#8EA2FF\"", "A color input, read as a #RRGGBB string.", []),
            Field(UiFieldKind.Color)),
        (new("label", "//@ui:label \"Text\"", "Static text.", []),
            static (panel, rest, attributes) => panel.Add(new UiLabelNode(Unquote(StripAttributes(rest))) { Attr = attributes })),
        (new("button", "//@ui:button name \"Label\" [style=primary|normal|quiet|danger]", "A button. The first one is filled.", []),
            static (panel, rest, attributes) =>
            {
                if (ParseNameLabel(rest) is not ({ Length: > 0 } name, var label))
                    return;
                var button = new UiButton(name, label, attributes);
                panel.Spec.Buttons.Add(button);
                panel.Add(new UiButtonNode(button) { Attr = attributes });
            }),
        (new("output", "//@ui:output name \"Label\" [height=200] [wrap] [mono=false] [toolbar=false]", "A text box the script writes to.", ["log"]),
            static (panel, rest, attributes) =>
            {
                if (ParseNameLabel(rest) is not ({ Length: > 0 } name, var label))
                    return;
                var output = new UiOutput(name, label, attributes);
                panel.Spec.Outputs.Add(output);
                panel.Add(new UiOutputNode(output) { Attr = attributes });
            }),
        (new("progress", "//@ui:progress name \"Label\"", "A progress bar.", []),
            static (panel, rest, attributes) =>
            {
                if (ParseNameLabel(rest) is not ({ Length: > 0 } name, var label))
                    return;
                var progress = new UiProgressNode(name, label) { Attr = attributes };
                panel.Spec.Progresses.Add(progress);
                panel.Add(progress);
            }),
        (new("status", "//@ui:status name \"Label\" =\"text\"", "A status line.", []),
            static (panel, rest, attributes) =>
            {
                if (ParseNameLabel(rest) is not ({ Length: > 0 } name, var label))
                    return;
                var status = new UiStatusNode(name, label, ParseDefault(rest)) { Attr = attributes };
                panel.Spec.Statuses.Add(status);
                panel.Add(status);
            }),
        (new("table", "//@ui:table name \"Label\" [Col,Col] [height=220] [selectable=false] [toolbar=false]", "A table with the bracket list as columns.", []),
            static (panel, rest, attributes) =>
            {
                if (ParseNameLabel(rest) is not ({ Length: > 0 } name, var label))
                    return;
                Match bracket = BracketRegex().Match(StripAttributes(rest));
                List<string> columns = bracket.Success
                    ? [.. bracket.Groups["v"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]
                    : [];
                var table = new UiTableNode(name, label, columns) { Attr = attributes };
                panel.Spec.Tables.Add(table);
                panel.Add(table);
            }),
        (new("row", "//@ui:row [gap=12] [align=start|center|end|stretch]", "Places controls side by side until //@ui:endrow.", []),
            static (panel, _, attributes) =>
            {
                var row = new UiRowNode { Attr = attributes };
                panel.Open(row, row.Children);
            }),
        (new("endrow", "//@ui:endrow", "Closes the open row.", ["end"]),
            static (panel, _, _) => panel.Close()),
        (new("group", "//@ui:group \"Title\" [collapsed=true]", "A titled box that folds away, until //@ui:endgroup.", []),
            static (panel, rest, attributes) =>
            {
                var group = new UiGroupNode(Unquote(StripAttributes(rest))) { Attr = attributes };
                panel.Open(group, group.Children);
            }),
        (new("endgroup", "//@ui:endgroup", "Closes the open group.", []),
            static (panel, _, _) => panel.Close()),
        (new("section", "//@ui:section Heading", "A heading.", []),
            static (panel, rest, _) =>
            {
                // The older grammar attached a section to the next field. It now stands on its
                // own as a heading, which is what it always looked like, and the pending value
                // is still carried so a field's Section keeps reporting it.
                panel.PendingSection = Unquote(rest);
                panel.Add(new UiSectionNode(panel.PendingSection));
            }),
        (new("separator", "//@ui:separator", "A line.", ["divider"]),
            static (panel, _, attributes) => panel.Add(new UiSeparatorNode { Attr = attributes })),
        (new("spacer", "//@ui:spacer [height=12]", "Empty space.", ["space"]),
            static (panel, _, attributes) => panel.Add(new UiSpacerNode { Attr = attributes }))
    ];

    private static readonly FrozenDictionary<string, DirectiveReader> readers = grammar
        .SelectMany(entry => entry.Directive.Aliases.Prepend(entry.Directive.Key).Select(key => KeyValuePair.Create(key, entry.Read)))
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets every directive the parser knows, in the order the panel documentation lists them.</summary>
    public static IReadOnlyList<UiDirective> Directives { get; } = [.. grammar.Select(entry => entry.Directive)];

    internal List<UiUnknownDirective> UnknownDirectives { get; } = [];

    internal IEnumerable<string> Controls =>
        Fields.Select(input => input.Name)
            .Concat(Outputs.Select(output => output.Name))
            .Concat(Buttons.Select(button => button.Name))
            .Concat(Progresses.Select(progress => progress.Name))
            .Concat(Statuses.Select(status => status.Name))
            .Concat(Tables.Select(table => table.Name));

    /// <summary>Gets whether the script declares a panel.</summary>
    /// <remarks>
    /// <see langword="true"/> when there is at least one node, a title or a description, or a
    /// <c>//@ui:required</c>, <c>//@ui:console</c> or <c>//@ui:layout</c> directive.
    /// </remarks>
    public bool HasUi =>
        Nodes.Count > 0 || Title.Length > 0 || Description.Length > 0 || Required || Console is not null || Layout is not null;

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

    /// <summary>Parses the <c>//@ui:</c> directives in a script into a panel.</summary>
    /// <remarks>
    /// Every line that is a <c>//@ui:</c> comment on its own counts, wherever it sits in the file.
    /// Directive keys are matched case-insensitively, unknown directives are ignored, and a control
    /// without a usable name is dropped. A row or group left open is closed by the end of the file.
    /// </remarks>
    /// <param name="code">The script source.</param>
    /// <returns>The parsed panel, empty when the script declares none.</returns>
    public static UiSpec Parse(string code)
    {
        var panel = new PanelBuilder(new UiSpec());
        int offset = 0;
        foreach (string line in code.Split('\n'))
        {
            int line_offset = offset;
            offset += line.Length + 1;
            Match m = DirectiveRegex().Match(line);
            if (!m.Success)
                continue;

            Group written = m.Groups["key"];
            if (readers.TryGetValue(written.Value, out DirectiveReader? read))
            {
                string rest = m.Groups["rest"].Value.Trim();
                read(panel, rest, ParseAttributes(rest));
            }
            else
            {
                panel.Spec.UnknownDirectives.Add(new UiUnknownDirective(written.Value, line_offset + written.Index));
            }
        }

        return panel.Spec;
    }

    private static DirectiveReader Field(UiFieldKind kind) => (panel, rest, attributes) =>
    {
        if (ParseField(kind, rest, panel.PendingSection, attributes) is not { } field)
            return;
        panel.Spec.Fields.Add(field);
        panel.Add(new UiFieldNode(field) { Attr = attributes });
        panel.PendingSection = null;
    };

    private sealed class PanelBuilder
    {
        // Containers nest, so the parser keeps a stack and appends to whatever is open. An
        // unclosed row or group is closed by the end of the file rather than discarded, because a
        // panel that is missing its last line should still render.
        private readonly Stack<List<UiNode>> _open = new();

        public PanelBuilder(UiSpec spec)
        {
            Spec = spec;
            _open.Push(spec.Nodes);
        }

        public UiSpec Spec { get; }

        public string? PendingSection { get; set; }

        public void Add(UiNode node) => _open.Peek().Add(node);

        public void Open(UiNode node, List<UiNode> children)
        {
            Add(node);
            _open.Push(children);
        }

        public void Close()
        {
            if (_open.Count > 1)
                _open.Pop();
        }
    }

    private static UiField? ParseField(UiFieldKind kind, string rest, string? section, UiAttributes attributes)
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

        if (kind == UiFieldKind.Select && def.Length == 0 && options.Count > 0)
            def = options[0];

        return new UiField(kind, name, labelText, def, options, min, max, section, attributes);
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

    private static UiAttributes ParseAttributes(string rest, bool named = true)
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
        if (named)
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
