using Qx.Interception.GEarth;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Lifecycle;

public sealed class HostedLifecyclePolicy : IAlwaysOn, IDisposable
{
    public static readonly TimeSpan WatchdogDelay = TimeSpan.FromSeconds(10);

    readonly DesktopRuntime _runtime;
    readonly IShellWindow _shell;
    readonly ShellCloseCoordinator _close;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    ITimer? _watchdog;
    int _ready;

    public HostedLifecyclePolicy(DesktopRuntime runtime, IShellWindow shell, ShellCloseCoordinator close, IUiDispatcher dispatcher, TimeProvider time)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _close = close ?? throw new ArgumentNullException(nameof(close));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        GEarthExtension extension = runtime.Extension;
        extension.Activated += OnActivated;
        extension.InterceptorDisconnected += OnInterceptorDisconnected;
    }

    public void MarkShellReady()
    {
        if (Interlocked.Exchange(ref _ready, 1) == 0 && _runtime.Extension.Activations > 0)
            _dispatcher.Post(_shell.ShowAndActivate);
    }

    public void StartWatchdog()
    {
        _watchdog ??= _time.CreateTimer(OnWatchdog, null, WatchdogDelay, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        GEarthExtension extension = _runtime.Extension;
        extension.Activated -= OnActivated;
        extension.InterceptorDisconnected -= OnInterceptorDisconnected;
        _watchdog?.Dispose();
    }

    void OnActivated()
    {
        if (Volatile.Read(ref _ready) != 0)
            _dispatcher.Post(_shell.ShowAndActivate);
    }

    void OnInterceptorDisconnected()
    {
        if (_runtime.Launch.HostedByGEarth)
            _dispatcher.Post(() => _close.RequestCloseAsync(CloseReason.GEarthLost).Observe("app"));
    }

    void OnWatchdog(object? state) => _dispatcher.Post(CheckConnection);

    void CheckConnection()
    {
        if (!_runtime.Extension.IsInterceptorConnected)
            _close.RequestCloseAsync(CloseReason.GEarthUnavailable).Observe("app");
    }
}
