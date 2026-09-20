using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":icon", ":detail", ":muted", ":actionable")]
public sealed class StatusBarItem : Button
{
    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<StatusBarItem, IconKind>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatusBarItem, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<StatusBarItem, string?>(nameof(Detail));

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<StatusBarItem, bool>(nameof(IsMuted));

    protected override Type StyleKeyOverride => typeof(StatusBarItem);

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
            PseudoClasses.Set(":icon", Icon != IconKind.None);
        else if (change.Property == CommandProperty)
            PseudoClasses.Set(":actionable", Command is not null);
        else if (change.Property == DetailProperty)
            PseudoClasses.Set(":detail", !string.IsNullOrEmpty(Detail));
        else if (change.Property == IsMutedProperty)
            PseudoClasses.Set(":muted", IsMuted);
    }
}
