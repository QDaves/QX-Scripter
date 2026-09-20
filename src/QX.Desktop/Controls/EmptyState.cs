using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":neutral", ":accent", ":running", ":success", ":warning", ":danger", ":info", ":action", ":description")]
public sealed class EmptyState : TemplatedControl
{
    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<EmptyState, IconKind>(nameof(Icon), IconKind.Info);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<ICommand?> ActionProperty =
        AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(Action));

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ActionText));

    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<EmptyState, StatusTone>(nameof(Tone));

    public EmptyState()
    {
        StatusTones.Apply(PseudoClasses, Tone);
    }

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

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

    public ICommand? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public StatusTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
            StatusTones.Apply(PseudoClasses, Tone);
        else if (change.Property == ActionProperty || change.Property == ActionTextProperty)
            PseudoClasses.Set(":action", Action is not null && !string.IsNullOrEmpty(ActionText));
        else if (change.Property == DescriptionProperty)
            PseudoClasses.Set(":description", !string.IsNullOrEmpty(Description));
    }
}
