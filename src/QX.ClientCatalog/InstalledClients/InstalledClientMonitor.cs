using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Qx.ClientCatalog.InstalledClients;

public sealed class InstalledClientMonitor : IAsyncDisposable
{
    readonly InstalledClientMonitorOptions _options;
    readonly InstalledClientDiscovery _discovery;
    readonly Channel<bool> _wakes;
    readonly InstalledClientWatchSet _watch_set;
    readonly SemaphoreSlim _reconcile_gate = new(1, 1);
    readonly object _state_gate = new();
    readonly Dictionary<string, StableObservation> _observations = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<InstalledClientFamily, PublishedCandidate> _published = [];
    readonly Dictionary<InstalledClientFamily, MissingObservation> _missing = [];
    CancellationTokenSource? _stop;
    Task? _startup;
    Task? _loop;
    bool _disposed;

    public InstalledClientMonitor(
        HttpClient http,
        InstalledClientMonitorOptions? options = null)
    {
        _options = options ?? new InstalledClientMonitorOptions();
        _options.Validate();
        _discovery = new InstalledClientDiscovery(http, _options.LauncherDataPath, _options.CacheRootPath);
        _wakes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
        _watch_set = new InstalledClientWatchSet(() => _discovery.WatchRoots, Wake);
    }

    public event EventHandler<InstalledClientCandidateChangedEventArgs>? CandidateChanged;

    public IReadOnlyDictionary<InstalledClientFamily, InstalledClientCandidate> Candidates
    {
        get
        {
            lock (_state_gate)
            {
                return new ReadOnlyDictionary<InstalledClientFamily, InstalledClientCandidate>(
                    _published.ToDictionary(entry => entry.Key, entry => entry.Value.Candidate));
            }
        }
    }

    public Task StartAsync(CancellationToken cancellation_token = default)
    {
        Task startup;
        lock (_state_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (cancellation_token.IsCancellationRequested)
                return Task.FromCanceled(cancellation_token);
            if (_startup is null)
            {
                var stop = new CancellationTokenSource();
                _stop = stop;
                _startup = StartCoreAsync(stop);
            }
            startup = _startup;
        }

        return startup;
    }

    async Task StartCoreAsync(CancellationTokenSource stop)
    {
        try
        {
            await ReconcileRecoveringAsync(stop.Token).ConfigureAwait(false);
            await Task.Delay(InitialDelay(), stop.Token).ConfigureAwait(false);
            await ReconcileRecoveringAsync(stop.Token).ConfigureAwait(false);
            lock (_state_gate)
            {
                if (_disposed || !ReferenceEquals(_stop, stop))
                    throw new OperationCanceledException(stop.Token);
                _loop = RunAsync(stop.Token);
            }
        }
        catch
        {
            StopAfterFailedStart(stop);
            throw;
        }
    }

    internal void Wake() => _wakes.Writer.TryWrite(true);

