using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":icon")]
public sealed class Chip : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Chip, string?>(nameof(Text));

    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<Chip, IconKind>(nameof(Icon));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
            PseudoClasses.Set(":icon", Icon != IconKind.None);
    }
}
