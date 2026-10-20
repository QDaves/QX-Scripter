namespace Qx.Model.Wired;

internal static class WiredFormRules
{
    internal const string DarkenLevelsVariable = "wired.furni_highlight.darken_levels";

    private const int white = 16777215;
    private static readonly int[] highlight_colors =
    [
        16777215, 16772910, 16758528, 16747008, 16734774, 16724016, 16724855, 16733627, 15741144, 12076287, 7883007,
        4611327, 2395391, 2146303, 2156016, 1628592, 2149480, 5300272, 10546464, 16557530, 12291417
    ];
    private static readonly int[] default_darken_levels = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
    private static readonly int[] glow_strengths = [0, 100];
    private static readonly int[] tint_strengths = [0, 25, 50, 75, 100];
    private static readonly int[] brighten_strengths = [0, 38];

    internal static IReadOnlyList<int> DarkenLevels(string? configured) =>
        configured is null
            ? default_darken_levels
            : Array.AsReadOnly(configured.Split(',', StringSplitOptions.TrimEntries)
                .Select(level => int.TryParse(level, out int value) ? value : 0).ToArray());

    internal static void Check(WiredForm form, IReadOnlyList<int>? darken_levels = null, bool colors = true)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form is WiredSetFurniHighlightActionForm highlight)
            check_highlight(highlight, darken_levels ?? default_darken_levels, colors);
    }

    private static void check_highlight(WiredSetFurniHighlightActionForm form, IReadOnlyList<int> darken_levels, bool colors)
    {
        switch (form.HighlightType)
        {
            case WiredHighlightTypeOption.Darken:
                if (!darken_levels.Any(level => level > 0))
                    throw new ArgumentException($"Darken is disabled by this hotel ({DarkenLevelsVariable} has no level above 0).");
                if (form.ColorRgb != 0)
                    form.ColorRgb = 0;
                require_strength(form, darken_levels, "Darken");
                break;
            case WiredHighlightTypeOption.Brighten:
                if (form.Animation != WiredAnimationOption.Pulse)
                    throw new ArgumentException("Brighten only supports the pulse animation.");
                if (form.ColorRgb != white)
                    form.ColorRgb = white;
                require_strength(form, brighten_strengths, "Brighten");
                break;
            case WiredHighlightTypeOption.ColorTint:
                require_strength(form, tint_strengths, "Color tint");
                if (colors)
                    require_color(form, highlight_colors.Skip(1));
                break;
            case WiredHighlightTypeOption.Outline or WiredHighlightTypeOption.SoftGlow or WiredHighlightTypeOption.NeonAura:
                require_strength(form, glow_strengths, form.HighlightType.ToString());
                if (colors)
                    require_color(form, highlight_colors);
                break;
        }
    }

    private static void require_strength(WiredSetFurniHighlightActionForm form, IEnumerable<int> allowed, string type)
    {
        int[] values = allowed.Distinct().Order().ToArray();
        if (!values.Contains(form.Strength))
            throw new ArgumentException($"{type} highlight strength must be one of {string.Join(", ", values)}; got {form.Strength}.");
    }

    private static void require_color(WiredSetFurniHighlightActionForm form, IEnumerable<int> palette)
    {
        IReadOnlySet<string> edited = form.EditedControls;
        if (!edited.Contains("color_rgb") && !edited.Contains("highlight_type"))
            return;
        int color = form.ColorRgb;
        if (palette.Contains(color) || form.GetDefaultField("color_rgb")?.Integer == color)
            return;
        throw new ArgumentException($"Highlight color 0x{color:X6} is not in the client palette: {string.Join(", ", palette.Select(value => "0x" + value.ToString("X6")))}.");
    }
}
