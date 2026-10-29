using Qx.ClientCatalog.InstalledClients;
using Qx.ClientCatalog;
using Qx.Diagnostics;
using Qx.Game.Api;
using Qx.Game.Messages;
using Qx.Game.Snapshots;
using Qx.Game;
using Qx.Hosting;
using Qx.Interception.GEarth;
using Qx.Mcp;
using Qx.Platform;
using Qx.Protocol;
using Qx.Scripter.Rules;
using Qx.Scripting.Hosting;
using Qx.Scripting;

namespace Qx.Scripter;

public sealed record ScripterRuntimeStatus(
    bool Started,
    bool Disposed,
    HostServiceStatus Transport,
    HostServiceStatus FallbackCatalogs,
    HostServiceStatus HeaderPreparation,
    HostServiceStatus Mcp);

public sealed class ScripterRuntime : IDisposable, IAsyncDisposable
{
    readonly GameHost _runtime;
    readonly ScripterRuntimeOptions _options;
    readonly CancellationTokenSource _lifetime = new();
    readonly object _gate = new();
    readonly bool _owns_keyboard;
    Task? _startup;
    Task? _disposal;
    Exception? _mcp_error;
    bool _mcp_initialized;
    bool _disposed;

    public ScripterRuntime(ScripterRuntimeOptions options, IEditorBridge? editor = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        options.ValidateScripter();
        string scripts = Path.GetFullPath(options.ScriptsDirectory);
        Directory.CreateDirectory(scripts);
        _runtime = new GameHost(options);
        _owns_keyboard = options.Keyboard is null;
        try
        {
            Keyboard = options.Keyboard ?? KeyboardReader.Create();
            Rules = new SessionRules(Extension, Game, Api, options.SessionRulesPath,
                Keyboard.IsSupported ? () => Keyboard.IsDown(Key.Shift) : null);
            Rules.Bind();
            ScriptExecution = new ScriptExecutionService(Extension, Game, Api, Keyboard, scripts, _lifetime.Token);
            McpHost = new McpHost(Extension, Game, Queries, Api, Contracts, ScriptExecution, scripts, editor);
            var configuration = options.McpConfiguration ?? (options.EnableMcp
                ? McpConfig.Load(Path.Combine(ScripterPaths.Configuration, "mcp.json"))
                : McpConfig.CreateDefault());
            var tools = new ScripterMcpTools(McpHost, () => Mcp?.Config ?? configuration);
            Mcp = new McpServer(new McpServerOptions
            {
                Name = "QX Scripter",
                Port = options.McpPort,
                ConfigurationPath = Path.Combine(ScripterPaths.Configuration, "mcp.json"),
                Instructions = () => tools.Instructions,
                RuntimeCapabilities = () => McpHost.RuntimeCapabilities
            }, tools.Create().Concat(GameApiTools.Create(Api)), configuration);
        }
        catch
        {
            Rules?.Dispose();
            _runtime.Dispose();
            if (_owns_keyboard)
                Keyboard?.Dispose();
            _lifetime.Dispose();
            throw;
        }
    }

    public MessageManager Messages => _runtime.Messages;
    public MessageContractCatalog Contracts => _runtime.Contracts;
    public HttpClient Http => _runtime.Http;
    public GEarthExtension Extension => _runtime.Extension;
    public GameState Game => _runtime.Game;
    public IGameApi Api => _runtime.Api;
    public GameQueries Queries => _runtime.Queries;
    public KeyboardReader Keyboard { get; }
    public SessionRules Rules { get; }
    public ScriptExecutionService ScriptExecution { get; }
    public IMcpHost McpHost { get; }
    public McpServer Mcp { get; }
    public InstalledClientMonitor? InstalledClients => _runtime.InstalledClients;
    public HeaderCatalogCoordinator? HeaderCatalogs => _runtime.HeaderCatalogs;
    public Task TransportTask => _runtime.TransportTask;
    public Task FallbackCatalogTask => _runtime.FallbackCatalogTask;
    public Task HeaderPreparationTask => _runtime.HeaderPreparationTask;
    public ClientCatalogLoadResult? FallbackCatalog => _runtime.FallbackCatalog;
    public IReadOnlyList<HeaderCatalogFailure> HeaderCatalogErrors => _runtime.HeaderCatalogErrors;

    public ScripterRuntimeStatus Status
    {
        get
        {
            lock (_gate)
            {
                GameHostStatus status = _runtime.Status;
                var mcp = !_options.EnableMcp ? new HostServiceStatus(false, HostServicePhase.Disabled)
                    : !_mcp_initialized ? new HostServiceStatus(true, HostServicePhase.Pending)
                    : _mcp_error is not null ? new HostServiceStatus(true, HostServicePhase.Faulted, _mcp_error)
                    : new HostServiceStatus(true, Mcp.IsRunning ? HostServicePhase.Running : HostServicePhase.Completed);
                return new(status.Started, _disposed, status.Transport, status.FallbackCatalogs, status.HeaderPreparation, mcp);
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _startup ??= StartCoreAsync();
            return _startup.WaitAsync(cancellationToken);
        }
    }

    async Task StartCoreAsync()
    {
        if (_options.EnableMcp)
        {
            try
            {
                if (!Mcp.Start())
                    _mcp_error = new InvalidOperationException(Mcp.StartFailure);
            }
            catch (Exception error)
            {
                _mcp_error = error;
            }
            if (_mcp_error is null)
                Diag.Info($"MCP server listening on http://127.0.0.1:{Mcp.Port}/mcp", "mcp");
            else
                Diag.Warn(_mcp_error.Message, "mcp");
        }
        _mcp_initialized = true;
        await _runtime.StartAsync(_lifetime.Token).ConfigureAwait(false);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed = true;
            return new ValueTask(_disposal ??= DisposeCoreAsync());
        }
    }

    async Task DisposeCoreAsync()
    {
        try
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
            Mcp.Dispose();
            ScriptExecution.RequestStopAll();
            try
            {
                await ScriptExecution.WhenAllStoppedAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Diag.Warn("Script shutdown timed out.", "scripting");
            }
            Rules.Dispose();
        }
        finally
        {
            try
            {
                await _runtime.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                if (_owns_keyboard)
                    Keyboard.Dispose();
                _lifetime.Dispose();
            }
        }
    }
}
