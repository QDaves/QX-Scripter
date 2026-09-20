using Qx.Diagnostics;
using Qx.Mcp;
using Qx.Presentation.Input;
using Qx.Presentation.Navigation;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Services.Updates;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Lifecycle;

public sealed class ShellStartup
{
    readonly LaunchOptions _launch;
    readonly INavigationService _navigation;
    readonly DesktopRuntime _runtime;
    readonly ISessionStatusService _status;
    readonly DeferredEditorBridge _bridge;
    readonly IReadOnlyList<IEditorBridge> _bridge_targets;
    readonly IReadOnlyList<IEditorWarmup> _warmups;
    readonly UpdateNoticeCoordinator _updates;
    readonly HostedLifecyclePolicy _hosted;
    readonly IScriptWorkspace _workspace;
    readonly PanicKey _panic;
    readonly ICommandRegistry _commands;

    public ShellStartup(
        LaunchOptions launch,
        INavigationService navigation,
        DesktopRuntime runtime,
        ISessionStatusService status,
        DeferredEditorBridge bridge,
        IEnumerable<IEditorBridge> bridge_targets,
        IEnumerable<IEditorWarmup> warmups,
        UpdateNoticeCoordinator updates,
        HostedLifecyclePolicy hosted,
        IScriptWorkspace workspace,
        PanicKey panic,
        ICommandRegistry commands)
    {
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        ArgumentNullException.ThrowIfNull(bridge_targets);
        ArgumentNullException.ThrowIfNull(warmups);
        _bridge_targets = [.. bridge_targets];
        _warmups = [.. warmups];
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));
        _hosted = hosted ?? throw new ArgumentNullException(nameof(hosted));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _panic = panic ?? throw new ArgumentNullException(nameof(panic));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public async Task RunAsync(CancellationToken lifetime)
    {
        await StepAsync("editor bridge", () =>
        {
            if (_bridge_targets.Count > 1)
                Diag.Warn($"{_bridge_targets.Count} editor bridges are registered; the first one is attached.", "editor");
            if (_bridge_targets.Count > 0)
                _bridge.Attach(_bridge_targets[0]);
            return Task.CompletedTask;
        });
        await StepAsync("workspace restore", () => _workspace.RestoreAsync(lifetime));
        await StepAsync("navigation", () =>
        {
            _navigation.Start();
            return Task.CompletedTask;
        });
        await StepAsync("runtime", async () =>
        {
            await _runtime.StartAsync(lifetime);
            _status.RefreshRuntime();
            _runtime.TransportTask.Observe("host");
        });
        await StepAsync("panic key", async () =>
        {
            await _panic.RegisterAsync(lifetime);
            _commands.Update(ShellCommandCatalog.RunStopAll, command => command with { GestureText = _panic.GestureText });
        });
        await StepAsync("editor warm-up", () =>
        {
            foreach (IEditorWarmup warmup in _warmups)
                warmup.WarmUp();
            return Task.CompletedTask;
        });
        await StepAsync("updates", () =>
        {
            _updates.Start();
            return Task.CompletedTask;
        });
        await StepAsync("hosted watchdog", () =>
        {
            if (_launch.HostedByGEarth)
                _hosted.StartWatchdog();
            return Task.CompletedTask;
        });
        Diag.Info("Application initialized", "app");
    }

    static async Task StepAsync(string name, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Diag.Error($"Startup step {name} failed: {error}", "app");
        }
    }
}
