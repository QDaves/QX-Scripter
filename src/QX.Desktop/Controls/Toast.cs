using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":info", ":success", ":warning", ":error", ":message", ":action")]
public sealed class Toast : TemplatedControl
{
    public static readonly StyledProperty<NoticeSeverity> SeverityProperty =
        AvaloniaProperty.Register<Toast, NoticeSeverity>(nameof(Severity));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Message));

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(ActionText));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<Toast, ICommand?>(nameof(ActionCommand));

    public static readonly StyledProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.Register<Toast, ICommand?>(nameof(DismissCommand));

    public static readonly StyledProperty<object?> DismissParameterProperty =
        AvaloniaProperty.Register<Toast, object?>(nameof(DismissParameter));

    public static readonly DirectProperty<Toast, IconKind> GlyphProperty =
        AvaloniaProperty.RegisterDirect<Toast, IconKind>(nameof(Glyph), toast => toast.Glyph);

    IconKind _glyph = IconKind.Info;

    public Toast()
    {
        PseudoClasses.Set(":info", true);
    }

    public NoticeSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    public object? DismissParameter
    {
        get => GetValue(DismissParameterProperty);
        set => SetValue(DismissParameterProperty, value);
    }

    public IconKind Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MessageProperty)
        {
            PseudoClasses.Set(":message", !string.IsNullOrEmpty(Message));
            return;
        }
        if (change.Property == ActionTextProperty || change.Property == ActionCommandProperty)
        {
            PseudoClasses.Set(":action", !string.IsNullOrEmpty(ActionText) && ActionCommand is not null);
            return;
        }
        if (change.Property != SeverityProperty)
            return;
        NoticeSeverity severity = Severity;
        PseudoClasses.Set(":info", severity == NoticeSeverity.Info);
        PseudoClasses.Set(":success", severity == NoticeSeverity.Success);
        PseudoClasses.Set(":warning", severity == NoticeSeverity.Warning);
        PseudoClasses.Set(":error", severity == NoticeSeverity.Error);
        Glyph = severity switch
        {
            NoticeSeverity.Success => IconKind.Success,
            NoticeSeverity.Warning => IconKind.Warning,
            NoticeSeverity.Error => IconKind.Error,
            _ => IconKind.Info
        };
    }
}
