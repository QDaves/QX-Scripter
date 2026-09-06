using Qx.ClientCatalog;
using Qx.ClientCatalog.InstalledClients;
using Qx.Diagnostics;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Game.Rules;
using Qx.Game.Snapshots;
using Qx.Interception.GEarth;
using Qx.Mcp;
using Qx.Protocol;
using Qx.Scripting;

namespace Qx.Hosting;

public sealed class RuntimeHost : IDisposable, IAsyncDisposable
{
    readonly RuntimeHostOptions _options;
    readonly CancellationTokenSource _lifetime = new();
    readonly object _gate = new();
    readonly Dictionary<(ClientType Client, string Path), Exception> _header_catalog_errors = [];
    readonly ApplicationRuntime application_runtime;
    Task _transport_task = Task.CompletedTask;
    Task _fallback_task = Task.CompletedTask;
    Task _header_task = Task.CompletedTask;
    Task? _startup;
    Task? _disposal;
    IReadOnlyList<ClientCatalogLoadResult> _fallback_catalogs = [];
    Exception? _mcp_error;
    bool _mcp_started;
    bool _mcp_initialized;
    bool _transport_started;
    bool _fallback_started;
    bool _header_started;
    bool _session_established;
    bool _started;
    bool _disposed;

    public RuntimeHost(
        RuntimeHostOptions options,
        IEditorBridge? editor = null,
        Func<bool>? shift_pressed = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        string scripts_directory = Path.GetFullPath(_options.ScriptsDirectory);
        Directory.CreateDirectory(scripts_directory);

        Messages = MessageManager.CreateWithEmbeddedMap();
        Contracts = new MessageContractCatalog(Messages.Registry, MessageContracts.All);
        Http = new HttpClient { Timeout = _options.HttpTimeout };
        Extension = new GEarthExtension(_options.GEarth, Messages);
        Extension.Connected += _ => Volatile.Write(ref _session_established, true);
        Game = new GameState();
        Game.Attach(Extension);
        application_runtime = new ApplicationRuntime(Extension, Game, Contracts);
        application_runtime.ObserverFailed += error => Diag.Error(error.ToString(), "application");
        Application = application_runtime;
        Queries = new GameQueryService(
            Game,
            Application,
            () => Extension.Session,
            () => Extension.IsInterceptorConnected,
            () => Extension.Session is { } session && Messages.HasCatalog(session.Client),
            () => Extension.Session is { } session &&
                  Messages.GetWireProfile(session.Client).IsAnalyzed,
            () => Extension.Session is { } session &&
                  Messages.GetWireProfile(session.Client).HasExactIncomingLayout(session.Client),
            () => Extension.Session is { } session
                ? Messages.GetWireProfile(session.Client).MissingIncomingCapabilities(session.Client)
                : []);
        Rules = new SessionRules(
            Extension,
            Game,
            Application,
            _options.SessionRulesPath,
            shift_pressed);
        Rules.Bind();
        ScriptExecution = new ScriptExecutionService(Extension, Game, Application, _lifetime.Token);
        McpHost = new McpHost(
            Extension,
            Game,
            Queries,
            Application,
            ScriptExecution,
            scripts_directory,
            editor);
        Mcp = new McpServer(
            McpHost,
            _options.McpPort,
            _options.McpConfiguration ?? (_options.EnableMcp ? McpConfig.Load() : McpConfig.CreateDefault()),
            ApplicationMcpTools.Create(Application));

        if (_options.EnableClientMonitoring)
        {
            InstalledClients = new InstalledClientMonitor(Http, _options.InstalledClients);
            HeaderCatalogs = new HeaderCatalogCoordinator(
                InstalledClients,
                Path.GetFullPath(_options.HeaderCatalogCachePath));
            HeaderCatalogs.PreparationChanged += HeaderPreparationChanged;
            Extension.SessionCatalogSelector = new PreparedSessionCatalogSelector(
                HeaderCatalogs,
                Messages.Registry);
            Extension.CatalogReadiness = HeaderCatalogs;
        }
    }

    public MessageManager Messages { get; }

    public MessageContractCatalog Contracts { get; }

    public HttpClient Http { get; }

    public GEarthExtension Extension { get; }

    public GameState Game { get; }

    public IApplicationRuntime Application { get; }

    public GameQueryService Queries { get; }

    public SessionRules Rules { get; }

    public ScriptExecutionService ScriptExecution { get; }

    public IMcpHost McpHost { get; }

    public McpServer Mcp { get; }

    public InstalledClientMonitor? InstalledClients { get; }

    public HeaderCatalogCoordinator? HeaderCatalogs { get; }

    public Task TransportTask
    {
        get
        {
            lock (_gate)
                return _transport_task;
        }
    }

    public Task FallbackCatalogTask
    {
        get
        {
            lock (_gate)
                return _fallback_task;
        }
    }

    public Task HeaderPreparationTask
    {
        get
        {
            lock (_gate)
                return _header_task;
        }
    }

