using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":neutral", ":accent", ":running", ":success", ":warning", ":danger", ":info", ":busy", ":icon", ":detail")]
public sealed class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Badge, string?>(nameof(Text));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<Badge, string?>(nameof(Detail));

    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<Badge, StatusTone>(nameof(Tone));

    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<Badge, IconKind>(nameof(Icon));

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<Badge, bool>(nameof(IsBusy));

    public Badge()
    {
        StatusTones.Apply(PseudoClasses, Tone);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public StatusTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsBusy
    {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
            StatusTones.Apply(PseudoClasses, Tone);
        else if (change.Property == IsBusyProperty)
            PseudoClasses.Set(":busy", IsBusy);
        else if (change.Property == IconProperty)
            PseudoClasses.Set(":icon", Icon != IconKind.None);
        else if (change.Property == DetailProperty)
            PseudoClasses.Set(":detail", !string.IsNullOrEmpty(Detail));
    }
}
