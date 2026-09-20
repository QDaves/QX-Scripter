using System.ComponentModel;
using Qx.Diagnostics;
using Qx.Hosting;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Runs;

public sealed class ScriptRunRegistry : IScriptRunRegistry, IDisposable
{
    readonly IScriptWorkspace _workspace;
    readonly ScriptExecutionService _scripts;
    readonly CoalescingSignal _refresh;
    readonly List<ScriptDocument> _watched = [];
    HashSet<string> _live = new(PathComparison.Comparer);
    HashSet<string> _working = new(PathComparison.Comparer);
    int _running;
    int _external;

    public ScriptRunRegistry(IScriptWorkspace workspace, DesktopRuntime runtime, IUiDispatcher dispatcher)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ArgumentNullException.ThrowIfNull(runtime);
        _scripts = runtime.Scripts;
        _refresh = new CoalescingSignal(dispatcher, Refresh, UiPriority.Background);
        _workspace.DocumentsChanged += OnDocumentsChanged;
        _scripts.ActiveRunsChanged += OnActiveRunsChanged;
        Rewatch();
    }

    public int RunningCount => _running;

    public int ExternalCount => _external;

    public IReadOnlySet<string> LivePaths => _live;

    public IReadOnlySet<string> WorkingPaths => _working;

    public event Action? Changed;

    public int StopAll()
    {
        Refresh();
        int external = _external;
        int stopped = 0;
        foreach (ScriptDocument document in _workspace.Documents)
        {
            if (document.Run.RequestStop())
                stopped++;
        }
        _scripts.RequestStopAll();
        stopped += external;
        if (stopped > 0)
            Diag.Warn($"Stopped {stopped} running script(s).", "scripts");
        return stopped;
    }

    public async Task WhenAllStoppedAsync(CancellationToken cancellation_token)
    {
        var pending = new List<Task>();
        foreach (ScriptDocument document in _workspace.Documents)
            pending.Add(document.Run.Completion);
        pending.Add(_scripts.WhenAllStoppedAsync(cancellation_token));
        await Task.WhenAll(pending).WaitAsync(cancellation_token);
    }

    public void Dispose()
    {
        _workspace.DocumentsChanged -= OnDocumentsChanged;
        _scripts.ActiveRunsChanged -= OnActiveRunsChanged;
        Unwatch();
    }

    void OnDocumentsChanged()
    {
        Rewatch();
        Refresh();
    }

    void OnActiveRunsChanged() => _refresh.Raise();

    void OnRunChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ScriptRunController.State) or nameof(ScriptRunController.PanelArmed) or nameof(ScriptRunController.BusyHandlers))
            Refresh();
    }

    void Rewatch()
    {
        Unwatch();
        foreach (ScriptDocument document in _workspace.Documents)
        {
            document.Run.PropertyChanged += OnRunChanged;
            _watched.Add(document);
        }
    }

    void Unwatch()
    {
        foreach (ScriptDocument document in _watched)
            document.Run.PropertyChanged -= OnRunChanged;
        _watched.Clear();
    }

    void Refresh()
    {
        var live = new HashSet<string>(PathComparison.Comparer);
        var working = new HashSet<string>(PathComparison.Comparer);
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int alive = 0;
        foreach (ScriptDocument document in _workspace.Documents)
        {
            if (!document.Run.IsAlive)
                continue;
            alive++;
            owned.Add(document.FilePath is { } path ? PathComparison.Full(path) : document.ExecutionIdentity);
            if (document.FilePath is { } file)
            {
                live.Add(file);
                if (document.Run.IsWorking)
                    working.Add(file);
            }
        }
        int external = 0;
        foreach (ActiveScriptRun run in _scripts.ActiveRuns)
        {
            if (owned.Contains(run.SourceIdentity))
                continue;
            external++;
            if (Path.IsPathRooted(run.SourceIdentity))
            {
                live.Add(run.SourceIdentity);
                working.Add(run.SourceIdentity);
            }
        }
        bool changed = alive + external != _running || external != _external || !_live.SetEquals(live) || !_working.SetEquals(working);
        _running = alive + external;
        _external = external;
        _live = live;
        _working = working;
        if (changed)
            Changed?.Invoke();
    }
}
