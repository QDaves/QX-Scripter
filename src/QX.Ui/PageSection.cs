using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Qx.Ui;

/// <summary>
/// A titled block that folds away.
/// </summary>
/// <remarks>
/// <para>
/// Written rather than taken from the framework's <see cref="Expander"/>. Its own template drives
/// the header from a toggle bound back to <c>IsExpanded</c>, and that binding does not carry the
/// answer back when the template is replaced — the header lights up and nothing opens. Rather than
/// keep guessing at why, the click is handled here, where there is nothing to go wrong.
/// </para>
/// <para>
/// The whole header row is the button, so there is no small triangle to aim at, and the state is a
/// plain property anything can read or set.
/// </para>
/// <para>
/// No default style key is claimed. That sends the lookup to a theme dictionary this assembly does
/// not ship, and the plain style carried in the application's own resources is what dresses these.
/// </para>
/// </remarks>
public sealed class PageSection : HeaderedContentControl
{
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(PageSection),
            new FrameworkPropertyMetadata(false));

    /// <summary>Whether what is inside is showing.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(
            nameof(IsLocked),
            typeof(bool),
            typeof(PageSection),
            new FrameworkPropertyMetadata(false));

    /// <summary>
    /// Whether this one stays open.
    /// </summary>
    /// <remarks>
    /// A page whose sections are all worth reading at once has nothing to gain from folding them,
    /// and an arrow that folds away the thing somebody came to read is a trap rather than a control.
    /// Locked sections show no arrow and do not answer a click.
    /// </remarks>
    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(
            nameof(Note),
            typeof(string),
            typeof(PageSection),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// A word or a number shown at the end of the header, for what is worth knowing while shut.
    /// </summary>
    /// <remarks>
    /// A folded section that says nothing forces the reader to open all of them to find where the
    /// switch they turned on lives. Null hides it, so a section with nothing to report shows nothing.
    /// </remarks>
    public string? Note
    {
        get => (string?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>The header row, named in the template so the click can be taken here.</summary>
    private const string HeadPart = "PART_Head";

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild(HeadPart) is not ButtonBase head)
            return;

        head.Click -= OnHeadClicked;
        head.Click += OnHeadClicked;
    }

    private void OnHeadClicked(object sender, RoutedEventArgs e)
    {
        if (!IsLocked)
            IsOpen = !IsOpen;
    }
}
