using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

internal sealed record WalletState(
    long Generation,
    long Revision,
    Session? Session,
    long CreditsSnapshotRevision,
    long ActivityPointsSnapshotRevision,
    bool CreditsLoaded,
    int Credits,
    bool ActivityPointsLoaded,
    IReadOnlyDictionary<int, int> ActivityPoints);

internal enum WalletStateChangeKind
{
    CreditsRefreshed,
    ActivityPointsRefreshed,
    ActivityPointUpdated,
    Reset
}

internal sealed record WalletPointUpdate(int Type, int Amount, int Change);

internal sealed record WalletStateUpdate(
    WalletStateChangeKind Kind,
    WalletState State,
    WalletPointUpdate? Point);

internal sealed class EconomyManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<WalletStateUpdate> publications = [];
    private WalletState state;
    private IReadOnlyDictionary<int, int> activity_points = EmptyPoints();
    private Session? session;
    private long generation;
    private long committed_generation;
    private long reset_generation = -1;
    private long revision;
    private long credits_snapshot_revision;
    private long activity_points_snapshot_revision;
    private int credits;
    private bool credits_loaded;
    private bool activity_points_loaded;
    private bool publishing;

    public EconomyManager()
    {
        state = Snapshot();
    }

    internal WalletState State => Volatile.Read(ref state);
    internal event Action<WalletStateUpdate>? StateCommitted;
    internal event Action<WalletStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        Reset();
        OnConnected(BindSession);
        OnIncoming(MessageContracts.Wallet.CreditsBalance, ApplyCredits);
        OnIncoming(MessageContracts.Wallet.ActivityPoints, ApplyActivityPoints);
        OnIncoming(MessageContracts.Wallet.ActivityPointUpdated, ApplyActivityPointUpdate);
    }

    protected override void Reset()
    {
        long state_generation = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        bool drain;
        lock (publication_sync)
        {
            WalletState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation || state_generation == reset_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = state_generation;
                generation = state_generation;
                session = active_session;
                ResetBalances();
                revision = checked(revision + 1);
                updated = Publish();
            }
            var update = new WalletStateUpdate(WalletStateChangeKind.Reset, updated, null);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private void BindSession(Session active_session)
    {
        long state_generation = CurrentStateGeneration;
        bool drain;
        lock (publication_sync)
        {
            WalletState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                generation = state_generation;
                session = active_session;
                ResetBalances();
                revision = checked(revision + 1);
                updated = Publish();
            }
            var update = new WalletStateUpdate(WalletStateChangeKind.Reset, updated, null);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private void ApplyCredits(CreditBalance message, long state_generation)
    {
        int value = message.Credits;
        Store(
            state_generation,
            WalletStateChangeKind.CreditsRefreshed,
            null,
            () =>
            {
                credits = value;
                credits_loaded = true;
                credits_snapshot_revision = checked(credits_snapshot_revision + 1);
            });
    }

    private void ApplyActivityPoints(ActivityPoints message, long state_generation)
    {
        var values = new Dictionary<int, int>();
        foreach (ActivityPoint point in message.Points)
            values[point.Type] = point.Amount;
        IReadOnlyDictionary<int, int> snapshot = Freeze(values);
        Store(
            state_generation,
            WalletStateChangeKind.ActivityPointsRefreshed,
            null,
            () =>
            {
                activity_points = snapshot;
                activity_points_loaded = true;
                activity_points_snapshot_revision = checked(activity_points_snapshot_revision + 1);
            });
    }

    private void ApplyActivityPointUpdate(
        ActivityPointNotification message,
        long state_generation)
    {
        var point = new WalletPointUpdate(message.Type, message.Amount, message.Change);
        Store(
            state_generation,
            WalletStateChangeKind.ActivityPointUpdated,
            point,
            () =>
            {
                var values = new Dictionary<int, int>(activity_points)
                {
                    [message.Type] = message.Amount
                };
                activity_points = Freeze(values);
                activity_points_snapshot_revision = checked(activity_points_snapshot_revision + 1);
            });
    }

    private void Store(
        long state_generation,
        WalletStateChangeKind kind,
        WalletPointUpdate? point,
        Action mutation)
    {
        Session? active_session = Interceptor.Session;
        if (active_session is null)
            return;
        bool drain;
        lock (publication_sync)
        {
            WalletState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                if (generation != state_generation || !ReferenceEquals(session, active_session))
                {
                    generation = state_generation;
                    session = active_session;
                    ResetBalances();
                }
                mutation();
                committed_generation = state_generation;
                reset_generation = -1;
                revision = checked(revision + 1);
                updated = Publish();
            }
            var update = new WalletStateUpdate(kind, updated, point);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private void ResetBalances()
    {
        credits = 0;
        credits_loaded = false;
        activity_points_loaded = false;
        activity_points = EmptyPoints();
        credits_snapshot_revision = checked(credits_snapshot_revision + 1);
        activity_points_snapshot_revision = checked(activity_points_snapshot_revision + 1);
    }

    private WalletState Publish()
    {
        WalletState updated = Snapshot();
        Volatile.Write(ref state, updated);
        return updated;
    }

    private WalletState Snapshot() => new(
        generation,
        revision,
        session,
        credits_snapshot_revision,
        activity_points_snapshot_revision,
        credits_loaded,
        credits,
        activity_points_loaded,
        activity_points);

    private bool EnqueuePublication(WalletStateUpdate update)
    {
        publications.Enqueue(update);
        if (publishing)
            return false;
        publishing = true;
        return true;
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            WalletStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
            }
            try
            {
                StateChanged?.Invoke(update);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IReadOnlyDictionary<int, int> Freeze(Dictionary<int, int> values) =>
        new ReadOnlyDictionary<int, int>(values);

    private static IReadOnlyDictionary<int, int> EmptyPoints() =>
        Freeze(new Dictionary<int, int>());
}
