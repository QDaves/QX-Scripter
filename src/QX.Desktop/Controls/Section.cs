using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Qx.Desktop.Controls;

[PseudoClasses(":footer", ":description", ":actions")]
public sealed class Section : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Section, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Section, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<Section, object?>(nameof(Actions));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Section, object?>(nameof(Footer));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FooterProperty)
            PseudoClasses.Set(":footer", Footer is not null);
        else if (change.Property == DescriptionProperty)
            PseudoClasses.Set(":description", !string.IsNullOrEmpty(Description));
        else if (change.Property == ActionsProperty)
            PseudoClasses.Set(":actions", Actions is not null);
    }
}
