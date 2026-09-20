using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":destructive", ":caption", ":detail", ":hint", ":close")]
public sealed class Dialog : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Title));

    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<Dialog, IconKind>(nameof(Icon), IconKind.Info);

    public static readonly StyledProperty<DialogTone> ToneProperty =
        AvaloniaProperty.Register<Dialog, DialogTone>(nameof(Tone));

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Caption));

    public static readonly StyledProperty<object?> DetailProperty =
        AvaloniaProperty.Register<Dialog, object?>(nameof(Detail));

    public static readonly StyledProperty<object?> HintProperty =
        AvaloniaProperty.Register<Dialog, object?>(nameof(Hint));

    public static readonly StyledProperty<object?> ButtonsProperty =
        AvaloniaProperty.Register<Dialog, object?>(nameof(Buttons));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<Dialog, ICommand?>(nameof(CloseCommand));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public DialogTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public object? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public object? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public object? Buttons
    {
        get => GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
            PseudoClasses.Set(":destructive", Tone == DialogTone.Destructive);
        else if (change.Property == CaptionProperty)
            PseudoClasses.Set(":caption", !string.IsNullOrEmpty(Caption));
        else if (change.Property == DetailProperty)
            PseudoClasses.Set(":detail", Detail is not null);
        else if (change.Property == HintProperty)
            PseudoClasses.Set(":hint", Hint is not null);
        else if (change.Property == CloseCommandProperty)
            PseudoClasses.Set(":close", CloseCommand is not null);
    }
}
