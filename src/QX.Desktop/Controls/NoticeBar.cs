using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":notice", ":info", ":success", ":warning", ":error", ":dismiss")]
public sealed class NoticeBar : TemplatedControl
{
    public static readonly StyledProperty<Notice?> NoticeProperty =
        AvaloniaProperty.Register<NoticeBar, Notice?>(nameof(Notice));

    public static readonly StyledProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.Register<NoticeBar, ICommand?>(nameof(DismissCommand));

    public static readonly DirectProperty<NoticeBar, string> TextProperty =
        AvaloniaProperty.RegisterDirect<NoticeBar, string>(nameof(Text), bar => bar.Text);

    public static readonly DirectProperty<NoticeBar, IconKind> GlyphProperty =
        AvaloniaProperty.RegisterDirect<NoticeBar, IconKind>(nameof(Glyph), bar => bar.Glyph);

    string _text = "";
    IconKind _glyph = IconKind.Info;

    public Notice? Notice
    {
        get => GetValue(NoticeProperty);
        set => SetValue(NoticeProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    public string Text
    {
        get => _text;
        private set => SetAndRaise(TextProperty, ref _text, value);
    }

    public IconKind Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DismissCommandProperty)
        {
            PseudoClasses.Set(":dismiss", DismissCommand is not null);
            return;
        }
        if (change.Property != NoticeProperty)
            return;
        Notice? notice = Notice;
        Text = notice?.Text ?? "";
        Glyph = notice?.Severity switch
        {
            NoticeSeverity.Success => IconKind.Success,
            NoticeSeverity.Warning => IconKind.Warning,
            NoticeSeverity.Error => IconKind.Error,
            _ => IconKind.Info
        };
        PseudoClasses.Set(":notice", notice is not null);
        PseudoClasses.Set(":info", notice?.Severity == NoticeSeverity.Info);
        PseudoClasses.Set(":success", notice?.Severity == NoticeSeverity.Success);
        PseudoClasses.Set(":warning", notice?.Severity == NoticeSeverity.Warning);
        PseudoClasses.Set(":error", notice?.Severity == NoticeSeverity.Error);
    }
}