    async Task RunAsync(CancellationToken cancellation_token)
    {
        DateTimeOffset next_periodic = DateTimeOffset.UtcNow + _options.ReconcilePeriod;
        try
        {
            while (!cancellation_token.IsCancellationRequested)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DateTimeOffset next_stability = NextStabilityCheck() ?? DateTimeOffset.MaxValue;
                DateTimeOffset due = next_stability < next_periodic ? next_stability : next_periodic;
                TimeSpan wait = due <= now ? TimeSpan.Zero : due - now;
                bool signaled = await WaitForWakeAsync(wait, cancellation_token).ConfigureAwait(false);
                if (signaled)
                {
                    DrainWakes();
                    if (_options.DebouncePeriod > TimeSpan.Zero)
                        await Task.Delay(_options.DebouncePeriod, cancellation_token).ConfigureAwait(false);
                    DrainWakes();
                }

                await ReconcileRecoveringAsync(cancellation_token).ConfigureAwait(false);
                if (DateTimeOffset.UtcNow >= next_periodic)
                    next_periodic = DateTimeOffset.UtcNow + _options.ReconcilePeriod;
            }
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
        }
    }

    async Task<bool> WaitForWakeAsync(TimeSpan delay, CancellationToken cancellation_token)
    {
        if (delay <= TimeSpan.Zero)
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
        timeout.CancelAfter(delay);
        try
        {
            return await _wakes.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellation_token.IsCancellationRequested)
        {
            return false;
        }
    }

    async Task ReconcileAsync(CancellationToken cancellation_token)
    {
        await _reconcile_gate.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            _watch_set.Refresh();
            IReadOnlyList<InstalledClientCandidate> discovered = await Task.Run(
                _discovery.Find,
                cancellation_token).ConfigureAwait(false);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stable = new List<InstalledClientCandidate>();

            foreach (InstalledClientCandidate candidate in discovered)
            {
                if (!TryFingerprint(candidate, out string fingerprint))
                    continue;

                string key = CandidateKey(candidate);
                present.Add(key);
                if (!_observations.TryGetValue(key, out StableObservation? observation) ||
                    !string.Equals(observation.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    observation = new StableObservation(fingerprint, now, 1);
                    _observations[key] = observation;
                }
                else
                {
                    observation = observation with { Count = observation.Count + 1 };
                    _observations[key] = observation;
                }

                if (observation.Count >= 2 &&
                    now - observation.ChangedAt >= _options.QuietPeriod)
                    stable.Add(candidate);
            }

            foreach (string missing in _observations.Keys.Where(key => !present.Contains(key)).ToArray())
                _observations.Remove(missing);

            ReconcilePublished(
                InstalledClientFamily.Flash,
                Published(SelectLatestVerified(
                    stable,
                    InstalledClientFamily.Flash,
                    Verify)),
                discovered.Where(candidate => candidate.Family == InstalledClientFamily.Flash).ToArray(),
                now);
            ReconcilePublished(
                InstalledClientFamily.Unity,
                Published(SelectLatestVerified(
                    stable,
                    InstalledClientFamily.Unity,
                    Verify)),
                discovered.Where(candidate => candidate.Family == InstalledClientFamily.Unity).ToArray(),
                now);
        }
        finally
        {
            _reconcile_gate.Release();
        }
    }

    async Task ReconcileRecoveringAsync(CancellationToken cancellation_token)
    {
        while (true)
        {
            try
            {
                await ReconcileAsync(cancellation_token).ConfigureAwait(false);
                return;
            }
            catch (Exception error) when (IsTransient(error))
            {
                _watch_set.Clear();
                await Task.Delay(RetryDelay(), cancellation_token).ConfigureAwait(false);
            }
        }
    }

    void ReconcilePublished(
        InstalledClientFamily family,
        PublishedCandidate? candidate,
        IReadOnlyList<InstalledClientCandidate> discovered,
        DateTimeOffset now)
    {
        _published.TryGetValue(family, out PublishedCandidate? current);
        if (current is null)
        {
            _missing.Remove(family);
            if (candidate is not null)
                Publish(family, candidate);
            return;
        }

        if (candidate is not null &&
            (string.Equals(current.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal) ||
                string.Equals(
                    CandidateKey(current.Candidate),
                    CandidateKey(candidate.Candidate),
                    StringComparison.OrdinalIgnoreCase) ||
                IsNewer(candidate.Candidate, current.Candidate)))
        {
            _missing.Remove(family);
            Publish(family, candidate);
            return;
        }

        bool current_discovered = discovered.Any(found =>
            string.Equals(CandidateKey(found), CandidateKey(current.Candidate), StringComparison.OrdinalIgnoreCase));
        if (current_discovered)
        {
            _missing.Remove(family);
            return;
        }

        if (!_missing.TryGetValue(family, out MissingObservation? missing))
        {
            _missing[family] = new MissingObservation(now, 1);
            return;
        }

        missing = missing with { Count = missing.Count + 1 };
        _missing[family] = missing;
        if (missing.Count >= 2 && now - missing.ChangedAt >= _options.QuietPeriod)
        {
            _missing.Remove(family);
            Publish(family, candidate);
        }
    }

    void Publish(InstalledClientFamily family, PublishedCandidate? candidate)
    {
        InstalledClientCandidate? previous;
        lock (_state_gate)
        {
            _published.TryGetValue(family, out PublishedCandidate? current);
            if (current is null && candidate is null)
                return;
            if (current is not null && candidate is not null &&
                string.Equals(current.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal))
                return;

            previous = current?.Candidate;
            if (candidate is null)
                _published.Remove(family);
            else
                _published[family] = candidate;
        }

        RaiseCandidateChanged(new InstalledClientCandidateChangedEventArgs(
            family,
            previous,
            candidate?.Candidate));
    }

    void RaiseCandidateChanged(InstalledClientCandidateChangedEventArgs args)
    {
        EventHandler<InstalledClientCandidateChangedEventArgs>? handlers = CandidateChanged;
        if (handlers is null)
            return;

        foreach (EventHandler<InstalledClientCandidateChangedEventArgs> handler in handlers.GetInvocationList())
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

    DateTimeOffset? NextStabilityCheck()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? next = null;
        foreach (StableObservation observation in _observations.Values)
        {
            DateTimeOffset due = observation.ChangedAt + Max(_options.QuietPeriod, _options.StabilityProbePeriod);
            if (observation.Count >= 2 && due <= now)
                continue;
            if (next is null || due < next)
                next = due;
        }
        foreach (MissingObservation missing in _missing.Values)
        {
            DateTimeOffset due = missing.ChangedAt + Max(_options.QuietPeriod, _options.StabilityProbePeriod);
            if (missing.Count >= 2 && due <= now)
                continue;
            if (next is null || due < next)
                next = due;
        }
        return next;
    }

    void DrainWakes()
    {
        while (_wakes.Reader.TryRead(out _))
        {
        }
    }

    TimeSpan InitialDelay() => Max(_options.QuietPeriod, _options.StabilityProbePeriod);

    void StopAfterFailedStart(CancellationTokenSource stop)
    {
        bool owned;
        lock (_state_gate)
        {
            owned = ReferenceEquals(_stop, stop);
            if (owned)
            {
                _stop = null;
                _startup = null;
                _loop = null;
            }
        }
        if (!owned)
            return;
        stop.Cancel();
        if (!_disposed)
        {
            _watch_set.Clear();
            stop.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? stop;
        Task? startup;
        lock (_state_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            stop = _stop;
            startup = _startup;
        }

        stop?.Cancel();
        _wakes.Writer.TryComplete();
        ExceptionDispatchInfo? failure = null;
        if (startup is not null)
        {
            try
            {
                await startup.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop?.IsCancellationRequested == true)
            {
            }
            catch (Exception error)
            {
                failure = ExceptionDispatchInfo.Capture(error);
            }
        }

        Task? loop;
        lock (_state_gate)
            loop = _loop;
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop?.IsCancellationRequested == true)
            {
            }
            catch (Exception error)
            {
                failure ??= ExceptionDispatchInfo.Capture(error);
            }
        }

        _watch_set.Dispose();
        stop?.Dispose();
        _reconcile_gate.Dispose();
        lock (_state_gate)
        {
            _stop = null;
            _startup = null;
            _loop = null;
        }
        failure?.Throw();
    }

    string? Verify(InstalledClientCandidate candidate) =>
        _discovery.TryVerify(candidate, out string revision) ? revision : null;

    static PublishedCandidate? Published(InstalledClientCandidate? candidate) =>
        candidate is null
            ? null
            : new PublishedCandidate(candidate, candidate.ContentRevision!);

    internal static InstalledClientCandidate? SelectLatestVerified(
        IEnumerable<InstalledClientCandidate> candidates,
        InstalledClientFamily family,
        Func<InstalledClientCandidate, string?> verify)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(verify);

        foreach (InstalledClientCandidate candidate in candidates
            .Where(candidate => candidate.Family == family)
            .OrderByDescending(candidate => candidate.LastModified)
            .ThenByDescending(candidate => ParseVersion(candidate.Version))
            .ThenByDescending(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase))
        {
            string? revision = verify(candidate);
            if (!string.IsNullOrEmpty(revision))
                return candidate with { ContentRevision = revision };
        }

        return null;
    }

    static bool TryFingerprint(InstalledClientCandidate candidate, out string fingerprint)
    {
        var values = new List<string>(candidate.Files.Count + 3)
        {
            candidate.Family.ToString(),
            candidate.Version,
            Path.GetFullPath(candidate.Path)
        };
        try
        {
            foreach (string path in candidate.Files.Order(StringComparer.OrdinalIgnoreCase))
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                {
                    fingerprint = string.Empty;
                    return false;
                }
                values.Add($"{Path.GetFullPath(path)}:{file.Length}:{file.LastWriteTimeUtc.Ticks}");
            }
        }
        catch (Exception error) when (IsTransient(error))
        {
            fingerprint = string.Empty;
            return false;
        }
        fingerprint = string.Join('|', values);
        return true;
    }

    static string CandidateKey(InstalledClientCandidate candidate) =>
        $"{candidate.Family}:{candidate.Version}:{Path.GetFullPath(candidate.Path)}";

    static long ParseVersion(string version) => long.TryParse(version, out long parsed) ? parsed : -1;

    static bool IsNewer(InstalledClientCandidate candidate, InstalledClientCandidate current)
    {
        int modified = candidate.LastModified.CompareTo(current.LastModified);
        if (modified != 0)
            return modified > 0;
        int version = ParseVersion(candidate.Version).CompareTo(ParseVersion(current.Version));
        if (version != 0)
            return version > 0;
        return string.Compare(candidate.Path, current.Path, StringComparison.OrdinalIgnoreCase) > 0;
    }

    static TimeSpan Max(TimeSpan first, TimeSpan second) => first >= second ? first : second;

    TimeSpan RetryDelay() => Max(_options.StabilityProbePeriod, TimeSpan.FromMilliseconds(25));

    static bool IsTransient(Exception error) => error is
        IOException or
        UnauthorizedAccessException or
        ArgumentException or
        NotSupportedException;

    sealed record StableObservation(string Fingerprint, DateTimeOffset ChangedAt, int Count);

    sealed record MissingObservation(DateTimeOffset ChangedAt, int Count);

    sealed record PublishedCandidate(InstalledClientCandidate Candidate, string Fingerprint);
}
