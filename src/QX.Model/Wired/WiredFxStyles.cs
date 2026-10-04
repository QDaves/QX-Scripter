namespace Qx.Model.Wired;

/// <summary>The options accepted by one verified variable-FX style.</summary>
/// <param name="Category">The FX category.</param>
/// <param name="Id">The category-specific style identifier.</param>
/// <param name="Name">The client style name.</param>
/// <param name="Renderer">The default renderer.</param>
/// <param name="Color">The default color.</param>
/// <param name="Colors">Allowed colors.</param>
/// <param name="Width">The default width.</param>
/// <param name="Widths">Allowed widths.</param>
/// <param name="Renderers">Allowed renderers.</param>
public sealed record WiredFxStyleDefinition(
    WiredFxCategory Category, int Id, string Name, WiredFxRenderer Renderer,
    WiredFxColor Color, IReadOnlyList<WiredFxColor> Colors,
    WiredFxWidth Width, IReadOnlyList<WiredFxWidth> Widths,
    IReadOnlyList<WiredFxRenderer> Renderers)
{
    /// <summary>Applies this style and its dependent visualization controls atomically.</summary>
    /// <param name="form">The FX form in the matching category.</param>
    /// <param name="color">A supported color, or the style default.</param>
    /// <param name="width">A supported width, or the style default.</param>
    /// <param name="renderer">A supported renderer, or the style default.</param>
    /// <param name="subRenderer">A level sub-renderer, or its default.</param>
    public void ApplyTo(WiredForm form, WiredFxColor? color = null, WiredFxWidth? width = null,
        WiredFxRenderer? renderer = null, WiredFxRenderer? subRenderer = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.Definition.Category != WiredFormCategory.Addon || form.Definition.Code != 1200 + (int)Category)
            throw new ArgumentException("The form belongs to another FX category.", nameof(form));
        WiredFxColor selected_color = color ?? Color;
        WiredFxWidth selected_width = width ?? Width;
        WiredFxRenderer selected_renderer = renderer ?? Renderer;
        if (!Colors.Contains(selected_color)) throw new ArgumentOutOfRangeException(nameof(color));
        if (!Widths.Contains(selected_width)) throw new ArgumentOutOfRangeException(nameof(width));
        if (!Renderers.Contains(selected_renderer)) throw new ArgumentOutOfRangeException(nameof(renderer));
        var edits = new List<WiredFormEdit>
        {
            new("style_id", new(Integer: Id)),
            new("color_id", new(Integer: (int)selected_color)),
            new("width_id", new(Integer: (int)selected_width)),
            new("renderer_id", new(Integer: (int)selected_renderer))
        };
        WiredFxRenderer effective_renderer = selected_renderer;
        if (Category == WiredFxCategory.Level)
        {
            WiredFxRenderer selected_sub = subRenderer ?? (Id == 0 ? WiredFxRenderer.BlockProgress : WiredFxRenderer.ClassicMiniProgress);
            bool valid = Id == 0 ? (int)selected_sub is 2 or 3 or 4 : (int)selected_sub == 1;
            if (!valid) throw new ArgumentOutOfRangeException(nameof(subRenderer));
            edits.Add(new("sub_renderer_id", new(Integer: (int)selected_sub)));
            if ((int)selected_renderer == 20) effective_renderer = selected_sub;
        }
        else if (subRenderer is not null)
            throw new ArgumentException("Only level styles have a sub-renderer.", nameof(subRenderer));
        if (!WiredFxStyles.SupportsSegments(effective_renderer))
            edits.Add(new("segments", new(Integer: 0)));
        form.SetFields(edits);
    }
}

/// <summary>Verified variable-FX styles and visualization choices.</summary>
public static partial class WiredFxStyles
{
    /// <summary>Finds a style without substituting unknown identifiers.</summary>
    /// <param name="category">The FX category.</param>
    /// <param name="id">The style identifier.</param>
    /// <returns>The definition, or null for an unknown style.</returns>
    public static WiredFxStyleDefinition? Find(WiredFxCategory category, int id) =>
        All.FirstOrDefault(style => style.Category == category && style.Id == id);

    /// <summary>Reports whether the effective renderer supports segment counts.</summary>
    /// <param name="renderer">The renderer, after resolving a level sub-renderer.</param>
    /// <returns>Whether segment controls apply.</returns>
    public static bool SupportsSegments(WiredFxRenderer renderer) => (int)renderer is 2 or 4 or 13;

    /// <summary>Gets standard number-display icons, including the empty icon.</summary>
    public static IReadOnlyList<string> StandardIcons { get; } = Array.AsReadOnly<string>(
        ["", "battery", "burning", "cash", "cooldown", "droplet", "energy", "eye", "fish", "food", "freezing", "gems", "gold", "health", "honor", "magic", "mana", "poison", "repairing", "reputation", "shield", "stamina", "star_power", "stealth", "timeleft", "upgrading", "wooden_logs"]);

    /// <summary>Gets icons requiring the campaign setting or security permission 4.</summary>
    public static IReadOnlyList<string> CampaignIcons { get; } = Array.AsReadOnly<string>(
        ["ranch.aubergine", "ranch.carrot", "ranch.corn", "ranch.egg", "ranch.grape", "ranch.potato", "ranch.pumpkin", "ranch.sapling", "ranch.tomato", "ranch.wheat"]);
}
