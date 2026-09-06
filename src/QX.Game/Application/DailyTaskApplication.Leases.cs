using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class DailyTaskApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, DailyTaskSnapshotLease> leases = [];
    private readonly LinkedList<long> lease_order = [];
    private long lease_revision;

    private DailyTaskSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            DailyTaskState state = daily_tasks.State;
            DailyTaskSnapshotLease lease;
            lock (leases_sync)
            {
                ThrowIfDisposed();
                lease = StoreLeaseUnsafe(state);
            }
            if (LeaseActive(lease))
                return lease;
            RemoveLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The daily task state changed while its snapshot was being captured.");
    }

    private DailyTaskSnapshotLease StoreLease(DailyTaskState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the daily task snapshot was stored.");
            }
            return StoreLeaseUnsafe(state);
        }
    }

    private DailyTaskSnapshotLease StoreLeaseUnsafe(DailyTaskState state)
    {
        DailyTaskSnapshotLease? existing = leases.Values.FirstOrDefault(
            lease => StatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;
        DailyTaskView[] tasks = state.Tasks
            .Select((task, ordinal) => TaskView(task, ordinal))
            .ToArray();
        long revision = checked(++lease_revision);
        var lease = new DailyTaskSnapshotLease(
            revision,
            state,
            Array.AsReadOnly(tasks));
        leases.Add(revision, lease);
        lease_order.AddLast(revision);
        while (leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The daily task lease order is invalid.");
            lease_order.RemoveFirst();
            leases.Remove(oldest.Value);
        }
        return lease;
    }

    private DailyTaskSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out DailyTaskSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The daily task snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(DailyTaskSnapshotLease lease) => StateSessionActive(lease.State);

    private bool StateSessionActive(DailyTaskState state)
    {
        DailyTaskState current = daily_tasks.State;
        return ReferenceEquals(current.Session, state.Session) &&
            current.SessionGeneration == state.SessionGeneration &&
            ReferenceEquals(connection.Session, state.Session);
    }

    private void RemoveLease(long revision)
    {
        lock (leases_sync)
        {
            leases.Remove(revision);
            LinkedListNode<long>? node = lease_order.Find(revision);
            if (node is not null)
                lease_order.Remove(node);
        }
    }

    private void ClearLeases()
    {
        lock (leases_sync)
        {
            leases.Clear();
            lease_order.Clear();
        }
    }

    private static bool StatesEquivalent(DailyTaskState left, DailyTaskState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private static DailyTaskView TaskView(DailyTask task, int ordinal)
    {
        DailyTaskRewardView[] rewards = task.Rewards
            .Select(reward => new DailyTaskRewardView(
                reward.ProductItemTypeId,
                reward.RewardTypeId,
                reward.ExtraParams,
                reward.Amount))
            .ToArray();
        return new DailyTaskView(
            ordinal,
            task.TaskId,
            task.TaskCode,
            task.QuestTypeCode,
            task.IsBonus,
            task.ImageVersion,
            task.CatalogName,
            task.RequiredRepeats,
            task.Repeats,
            (int)task.Status,
            task.SecondsLeftAtArrival,
            task.ReceivedAt,
            Array.AsReadOnly(rewards),
            task.IsClaimable);
    }

    private static DailyTaskSummary Summary(DailyTaskState state) => new(
        state.Loaded,
        state.Tasks.Count,
        state.Tasks.Count(task => task.IsClaimable),
        state.Tasks.Any(task => task.IsBonus));

    private sealed record DailyTaskSnapshotLease(
        long Revision,
        DailyTaskState State,
        IReadOnlyList<DailyTaskView> Tasks);
}
