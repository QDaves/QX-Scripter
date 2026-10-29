using Qx.Diagnostics;
using Qx.Game.Api;
using Qx.Game;
using Qx.Interception.GEarth;
using Qx.Mcp;
using Qx.Platform;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Editor;
using Qx.Protocol;
using Qx.Scripter.Rules;
using Qx.Scripter;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Runtime;

public sealed class DesktopRuntime : IAsyncDisposable
{
    public static readonly TimeSpan DisposeBudget = TimeSpan.FromSeconds(3);

    readonly ScripterRuntime _host;
    int _disposed;

    public DesktopRuntime(LaunchOptions launch, IAppPaths paths, RuntimeProfile profile, int? mcpPort, DeferredEditorBridge editor)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(editor);
        Launch = launch;
        bool live = profile == RuntimeProfile.Live;
        var options = new ScripterRuntimeOptions
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
            McpConfiguration = live ? McpConfig.Load(paths.McpConfigFile) : McpConfig.CreateDefault(),
            Keyboard = live ? null : KeyboardReader.Unsupported("The keyboard is only read in a live session.")
        };
        if (mcpPort is { } port)
            options = options with { McpPort = port };
        _host = new ScripterRuntime(options, editor);
        _host.Rules.AntiIdleSeconds = SessionRules.DefaultAntiIdleSeconds;
    }

    public LaunchOptions Launch { get; }

    public GEarthExtension Extension => _host.Extension;

    public GameState Game => _host.Game;

    public IGameApi Api => _host.Api;

    public SessionRules Rules => _host.Rules;

    public KeyboardReader Keyboard => _host.Keyboard;

    public ScriptExecutionService Scripts => _host.ScriptExecution;

    public McpServer Mcp => _host.Mcp;

    public HttpClient Http => _host.Http;

    public MessageManager Messages => _host.Messages;

    public ScripterRuntimeStatus Status => _host.Status;

    public Task TransportTask => _host.TransportTask;

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.Run(() => _host.StartAsync(cancellationToken), cancellationToken);

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
