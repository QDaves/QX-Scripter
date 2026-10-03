using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Qx.ClientCatalog.InstalledClients;
using Qx.Protocol;

namespace Qx.ClientCatalog;

public sealed class HeaderCatalogCoordinator : IAsyncDisposable, IMessageCatalogReadiness
{
    readonly IInstalledClientCandidateSource _source;
    readonly IHeaderCatalogExtractor _extractor;
    readonly HeaderCatalogStore _store;
    readonly CancellationTokenSource _stop = new();
    readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    readonly object _gate = new();
    readonly Dictionary<string, PendingPreparation> _pending;
    readonly Dictionary<string, HeaderCatalogPreparationStatus> _statuses;
    readonly Dictionary<string, PreparedHeaderCatalog> _prepared = new(StringComparer.Ordinal);
    readonly Dictionary<string, PreparedHeaderCatalog> _by_path;
    readonly Dictionary<string, string> _current_by_path;
    TaskCompletionSource _idle = CompletedSource();
    Task? _startup;
    Task? _worker;
    Task? _disposal;
    bool _subscribed;
    bool _disposed;
    int _outstanding;
    long _generation;

    public HeaderCatalogCoordinator(
        InstalledClientMonitor monitor,
        string cacheRoot)
        : this(
            new InstalledClientCandidateSource(monitor),
            cacheRoot,
            new HeaderCatalogExtractor())
    {
    }

