using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Qx.Scripting;
using RoslynPad.Editor;

namespace Qx.Ui;

public sealed class ScriptTab : INotifyPropertyChanged
{
    /// <summary>
    /// How many console lines a tab keeps.
    /// </summary>
    /// <remarks>
    /// A line count rather than a character count: the view is virtualized and renders one
    /// container per line, so lines are what cost anything. The old character cap forced the
    /// whole buffer to be re-flowed into the console on every write once it was reached, which
    /// a script logging a line a second reached within the hour and never recovered from.
    /// </remarks>
    private const int MaxOutputLines = 5_000;

    /// <summary>How many lines are dropped at once when the cap is reached.</summary>
    /// <remarks>
    /// Trimming one line per write would put a collection change behind every line forever.
    /// Dropping a block amortises it to one change per <see cref="OutputTrimBlock"/> lines.
    /// </remarks>
    private const int OutputTrimBlock = 500;
    private string _name = "untitled";
    private bool _isModified;
    private string? _filePath;
    private ScriptRunState _runState;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;
    private bool _panelMode;
    private bool _panelArmed;
    private int _handlers;
    private bool _outputCollapsed = true;
    private double _outputHeight = 170;
    private readonly List<ScriptExecutionError> _errors = [];
    private readonly Stopwatch _runtime = new();

