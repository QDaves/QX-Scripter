using Qx.Diagnostics;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Logging;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Lifecycle;

public sealed class ShellCloseCoordinator
{
    public static readonly TimeSpan RunStopBudget = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan FlushBudget = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan SessionEndBudget = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LogFlushBudget = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan DraftSaveBudget = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan SettingsFlushNowBudget = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan StoreFlushNowBudget = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan LogFlushNowBudget = TimeSpan.FromMilliseconds(100);

    readonly LaunchOptions _launch;
    readonly IShellWindow _shell;
    readonly ISettingsStore _settings;
    readonly IWorkspaceSession _workspace;
    readonly IDialogService _dialogs;
    readonly IScriptRunRegistry _runs;
    readonly IScriptPrompts _prompts;
    readonly IScriptLibrary _library;
    readonly PanicKey _panic;
    readonly IOutfitStore _outfits;
    readonly DesktopRuntime _runtime;
    readonly DiagnosticsHub _diagnostics;
    readonly AppLifetime _lifetime;
    bool _pending;

    public ShellCloseCoordinator(
        LaunchOptions launch,
        IShellWindow shell,
        ISettingsStore settings,
        IWorkspaceSession workspace,
        IDialogService dialogs,
        IScriptRunRegistry runs,
        IScriptPrompts prompts,
        IScriptLibrary library,
        PanicKey panic,
        IOutfitStore outfits,
        DesktopRuntime runtime,
        DiagnosticsHub diagnostics,
        AppLifetime lifetime)
    {
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _panic = panic ?? throw new ArgumentNullException(nameof(panic));
        _outfits = outfits ?? throw new ArgumentNullException(nameof(outfits));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public bool IsClosing { get; private set; }

    public bool CanCloseWindowNow { get; private set; }

    public async Task RequestCloseAsync(CloseReason reason)
    {
        if (IsClosing)
            return;
        if (_pending)
        {
            if (reason is not (CloseReason.GEarthLost or CloseReason.GEarthUnavailable))
                return;
            _dialogs.DismissAll();
        }
        else
        {
            _pending = true;
            if (_launch.HostedByGEarth && reason == CloseReason.User)
            {
                _shell.HideForHost();
                _pending = false;
                return;
            }
            Step("persist", Persist);
            if (reason is CloseReason.User or CloseReason.QuitRequested && _workspace.ModifiedCount > 0)
            {
                bool discard = await _dialogs.ConfirmAsync("Close QX Scripter?", DiscardMessage(_workspace.ModifiedCount), "Discard and close", DialogTone.Destructive);
                if (IsClosing)
                    return;
                if (!discard)
                {
                    _pending = false;
                    return;
                }
            }
        }
        IsClosing = true;
        try
        {
            Step("hide", _shell.Hide);
            await StepAsync("stop autosave", _workspace.StopAutosaveAsync);
            Step("close prompts", _prompts.CloseAll);
            Step("dismiss dialogs", _dialogs.DismissAll);
            await StepAsync("drafts", () => reason is CloseReason.User or CloseReason.QuitRequested
                ? _workspace.ClearDraftsAsync(CancellationToken.None)
                : _workspace.SaveDraftsAsync(include_modified_files: true, CancellationToken.None));
            Step("seal drafts", _workspace.SealDrafts);
            Step("release panic key", _panic.Dispose);
            Step("cancel lifetime", _lifetime.Cancel);
            await StepAsync("stop runs", () => BoundedAsync(_runs.WhenAllStoppedAsync(CancellationToken.None), RunStopBudget));
            await StepAsync("flush settings", () => BoundedAsync(_settings.FlushAsync(CancellationToken.None), FlushBudget));
            await StepAsync("flush library", () => BoundedAsync(_library.FlushAsync(CancellationToken.None), FlushBudget));
            await StepAsync("flush wardrobe", () => BoundedAsync(_outfits.FlushAsync(CancellationToken.None), FlushBudget));
            Step("detach diagnostics", _diagnostics.DetachRuntime);
            await StepAsync("dispose runtime", () => _runtime.DisposeAsync().AsTask());
            await StepAsync("flush log", () => BoundedAsync(_diagnostics.FlushAsync(CancellationToken.None), LogFlushBudget));
        }
        finally
        {
            CanCloseWindowNow = true;
            _shell.Shutdown(0);
        }
    }

    public void EndSession()
    {
        if (CanCloseWindowNow)
            return;
        IsClosing = true;
        CanCloseWindowNow = true;
        Step("persist", Persist);
        Step("stop autosave", _workspace.StopAutosave);
        Step("save drafts", () => _workspace.SaveDraftsNow(include_modified_files: true, DraftSaveBudget));
        Step("seal drafts", _workspace.SealDrafts);
        Step("flush settings", () => _settings.FlushNow(SettingsFlushNowBudget));
        Step("flush library", () => _library.FlushNow(StoreFlushNowBudget));
        Step("flush wardrobe", () => _outfits.FlushNow(StoreFlushNowBudget));
        Step("close prompts", _prompts.CloseAll);
        Step("dismiss dialogs", _dialogs.DismissAll);
        Step("stop runs", () => _runs.StopAll());
        Step("release panic key", _panic.Dispose);
        Step("flush log", () => _diagnostics.FlushNow(LogFlushNowBudget));
    }

    void Persist()
    {
        WindowPlacement? placement = _shell.CapturePlacement();
        SessionState session = _workspace.CaptureSession();
        _settings.Update(document => document with { Window = placement ?? document.Window, Session = session });
        _workspace.RememberAllPanels();
    }

    static string DiscardMessage(int modified) =>
        modified == 1
            ? "One script has unsaved changes. Close QX Scripter and discard them?"
            : $"{modified} scripts have unsaved changes. Close QX Scripter and discard them?";

    static void Step(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Diag.Error($"Close step {name} failed: {error}", "app");
        }
    }

    static async Task StepAsync(string name, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Diag.Error($"Close step {name} failed: {error}", "app");
        }
    }

    static async Task BoundedAsync(Task work, TimeSpan budget)
    {
        try
        {
            await work.WaitAsync(budget);
        }
        catch (TimeoutException)
        {
        }
    }
}
