using Avalonia;
using Avalonia.Controls;
using Qx.Presentation.Services.Panels;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelStack : Panel
{
    double Gap => Application.Current is { } app && app.TryGetResource("QxSpace3", app.ActualThemeVariant, out object? value) && value is double space ? space : 12;

    protected override Size MeasureOverride(Size available)
    {
        double gap = Gap;
        double height = 0;
        double width = 0;
        bool first = true;
        foreach (Control child in Children)
        {
            if (!Shown(child))
            {
                child.Measure(default);
                continue;
            }
            child.Measure(new Size(available.Width, double.PositiveInfinity));
            if (!first)
                height += gap;
            first = false;
            height += child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(double.IsFinite(available.Width) ? available.Width : width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double gap = Gap;
        double y = 0;
        bool first = true;
        foreach (Control child in Children)
        {
            if (!Shown(child))
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }
            if (!first)
                y += gap;
            first = false;
            double height = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, final.Width, height));
            y += height;
        }
        return new Size(final.Width, Math.Max(y, 0));
    }

    static bool Shown(Control child) => child.DataContext is not PanelNode node || node.IsVisible;
}
