using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

public sealed class Icon : Control
{
    public static readonly StyledProperty<IconKind> KindProperty =
        AvaloniaProperty.Register<Icon, IconKind>(nameof(Kind));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(Size), 16);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    public static readonly StyledProperty<IconPaint> PaintProperty =
        AvaloniaProperty.Register<Icon, IconPaint>(nameof(Paint), IconPaint.Stroke, inherits: true);

    public static readonly StyledProperty<double> ViewBoxProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(ViewBox), 24, inherits: true);

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(StrokeWidth), 1.5, inherits: true);

    Geometry? _geometry;
    IconKind _geometry_kind;
    Pen? _pen;

    static Icon()
    {
        AffectsRender<Icon>(KindProperty, ForegroundProperty, PaintProperty, ViewBoxProperty, StrokeWidthProperty);
        AffectsMeasure<Icon>(SizeProperty);
    }

    public IconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IconPaint Paint
    {
        get => GetValue(PaintProperty);
        set => SetValue(PaintProperty, value);
    }

    public double ViewBox
    {
        get => GetValue(ViewBoxProperty);
        set => SetValue(ViewBoxProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == SizeProperty || change.Property == StrokeWidthProperty || change.Property == ViewBoxProperty)
            _pen = null;
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _geometry = null;
    }

    protected override Size MeasureOverride(Size available_size) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Kind == IconKind.None || Foreground is not { } brush || Find(Kind) is not { } geometry)
            return;
        double scale = Size / ViewBox;
        double left = Math.Round((Bounds.Width - Size) / 2);
        double top = Math.Round((Bounds.Height - Size) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(left, top)))
        {
            if (Paint == IconPaint.Fill)
            {
                context.DrawGeometry(brush, null, geometry);
                return;
            }
            _pen ??= new Pen(brush, StrokeWidth / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, _pen, geometry);
        }
    }

    Geometry? Find(IconKind kind)
    {
        if (_geometry is not null && _geometry_kind == kind)
            return _geometry;
        _geometry_kind = kind;
        _geometry = this.TryFindResource($"Icon.{kind}", ActualThemeVariant, out object? resource) && resource is Geometry geometry ? geometry : null;
        return _geometry;
    }
}
