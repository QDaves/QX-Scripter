using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using Microsoft.CodeAnalysis;
using Qx.Desktop.Editor;
using Qx.Desktop.Input;
using Qx.Diagnostics;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;
using RoslynPad.Editor;

namespace Qx.Desktop.Views.Editor;

public sealed partial class ScriptEditorView : UserControl
{
    readonly UiDirectiveColorizer _directives;
    RoslynHostProvider? _hosts;
    EditorPreferences? _preferences;
    ScriptDocument? _document;
    EditorBufferAdapter? _buffer;
    TextEditor? _surface;
    bool _started;

    public ScriptEditorView()
    {
        InitializeComponent();
        _directives = new UiDirectiveColorizer(DirectiveBrush);
        ActualThemeVariantChanged += OnThemeChanged;
    }

    public TextEditor? Surface => _surface;

    public void Use(ScriptDocument document, RoslynHostProvider hosts, EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(hosts);
        ArgumentNullException.ThrowIfNull(preferences);
        _document = document;
        _hosts = hosts;
        _preferences = preferences;
        DataContext = document;
        Apply(preferences);
        preferences.PropertyChanged += OnPreferencesChanged;
    }

    public void Release()
    {
        ActualThemeVariantChanged -= OnThemeChanged;
        if (_preferences is { } preferences)
            preferences.PropertyChanged -= OnPreferencesChanged;
        _preferences = null;
        if (_surface is { } editor)
        {
            editor.RemoveHandler(PointerWheelChangedEvent, OnWheel);
            editor.TextArea.TextView.LineTransformers.Remove(_directives);
        }
        _buffer?.Dispose();
        _buffer = null;
        _surface = null;
        _document = null;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_started || _document is null || _hosts is null)
            return;
        _started = true;
        StartAsync(_document, _hosts).Observe("editor");
    }

    async Task StartAsync(ScriptDocument document, RoslynHostProvider hosts)
    {
        QxRoslynHost? host = null;
        try
        {
            host = await hosts.GetAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Editor code-intelligence unavailable ({error.Message}); scripts still run.", "editor");
        }
        if (host is not null)
        {
            try
            {
                Directory.CreateDirectory(hosts.WorkingDirectory);
                Code.TextArea.SelectionCornerRadius = 2;
                _ = await Code.InitializeAsync(host, ThemeColors(), hosts.WorkingDirectory, document.Text, SourceCodeKind.Script);
                Adopt(Code);
                return;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Diag.Warn($"Editor code-intelligence unavailable ({error.Message}); scripts still run.", "editor");
            }
        }
        Plain.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        Plain.Document = new AvaloniaEdit.Document.TextDocument(document.Text);
        Fallback.Notice = new Notice(NoticeSeverity.Warning, "Code completion is unavailable. Scripts still run.", DateTimeOffset.UtcNow);
        Fallback.IsVisible = true;
        Code.IsVisible = false;
        Plain.IsVisible = true;
        Adopt(Plain);
    }

    void Adopt(TextEditor editor)
    {
        if (_document is not { } document)
            return;
        Loading.IsVisible = false;
        _surface = editor;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        editor.Options.AllowScrollBelowDocument = true;
        editor.Options.HighlightCurrentLine = false;
        editor.TextArea.TextView.LineTransformers.Add(_directives);
        editor.TextArea.TextView.Margin = new Thickness(8, 0, 0, 0);
        foreach (Control margin in editor.TextArea.LeftMargins)
        {
            if (margin is LineNumberMargin)
                margin.Margin = new Thickness(8, 0, 0, 0);
        }
        editor.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        editor.ContextMenu = EditMenu(editor);
        if (_preferences is { } preferences)
            Apply(preferences);
        _buffer = new EditorBufferAdapter(editor, document);
        document.AttachBuffer(_buffer);
    }

    void OnWheel(object? sender, PointerWheelEventArgs args)
    {
        if (_preferences is not { } preferences || !args.KeyModifiers.HasFlag(PrimaryModifier.Current))
            return;
        if (args.Delta.Y > 0)
            preferences.ZoomIn();
        else if (args.Delta.Y < 0)
            preferences.ZoomOut();
        args.Handled = true;
    }

    void OnPreferencesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (_preferences is { } preferences)
            Apply(preferences);
    }

    static ContextMenu EditMenu(TextEditor editor)
    {
        var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        MenuItem cut = Entry("Cut", Primary(hotkeys?.Cut), editor.Cut);
        MenuItem copy = Entry("Copy", Primary(hotkeys?.Copy), editor.Copy);
        MenuItem paste = Entry("Paste", Primary(hotkeys?.Paste), editor.Paste);
        var menu = new ContextMenu();
        menu.Items.Add(cut);
        menu.Items.Add(copy);
        menu.Items.Add(paste);
        menu.Items.Add(new Separator());
        menu.Items.Add(Entry("Select all", Primary(hotkeys?.SelectAll), editor.SelectAll));
        menu.Opening += (_, _) =>
        {
            bool selected = !editor.TextArea.Selection.IsEmpty;
            cut.IsEnabled = selected && !editor.IsReadOnly;
            copy.IsEnabled = selected;
            paste.IsEnabled = !editor.IsReadOnly;
        };
        return menu;
    }

    static MenuItem Entry(string header, KeyGesture? gesture, Action action)
    {
        var item = new MenuItem { Header = header, InputGesture = gesture };
        item.Click += (_, _) => action();
        return item;
    }

    static KeyGesture? Primary(List<KeyGesture>? gestures) => gestures is { Count: > 0 } ? gestures[0] : null;

    void Apply(EditorPreferences preferences)
    {
        Code.FontSize = preferences.FontSize;
        Code.WordWrap = preferences.WordWrap;
        Plain.FontSize = preferences.FontSize;
        Plain.WordWrap = preferences.WordWrap;
    }

    void OnThemeChanged(object? sender, EventArgs args)
    {
        if (ReferenceEquals(_surface, Code))
        {
            Code.ClassificationHighlightColors = ThemeColors();
            Code.TextArea.TextView.Redraw();
            return;
        }
        _surface?.TextArea.TextView.Redraw();
    }

    IClassificationHighlightColors ThemeColors() => new QxEditorColors(TokenColor);

    Color? TokenColor(string key) => TokenBrush(key) is ISolidColorBrush brush ? brush.Color : null;

    IBrush? DirectiveBrush(DirectiveRole role) => TokenBrush(role switch
    {
        DirectiveRole.Comment => "QxSyntaxCommentBrush",
        DirectiveRole.Keyword => "QxSyntaxKeywordBrush",
        DirectiveRole.String => "QxSyntaxStringBrush",
        DirectiveRole.Number => "QxSyntaxNumberBrush",
        DirectiveRole.Type => "QxSyntaxTypeBrush",
        DirectiveRole.Parameter => "QxSyntaxParameterBrush",
        _ => "QxSyntaxPunctuationBrush"
    });

    IBrush? TokenBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? found) && found is IBrush brush ? brush : null;
}
