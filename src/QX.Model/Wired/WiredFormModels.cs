namespace Qx.Model.Wired;

/// <summary>The six Wired configuration categories.</summary>
public enum WiredFormCategory
{
    /// <summary>A trigger.</summary>
    Trigger,
    /// <summary>An action.</summary>
    Action,
    /// <summary>A condition.</summary>
    Condition,
    /// <summary>A selector.</summary>
    Selector,
    /// <summary>An add-on.</summary>
    Addon,
    /// <summary>A variable.</summary>
    Variable
}

/// <summary>The value accepted by a named Wired form field.</summary>
public enum WiredFormFieldKind
{
    /// <summary>A signed integer or numeric option.</summary>
    Integer,
    /// <summary>A checkbox.</summary>
    Boolean,
    /// <summary>Text.</summary>
    Text,
    /// <summary>A variable identifier.</summary>
    Variable,
    /// <summary>Selected neighborhood tiles.</summary>
    Neighborhood,
    /// <summary>Reward table entries.</summary>
    Rewards
}

/// <summary>A furniture input source. Availability comes from the received configuration.</summary>
public enum WiredFurnitureSource
{
    /// <summary>The triggering furniture.</summary>
    Triggering = 0,
    /// <summary>The primary furniture selection.</summary>
    Picked = 100,
    /// <summary>The secondary furniture selection.</summary>
    SecondaryPicked = 101,
    /// <summary>The saved snapshot.</summary>
    Snapshot = 110,
    /// <summary>Furniture selected by selectors.</summary>
    Selector = 200,
    /// <summary>Furniture carried by a signal.</summary>
    Signal = 201,
    /// <summary>All furniture in the room.</summary>
    Room = 900,
    /// <summary>Furniture moved by the current stack.</summary>
    MovedByStack = 901
}

/// <summary>A user input source. Availability comes from the received configuration.</summary>
public enum WiredUserSource
{
    /// <summary>The triggering user.</summary>
    Triggering = 0,
    /// <summary>The reached user.</summary>
    Reached = 10,
    /// <summary>The clicked user.</summary>
    Clicked = 11,
    /// <summary>The bot named by the configuration.</summary>
    NamedBot = 100,
    /// <summary>The user named by the configuration.</summary>
    NamedUser = 101,
    /// <summary>Users selected by selectors.</summary>
    Selector = 200,
    /// <summary>Users carried by a signal.</summary>
    Signal = 201,
    /// <summary>All users in the room.</summary>
    Room = 900
}

/// <summary>The target domain of a merged source or variable reference.</summary>
public enum WiredSourceDomain
{
    /// <summary>Furniture.</summary>
    Furniture = 0,
    /// <summary>Users.</summary>
    User = 1,
    /// <summary>Global variables.</summary>
    Global = -10,
    /// <summary>Context variables.</summary>
    Context = -20
}

/// <summary>Whether a value control uses a constant or a variable.</summary>
public enum WiredValueSource
{
    /// <summary>A constant value.</summary>
    Constant = 0,
    /// <summary>A variable value.</summary>
    Variable = 1
}

/// <summary>A value-or-variable control shared by Wired forms.</summary>
/// <param name="Source">Whether the constant or variable is used.</param>
/// <param name="Value">The constant value.</param>
/// <param name="Domain">The variable source domain.</param>
/// <param name="VariableId">The variable identifier; n means no variable selected.</param>
public sealed record WiredValueReference(WiredValueSource Source, int Value, WiredSourceDomain Domain, string VariableId);

/// <summary>A coordinate relative to the center of a neighborhood selection.</summary>
/// <param name="X">The horizontal offset, from -10 to 10.</param>
/// <param name="Y">The vertical offset, from -10 to 10.</param>
public readonly record struct WiredTileOffset(int X, int Y);

/// <summary>A row in the Wired reward table.</summary>
/// <param name="Type">Zero for a badge, one for a product.</param>
/// <param name="Code">The badge or product code.</param>
/// <param name="Probability">The integer probability; zero may be used when probabilities are disabled.</param>
public sealed record WiredRewardEntry(int Type, string Code, int Probability);

/// <summary>A typed value for a named form field. Exactly one property must be supplied when editing.</summary>
/// <param name="Integer">An integer value or numeric option.</param>
/// <param name="Boolean">A checkbox value.</param>
/// <param name="Text">Text or a variable identifier.</param>
/// <param name="Tiles">The selected neighborhood coordinates.</param>
/// <param name="Rewards">Reward table entries.</param>
/// <param name="Choice">A named integer choice, or comma-separated names for flags.</param>
public sealed record WiredFormValue(
    int? Integer = null,
    bool? Boolean = null,
    string? Text = null,
    IReadOnlyList<WiredTileOffset>? Tiles = null,
    IReadOnlyList<WiredRewardEntry>? Rewards = null,
    string? Choice = null);

/// <summary>An edit to one named form field.</summary>
/// <param name="Name">The field name from the form definition.</param>
/// <param name="Value">The typed replacement value.</param>
public sealed record WiredFormEdit(string Name, WiredFormValue Value);

/// <summary>The verified meaning and editing constraints of one named form field.</summary>
/// <param name="Name">The stable field name.</param>
/// <param name="Kind">The field's value type.</param>
/// <param name="LabelKey">The client localization key or literal caption.</param>
/// <param name="Values">Verified options, units and value constraints.</param>
/// <param name="Initial">How the client initializes the field.</param>
/// <param name="Dependency">Dependencies on other controls.</param>
/// <param name="Writable">Whether the field is directly editable rather than derived.</param>
public sealed record WiredFormFieldDefinition(
    string Name,
    WiredFormFieldKind Kind,
    string LabelKey,
    string Values,
    string Initial,
    string Dependency,
    bool Writable);

/// <summary>A registered Wired form and its verified named controls.</summary>
/// <param name="Category">The configuration category.</param>
/// <param name="Code">The primary effective code.</param>
/// <param name="Name">The form name.</param>
/// <param name="Aliases">Other codes using the same form.</param>
/// <param name="Fields">The named fields in the form.</param>
/// <param name="Details">The verified opening and saving behavior.</param>
/// <param name="Sources">Form-specific source and selection behavior.</param>
/// <param name="ExternalVariables">Client configuration keys used by this form.</param>
/// <param name="Evidence">The SWF class and method evidence identifiers.</param>
public sealed record WiredFormDefinition(
    WiredFormCategory Category,
    int Code,
    string Name,
    IReadOnlyList<int> Aliases,
    IReadOnlyList<WiredFormFieldDefinition> Fields,
    string Details,
    string Sources,
    IReadOnlyList<string> ExternalVariables,
    IReadOnlyList<string> Evidence)
{
    /// <summary>Gets concise usage knowledge with explicit evidence levels, or null for an unknown form.</summary>
    public WiredFormUsage? Usage => WiredFormKnowledge.Find(Category, Code);
}

internal enum WiredFieldStorage { Integer, Text, TextPart, TextTail, Variable, Neighborhood, Rewards }

internal sealed record WiredFieldLayout(
    WiredFormFieldDefinition Definition,
    WiredFieldStorage Storage,
    int Index = 0,
    bool Nonzero = false,
    int? Minimum = null,
    int? Maximum = null,
    int? MaximumLength = null,
    int? DerivedFrom = null,
    bool NormalizeName = false);

internal sealed record WiredFormLayout(WiredFormDefinition Definition, IReadOnlyList<WiredFieldLayout> Fields);
