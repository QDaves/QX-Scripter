namespace Qx.Model.Wired;

/// <summary>The forms registered by the analyzed Flash client, including alternate negative-variant codes.</summary>
public static partial class WiredFormRegistry
{
    private static readonly IReadOnlyList<WiredFormLayout> layouts = create_layouts();
    private static readonly IReadOnlyDictionary<(WiredFormCategory Category, int Code), WiredFormLayout> by_code = index_layouts();

    /// <summary>Gets all 176 distinct form definitions. The client registers the same code-only trigger twice.</summary>
    public static IReadOnlyList<WiredFormDefinition> Definitions { get; } = Array.AsReadOnly(layouts.Select(layout => layout.Definition).ToArray());

    /// <summary>Gets the number of client registrations, including both identical trigger-code-11 classes.</summary>
    public static int RegistrationCount => layouts.Sum(layout => layout.Definition.Evidence.Count);

    /// <summary>Looks up a form by its received category and code.</summary>
    /// <param name="category">The configuration category.</param>
    /// <param name="code">The received code, including alternate variants.</param>
    /// <returns>The matching definition, or null when unknown.</returns>
    public static WiredFormDefinition? Find(WiredFormCategory category, int code) =>
        by_code.TryGetValue((category, code), out WiredFormLayout? layout) ? layout.Definition : null;

    internal static WiredForm Create(WiredConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        WiredFormCategory category = configuration switch
        {
            WiredTriggerConfig => WiredFormCategory.Trigger,
            WiredActionConfig => WiredFormCategory.Action,
            WiredConditionConfig => WiredFormCategory.Condition,
            WiredSelectorConfig => WiredFormCategory.Selector,
            WiredAddonConfig => WiredFormCategory.Addon,
            WiredVariableConfig => WiredFormCategory.Variable,
            _ => throw new ArgumentException("Unsupported Wired category.", nameof(configuration))
        };
        if (by_code.TryGetValue((category, configuration.Code), out WiredFormLayout? layout))
            return create_form(configuration, layout);
        return new UnknownWiredForm(configuration, new WiredFormLayout(
            new WiredFormDefinition(category, configuration.Code, "Unknown", [], [],
                "No registered form for this code; existing raw values are retained.", "", [], []), []));
    }

    private static IReadOnlyDictionary<(WiredFormCategory, int), WiredFormLayout> index_layouts()
    {
        var result = new Dictionary<(WiredFormCategory, int), WiredFormLayout>();
        foreach (WiredFormLayout layout in layouts)
        {
            result.TryAdd((layout.Definition.Category, layout.Definition.Code), layout);
            foreach (int alias in layout.Definition.Aliases)
                result.TryAdd((layout.Definition.Category, alias), layout);
        }
        return result;
    }
}