    internal HeaderCatalogCoordinator(
        IInstalledClientCandidateSource source,
        string cache_root,
        IHeaderCatalogExtractor extractor)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _store = new HeaderCatalogStore(cache_root, _stop.Token);
        StringComparer path_comparer = PathComparer();
        _pending = new Dictionary<string, PendingPreparation>(path_comparer);
        _statuses = new Dictionary<string, HeaderCatalogPreparationStatus>(path_comparer);
        _current_by_path = new Dictionary<string, string>(path_comparer);
        _by_path = new Dictionary<string, PreparedHeaderCatalog>(path_comparer);
    }

    public event EventHandler<HeaderCatalogPreparationChangedEventArgs>? PreparationChanged;

    public IReadOnlyList<PreparedHeaderCatalog> Catalogs
    {
        get
        {
            lock (_gate)
                return Array.AsReadOnly(_prepared.Values.OrderBy(value => value.PreparedAt).ToArray());
        }
    }

    public IReadOnlyList<HeaderCatalogPreparationStatus> Statuses
    {
        get
        {
            lock (_gate)
                return Array.AsReadOnly(_statuses.Values.OrderBy(value => value.ChangedAt).ToArray());
        }
    }

    public IReadOnlyList<PreparedHeaderCatalog> CurrentCatalogs()
    {
        lock (_gate)
            return Array.AsReadOnly(_by_path.Values.OrderBy(value => value.NormalizedPath, PathComparer()).ToArray());
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task startup;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _startup ??= StartCoreAsync();
            startup = _startup;
        }
        return startup.WaitAsync(cancellationToken);
    }

    public Task WaitForIdleAsync(CancellationToken cancellationToken = default) =>
        WaitForIdleCoreAsync(null, cancellationToken);

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        await WaitForIdleAsync(cancellationToken).ConfigureAwait(false);
    }

    internal Task WaitForIdleAsync(
        Func<CancellationToken, ValueTask> before_validation,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(before_validation);
        return WaitForIdleCoreAsync(before_validation, cancellation_token);
    }

    async Task WaitForIdleCoreAsync(
        Func<CancellationToken, ValueTask>? before_validation,
        CancellationToken cancellation_token)
    {
        while (true)
        {
            Task idle;
            long generation;
            lock (_gate)
            {
                generation = _generation;
                if (_outstanding == 0)
                    return;
                idle = _idle.Task;
            }
            await idle.WaitAsync(cancellation_token).ConfigureAwait(false);
            if (before_validation is not null)
                await before_validation(cancellation_token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_outstanding == 0 && _generation == generation)
                    return;
            }
        }
    }

    public bool TryGet(HeaderCatalogKey key, out PreparedHeaderCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (!_prepared.TryGetValue(key.Fingerprint, out catalog) || catalog.Key != key)
            {
                catalog = null;
                return false;
            }
            return true;
        }
    }

    public bool TryGet(
        string sourceSha256,
        out PreparedHeaderCatalog? catalog)
    {
        string source = HeaderCatalogKey.NormalizeHash(sourceSha256, nameof(sourceSha256));
        lock (_gate)
        {
            PreparedHeaderCatalog[] matches = _prepared.Values
                .Where(value => value.Key.SourceSha256 == source)
                .ToArray();
            catalog = matches.Length == 1 ? matches[0] : null;
            return matches.Length == 1;
        }
    }

    public IReadOnlyList<PreparedHeaderCatalog> Find(string sourceSha256)
    {
        string source = HeaderCatalogKey.NormalizeHash(sourceSha256, nameof(sourceSha256));
        lock (_gate)
        {
            return Array.AsReadOnly(_prepared.Values
                .Where(value => value.Key.SourceSha256 == source)
                .OrderBy(value => value.PreparedAt)
                .ToArray());
        }
    }

    public bool TryGetByPath(
        string path,
        out PreparedHeaderCatalog? catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = Path.GetFullPath(path);
        lock (_gate)
        {
            catalog = _by_path.GetValueOrDefault(normalized);
            return catalog is not null;
        }
    }

    async Task StartCoreAsync()
    {
        try
        {
            await _source.StartAsync(_stop.Token).ConfigureAwait(false);
            Subscribe();
            if (_source.Candidate is { } candidate &&
                await ProbeInitialCacheAsync(candidate, _stop.Token).ConfigureAwait(false) is { } preparation &&
                IsCurrent(preparation))
                Queue(new PendingPreparation(candidate, preparation), false);
            lock (_gate)
                _worker ??= RunAsync(_stop.Token);
        }
        catch
        {
            Unsubscribe();
            _stop.Cancel();
            _queue.Writer.TryComplete();
            throw;
        }
    }

    void Subscribe()
    {
        lock (_gate)
        {
            if (_subscribed)
                return;
            ObjectDisposedException.ThrowIf(_disposed, this);
            _source.CandidateChanged += CandidateChanged;
            _subscribed = true;
        }
    }

    void Unsubscribe()
    {
        lock (_gate)
        {
            if (!_subscribed)
                return;
            _source.CandidateChanged -= CandidateChanged;
            _subscribed = false;
        }
    }

    void CandidateChanged(object? sender, InstalledClientCandidateChangedEventArgs args)
    {
        if (args.Previous is not null)
            RemovePathBinding(args.Previous);
        if (args.Candidate is not null)
        {
            Queue(args.Candidate);
        }
    }

    void Queue(InstalledClientCandidate candidate) =>
        Queue(new PendingPreparation(candidate, null), true);

    void Queue(PendingPreparation preparation, bool publish_discovered)
    {
        InstalledClientCandidate candidate = preparation.Candidate;
        string normalized_path;
        string identity;
        try
        {
            normalized_path = Path.GetFullPath(candidate.Path);
            identity = Identity(candidate, normalized_path);
        }
        catch
        {
            return;
        }

        bool queued;
        lock (_gate)
        {
            if (_disposed)
                return;
            _current_by_path[normalized_path] = identity;
            queued = _pending.TryAdd(identity, preparation);
            if (queued)
            {
                _generation++;
                if (_outstanding++ == 0)
                    _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        if (!queued)
            return;

        if (publish_discovered)
        {
            PublishStatus(new HeaderCatalogPreparationStatus(
                candidate,
                normalized_path,
                HeaderCatalogPreparationStage.Discovered,
                DateTimeOffset.UtcNow));
        }
        if (_queue.Writer.TryWrite(identity))
            return;

        lock (_gate)
        {
            _pending.Remove(identity);
            CompleteOutstanding();
        }
    }

    async Task RunAsync(CancellationToken cancellation_token)
    {
        try
        {
            await foreach (string identity in _queue.Reader.ReadAllAsync(cancellation_token).ConfigureAwait(false))
            {
                PendingPreparation? preparation;
                lock (_gate)
                {
                    _pending.Remove(identity, out preparation);
                }
                if (preparation is null)
                {
                    CompleteOne();
                    continue;
                }

                try
                {
                    await PrepareAsync(preparation, cancellation_token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
                {
                }
                finally
                {
                    CompleteOne();
                }
            }
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
        }
    }

    async Task<InitialPreparation?> ProbeInitialCacheAsync(
        InstalledClientCandidate candidate,
        CancellationToken cancellation_token)
    {
        string normalized_path;
        try
        {
            normalized_path = Path.GetFullPath(candidate.Path);
        }
        catch (Exception error)
        {
            PublishStatus(new HeaderCatalogPreparationStatus(
                candidate,
                candidate.Path,
                HeaderCatalogPreparationStage.Failed,
                DateTimeOffset.UtcNow,
                Error: error));
            return null;
        }

        lock (_gate)
        {
            if (_disposed)
                return null;
            _current_by_path[normalized_path] = Identity(candidate, normalized_path);
        }
        PublishStatus(Status(
            candidate,
            normalized_path,
            HeaderCatalogPreparationStage.Discovered));

        string? source_sha256 = null;
        try
        {
            InitialPreparation preparation = await CreateInitialPreparationAsync(
                candidate,
                normalized_path,
                cancellation_token).ConfigureAwait(false);
            source_sha256 = preparation.SourceSha256;
            HeaderCatalogCacheResult? result = await _store.TryGetAsync(
                preparation.Key,
                cancellation_token).ConfigureAwait(false);
            if (result is null)
                return preparation;

            string current_source = await HashFileAsync(
                preparation.SourcePath,
                cancellation_token).ConfigureAwait(false);
            RequireSameSource(preparation.SourceSha256, current_source);
            cancellation_token.ThrowIfCancellationRequested();
            PublishPrepared(new PreparedHeaderCatalog(
                candidate,
                normalized_path,
                preparation.SourcePath,
                preparation.Key,
                result.Catalog,
                result.State,
                result.ContentSha256,
                DateTimeOffset.UtcNow));
            PublishStatus(Status(
                candidate,
                normalized_path,
                HeaderCatalogPreparationStage.Ready,
                preparation.SourceSha256,
                result.State));
            return null;
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            PublishStatus(Status(
                candidate,
                normalized_path,
                HeaderCatalogPreparationStage.Failed,
                source_sha256,
                error: error));
            return null;
        }
    }

    async Task<InitialPreparation> CreateInitialPreparationAsync(
        InstalledClientCandidate candidate,
        string normalized_path,
        CancellationToken cancellation_token)
    {
        HeaderCatalogExtractionTarget target = _extractor.Resolve(candidate);
        string source_path = Path.GetFullPath(target.SourcePath);
        PublishStatus(Status(
            candidate,
            normalized_path,
            HeaderCatalogPreparationStage.Hashing));
        string source_sha256 = await HashFileAsync(source_path, cancellation_token).ConfigureAwait(false);
        var provenance = new HeaderCatalogProvenance(
            candidate.Version,
            candidate.Source,
            candidate.ContentRevision);
        var key = new HeaderCatalogKey(
            source_sha256,
            target.NameDatabaseSha256,
            target.ExtractorRevision,
            provenance);
        PublishStatus(Status(
            candidate,
            normalized_path,
            HeaderCatalogPreparationStage.CacheLookup,
            source_sha256));
        return new InitialPreparation(
            candidate,
            normalized_path,
            target,
            source_path,
            source_sha256,
            key);
    }

    async Task PrepareAsync(
        PendingPreparation pending,
        CancellationToken cancellation_token)
    {
        InstalledClientCandidate candidate = pending.Candidate;
        string normalized_path = Path.GetFullPath(candidate.Path);
        string? source_sha256 = null;
        try
        {
            InitialPreparation preparation = pending.Initial ?? await CreateInitialPreparationAsync(
                candidate,
                normalized_path,
                cancellation_token).ConfigureAwait(false);
            source_sha256 = preparation.SourceSha256;

            HeaderCatalogCacheResult result = await _store.GetOrCreateAsync(
                preparation.Key,
                async token =>
                {
                    PublishStatus(Status(
                        candidate,
                        normalized_path,
                        HeaderCatalogPreparationStage.Extracting,
                        source_sha256));
                    HeaderCatalogExtractionResult extraction = await _extractor.ExtractAsync(
                        preparation.Target,
                        preparation.Key.Provenance,
                        token).ConfigureAwait(false);
                    RequireSameSource(source_sha256, extraction.SourceSha256);
                    string extracted_source = await HashFileAsync(preparation.SourcePath, token).ConfigureAwait(false);
                    RequireSameSource(source_sha256, extracted_source);
                    return extraction.Catalog;
                },
                cancellation_token).ConfigureAwait(false);
            string current_source = await HashFileAsync(
                preparation.SourcePath,
                cancellation_token).ConfigureAwait(false);
            RequireSameSource(source_sha256, current_source);
            cancellation_token.ThrowIfCancellationRequested();

            var prepared = new PreparedHeaderCatalog(
                candidate,
                normalized_path,
                preparation.SourcePath,
                preparation.Key,
                result.Catalog,
                result.State,
                result.ContentSha256,
                DateTimeOffset.UtcNow);
            PublishPrepared(prepared);
            PublishStatus(Status(
                candidate,
                normalized_path,
                HeaderCatalogPreparationStage.Ready,
                source_sha256,
                result.State));
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            PublishStatus(Status(
                candidate,
                normalized_path,
                HeaderCatalogPreparationStage.Failed,
                source_sha256,
                error: error));
        }
    }

    void PublishPrepared(PreparedHeaderCatalog prepared)
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _prepared[prepared.Key.Fingerprint] = prepared;
            string identity = Identity(prepared.Candidate, prepared.NormalizedPath);
            if (_current_by_path.GetValueOrDefault(prepared.NormalizedPath) == identity)
            {
                _by_path[prepared.NormalizedPath] = prepared;
            }
        }
    }

    void RemovePathBinding(InstalledClientCandidate candidate)
    {
        string normalized_path;
        try
        {
            normalized_path = Path.GetFullPath(candidate.Path);
        }
        catch
        {
            return;
        }
        lock (_gate)
        {
            if (_disposed)
                return;
            string candidate_identity = Identity(candidate, normalized_path);
            if (_current_by_path.GetValueOrDefault(normalized_path) != candidate_identity)
                return;
            _current_by_path.Remove(normalized_path);
            if (_by_path.TryGetValue(normalized_path, out PreparedHeaderCatalog? prepared) &&
                Identity(prepared.Candidate, prepared.NormalizedPath) == candidate_identity)
            {
                _by_path.Remove(normalized_path);
            }
        }
    }

    bool IsCurrent(InitialPreparation preparation)
    {
        lock (_gate)
        {
            return !_disposed &&
                _current_by_path.GetValueOrDefault(preparation.NormalizedPath) ==
                Identity(preparation.Candidate, preparation.NormalizedPath);
        }
    }

    void PublishStatus(HeaderCatalogPreparationStatus status)
    {
        EventHandler<HeaderCatalogPreparationChangedEventArgs>? handlers;
        lock (_gate)
        {
            if (_disposed)
                return;
            _statuses[Identity(status.Candidate, status.NormalizedPath)] = status;
            handlers = PreparationChanged;
        }
        if (handlers is null)
            return;

        var args = new HeaderCatalogPreparationChangedEventArgs(status);
        foreach (EventHandler<HeaderCatalogPreparationChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch
            {
            }
        }
    }

    static HeaderCatalogPreparationStatus Status(
        InstalledClientCandidate candidate,
        string normalized_path,
        HeaderCatalogPreparationStage stage,
        string? source_sha256 = null,
        HeaderCatalogCacheState? cache_state = null,
        Exception? error = null) => new(
            candidate,
            normalized_path,
            stage,
            DateTimeOffset.UtcNow,
            source_sha256,
            cache_state,
            error);

    static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellation_token)
    {
        using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            131072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(source, cancellation_token).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    static void RequireSameSource(string expected, string actual)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(actual)))
        {
            throw new InvalidDataException("The installed client source changed while its header catalog was prepared.");
        }
    }

    void CompleteOne()
    {
        lock (_gate)
            CompleteOutstanding();
    }

    void CompleteOutstanding()
    {
        if (_outstanding <= 0)
            return;
        _outstanding--;
        if (_outstanding == 0)
            _idle.TrySetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposal ??= DisposeCoreAsync();
            return new ValueTask(_disposal);
        }
    }

    async Task DisposeCoreAsync()
    {
        Task? startup;
        Task? worker;
        lock (_gate)
        {
            _disposed = true;
            startup = _startup;
            worker = _worker;
        }

        Unsubscribe();
        _stop.Cancel();
        _queue.Writer.TryComplete();
        ExceptionDispatchInfo? failure = null;
        try
        {
            await _source.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            failure = ExceptionDispatchInfo.Capture(error);
        }
        if (startup is not null)
        {
            try
            {
                await startup.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                failure = ExceptionDispatchInfo.Capture(error);
            }
        }
        if (worker is not null)
        {
            try
            {
                await worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                failure ??= ExceptionDispatchInfo.Capture(error);
            }
        }

        lock (_gate)
        {
            _pending.Clear();
            _outstanding = 0;
            _idle.TrySetResult();
        }
        _stop.Dispose();
        failure?.Throw();
    }

    static string Identity(InstalledClientCandidate candidate, string normalized_path) => string.Join(
        '\0',
        normalized_path,
        candidate.Version,
        candidate.ContentRevision ?? string.Empty);

    static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    sealed record InitialPreparation(
        InstalledClientCandidate Candidate,
        string NormalizedPath,
        HeaderCatalogExtractionTarget Target,
        string SourcePath,
        string SourceSha256,
        HeaderCatalogKey Key);

    sealed record PendingPreparation(
        InstalledClientCandidate Candidate,
        InitialPreparation? Initial);
}

internal interface IInstalledClientCandidateSource : IAsyncDisposable
{
    event EventHandler<InstalledClientCandidateChangedEventArgs>? CandidateChanged;

    InstalledClientCandidate? Candidate { get; }

    Task StartAsync(CancellationToken cancellation_token);
}

internal sealed class InstalledClientCandidateSource : IInstalledClientCandidateSource
{
    readonly InstalledClientMonitor _monitor;

    public InstalledClientCandidateSource(InstalledClientMonitor monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }

    public event EventHandler<InstalledClientCandidateChangedEventArgs>? CandidateChanged
    {
        add => _monitor.CandidateChanged += value;
        remove => _monitor.CandidateChanged -= value;
    }

    public InstalledClientCandidate? Candidate => _monitor.Candidate;

    public Task StartAsync(CancellationToken cancellation_token) => _monitor.StartAsync(cancellation_token);

    public ValueTask DisposeAsync() => _monitor.DisposeAsync();
}
