using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelColorSwatch : Border
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<PanelColorSwatch, string?>(nameof(Value));

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            Paint();
    }

    void Paint()
    {
        if (Value is { Length: > 0 } text && Color.TryParse(text, out Color color))
            Background = new SolidColorBrush(color);
    }
}
