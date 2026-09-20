using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":icon", ":text")]
public sealed class Segment : TemplatedControl
{
    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<Segment, IconKind>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Segment, string?>(nameof(Text));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<Segment, double>(nameof(IconSize), 14);

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
            PseudoClasses.Set(":icon", Icon != IconKind.None);
        else if (change.Property == TextProperty)
            PseudoClasses.Set(":text", !string.IsNullOrEmpty(Text));
    }
}
