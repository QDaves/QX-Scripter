using Avalonia.Controls.Primitives;
using Avalonia;

namespace Qx.Desktop.Controls;

public sealed class CountBadge : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CountBadge, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
