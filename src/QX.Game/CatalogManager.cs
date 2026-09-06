using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;
using System.Runtime.ExceptionServices;

namespace Qx.Game;

public enum CatalogPurchaseStatus
{
    Completed,
    Failed,
    NotAllowed,
    Dispatched
}

public sealed record CatalogPurchaseOutcome(
    CatalogPurchaseStatus Status,
    PurchaseOffer? Offer,
    int ErrorCode)
{
    public bool Succeeded => Status is CatalogPurchaseStatus.Completed;
}

internal sealed record CatalogPurchaseState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    CatalogPurchaseOutcome? LastOutcome,
    DateTimeOffset? LastOutcomeAtUtc);

internal sealed record CatalogPurchaseUpdate(
    CatalogPurchaseState State,
    long PublicationEpoch);

public sealed partial class CatalogManager : GameStateManager
{
    private readonly object _catalog_sync = new();
    private readonly object _operations_sync = new();
    private readonly object _publication_sync = new();
    private readonly object _purchase_sync = new();
    private readonly object _purchase_publication_sync = new();
    private readonly CatalogCache _cache;
    private readonly TimeProvider _time_provider;
    private readonly Queue<CatalogInvalidationUpdate> _publications = [];
    private readonly Queue<CatalogPurchaseUpdate> _purchase_publications = [];
    private CatalogManagerState _state = new(null, 0, 0, 0, null, null);
    private CatalogPurchaseState _purchase_state = new(null, 0, 0, null, null);
    private ICatalogBrowseOperations? _browse_operations;
    private ICatalogPurchaseOperations? _purchase_operations;
    private long _reset_generation = -1;
    private long _purchase_committed_generation;
    private long _purchase_publication_epoch;
    private long _purchase_reset_generation = -1;
    private bool _publishing;
    private bool _purchase_delivering;
    private bool _purchase_publishing;
    private int _purchase_delivery_thread_id;

    public CatalogManager()
        : this(TimeProvider.System)
    {
    }

    internal CatalogManager(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time_provider = time;
        _cache = new CatalogCache(time);
    }

    public CatalogPurchaseOutcome? LastPurchase
    {
        get
        {
            lock (_purchase_sync)
                return _purchase_state.LastOutcome;
        }
    }

    internal CatalogManagerState State
    {
        get
        {
            lock (_catalog_sync)
                return _state;
        }
    }

    internal CatalogPurchaseState PurchaseState
    {
        get
        {
            lock (_purchase_sync)
                return _purchase_state;
        }
    }

    public event Action<CatalogPurchaseOutcome>? PurchaseAnswered;
    public event Action<CatalogPublished>? Published;
    internal event Action<CatalogInvalidationUpdate>? CacheInvalidated;
    internal event Action<CatalogInvalidationUpdate>? InvalidationPublished;
    internal event Action<CatalogPurchaseUpdate>? PurchaseOutcomePublished;

    protected override void OnAttach()
    {
        ResetPurchaseState(CurrentSession);
        ResetCatalogState();
        OnConnected(BindSession);

        OnIncoming(MessageContracts.Catalog.Accepted, ApplyPurchaseAccepted);
        OnIncoming(MessageContracts.Catalog.Failed, ApplyPurchaseFailed);
        OnIncoming(MessageContracts.Catalog.Forbidden, ApplyPurchaseForbidden);
        OnIncoming<CatalogPublished>(MessageKeys.Catalog.Published, ApplyPublished);
    }

