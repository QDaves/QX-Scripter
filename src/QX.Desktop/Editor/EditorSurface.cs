using Avalonia;
using Avalonia.Controls;
using Qx.Desktop.Views.Editor;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Workspace;

namespace Qx.Desktop.Editor;

public sealed class EditorSurface : Panel
{
    public static readonly StyledProperty<ScriptDocument?> DocumentProperty =
        AvaloniaProperty.Register<EditorSurface, ScriptDocument?>(nameof(Document));

    readonly Dictionary<ScriptDocument, ScriptEditorView> _views = new(ReferenceEqualityComparer.Instance);
    RoslynHostProvider? _hosts;
    EditorPreferences? _preferences;

    public ScriptDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public ScriptEditorView? Current => Document is { } document ? _views.GetValueOrDefault(document) : null;

    public void Use(RoslynHostProvider hosts, EditorPreferences preferences)
    {
        _hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        Show(Document);
    }

    public void Retire()
    {
        foreach (ScriptDocument document in _views.Keys.ToArray())
        {
            if (document.IsClosed)
                Drop(document);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
            Show(Document);
    }

    void Show(ScriptDocument? document)
    {
        Retire();
        if (document is not null && _hosts is { } hosts && _preferences is { } preferences && !_views.ContainsKey(document))
        {
            var view = new ScriptEditorView();
            view.Use(document, hosts, preferences);
            _views[document] = view;
            Children.Add(view);
        }
        foreach ((ScriptDocument known, ScriptEditorView view) in _views)
            view.IsVisible = ReferenceEquals(known, document);
    }

    void Drop(ScriptDocument document)
    {
        if (!_views.Remove(document, out ScriptEditorView? view))
            return;
        view.Release();
        Children.Remove(view);
    }
}
