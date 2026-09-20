using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis;
using Qx.Hosting;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Output;
using Qx.Presentation.Threading;
using Qx.Scripting;

namespace Qx.Presentation.Services.Runs;

public enum RunPhase
{
    Idle,
    Compiling,
    Running,
    Ready,
    Stopping
}

public enum RunStartOutcome
{
    Started,
    AlreadyAlive
}

public enum PressOutcome
{
    HandlerStarted,
    StopRequested,
    RunStarted
}

public interface IRunSource
{
    string Name { get; }

    string? FilePath { get; }

    string ExecutionIdentity { get; }

    string Text { get; }
}

public interface IRunHooks
{
    Task SaveBeforeRunAsync(string code, CancellationToken cancellation_token);

    void RecordStarted(DateTimeOffset at);

    void RecordFinished(ScriptRunState state);
}

public interface IPanelRunTarget
{
    void BeginStarting(string? pressed_button);

    void EndStarting(string? pressed_button);

    void SetRunBusy(bool busy);

    IDisposable Attach(ScriptUi ui, long run_epoch, string file_name, string? pressed_button, CancellationToken run_token);

    void SetButtonBusy(string button, bool busy);

    IReadOnlySet<string> DeclaredButtons { get; }
}

public sealed partial class ScriptRunController : ObservableObject
{
    readonly IRunSource _source;
    readonly ScriptExecutionService _scripts;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly IRunHooks _hooks;
    readonly IPanelRunTarget _panel;
    readonly CancellationToken _lifetime;
    readonly ObservableCollection<ScriptExecutionError> _errors = [];
    RunFrame? _frame;
    ArmedRun? _armed;
    long _epoch;

