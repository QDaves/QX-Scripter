using Avalonia.Controls.Primitives;
using Avalonia;

namespace Qx.Desktop.Controls;

public sealed class Kbd : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Kbd, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
