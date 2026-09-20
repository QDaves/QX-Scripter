using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Qx.Desktop.Controls;

public sealed class QxMark : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<QxMark>();

    Geometry? _geometry;

    static QxMark()
    {
        AffectsRender<QxMark>(ForegroundProperty);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _geometry = null;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size available_size)
    {
        double aspect = Aspect();
        if (!double.IsNaN(Height))
            return new Size(Height * aspect, Height);
        if (!double.IsNaN(Width))
            return new Size(Width, Width / aspect);
        double height = double.IsInfinity(available_size.Height) ? 16 : available_size.Height;
        return new Size(height * aspect, height);
    }

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush || Bounds.Width <= 0 || Bounds.Height <= 0 || Find() is not { } geometry)
            return;
        Rect artwork = geometry.Bounds;
        double scale = Math.Min(Bounds.Width / artwork.Width, Bounds.Height / artwork.Height);
        double left = (Bounds.Width - artwork.Width * scale) / 2 - artwork.X * scale;
        double top = (Bounds.Height - artwork.Height * scale) / 2 - artwork.Y * scale;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(left, top)))
            context.DrawGeometry(brush, null, geometry);
    }

    double Aspect()
    {
        Rect artwork = Find()?.Bounds ?? default;
        return artwork.Height > 0 ? artwork.Width / artwork.Height : 1.3226;
    }

    Geometry? Find()
    {
        _geometry ??= this.TryFindResource("QxMarkGeometry", ActualThemeVariant, out object? resource) && resource is Geometry geometry ? geometry : null;
        return _geometry;
    }
}