    public ScriptRunController(
        IRunSource source,
        ScriptExecutionService scripts,
        IUiDispatcher dispatcher,
        TimeProvider time,
        IRunHooks hooks,
        IPanelRunTarget panel,
        OutputBuffer output,
        CancellationToken lifetime)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _lifetime = lifetime;
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Errors = new ReadOnlyObservableCollection<ScriptExecutionError>(_errors);
    }

    public OutputBuffer Output { get; }

    public ReadOnlyObservableCollection<ScriptExecutionError> Errors { get; }

    public Task Completion { get; private set; } = Task.CompletedTask;

    public event Action? Started;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlive), nameof(IsWorking), nameof(IsArmedIdle), nameof(IsRunning), nameof(IsCompiling), nameof(IsStopping), nameof(IsFaulted), nameof(Phase))]
    public partial ScriptRunState State { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking), nameof(IsArmedIdle), nameof(Phase))]
    public partial bool PanelArmed { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking), nameof(IsArmedIdle), nameof(Phase))]
    public partial int BusyHandlers { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? StartedAt { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? FinishedAt { get; private set; }

    public bool IsAlive => State is ScriptRunState.Compiling or ScriptRunState.Running or ScriptRunState.Stopping;

    public bool IsArmedIdle => PanelArmed && BusyHandlers == 0;

    public bool IsWorking => IsAlive && !IsArmedIdle;

    public bool IsRunning => State == ScriptRunState.Running;

    public bool IsCompiling => State == ScriptRunState.Compiling;

    public bool IsStopping => State == ScriptRunState.Stopping;

    public bool IsFaulted => State == ScriptRunState.Faulted;

    public RunPhase Phase => State switch
    {
        ScriptRunState.Stopping => RunPhase.Stopping,
        ScriptRunState.Compiling => RunPhase.Compiling,
        _ => IsArmedIdle ? RunPhase.Ready : IsAlive ? RunPhase.Running : RunPhase.Idle
    };

    public double RuntimeMs => StartedAt is { } started ? ((FinishedAt ?? _time.GetUtcNow()) - started).TotalMilliseconds : 0;

    public bool IsCurrent(long run_epoch) => IsAlive && _frame is { } frame && frame.Epoch == run_epoch;

    public RunStartOutcome Start(string? pressed_button, bool panel_mode)
    {
        if (IsAlive)
            return RunStartOutcome.AlreadyAlive;
        var frame = new RunFrame(++_epoch, CancellationTokenSource.CreateLinkedTokenSource(_lifetime), panel_mode);
        _frame = frame;
        _armed = null;
        _errors.Clear();
        PanelArmed = false;
        BusyHandlers = 0;
        StartedAt = _time.GetUtcNow();
        FinishedAt = null;
        State = ScriptRunState.Compiling;
        _hooks.RecordStarted(_time.GetLocalNow());
        Completion = RunAsync(frame, pressed_button);
        Started?.Invoke();
        return RunStartOutcome.Started;
    }

    public bool RequestStop()
    {
        if (!IsAlive || _frame is not { } frame)
            return false;
        State = ScriptRunState.Stopping;
        frame.Source.CancelAsync().Observe("scripts");
        return true;
    }

    public PressOutcome Press(string button)
    {
        ArgumentException.ThrowIfNullOrEmpty(button);
        if (_armed is { } armed && armed.Globals.Ui.HandledButtons.Contains(button, StringComparer.OrdinalIgnoreCase))
            return FireHandler(armed, button) ? PressOutcome.HandlerStarted : PressOutcome.StopRequested;
        if (IsAlive)
        {
            RequestStop();
            return PressOutcome.StopRequested;
        }
        Start(button, panel_mode: true);
        return PressOutcome.RunStarted;
    }

    partial void OnStateChanged(ScriptRunState value) => _panel.SetRunBusy(IsAlive && !PanelArmed);

    partial void OnPanelArmedChanged(bool value) => _panel.SetRunBusy(IsAlive && !PanelArmed);

    async Task RunAsync(RunFrame frame, string? pressed_button)
    {
        string file_name = _source.FilePath ?? ScriptFileName.Normalize(_source.Name) + ScriptFileName.Extension;
        string identity = _source.FilePath is { } path ? Path.GetFullPath(path) : _source.ExecutionIdentity;
        string code = _source.Text;
        IDisposable? link = null;
        ScriptRunState terminal = ScriptRunState.Faulted;
        try
        {
            Output.Clear();
            if (frame.PanelMode)
                _panel.BeginStarting(pressed_button);
            await _hooks.SaveBeforeRunAsync(code, frame.Source.Token);
            ScriptExecutionResult result = await _scripts.RunAsync(new ScriptExecutionRequest
            {
                Code = code,
                SourceIdentity = identity,
                FileName = file_name,
                OutputWritten = line => Output.Write(line, OutputLevel.Info),
                DiagnosticReported = diagnostic =>
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Warning)
                        Output.Write("Warning " + ScriptExecutionError.FromDiagnostic(diagnostic, file_name).Format(), OutputLevel.Warning);
                },
                ErrorReported = error =>
                {
                    Output.Write(error.Format(), OutputLevel.Error);
                    _dispatcher.Post(() => AddError(frame, error));
                },
                StateChanged = state => _dispatcher.Post(() => ApplyState(frame, state)),
                ConfigureAsync = frame.PanelMode
                    ? (globals, token) => _dispatcher.InvokeAsync(() => link = _panel.Attach(globals.Ui, frame.Epoch, file_name, pressed_button, frame.Source.Token), token)
                    : null,
                ContinueAsync = frame.PanelMode ? (globals, token) => ParkAsync(frame, globals, pressed_button, token) : null,
                DrainAsync = frame.PanelMode ? () => DrainHandlersAsync(frame) : null
            }, frame.Source.Token);
            terminal = result.State;
            frame.PublishTerminal();
            if (ReferenceEquals(_frame, frame))
            {
                if (terminal == ScriptRunState.Finished)
                    Output.Write("[finished]", OutputLevel.Info);
                else if (terminal == ScriptRunState.Stopped)
                    Output.Write("[stopped]", OutputLevel.Info);
            }
        }
        catch (Exception error)
        {
            frame.PublishTerminal();
            ScriptExecutionError host_error = ScriptExecutionError.FromException(error, "host", file_name);
            if (ReferenceEquals(_frame, frame))
                Output.Write(host_error.Format(), OutputLevel.Error);
            AddError(frame, host_error);
            terminal = ScriptRunState.Faulted;
        }
        finally
        {
            link?.Dispose();
            _hooks.RecordFinished(terminal);
            if (ReferenceEquals(_frame, frame))
            {
                _armed = null;
                PanelArmed = false;
                BusyHandlers = 0;
                FinishedAt = _time.GetUtcNow();
                State = terminal;
                if (frame.PanelMode)
                    _panel.EndStarting(pressed_button);
            }
            frame.Source.Dispose();
        }
    }

    void ApplyState(RunFrame frame, ScriptRunState state)
    {
        if (!ReferenceEquals(_frame, frame) || frame.IsTerminalPublished)
            return;
        if (state is not (ScriptRunState.Compiling or ScriptRunState.Running or ScriptRunState.Stopping))
            return;
        if (State == ScriptRunState.Stopping)
            return;
        State = state;
    }

    void AddError(RunFrame frame, ScriptExecutionError error)
    {
        if (ReferenceEquals(_frame, frame))
            _errors.Add(error);
    }

    async Task ParkAsync(RunFrame frame, ScriptGlobals globals, string? pressed_button, CancellationToken cancellation_token)
    {
        bool armed = await _dispatcher.InvokeAsync(() => Arm(frame, globals, pressed_button), cancellation_token);
        if (armed)
            await frame.Finished.WaitAsync(cancellation_token);
    }

    bool Arm(RunFrame frame, ScriptGlobals globals, string? pressed_button)
    {
        if (!ReferenceEquals(_frame, frame))
            return false;
        _panel.EndStarting(pressed_button);
        if (!globals.Ui.HasClickHandlers)
            return false;
        foreach (string handled in globals.Ui.HandledButtons)
        {
            if (!_panel.DeclaredButtons.Contains(handled))
                Output.Write($"warning: Ui.OnClick(\"{handled}\", ...) has no //@ui:button {handled}", OutputLevel.Warning);
        }
        var armed = new ArmedRun(frame, globals);
        _armed = armed;
        PanelArmed = true;
        if (pressed_button is { Length: > 0 } && globals.Ui.HandledButtons.Contains(pressed_button, StringComparer.OrdinalIgnoreCase))
            FireHandler(armed, pressed_button);
        return true;
    }

    bool FireHandler(ArmedRun armed, string button)
    {
        if (armed.Frame.StartHandlerAsync(() => armed.Globals.Ui.Invoke(button) ?? Task.CompletedTask) is not { } work)
            return false;
        BusyHandlers++;
        _panel.SetButtonBusy(button, true);
        WatchHandlerAsync(armed, button, work).Observe("scripts");
        return true;
    }

    async Task WatchHandlerAsync(ArmedRun armed, string button, Task work)
    {
        try
        {
            await work.WaitAsync(CancellationToken.None);
        }
        catch (Exception thrown)
        {
            IEnumerable<Exception> faults = work.Exception is { } failure ? failure.InnerExceptions : [thrown];
            foreach (Exception error in faults)
            {
                if (error is ScriptFinishedException)
                {
                    armed.Frame.Finish();
                    continue;
                }
                if (error is OperationCanceledException)
                    continue;
                ScriptExecutionError handler_error = ScriptExecutionError.FromException(error, "handler", _source.FilePath ?? _source.Name);
                if (ReferenceEquals(_frame, armed.Frame))
                    Output.Write(handler_error.Format(), OutputLevel.Error);
                AddError(armed.Frame, handler_error);
            }
        }
        finally
        {
            armed.Frame.ForgetHandler(work);
            if (ReferenceEquals(_armed, armed))
                BusyHandlers = Math.Max(0, BusyHandlers - 1);
            _panel.SetButtonBusy(button, false);
        }
    }

    async Task DrainHandlersAsync(RunFrame frame)
    {
        Task[] pending = frame.BeginDrain();
        _dispatcher.Post(() => Disarm(frame));
        if (pending.Length == 0)
            return;
        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
        }
    }

    void Disarm(RunFrame frame)
    {
        if (_armed is { } armed && ReferenceEquals(armed.Frame, frame))
            _armed = null;
    }

    sealed class RunFrame(long epoch, CancellationTokenSource source, bool panel_mode)
    {
        readonly Lock _handlers_gate = new();
        readonly List<Task> _handlers = [];
        readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool _draining;
        int _terminal_published;

        public long Epoch { get; } = epoch;

        public CancellationTokenSource Source { get; } = source;

        public bool PanelMode { get; } = panel_mode;

        public bool IsTerminalPublished => Volatile.Read(ref _terminal_published) != 0;

        public Task Finished => _finished.Task;

        public void PublishTerminal() => Interlocked.Exchange(ref _terminal_published, 1);

        public void Finish() => _finished.TrySetResult();

        public Task? StartHandlerAsync(Func<Task> handler)
        {
            lock (_handlers_gate)
            {
                if (_draining)
                    return null;
                Task work = Task.Run(handler);
                _handlers.Add(work);
                return work;
            }
        }

        public void ForgetHandler(Task work)
        {
            lock (_handlers_gate)
                _handlers.Remove(work);
        }

        public Task[] BeginDrain()
        {
            lock (_handlers_gate)
            {
                _draining = true;
                return [.. _handlers];
            }
        }
    }

    sealed class ArmedRun(RunFrame frame, ScriptGlobals globals)
    {
        public RunFrame Frame { get; } = frame;

        public ScriptGlobals Globals { get; } = globals;
    }
}
