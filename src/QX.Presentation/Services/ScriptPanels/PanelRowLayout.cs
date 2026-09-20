using Qx.Scripting;

namespace Qx.Presentation.Services.ScriptPanels;

public enum PanelRowFit
{
    Fixed,
    Grow,
    Auto
}

public readonly record struct PanelRowChild(bool IsVisible, PanelRowFit Fit, double Width, double Weight, double Natural);

public readonly record struct PanelRowSlot(double Offset, double Width);

public static class PanelRowLayout
{
    public static PanelRowFit FitOf(double? width, double? grow, UiRowAlign align, bool nested_row)
    {
        if (width is { } fixed_width && double.IsFinite(fixed_width) && fixed_width > 0)
            return PanelRowFit.Fixed;
        if (grow is { } share && double.IsFinite(share) && share > 0)
            return PanelRowFit.Grow;
        if (align == UiRowAlign.Stretch && !nested_row)
            return PanelRowFit.Grow;
        return PanelRowFit.Auto;
    }

    public static bool AnyGrow(IReadOnlyList<PanelRowChild> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        foreach (PanelRowChild child in children)
        {
            if (child.IsVisible && child.Fit == PanelRowFit.Grow)
                return true;
        }
        return false;
    }

    public static double Content(IReadOnlyList<PanelRowChild> children, double gap)
    {
        ArgumentNullException.ThrowIfNull(children);
        double total = 0;
        int visible = 0;
        foreach (PanelRowChild child in children)
        {
            if (!child.IsVisible)
                continue;
            total += Intrinsic(child);
            visible++;
        }
        if (gap > 0 && visible > 1)
            total += gap * (visible - 1);
        return total;
    }

    public static double Extent(IReadOnlyList<PanelRowChild> children, double gap, double available) =>
        AnyGrow(children) && double.IsFinite(available) ? Math.Max(available, 0) : Content(children, gap);

    public static PanelRowSlot[] Solve(IReadOnlyList<PanelRowChild> children, double gap, UiRowAlign align, double available)
    {
        ArgumentNullException.ThrowIfNull(children);
        var slots = new PanelRowSlot[children.Count];
        bool shares = AnyGrow(children) && double.IsFinite(available);
        double weight = 0;
        double claimed = 0;
        int visible = 0;
        foreach (PanelRowChild child in children)
        {
            if (!child.IsVisible)
                continue;
            visible++;
            if (shares && child.Fit == PanelRowFit.Grow)
                weight += child.Weight;
            else
                claimed += Intrinsic(child);
        }
        double gaps = gap > 0 && visible > 1 ? gap * (visible - 1) : 0;
        double spare = shares ? Math.Max(0, available - claimed - gaps) : 0;
        double content = claimed + gaps;
        double cursor = shares
            ? 0
            : align switch
            {
                UiRowAlign.Center => Math.Max(0, (available - content) / 2),
                UiRowAlign.End => Math.Max(0, available - content),
                _ => 0
            };
        bool first = true;
        for (int index = 0; index < children.Count; index++)
        {
            PanelRowChild child = children[index];
            if (!child.IsVisible)
            {
                slots[index] = new PanelRowSlot(cursor, 0);
                continue;
            }
            if (!first && gap > 0)
                cursor += gap;
            first = false;
            double size = shares && child.Fit == PanelRowFit.Grow
                ? (weight > 0 ? spare * (child.Weight / weight) : 0)
                : Intrinsic(child);
            slots[index] = new PanelRowSlot(cursor, size);
            cursor += size;
        }
        return slots;
    }

    static double Intrinsic(PanelRowChild child) =>
        child.Fit == PanelRowFit.Fixed ? child.Width : Math.Max(0, child.Natural);
}