    public IReadOnlyList<ClientCatalogLoadResult> FallbackCatalogs =>
        Volatile.Read(ref _fallback_catalogs);

    public IReadOnlyList<RuntimeHeaderCatalogFailure> HeaderCatalogErrors
    {
        get
        {
            lock (_gate)
            {
                return _header_catalog_errors
                    .OrderBy(value => value.Key.Client)
                    .ThenBy(value => value.Key.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new RuntimeHeaderCatalogFailure(
                        value.Key.Client,
                        value.Key.Path,
                        value.Value))
                    .ToArray();
            }
        }
    }

    public RuntimeHostStatus Status
    {
        get
        {
            lock (_gate)
            {
                return new RuntimeHostStatus(
                    _started,
                    _disposed,
                    Service(_options.EnableTransport, _transport_started, _transport_task),
                    Service(_options.EnableFallbackCatalogs, _fallback_started, _fallback_task),
                    HeaderStatus(),
                    McpStatus());
            }
        }
    }

    public Task StartAsync(CancellationToken cancellation_token = default)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Task startup;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_startup is null)
            {
                _started = true;
                _startup = StartCoreAsync();
            }
            startup = _startup;
        }
        return startup.WaitAsync(cancellation_token);
    }

    Task StartCoreAsync()
    {
        if (_options.EnableFallbackCatalogs)
        {
            ClientCatalogBootstrapper.LoadEmbeddedReferences(Messages);
            lock (_gate)
                _fallback_started = true;
        }
        if (_options.EnableFallbackCatalogs && HeaderCatalogs is null)
        {
            Task fallback = LoadFallbackCatalogsAsync();
            lock (_gate)
                _fallback_task = fallback;
        }
        if (HeaderCatalogs is not null)
        {
            Task headers = PrepareHeadersAsync(HeaderCatalogs);
            lock (_gate)
            {
                _header_task = headers;
                _header_started = true;
            }
        }

        StartMcp();
        if (_options.EnableTransport)
        {
            lock (_gate)
            {
                if (_disposed)
                    return Task.CompletedTask;
                Task transport = Task.Run(RunTransportAsync);
                _transport_task = transport;
                _transport_started = true;
            }
        }
        return Task.CompletedTask;
    }

    async Task PrepareHeadersAsync(HeaderCatalogCoordinator catalogs)
    {
        await catalogs.StartAsync(_lifetime.Token).ConfigureAwait(false);
        await catalogs.WaitForIdleAsync(_lifetime.Token).ConfigureAwait(false);
    }

    void StartMcp()
    {
        bool started = false;
        Exception? failure = null;
        lock (_gate)
        {
            if (_disposed)
                return;
            if (_options.EnableMcp)
            {
                try
                {
                    started = Mcp.Start();
                    if (!started)
                        failure = new InvalidOperationException(
                            $"MCP port {_options.McpPort} is held by {McpServer.PortHolder(_options.McpPort)}.");
                }
                catch (Exception error)
                {
                    failure = error;
                }
            }

            _mcp_started = started;
            _mcp_error = failure;
            _mcp_initialized = true;
        }
        if (!_options.EnableMcp)
            return;
        if (failure is null)
            Diag.Info($"MCP server listening on http://127.0.0.1:{Mcp.Port}/mcp", "mcp");
        else
            Diag.Error(failure.Message, "mcp");
    }

    async Task RunTransportAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            try
            {
                await Extension.RunAsync(_lifetime.Token).ConfigureAwait(false);
                if (_lifetime.IsCancellationRequested ||
                    (!_options.ReconnectTransport && Volatile.Read(ref _session_established)))
                    return;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                return;
            }
            catch when (_options.ReconnectTransport || !Volatile.Read(ref _session_established))
            {
            }
            await Task.Delay(_options.TransportRetryDelay, _lifetime.Token).ConfigureAwait(false);
        }
    }

    async Task LoadFallbackCatalogsAsync()
    {
        var resolver = new ClientCatalogResolver(
            Http,
            _options.InstalledClients.LauncherDataPath,
            _options.InstalledClients.CacheRootPath);
        IReadOnlyList<ClientCatalogLoadResult> catalogs = await ClientCatalogBootstrapper.LoadInstalledAsync(
            Messages,
            resolver,
            _ => Extension.RebindInterceptors(),
            cancellation_token: _lifetime.Token).ConfigureAwait(false);
        Volatile.Write(ref _fallback_catalogs, catalogs);
        foreach (ClientCatalogLoadResult catalog in catalogs)
        {
            if (catalog.Resolution is { } loaded)
                Diag.Info($"Loaded {loaded.Client} {loaded.Version} header fallback from {loaded.Source}", "protocol");
            else if (catalog.Error is not null)
                Diag.Warn($"{catalog.Client} header fallback unavailable: {catalog.Error.Message}", "protocol");
        }
    }

    void HeaderPreparationChanged(object? sender, HeaderCatalogPreparationChangedEventArgs args)
    {
        HeaderCatalogPreparationStatus status = args.Status;
        if (status.Stage == HeaderCatalogPreparationStage.Failed)
        {
            Exception error = status.Error ?? new InvalidDataException(
                $"{status.Client} {status.Candidate.Version} header preparation failed.");
            lock (_gate)
                _header_catalog_errors[(status.Client, status.NormalizedPath)] = error;
            Diag.Warn(
                $"{status.Client} {status.Candidate.Version} header preparation failed: {error.Message}",
                "protocol");
            return;
        }
        if (status.Stage == HeaderCatalogPreparationStage.Ready)
        {
            lock (_gate)
                _header_catalog_errors.Remove((status.Client, status.NormalizedPath));
        }
        if (status.Stage != HeaderCatalogPreparationStage.Ready)
            return;
        if (!HeaderCatalogs!.TryGetByPath(status.Client, status.NormalizedPath, out PreparedHeaderCatalog? prepared) ||
            prepared is null)
            return;
        IntegratePreparedCatalog(prepared);
    }

    internal bool IntegratePreparedCatalog(PreparedHeaderCatalog prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        var identity = (prepared.Key.Client, prepared.NormalizedPath);
        try
        {
            MessageCatalog catalog = ClientCatalogFactory.Create(prepared);
            Messages.LoadVerifiedFallbackCatalog(prepared.Key.Client, catalog, preferred: false);
            lock (_gate)
                _header_catalog_errors.Remove(identity);
            Diag.Info(
                $"Prepared {prepared.Key.Client} catalog for build {prepared.Candidate.Version} with {catalog.HeaderCount} headers from {prepared.Candidate.Source}",
                "protocol");
            return true;
        }
        catch (Exception error)
        {
            lock (_gate)
                _header_catalog_errors[identity] = error;
            Diag.Error(
                $"Unable to load prepared {prepared.Key.Client} {prepared.Candidate.Version} headers: {error.Message}",
                "protocol");
            return false;
        }
    }

    RuntimeServiceStatus McpStatus()
    {
        if (!_options.EnableMcp)
            return new RuntimeServiceStatus(false, RuntimeServicePhase.Disabled);
        if (!_mcp_initialized)
            return new RuntimeServiceStatus(true, RuntimeServicePhase.Pending);
        if (_mcp_error is not null)
            return new RuntimeServiceStatus(true, RuntimeServicePhase.Faulted, _mcp_error);
        return new RuntimeServiceStatus(
            true,
            _mcp_started && Mcp.IsRunning
                ? RuntimeServicePhase.Running
                : RuntimeServicePhase.Completed);
    }

    RuntimeServiceStatus HeaderStatus()
    {
        if (!_options.EnableClientMonitoring)
            return new RuntimeServiceStatus(false, RuntimeServicePhase.Disabled);
        if (_header_catalog_errors.Count > 0)
            return new RuntimeServiceStatus(
                true,
                RuntimeServicePhase.Faulted,
                _header_catalog_errors.Values.First());
        return Service(true, _header_started, _header_task);
    }

    static RuntimeServiceStatus Service(bool enabled, bool started, Task task)
    {
        if (!enabled)
            return new RuntimeServiceStatus(false, RuntimeServicePhase.Disabled);
        if (!started)
            return new RuntimeServiceStatus(true, RuntimeServicePhase.Pending);
        if (!task.IsCompleted)
            return new RuntimeServiceStatus(true, RuntimeServicePhase.Running);
        if (task.IsCanceled)
            return new RuntimeServiceStatus(true, RuntimeServicePhase.Canceled);
        if (task.IsFaulted)
            return new RuntimeServiceStatus(
                true,
                RuntimeServicePhase.Faulted,
                task.Exception?.GetBaseException());
        return new RuntimeServiceStatus(true, RuntimeServicePhase.Completed);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
    {
        Task disposal;
        lock (_gate)
        {
            _disposal ??= DisposeCoreAsync();
            disposal = _disposal;
        }
        return new ValueTask(disposal);
    }

    async Task DisposeCoreAsync()
    {
        Task transport;
        Task fallback;
        Task headers;
        Task? startup;
        HeaderCatalogCoordinator? coordinator;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            startup = _startup;
            coordinator = HeaderCatalogs;
        }

        _lifetime.Cancel();
        if (HeaderCatalogs is not null)
            HeaderCatalogs.PreparationChanged -= HeaderPreparationChanged;
        Mcp.Stop();

        if (coordinator is not null)
            await IgnoreAsync(coordinator.DisposeAsync().AsTask()).ConfigureAwait(false);
        if (startup is not null)
            await IgnoreAsync(startup).ConfigureAwait(false);
        lock (_gate)
        {
            transport = _transport_task;
            fallback = _fallback_task;
            headers = _header_task;
        }
        await Task.WhenAll(
            IgnoreAsync(transport),
            IgnoreAsync(fallback),
            IgnoreAsync(headers)).ConfigureAwait(false);

        Rules.Dispose();
        application_runtime.Dispose();
        Game.Dispose();
        Extension.Dispose();
        Http.Dispose();
        _lifetime.Dispose();
    }

    static async Task IgnoreAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