    internal void BindBrowseOperations(ICatalogBrowseOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_operations_sync)
        {
            if (_browse_operations is not null)
                throw new InvalidOperationException("Catalog browse operations are already bound.");
            Volatile.Write(ref _browse_operations, operations);
        }
    }

    internal void UnbindBrowseOperations(ICatalogBrowseOperations operations)
    {
        lock (_operations_sync)
        {
            if (ReferenceEquals(_browse_operations, operations))
                Volatile.Write(ref _browse_operations, null);
        }
    }

    internal void BindPurchaseOperations(ICatalogPurchaseOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_operations_sync)
        {
            if (_purchase_operations is not null)
                throw new InvalidOperationException("Catalog purchase operations are already bound.");
            Volatile.Write(ref _purchase_operations, operations);
        }
    }

    internal void UnbindPurchaseOperations(ICatalogPurchaseOperations operations)
    {
        lock (_operations_sync)
        {
            if (ReferenceEquals(_purchase_operations, operations))
                Volatile.Write(ref _purchase_operations, null);
        }
    }

    internal CatalogManagerScope CaptureScope(
        long? expected_session_generation = null,
        long? expected_catalog_generation = null)
    {
        lock (_catalog_sync)
        {
            Session session = _state.Session
                ?? throw new InvalidOperationException("An active hotel session is required.");
            RequireExpectedGenerations(
                _state,
                expected_session_generation,
                expected_catalog_generation);
            return new CatalogManagerScope(
                session,
                _state.SessionGeneration,
                _state.CatalogGeneration);
        }
    }

    internal CatalogCommitStatus ReadIndex(
        CatalogManagerScope scope,
        string catalog_type,
        TimeSpan max_age,
        out CatalogCachedIndex? value,
        out CatalogManagerState state)
    {
        lock (_catalog_sync)
        {
            state = _state;
            CatalogCommitStatus status = ScopeStatus(scope, state);
            value = status is CatalogCommitStatus.Committed
                ? _cache.Index(catalog_type, max_age)
                : null;
            return status;
        }
    }

    internal CatalogCommitStatus ReadPage(
        CatalogManagerScope scope,
        string catalog_type,
        int page_id,
        TimeSpan max_age,
        out CatalogCachedPage? value,
        out long version,
        out CatalogManagerState state)
    {
        lock (_catalog_sync)
        {
            state = _state;
            CatalogCommitStatus status = ScopeStatus(scope, state);
            value = status is CatalogCommitStatus.Committed
                ? _cache.Page(catalog_type, page_id, max_age)
                : null;
            version = status is CatalogCommitStatus.Committed
                ? _cache.PageVersion(catalog_type, page_id)
                : 0;
            return status;
        }
    }

    internal CatalogCommitStatus TryCommitIndex(
        CatalogManagerScope scope,
        string catalog_type,
        CatalogIndex source,
        out CatalogCachedIndex value,
        out CatalogManagerState state)
    {
        CatalogIndex frozen = CatalogCache.FreezeIndex(source);
        if (!string.Equals(frozen.CatalogType, catalog_type, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The catalog index type does not match the request.");
        frozen = new CatalogIndex(frozen.Root, frozen.NewAdditionsAvailable, catalog_type);
        DateTimeOffset now = _time_provider.GetUtcNow();
        long timestamp = _time_provider.GetTimestamp();
        bool drain;
        lock (_publication_sync)
        {
            CatalogInvalidationUpdate update;
            lock (_catalog_sync)
            {
                CatalogCommitStatus status = ScopeStatus(scope, _state);
                if (status is not CatalogCommitStatus.Committed)
                {
                    value = default;
                    state = _state;
                    return status;
                }
                long revision = checked(_state.Revision + 1);
                long catalog_generation = checked(_state.CatalogGeneration + 1);
                _cache.StoreIndex(catalog_type, frozen, revision, timestamp, now);
                _state = _state with
                {
                    CatalogGeneration = catalog_generation,
                    Revision = revision
                };
                _reset_generation = -1;
                state = _state;
                value = new CatalogCachedIndex(frozen, revision, now, TimeSpan.Zero);
                update = new CatalogInvalidationUpdate(
                    CatalogInvalidationKind.IndexRefreshed,
                    state,
                    catalog_type,
                    now,
                    null);
            }
            CacheInvalidated?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
        return CatalogCommitStatus.Committed;
    }

    internal CatalogCommitStatus TryCommitPage(
        CatalogManagerScope scope,
        string catalog_type,
        long expected_version,
        CatalogPage source,
        out CatalogCachedPage value,
        out CatalogManagerState state)
    {
        CatalogPage frozen = CatalogCache.FreezePage(source);
        if (!string.Equals(frozen.CatalogType, catalog_type, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The catalog page type does not match the request.");
        if (!string.Equals(frozen.CatalogType, catalog_type, StringComparison.Ordinal))
        {
            frozen = new CatalogPage(
                frozen.PageId,
                catalog_type,
                frozen.LayoutCode,
                frozen.Localization,
                frozen.Offers,
                frozen.OfferId,
                frozen.AcceptSeasonCurrencyAsCredits,
                frozen.FrontPageItems);
        }
        DateTimeOffset now = _time_provider.GetUtcNow();
        long timestamp = _time_provider.GetTimestamp();
        lock (_catalog_sync)
        {
            CatalogCommitStatus status = ScopeStatus(scope, _state);
            if (status is not CatalogCommitStatus.Committed)
            {
                value = default;
                state = _state;
                return status;
            }
            if (_cache.PageVersion(catalog_type, frozen.PageId) != expected_version)
            {
                value = default;
                state = _state;
                return CatalogCommitStatus.Superseded;
            }
            long revision = checked(_state.Revision + 1);
            _cache.StorePage(catalog_type, frozen, revision, timestamp, now);
            _state = _state with { Revision = revision };
            state = _state;
            value = new CatalogCachedPage(frozen, revision, now, TimeSpan.Zero);
            return CatalogCommitStatus.Committed;
        }
    }

    internal CatalogCacheSnapshot Snapshot(string catalog_type)
    {
        lock (_catalog_sync)
        {
            return new CatalogCacheSnapshot(
                _state,
                catalog_type,
                _cache.HeldIndex(catalog_type),
                _cache.Pages(catalog_type));
        }
    }

    internal CatalogCacheState ReadCacheState(string catalog_type)
    {
        lock (_catalog_sync)
            return _cache.State(catalog_type);
    }

    internal CatalogInvalidationUpdate Clear(
        string? catalog_type,
        long? expected_session_generation,
        long? expected_catalog_generation) =>
        CommitInvalidation(
            CatalogInvalidationKind.Cleared,
            catalog_type,
            null,
            expected_session_generation,
            expected_catalog_generation,
            null) ?? throw new InvalidOperationException("The catalog clear was not committed.");

    private void ApplyPurchaseAccepted(PurchaseOK message, long state_generation) =>
        StorePurchaseOutcome(
            new CatalogPurchaseOutcome(CatalogPurchaseStatus.Completed, message.Offer, 0),
            state_generation);

    private void ApplyPurchaseFailed(PurchaseError message, long state_generation) =>
        StorePurchaseOutcome(
            new CatalogPurchaseOutcome(CatalogPurchaseStatus.Failed, null, message.ErrorCode),
            state_generation);

    private void ApplyPurchaseForbidden(PurchaseNotAllowed message, long state_generation) =>
        StorePurchaseOutcome(
            new CatalogPurchaseOutcome(CatalogPurchaseStatus.NotAllowed, null, message.ErrorCode),
            state_generation);

    private void StorePurchaseOutcome(CatalogPurchaseOutcome outcome, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        lock (_purchase_publication_sync)
        {
            CatalogPurchaseUpdate update;
            lock (_purchase_sync)
            {
                CatalogPurchaseState current = _purchase_state;
                if (state_generation != _purchase_committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                DateTimeOffset now = _time_provider.GetUtcNow();
                CatalogPurchaseState updated = current with
                {
                    Revision = checked(_purchase_state.Revision + 1),
                    LastOutcome = outcome,
                    LastOutcomeAtUtc = now
                };
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        _purchase_state = updated;
                        _purchase_committed_generation = state_generation;
                        _purchase_reset_generation = -1;
                        update = new CatalogPurchaseUpdate(
                            updated,
                            _purchase_publication_epoch);
                    }))
                {
                    return;
                }
            }
            _purchase_publications.Enqueue(update);
            drain = !_purchase_publishing;
            _purchase_publishing = true;
        }
        if (drain)
            DrainPurchasePublications();
    }

    private void ResetPurchaseState(Session? active_session)
    {
        long state_generation = CurrentStateGeneration;
        int thread_id = Environment.CurrentManagedThreadId;
        lock (_purchase_publication_sync)
        {
            while (_purchase_delivering && _purchase_delivery_thread_id != thread_id)
                Monitor.Wait(_purchase_publication_sync);
            lock (_purchase_sync)
            {
                if (state_generation < _purchase_committed_generation ||
                    state_generation == _purchase_reset_generation &&
                    ReferenceEquals(_purchase_state.Session, active_session))
                {
                    return;
                }
                _purchase_state = new CatalogPurchaseState(
                    active_session,
                    state_generation,
                    checked(_purchase_state.Revision + 1),
                    null,
                    null);
                _purchase_committed_generation = state_generation;
                _purchase_reset_generation = state_generation;
                _purchase_publication_epoch = checked(_purchase_publication_epoch + 1);
            }
        }
    }

    private void DrainPurchasePublications()
    {
        Exception? failure = null;
        while (true)
        {
            CatalogPurchaseUpdate update;
            lock (_purchase_publication_sync)
            {
                if (!_purchase_publications.TryDequeue(out update!))
                {
                    _purchase_publishing = false;
                    break;
                }
                _purchase_delivering = true;
                _purchase_delivery_thread_id = Environment.CurrentManagedThreadId;
            }
            try
            {
                if (!PurchaseUpdateCurrent(update))
                    continue;
                try
                {
                    PurchaseOutcomePublished?.Invoke(update);
                }
                catch (Exception error)
                {
                    failure ??= error;
                }
                if (!PurchaseUpdateCurrent(update))
                    continue;
                try
                {
                    PurchaseAnswered?.Invoke(update.State.LastOutcome!);
                }
                catch (Exception error)
                {
                    failure ??= error;
                }
            }
            finally
            {
                lock (_purchase_publication_sync)
                {
                    _purchase_delivering = false;
                    _purchase_delivery_thread_id = 0;
                    Monitor.PulseAll(_purchase_publication_sync);
                }
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private bool PurchaseUpdateCurrent(CatalogPurchaseUpdate update)
    {
        lock (_purchase_publication_sync)
        {
            if (_purchase_publication_epoch != update.PublicationEpoch)
                return false;
            lock (_purchase_sync)
            {
                if (_purchase_state.SessionGeneration != update.State.SessionGeneration ||
                    !ReferenceEquals(_purchase_state.Session, update.State.Session))
                {
                    return false;
                }
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == update.State.SessionGeneration &&
            after == update.State.SessionGeneration &&
            ReferenceEquals(active_session, update.State.Session);
    }

    private void ApplyPublished(CatalogPublished message, long state_generation)
    {
        CatalogPublished frozen = new(
            message.InstantlyRefreshCatalogue,
            message.NewFurniDataHash);
        _ = CommitInvalidation(
            CatalogInvalidationKind.Published,
            null,
            frozen,
            null,
            null,
            state_generation);
    }

    private void BindSession(Session session)
    {
        ResetPurchaseState(session);
        long state_generation = CurrentStateGeneration;
        bool drain;
        lock (_publication_sync)
        {
            CatalogInvalidationUpdate update;
            lock (_catalog_sync)
            {
                if (state_generation < _state.SessionGeneration ||
                    state_generation == _state.SessionGeneration &&
                    ReferenceEquals(_state.Session, session) &&
                    _reset_generation != state_generation)
                {
                    return;
                }
                _cache.Clear(null);
                _state = new CatalogManagerState(
                    session,
                    state_generation,
                    checked(_state.CatalogGeneration + 1),
                    checked(_state.Revision + 1),
                    null,
                    null);
                _reset_generation = -1;
                update = new CatalogInvalidationUpdate(
                    CatalogInvalidationKind.SessionChanged,
                    _state,
                    null,
                    _time_provider.GetUtcNow(),
                    null);
            }
            CacheInvalidated?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private CatalogInvalidationUpdate? CommitInvalidation(
        CatalogInvalidationKind kind,
        string? catalog_type,
        CatalogPublished? publication,
        long? expected_session_generation,
        long? expected_catalog_generation,
        long? callback_state_generation)
    {
        bool drain;
        CatalogInvalidationUpdate update;
        lock (_publication_sync)
        {
            lock (_catalog_sync)
            {
                if (callback_state_generation is { } state_generation &&
                    (state_generation != _state.SessionGeneration ||
                     _state.Session is null ||
                     !ReferenceEquals(_state.Session, CurrentSession)))
                {
                    return null;
                }
                RequireExpectedGenerations(
                    _state,
                    expected_session_generation,
                    expected_catalog_generation);
                _cache.Clear(catalog_type);
                DateTimeOffset now = _time_provider.GetUtcNow();
                _state = _state with
                {
                    CatalogGeneration = checked(_state.CatalogGeneration + 1),
                    Revision = checked(_state.Revision + 1),
                    LastPublication = publication ?? _state.LastPublication,
                    LastPublishedAtUtc = publication is null ? _state.LastPublishedAtUtc : now
                };
                if (callback_state_generation is not null)
                    _reset_generation = -1;
                update = new CatalogInvalidationUpdate(
                    kind,
                    _state,
                    catalog_type,
                    now,
                    publication);
            }
            CacheInvalidated?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
        return update;
    }

    private static CatalogCommitStatus ScopeStatus(
        CatalogManagerScope scope,
        CatalogManagerState state)
    {
        if (!ReferenceEquals(scope.Session, state.Session) ||
            scope.SessionGeneration != state.SessionGeneration)
        {
            return CatalogCommitStatus.SessionChanged;
        }
        return scope.CatalogGeneration == state.CatalogGeneration
            ? CatalogCommitStatus.Committed
            : CatalogCommitStatus.CatalogChanged;
    }

    private static void RequireExpectedGenerations(
        CatalogManagerState state,
        long? expected_session_generation,
        long? expected_catalog_generation)
    {
        if (expected_session_generation is { } session_generation &&
            session_generation != state.SessionGeneration)
        {
            throw new InvalidOperationException("The catalog session generation does not match the expected value.");
        }
        if (expected_catalog_generation is { } catalog_generation &&
            catalog_generation != state.CatalogGeneration)
        {
            throw new InvalidOperationException("The catalog generation does not match the expected value.");
        }
    }

    private void ResetCatalogState()
    {
        long state_generation = CurrentStateGeneration;
        Session? session = CurrentSession;
        bool drain;
        lock (_publication_sync)
        {
            CatalogInvalidationUpdate update;
            lock (_catalog_sync)
            {
                if (state_generation < _state.SessionGeneration ||
                    state_generation == _reset_generation)
                {
                    return;
                }
                _cache.Clear(null);
                _state = new CatalogManagerState(
                    session,
                    state_generation,
                    checked(_state.CatalogGeneration + 1),
                    checked(_state.Revision + 1),
                    null,
                    null);
                _reset_generation = state_generation;
                update = new CatalogInvalidationUpdate(
                    CatalogInvalidationKind.Reset,
                    _state,
                    null,
                    _time_provider.GetUtcNow(),
                    null);
            }
            CacheInvalidated?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private bool EnqueuePublication(CatalogInvalidationUpdate update)
    {
        _publications.Enqueue(update);
        if (_publishing)
            return false;
        _publishing = true;
        return true;
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            CatalogInvalidationUpdate update;
            lock (_publication_sync)
            {
                if (!_publications.TryDequeue(out update!))
                {
                    _publishing = false;
                    break;
                }
            }
            try
            {
                InvalidationPublished?.Invoke(update);
                if (update.Publication is { } publication)
                    Published?.Invoke(publication);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private ICatalogBrowseOperations BrowseOperations() =>
        Volatile.Read(ref _browse_operations) ??
        throw new InvalidOperationException(
            "Catalog browse operations are unavailable until the application runtime is active.");

    private ICatalogPurchaseOperations PurchaseOperations() =>
        Volatile.Read(ref _purchase_operations) ??
        throw new InvalidOperationException(
            "Catalog purchase operations are unavailable until the application runtime is active.");

    public Task<CatalogPurchaseOutcome> PurchaseAsync<T>(
        string name,
        T request,
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
        where T : Qx.Messages.IComposer
    {
        ICatalogPurchaseOperations operations = PurchaseOperations();
        if (request is PurchaseFromCatalogRequest purchase &&
            string.Equals(name, MessageKeys.Catalog.Purchase.Value, StringComparison.Ordinal))
        {
            return operations.PurchaseAsync(purchase, timeout_ms, cancellation_token);
        }
        return operations.DispatchCompatibility(
            () => SendMessage(name, request),
            timeout_ms,
            cancellation_token);
    }

    public Task<CatalogPurchaseOutcome> PurchaseAsync<T>(
        MessageKey key,
        T request,
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
        where T : Qx.Messages.IComposer
    {
        if (key.IsEmpty)
            throw new ArgumentException("A catalog purchase requires a message key.", nameof(key));
        ICatalogPurchaseOperations operations = PurchaseOperations();
        if (key == MessageKeys.Catalog.Purchase && request is PurchaseFromCatalogRequest purchase)
            return operations.PurchaseAsync(purchase, timeout_ms, cancellation_token);
        return operations.DispatchCompatibility(
            () => SendMessage(key, request),
            timeout_ms,
            cancellation_token);
    }

    public Task<CatalogPurchaseOutcome> PurchaseAsync(
        PurchaseFromCatalogRequest request,
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default) =>
        PurchaseOperations().PurchaseAsync(
            request,
            timeout_ms,
            cancellation_token);

    protected override void Reset()
    {
        ResetPurchaseState(CurrentSession);
        ResetCatalogState();
    }
}
