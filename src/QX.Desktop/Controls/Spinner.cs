using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Qx.Desktop.Controls;

public sealed class Spinner : Control
{
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Spinner, double>(nameof(Size), 14);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<Spinner, double>(nameof(Thickness), 1.5);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Spinner>();

    static Spinner()
    {
        AffectsRender<Spinner>(SizeProperty, ThicknessProperty, ForegroundProperty);
        AffectsMeasure<Spinner>(SizeProperty);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size available_size) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
            return;
        double radius = (Size - Thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var track = new Pen(brush, Thickness);
        using (context.PushOpacity(0.22))
            context.DrawEllipse(null, track, center, radius, radius);
        var arc = new StreamGeometry();
        using (StreamGeometryContext geometry = arc.Open())
        {
            geometry.BeginFigure(new Point(center.X, center.Y - radius), false);
            geometry.ArcTo(new Point(center.X + radius, center.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            geometry.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(brush, Thickness, lineCap: PenLineCap.Round), arc);
    }
}
