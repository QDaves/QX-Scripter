using Avalonia;
using Avalonia.Controls;
using Qx.Presentation.Services.Panels;
using Qx.Presentation.Services.ScriptPanels;
using Qx.Scripting;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelRowPanel : Panel
{
    PanelRowChild[] _children = [];

    protected override Size MeasureOverride(Size available)
    {
        (double gap, UiRowAlign align) = RowShape();
        double height = 0;
        _children = new PanelRowChild[Children.Count];
        for (int index = 0; index < Children.Count; index++)
        {
            Control child = Children[index];
            PanelNode? node = child.DataContext as PanelNode;
            bool visible = node?.IsVisible ?? true;
            double? width = node?.Width;
            double? grow = node?.Grow;
            PanelRowFit fit = PanelRowLayout.FitOf(width, grow, align, node is PanelRowNode);
            double natural = 0;
            if (visible)
            {
                double measure_width = fit == PanelRowFit.Fixed ? width!.Value : double.PositiveInfinity;
                child.Measure(new Size(measure_width, available.Height));
                natural = child.DesiredSize.Width;
            }
            double weight = fit == PanelRowFit.Grow ? (grow is { } share && share > 0 ? share : 1) : 0;
            double fixed_width = fit == PanelRowFit.Fixed ? width!.Value : 0;
            _children[index] = new PanelRowChild(visible, fit, fixed_width, weight, natural);
        }

        double extent = PanelRowLayout.Extent(_children, gap, available.Width);
        PanelRowSlot[] slots = PanelRowLayout.Solve(_children, gap, align, extent);
        for (int index = 0; index < Children.Count; index++)
        {
            if (!_children[index].IsVisible)
                continue;
            Control child = Children[index];
            child.Measure(new Size(slots[index].Width, available.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(extent, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        (double gap, UiRowAlign align) = RowShape();
        PanelRowSlot[] slots = PanelRowLayout.Solve(_children, gap, align, final.Width);
        for (int index = 0; index < Children.Count && index < slots.Length; index++)
        {
            Control child = Children[index];
            if (!_children[index].IsVisible)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }
            child.Arrange(new Rect(slots[index].Offset, 0, slots[index].Width, final.Height));
        }
        return final;
    }

    (double Gap, UiRowAlign Align) RowShape() =>
        DataContext is PanelRowNode row ? (row.Gap, row.Align) : (0d, UiRowAlign.Start);
}
