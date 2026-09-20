using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Qx.Desktop.Controls;

[PseudoClasses(":copied")]
public sealed class CopyButton : Button
{
    public static readonly StyledProperty<bool> IsCopiedProperty =
        AvaloniaProperty.Register<CopyButton, bool>(nameof(IsCopied));

    protected override Type StyleKeyOverride => typeof(CopyButton);

    public bool IsCopied
    {
        get => GetValue(IsCopiedProperty);
        set => SetValue(IsCopiedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCopiedProperty)
            PseudoClasses.Set(":copied", IsCopied);
    }
}
