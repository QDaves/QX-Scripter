using Qx.Game.Snapshots;
using Qx.Interception;

namespace Qx.Game.Application;

internal sealed partial class AchievementApplication :
    IApplicationFeature,
    IAchievementOperations,
    IBadgeInventoryOperations
{
    private readonly IConnection connection;
    private readonly AchievementManager achievements;
    private readonly BadgeInventoryManager badges;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<AchievementChanged> achievement_changed;
    private readonly GuardedEventSource<BadgeChanged> badge_changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public AchievementApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        achievements = game.Achievements;
        badges = game.Badges;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        achievement_changed = new GuardedEventSource<AchievementChanged>(observer_error);
        badge_changed = new GuardedEventSource<BadgeChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<AchievementStateRequest, AchievementStateView>(
                AchievementApplicationDescriptors.AchievementState,
                (request, _) => ValueTask.FromResult(ReadAchievementState(request))),
            new ApplicationCallBinding<AchievementPageRequest, AchievementPage>(
                AchievementApplicationDescriptors.AchievementList,
                (request, _) => ValueTask.FromResult(ReadAchievements(request))),
            new ApplicationCallBinding<
                AchievementPointLimitPageRequest,
                AchievementPointLimitPage>(
                AchievementApplicationDescriptors.AchievementPointLimitsList,
                (request, _) => ValueTask.FromResult(ReadAchievementPointLimits(request))),
            new ApplicationCallBinding<AchievementRefreshRequest, AchievementRefreshResult>(
                AchievementApplicationDescriptors.AchievementRefresh,
                RefreshAchievements),
            new ApplicationCallBinding<
                AchievementPointLimitsRefreshRequest,
                AchievementPointLimitsRefreshResult>(
                AchievementApplicationDescriptors.AchievementPointLimitsRefresh,
                RefreshAchievementPointLimits),
            new ApplicationEventBinding<AchievementChanged>(
                AchievementApplicationDescriptors.AchievementChanged,
                achievement_changed.Subscribe),
            new ApplicationCallBinding<BadgeStateRequest, BadgeStateView>(
                AchievementApplicationDescriptors.BadgeState,
                (request, _) => ValueTask.FromResult(ReadBadgeState(request))),
            new ApplicationCallBinding<OwnedBadgePageRequest, OwnedBadgePage>(
                AchievementApplicationDescriptors.OwnedBadgeList,
                (request, _) => ValueTask.FromResult(ReadOwnedBadges(request))),
            new ApplicationCallBinding<
                BadgeSelectedSetPageRequest,
                BadgeSelectedSetPage>(
                AchievementApplicationDescriptors.BadgeSelectedSetsList,
                (request, _) => ValueTask.FromResult(ReadBadgeSelectedSets(request))),
            new ApplicationCallBinding<BadgeSelectedPageRequest, BadgeSelectedPage>(
                AchievementApplicationDescriptors.BadgeSelectedList,
                (request, _) => ValueTask.FromResult(ReadSelectedBadges(request))),
            new ApplicationCallBinding<BadgeRefreshRequest, BadgeRefreshResult>(
                AchievementApplicationDescriptors.BadgeRefresh,
                RefreshBadges),
            new ApplicationEventBinding<BadgeChanged>(
                AchievementApplicationDescriptors.BadgeChanged,
                badge_changed.Subscribe)
        ]);
        achievements.StateCommitted += ObserveAchievementCommit;
        achievements.StateChanged += PublishAchievementChanged;
        badges.StateCommitted += ObserveBadgeCommit;
        badges.StateChanged += PublishBadgeChanged;
        bool achievement_bound = false;
        try
        {
            achievements.BindOperations(this);
            achievement_bound = true;
            badges.BindOperations(this);
        }
        catch
        {
            if (achievement_bound)
                achievements.UnbindOperations(this);
            achievements.StateCommitted -= ObserveAchievementCommit;
            achievements.StateChanged -= PublishAchievementChanged;
            badges.StateCommitted -= ObserveBadgeCommit;
            badges.StateChanged -= PublishBadgeChanged;
            achievement_changed.Dispose();
            badge_changed.Dispose();
            lifetime.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public void Dispose()
    {
        bool first;
        bool wait = invocation_depth.Value == 0;
        lock (lifecycle_sync)
        {
            first = !dispose_started;
            dispose_started = true;
        }
        if (first)
        {
            achievements.UnbindOperations(this);
            badges.UnbindOperations(this);
            achievements.StateCommitted -= ObserveAchievementCommit;
            achievements.StateChanged -= PublishAchievementChanged;
            badges.StateCommitted -= ObserveBadgeCommit;
            badges.StateChanged -= PublishBadgeChanged;
            lifetime.Cancel();
            ClearOperationState();
            ClearLeases();
            achievement_changed.Dispose();
            badge_changed.Dispose();
            lock (lifecycle_sync)
                cleanup_finished = true;
            CompleteDisposalIfReady();
        }
        if (wait)
            disposal.Task.GetAwaiter().GetResult();
    }

    private async ValueTask<TResult> InvokeAsync<TResult>(
        CancellationToken cancellation_token,
        Func<CancellationToken, ValueTask<TResult>> invocation)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Invocation active;
        try
        {
            active = EnterInvocation();
        }
        catch (ObjectDisposedException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        using (active)
        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            lifetime.Token))
        {
            try
            {
                TResult result = await invocation(linked.Token).ConfigureAwait(false);
                cancellation_token.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                return result;
            }
            catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (ObjectDisposedException) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (Exception) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(AchievementApplication));
            }
        }
    }

    private void InvokeLegacy(Action<CancellationToken> invocation)
    {
        using Invocation active = EnterInvocation();
        try
        {
            invocation(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(AchievementApplication));
        }
    }

    private Invocation EnterInvocation()
    {
        lock (lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(dispose_started, this);
            active_invocations++;
        }
        invocation_depth.Value++;
        return new Invocation(this);
    }

    private void LeaveInvocation()
    {
        invocation_depth.Value = Math.Max(0, invocation_depth.Value - 1);
        lock (lifecycle_sync)
            active_invocations--;
        CompleteDisposalIfReady();
    }

    private void CompleteDisposalIfReady()
    {
        bool complete = false;
        lock (lifecycle_sync)
        {
            if (dispose_started &&
                cleanup_finished &&
                active_invocations == 0 &&
                !disposal_finished)
            {
                disposal_finished = true;
                complete = true;
            }
        }
        if (complete)
        {
            lifetime.Dispose();
            disposal.TrySetResult();
        }
    }

    private bool DisposalStarted() => Volatile.Read(ref dispose_started);

    private bool AchievementPublicationCurrent(AchievementStateUpdate update) =>
        !DisposalStarted() && achievements.IsCurrentPublication(update);

    private bool BadgePublicationCurrent(BadgeInventoryStateUpdate update) =>
        !DisposalStarted() && badges.IsCurrentPublication(update);

    private void PublishAchievementChanged(AchievementStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            PublishAchievementChangedCore(update);
    }

    private void PublishAchievementChangedCore(AchievementStateUpdate update)
    {
        if (!AchievementPublicationCurrent(update) ||
            update.Kind is AchievementStateChangeKind.Request)
        {
            return;
        }
        AchievementState state = update.State;
        AchievementSnapshotLease? lease = TryStoreAchievementLease(state);
        long? snapshot_revision = lease?.Revision;
        AchievementChangeKind kind = update.Kind switch
        {
            AchievementStateChangeKind.Snapshot => AchievementChangeKind.Snapshot,
            AchievementStateChangeKind.Updated => AchievementChangeKind.Updated,
            AchievementStateChangeKind.Score => AchievementChangeKind.Score,
            AchievementStateChangeKind.PointLimits => AchievementChangeKind.PointLimits,
            AchievementStateChangeKind.NewCodes => AchievementChangeKind.NewCodes,
            AchievementStateChangeKind.Reset => AchievementChangeKind.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };
        AchievementDeltaCommit? delta = update.Value as AchievementDeltaCommit;
        var new_codes = new HashSet<string>(state.NewCodes, StringComparer.Ordinal);
        long source_revision = update.Kind switch
        {
            AchievementStateChangeKind.Snapshot or AchievementStateChangeKind.Updated =>
                state.ListRevision,
            AchievementStateChangeKind.Score => state.ScoreRevision,
            AchievementStateChangeKind.PointLimits => state.PointLimitsRevision,
            AchievementStateChangeKind.NewCodes => state.NewCodesRevision,
            _ => state.Revision
        };
        var value = new AchievementChanged(
            kind,
            time_provider.GetUtcNow(),
            state.Session?.Client,
            state.SessionGeneration,
            state.Revision,
            source_revision,
            snapshot_revision,
            update.Kind is AchievementStateChangeKind.Snapshot or
                AchievementStateChangeKind.Updated or
                AchievementStateChangeKind.NewCodes
                    ? AchievementSummary(state)
                    : null,
            delta is null
                ? null
                : AchievementItem(delta.Current, new_codes.Contains(delta.Current.Code)),
            delta?.Previous is null
                ? null
                : AchievementItem(
                    delta.Previous,
                    new_codes.Contains(delta.Previous.Code)),
            state.ScoreLoaded,
            state.ScoreLoaded ? state.Score : null,
            state.PointLimitsLoaded,
            update.Kind is AchievementStateChangeKind.PointLimits
                ? state.PointLimits.Limits.Count
                : null,
            update.Kind is AchievementStateChangeKind.NewCodes
                ? state.NewCodes.Count
                : null);
        achievement_changed.Publish(value, () => AchievementPublicationCurrent(update));
    }

    private void PublishBadgeChanged(BadgeInventoryStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            PublishBadgeChangedCore(update);
    }

    private void PublishBadgeChangedCore(BadgeInventoryStateUpdate update)
    {
        if (!BadgePublicationCurrent(update))
            return;
        BadgeSnapshotLease? lease = TryStoreBadgeLease(update.State);
        long? snapshot_revision = lease?.Revision;
        if (update.Kind is BadgeInventoryStateChangeKind.Mutation &&
            update.Value is BadgeMutationCommit commit)
        {
            foreach (BadgeMutation mutation in commit.Mutations)
            {
                if (!BadgePublicationCurrent(update))
                    return;
                PublishBadgeMutation(update, lease, snapshot_revision, mutation);
            }
            return;
        }
        BadgeInventoryState state = update.State;
        BadgeChangeKind kind = update.Kind switch
        {
            BadgeInventoryStateChangeKind.Request or
                BadgeInventoryStateChangeKind.Fragment => BadgeChangeKind.Loading,
            BadgeInventoryStateChangeKind.Loaded => BadgeChangeKind.Loaded,
            BadgeInventoryStateChangeKind.Selected => BadgeChangeKind.Selected,
            BadgeInventoryStateChangeKind.CorrelationFailed =>
                BadgeChangeKind.CorrelationFailed,
            BadgeInventoryStateChangeKind.Reset => BadgeChangeKind.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };
        BadgeSelectedState? selected = update.Value as BadgeSelectedState;
        var value = new BadgeChanged(
            kind,
            time_provider.GetUtcNow(),
            state.Session?.Client,
            state.SessionGeneration,
            state.Revision,
            update.Kind is BadgeInventoryStateChangeKind.Selected
                ? state.SelectedRevision
                : state.InventoryRevision,
            state.LoadGeneration,
            snapshot_revision,
            lease is null ? null : BadgeSummary(lease),
            null,
            selected is null
                ? null
                : new BadgeSelectedSetSummary(
                    selected.Value.UserId,
                    selected.Value.Badges.Count,
                    selected.Revision),
            state.RecoveryRetiredRequestEpoch > 0
                ? state.RecoveryRetiredRequestEpoch
                : null,
            state.RecoveryActiveRequestEpoch > 0
                ? state.RecoveryActiveRequestEpoch
                : null);
        badge_changed.Publish(value, () => BadgePublicationCurrent(update));
    }

    private void PublishBadgeMutation(
        BadgeInventoryStateUpdate update,
        BadgeSnapshotLease? lease,
        long? snapshot_revision,
        BadgeMutation mutation)
    {
        BadgeInventoryState state = update.State;
        BadgeChangeKind kind = mutation.Kind switch
        {
            BadgeMutationKind.Added => BadgeChangeKind.Added,
            BadgeMutationKind.Updated => BadgeChangeKind.Updated,
            BadgeMutationKind.Removed => BadgeChangeKind.Removed,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var value = new BadgeChanged(
            kind,
            time_provider.GetUtcNow(),
            state.Session?.Client,
            state.SessionGeneration,
            state.Revision,
            state.InventoryRevision,
            state.LoadGeneration,
            snapshot_revision,
            lease is null ? null : BadgeSummary(lease),
            SnapshotFactory.From(mutation.Badge),
            null,
            state.RecoveryRetiredRequestEpoch > 0
                ? state.RecoveryRetiredRequestEpoch
                : null,
            state.RecoveryActiveRequestEpoch > 0
                ? state.RecoveryActiveRequestEpoch
                : null);
        badge_changed.Publish(value, () => BadgePublicationCurrent(update));
    }

    private AchievementSnapshotLease? TryStoreAchievementLease(AchievementState state)
    {
        try
        {
            return StoreAchievementLease(state);
        }
        catch (Exception error) when (
            error is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    private BadgeSnapshotLease? TryStoreBadgeLease(BadgeInventoryState state)
    {
        try
        {
            return StoreBadgeLease(state);
        }
        catch (Exception error) when (
            error is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(DisposalStarted(), this);

    private bool TryEnterInvocation(out Invocation? invocation)
    {
        try
        {
            invocation = EnterInvocation();
            return true;
        }
        catch (ObjectDisposedException)
        {
            invocation = null;
            return false;
        }
    }

    private sealed class Invocation(AchievementApplication owner) : IDisposable
    {
        private AchievementApplication? current = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref current, null)?.LeaveInvocation();
        }
    }

    private sealed class GuardedEventSource<T>(Action<Exception>? observer_error) : IDisposable
    {
        private readonly object sync = new();
        private Action<T>? listeners;
        private bool disposed;

        public IDisposable Subscribe(Action<T> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                listeners += listener;
            }
            return new Subscription(this, listener);
        }

        public void Publish(T value, Func<bool> current)
        {
            ArgumentNullException.ThrowIfNull(current);
            Action<T>? snapshot;
            lock (sync)
            {
                if (disposed)
                    return;
                snapshot = listeners;
            }
            if (snapshot is null)
                return;
            foreach (Action<T> listener in snapshot.GetInvocationList().Cast<Action<T>>())
            {
                lock (sync)
                {
                    if (disposed)
                        return;
                }
                if (!current())
                    return;
                try
                {
                    listener(value);
                }
                catch (Exception error)
                {
                    observer_error?.Invoke(error);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                listeners = null;
            }
        }

        private void Unsubscribe(Action<T> listener)
        {
            lock (sync)
                listeners -= listener;
        }

        private sealed class Subscription(
            GuardedEventSource<T> source,
            Action<T> listener) : IDisposable
        {
            private GuardedEventSource<T>? current_source = source;
            private Action<T>? current_listener = listener;

            public void Dispose()
            {
                GuardedEventSource<T>? source_value = Interlocked.Exchange(
                    ref current_source,
                    null);
                Action<T>? listener_value = Interlocked.Exchange(
                    ref current_listener,
                    null);
                if (source_value is not null && listener_value is not null)
                    source_value.Unsubscribe(listener_value);
            }
        }
    }
}
