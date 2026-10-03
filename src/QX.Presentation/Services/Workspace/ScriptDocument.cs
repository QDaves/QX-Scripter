using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Panels;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.Editor;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Workspace;

public interface IScriptTextBuffer
{
    string Text { get; }

    void Replace(string text);

    void Insert(string text, int caretOffset);

    bool GoTo(int line, int column);

    void Focus();
}

public enum DocumentBadge
{
    Idle,
    Compiling,
    Running,
    Armed,
    Failed
}

public sealed record DocumentParts(ScriptRunController Run, OutputConsoleViewModel Console, PanelDocument Panel);

public sealed partial class ScriptDocument : ObservableObject, IRunSource, IDisposable
{
    public static readonly TimeSpan PanelProbeDelay = TimeSpan.FromMilliseconds(250);

    readonly Debouncer _panel_probe;
    IScriptTextBuffer? _buffer;
    string _text;
    bool _disposed;
    bool _console_seeded;
    string? _disk_text;

    public ScriptDocument(string name, string text, string? filePath, IUiDispatcher dispatcher, TimeProvider time, Func<ScriptDocument, DocumentParts> parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parts);
        _text = text ?? throw new ArgumentNullException(nameof(text));
        Name = ScriptFileName.FromDirective(text) ?? name;
        FilePath = filePath is null ? null : PathComparison.Full(filePath);
        ExecutionIdentity = "ui:" + Guid.NewGuid().ToString("N");
        Describe(UiSpec.Parse(text));
        _panel_probe = new Debouncer(dispatcher, time, PanelProbeDelay, ProbePanel);
        DocumentParts built = parts(this);
        Run = built.Run;
        Console = built.Console;
        Panel = built.Panel;
        Run.PropertyChanged += OnRunChanged;
        SeedConsole();
    }

    public ScriptRunController Run { get; }

    public OutputConsoleViewModel Console { get; }

    public PanelDocument Panel { get; }

    public bool IsClosed { get; private set; }

    public string StatusText => Run.IsArmedIdle && Run.IsAlive ? "ready" : Run.State switch
    {
        ScriptRunState.Compiling => "compiling…",
        ScriptRunState.Running => "running…",
        ScriptRunState.Stopping => "stopping…",
        ScriptRunState.Finished => "done",
        ScriptRunState.Stopped => "stopped",
        ScriptRunState.Faulted => "error",
        _ => ""
    };

    public DocumentBadge Badge => Run.State switch
    {
        ScriptRunState.Compiling => DocumentBadge.Compiling,
        ScriptRunState.Running or ScriptRunState.Stopping => Run.IsArmedIdle ? DocumentBadge.Armed : DocumentBadge.Running,
        ScriptRunState.Faulted => DocumentBadge.Failed,
        _ => DocumentBadge.Idle
    };

    public string ExecutionIdentity { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibilityText))]
    public partial string Name { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaved), nameof(LibraryName))]
    public partial string? FilePath { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibilityText))]
    public partial bool IsModified { get; private set; }

    [ObservableProperty]
    public partial bool PanelMode { get; private set; }

    [ObservableProperty]
    public partial bool HasUi { get; private set; }

    [ObservableProperty]
    public partial bool PanelRequired { get; private set; }

    [ObservableProperty]
    public partial UiConsole? PanelConsole { get; private set; }

    public bool IsSaved => FilePath is not null;

    public string? LibraryName => FilePath is null ? null : ScriptFileName.NameOf(FilePath);

    public string Text => _buffer?.Text ?? _text;

    public IScriptTextBuffer? Buffer => _buffer;

    public string AccessibilityText => string.Join(", ", new[] { Name, RunWord, IsModified ? "unsaved changes" : "" }.Where(part => part.Length > 0));

    string RunWord => Run.IsArmedIdle && Run.IsAlive ? "ready" : Run.State switch
    {
        ScriptRunState.Compiling => "compiling",
        ScriptRunState.Running => "running",
        ScriptRunState.Stopping => "stopping",
        ScriptRunState.Finished => "finished",
        ScriptRunState.Stopped => "stopped",
        ScriptRunState.Faulted => "failed",
        _ => ""
    };

    public event Action<ScriptDocument>? TextReplaced;

    public void AttachBuffer(IScriptTextBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (!string.Equals(buffer.Text, _text, StringComparison.Ordinal))
            buffer.Replace(_text);
        _buffer = buffer;
    }

    public void DetachBuffer(IScriptTextBuffer buffer)
    {
        if (!ReferenceEquals(_buffer, buffer))
            return;
        _text = buffer.Text;
        _buffer = null;
    }

    public void NoteEdited()
    {
        IsModified = true;
        RefreshName();
        _panel_probe.Trigger();
    }

    public void ReplaceText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_buffer is { } buffer)
            buffer.Replace(text);
        else
            _text = text;
        NoteEdited();
        _panel_probe.Flush();
        TextReplaced?.Invoke(this);
    }

    public void MarkSaved(string path, string savedText)
    {
        ArgumentNullException.ThrowIfNull(savedText);
        MoveTo(path);
        IsModified = !string.Equals(savedText, Text, StringComparison.Ordinal);
    }

    public void MoveTo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = PathComparison.Full(path);
        RefreshName();
    }

    public void MarkClean(string savedText)
    {
        if (string.Equals(savedText, Text, StringComparison.Ordinal))
            IsModified = false;
    }

    /// <summary>
    /// Records what the file on disk holds now and says whether that is news: different from the
    /// tab and from what was last noted, so each outside change is acted on once.
    /// </summary>
    public bool NoteDiskText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.Equals(text, Text, StringComparison.Ordinal) || string.Equals(text, _disk_text, StringComparison.Ordinal))
        {
            _disk_text = text;
            return false;
        }
        _disk_text = text;
        return true;
    }

    public void Reload(string text)
    {
        ReplaceText(text);
        MarkClean(text);
    }

    public void MarkUnsaved()
    {
        FilePath = null;
        IsModified = true;
    }

    public void MarkModified() => IsModified = true;

    public void Rename(string typed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typed);
        Name = typed.Trim();
        IsModified = true;
    }

    public void SetPanelMode(bool panelMode) => PanelMode = panelMode && HasUi;

    internal void MarkClosed() => IsClosed = true;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Run.PropertyChanged -= OnRunChanged;
        _panel_probe.Dispose();
        Console.Dispose();
        Panel.Dispose();
    }

    void OnRunChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ScriptRunController.State) or nameof(ScriptRunController.PanelArmed) or nameof(ScriptRunController.BusyHandlers))
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(AccessibilityText));
            OnPropertyChanged(nameof(Badge));
        }
    }

    void RefreshName()
    {
        if (ScriptFileName.FromDirective(Text) is { } directive)
            Name = directive;
        else if (FilePath is { } path)
            Name = ScriptFileName.NameOf(path);
    }

    void Describe(UiSpec spec)
    {
        HasUi = spec.HasUi;
        PanelRequired = spec.Required;
        PanelConsole = spec.Console;
    }

    void SeedConsole()
    {
        if (_console_seeded || PanelConsole is not { } options)
            return;
        _console_seeded = true;
        if (options.Height is { } height && height > 0 && Console.ExpandedHeight is null)
            Console.ExpandedHeight = height;
        if (!options.Collapsed)
            Console.Expand();
    }

    void ProbePanel()
    {
        Describe(UiSpec.Parse(Text));
        SeedConsole();
        if (!HasUi && PanelMode)
            PanelMode = false;
        if (PanelMode)
            Panel.Rebuild(Text);
    }
}
