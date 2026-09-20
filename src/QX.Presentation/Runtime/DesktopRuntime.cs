using Qx.Diagnostics;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Rules;
using Qx.Hosting;
using Qx.Interception.GEarth;
using Qx.Mcp;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Editor;
using Qx.Protocol;

namespace Qx.Presentation.Runtime;

public sealed class DesktopRuntime : IAsyncDisposable
{
    public static readonly TimeSpan DisposeBudget = TimeSpan.FromSeconds(3);

    readonly RuntimeHost _host;
    int _disposed;

    public DesktopRuntime(LaunchOptions launch, IAppPaths paths, RuntimeProfile profile, int? mcp_port, DeferredEditorBridge editor, IKeyboardState keyboard)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(keyboard);
        Launch = launch;
        bool live = profile == RuntimeProfile.Live;
        var options = new RuntimeHostOptions
        {
            GEarth = launch.GEarth,
            ScriptsDirectory = paths.ScriptsDirectory,
            SessionRulesPath = paths.RulesFile,
            HeaderCatalogCachePath = paths.HeaderCatalogCache,
            ReconnectTransport = !launch.HostedByGEarth,
            EnableTransport = live,
            EnableMcp = live,
            EnableFallbackCatalogs = live,
            EnableClientMonitoring = live,
            McpConfiguration = live ? McpConfig.Load(paths.McpConfigFile) : McpConfig.CreateDefault()
        };
        if (mcp_port is { } port)
            options = options with { McpPort = port };
        _host = new RuntimeHost(options, editor, keyboard.IsSupported ? keyboard.IsShiftDown : null);
        _host.Rules.AntiIdleSeconds = SessionRules.DefaultAntiIdleSeconds;
    }

    public LaunchOptions Launch { get; }

    public GEarthExtension Extension => _host.Extension;

    public GameState Game => _host.Game;

    public IApplicationRuntime Application => _host.Application;

    public SessionRules Rules => _host.Rules;

    public ScriptExecutionService Scripts => _host.ScriptExecution;

    public McpServer Mcp => _host.Mcp;

    public HttpClient Http => _host.Http;

    public MessageManager Messages => _host.Messages;

    public RuntimeHostStatus Status => _host.Status;

    public Task TransportTask => _host.TransportTask;

    public Task StartAsync(CancellationToken cancellation_token) =>
        Task.Run(() => _host.StartAsync(cancellation_token), cancellation_token);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            await _host.DisposeAsync().AsTask().WaitAsync(DisposeBudget).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Diag.Warn($"Runtime shutdown exceeded {DisposeBudget.TotalSeconds:0} s and was abandoned.", "host");
        }
    }
}
