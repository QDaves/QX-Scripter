using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using Qx.Presentation.Services.Panels;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelOutputView : Decorator
{
    readonly TextEditor _editor;
    PanelOutputNode? _node;
    bool _scroll_pending;

    public PanelOutputView()
    {
        _editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = false,
            Background = Brushes.Transparent,
            Padding = new Thickness(11, 9),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            FontSize = Resource("QxFontSizeCodeSmall") as double? ?? 12
        };
        _editor.Options.AllowScrollBelowDocument = false;
        Child = _editor;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Bind(DataContext as PanelOutputNode);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Bind(null);
    }

    void Bind(PanelOutputNode? node)
    {
        if (ReferenceEquals(_node, node))
            return;
        if (_node is { } previous)
            previous.Changed -= OnOutputChanged;
        _node = node;
        if (node is null)
            return;
        _editor.WordWrap = node.Wrap;
        _editor.HorizontalScrollBarVisibility = node.Wrap
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        _editor.FontFamily = Resource(node.Monospace ? "QxMonoFont" : "QxSansFont") as FontFamily ?? FontFamily.Default;
        _editor.Foreground = Resource("QxEditorForegroundBrush") as IBrush ?? Brushes.Gray;
        _editor.Document.Text = node.Text;
        node.Changed += OnOutputChanged;
        ScrollToEnd();
    }

    void OnOutputChanged(PanelOutputChange change)
    {
        bool at_end = AtEnd();
        if (change.Reset)
            _editor.Document.Text = change.Text;
        else
            _editor.Document.Insert(_editor.Document.TextLength, change.Text);
        if (at_end)
            ScrollToEnd();
    }

    bool AtEnd() =>
        _editor.ExtentHeight <= _editor.ViewportHeight + 1 ||
        _editor.VerticalOffset + _editor.ViewportHeight >= _editor.ExtentHeight - 4;

    void ScrollToEnd()
    {
        if (_scroll_pending)
            return;
        _scroll_pending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scroll_pending = false;
            _editor.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    object? Resource(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? value) ? value : null;
}