    internal string ExecutionIdentity { get; } = "ui:" + Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            Changed(nameof(Name));
            Changed(nameof(Header));
            Changed(nameof(AccessibilityText));
        }
    }

    public string Header => _name;
    public string AccessibilityText
    {
        get
        {
            string state = IsArmedIdle
                ? "ready"
                : _runState switch
                {
                    ScriptRunState.Compiling => "compiling",
                    ScriptRunState.Running => "running",
                    ScriptRunState.Stopping => "stopping",
                    ScriptRunState.Finished => "finished",
                    ScriptRunState.Stopped => "stopped",
                    ScriptRunState.Faulted => "failed",
                    _ => ""
                };
            string modified = _isModified ? "unsaved changes" : "";
            return string.Join(", ", new[] { _name, state, modified }.Where(value => value.Length > 0));
        }
    }

    public ScriptRunState RunState => _runState;
    public bool IsRunning => _runState == ScriptRunState.Running;
    public bool IsCompiling => _runState == ScriptRunState.Compiling;
    public bool IsStopping => _runState == ScriptRunState.Stopping;

    /// <summary>
    /// Whether a run exists at all, whatever it is doing.
    /// </summary>
    /// <remarks>
    /// This is what a stop acts on and what tells a second Run press from a first. It is not the
    /// same as <see cref="IsWorking"/>: a panel parked on its handlers is alive without doing
    /// anything, and starting a "second" run over it would leave two runs on one tab.
    /// </remarks>
    public bool IsAlive =>
        _runState is ScriptRunState.Compiling or ScriptRunState.Running or ScriptRunState.Stopping;

    /// <summary>
    /// Whether the tab should read as busy — the spinner in the strip, "running" in the status line.
    /// </summary>
    /// <remarks>
    /// An armed panel deliberately does not. Its run only stays up so the buttons answer, and
    /// showing that as a script hard at work made every panel look like one stuck in a loop.
    /// </remarks>
    public bool IsWorking => IsAlive && !IsArmedIdle;

    /// <summary>A panel run waiting for a press, with no handler in flight.</summary>
    public bool IsArmedIdle => _panelArmed && _handlers == 0;

    /// <summary>
    /// Whether a panel run is parked on the click handlers its body registered.
    /// </summary>
    /// <remarks>
    /// Set by the host when the body returns and the run stays up for the buttons, and cleared when
    /// the run ends. Armed on its own says nothing about activity — <see cref="BeginHandler"/> does.
    /// </remarks>
    public bool PanelArmed
    {
        get => _panelArmed;
        set
        {
            if (_panelArmed == value)
                return;
            _panelArmed = value;
            RaiseActivity();
        }
    }

    /// <summary>How many button handlers are running right now.</summary>
    public int BusyHandlers => _handlers;

    /// <summary>Counts a press whose handler has started, which is real activity to show.</summary>
    public void BeginHandler()
    {
        _handlers++;
        RaiseActivity();
    }

    /// <summary>Counts a press whose handler is over.</summary>
    public void EndHandler()
    {
        _handlers = Math.Max(0, _handlers - 1);
        RaiseActivity();
    }

    public bool IsFaulted => _runState == ScriptRunState.Faulted;
    public DateTimeOffset? StartedAt => _startedAt;
    public DateTimeOffset? FinishedAt => _finishedAt;
    public IReadOnlyList<ScriptExecutionError> Errors => _errors;
    public double RuntimeMs => _runtime.Elapsed.TotalMilliseconds;

    public bool IsModified
    {
        get => _isModified;
        set
        {
            _isModified = value;
            Changed(nameof(IsModified));
            Changed(nameof(Header));
            Changed(nameof(AccessibilityText));
        }
    }

    public string StatusText => IsArmedIdle
        ? "ready"
        : _runState switch
        {
            ScriptRunState.Compiling => "compiling…",
            ScriptRunState.Running => "running…",
            ScriptRunState.Stopping => "stopping…",
            ScriptRunState.Finished => "done",
            ScriptRunState.Stopped => "stopped",
            ScriptRunState.Faulted => "error",
            _ => ""
        };

    public string? FilePath
    {
        get => _filePath;
        set { _filePath = value; Changed(nameof(FilePath)); Changed(nameof(IsSavedToDisk)); }
    }

    public bool PanelMode
    {
        get => _panelMode;
        set
        {
            if (_panelMode == value)
                return;
            _panelMode = value;
            Changed(nameof(PanelMode));
        }
    }

    public bool OutputCollapsed
    {
        get => _outputCollapsed;
        set
        {
            if (_outputCollapsed == value)
                return;
            _outputCollapsed = value;
            Changed(nameof(OutputCollapsed));
        }
    }

    public double OutputHeight
    {
        get => _outputHeight;
        set => _outputHeight = Math.Max(64, value);
    }

    public bool IsSavedToDisk => _filePath is not null && File.Exists(_filePath);

    public string Code { get; set; } = "";

    public RoslynCodeEditor? Editor { get; set; }
    public bool EditorInitialized { get; set; }

    /// <summary>The tab's console, oldest first.</summary>
    public ObservableCollection<OutputLine> Output { get; } = [];

    public ScriptPanelState PanelState { get; } = new();

    public CancellationTokenSource? Cts { get; set; }
    public Task? ExecutionTask { get; set; }

    public string CurrentCode => Editor is not null && EditorInitialized ? Editor.Text : Code;

    /// <summary>Adds one line to the console, dropping the oldest block once the cap is reached.</summary>
    /// <remarks>
    /// Unlike the character buffer this replaces, the steady state costs one collection change
    /// per line whether the tab has logged ten lines or ten million — the view is virtualized, so
    /// nothing off screen is rendered and nothing is rebuilt.
    /// </remarks>
    /// <param name="message">The line, without a trailing newline.</param>
    /// <param name="level">What kind of line it is; the host sets this, it is never guessed.</param>
    public void AppendOutputLine(string message, OutputLevel level = OutputLevel.Info)
    {
        Output.Add(new OutputLine(message, level, DateTime.Now));
        if (Output.Count <= MaxOutputLines)
            return;

        for (int dropped = 0; dropped < OutputTrimBlock && Output.Count > 0; dropped++)
            Output.RemoveAt(0);
    }

    /// <summary>The whole console as text, which is what a copy puts on the clipboard.</summary>
    public string OutputText() =>
        string.Join(Environment.NewLine, Output.Select(line => line.Text));

    public void BeginExecution()
    {
        _errors.Clear();
        _startedAt = DateTimeOffset.UtcNow;
        _finishedAt = null;
        _runtime.Restart();

        // A run that ended without its finally reaching here — a tab closed under it, say — must not
        // leave the next run looking armed before it has registered anything.
        _panelArmed = false;
        _handlers = 0;
        SetRunState(ScriptRunState.Compiling);
        Changed(nameof(Errors));
        Changed(nameof(StartedAt));
        Changed(nameof(FinishedAt));
        Changed(nameof(RuntimeMs));
    }

    public void SetRunState(ScriptRunState state)
    {
        _runState = state;
        if (state is ScriptRunState.Finished or ScriptRunState.Stopped or ScriptRunState.Faulted)
        {
            _finishedAt = DateTimeOffset.UtcNow;
            _runtime.Stop();
        }
        Changed(nameof(RunState));
        Changed(nameof(IsRunning));
        Changed(nameof(IsCompiling));
        Changed(nameof(IsStopping));
        Changed(nameof(IsAlive));
        Changed(nameof(IsWorking));
        Changed(nameof(IsArmedIdle));
        Changed(nameof(IsFaulted));
        Changed(nameof(StatusText));
        Changed(nameof(AccessibilityText));
        Changed(nameof(FinishedAt));
        Changed(nameof(RuntimeMs));
    }

    /// <summary>Says the tab's activity changed without its run state having moved.</summary>
    private void RaiseActivity()
    {
        Changed(nameof(PanelArmed));
        Changed(nameof(BusyHandlers));
        Changed(nameof(IsWorking));
        Changed(nameof(IsArmedIdle));
        Changed(nameof(StatusText));
        Changed(nameof(AccessibilityText));
    }

    public void AddError(ScriptExecutionError error)
    {
        _errors.Add(error);
        Changed(nameof(Errors));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
