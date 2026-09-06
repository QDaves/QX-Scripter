using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class LeaderboardApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, LeaderboardSnapshotLease> leases = [];
    private readonly LinkedList<long> lease_order = [];
    private long lease_revision;

    private LeaderboardSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            LeaderboardState state = leaderboards.State;
            LeaderboardSnapshotLease lease;
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
            "The leaderboard state changed while its snapshot was being captured.");
    }

    private LeaderboardSnapshotLease StoreLease(LeaderboardState state)
    {
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the leaderboard snapshot was stored.");
            }
            return StoreLeaseUnsafe(state);
        }
    }

    private LeaderboardSnapshotLease StoreLeaseUnsafe(LeaderboardState state)
    {
        LeaderboardSnapshotLease? existing = leases.Values.FirstOrDefault(
            lease => StatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;
        var entries = new Dictionary<LeaderboardRoute, IReadOnlyList<LeaderboardEntryView>>();
        foreach ((LeaderboardRoute route, Leaderboard board) in state.Boards)
        {
            LeaderboardEntryView[] values = board.Entries
                .Select((entry, ordinal) => new LeaderboardEntryView(
                    ordinal,
                    entry.UserId,
                    entry.Score,
                    entry.Rank,
                    entry.Name,
                    entry.Figure,
                    entry.Gender))
                .ToArray();
            entries.Add(route, Array.AsReadOnly(values));
        }
        long revision = checked(++lease_revision);
        var lease = new LeaderboardSnapshotLease(
            revision,
            state,
            leaderboards.ViewSize,
            leaderboards.WindowSize,
            entries);
        leases.Add(revision, lease);
        lease_order.AddLast(revision);
        while (leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The leaderboard lease order is invalid.");
            lease_order.RemoveFirst();
            leases.Remove(oldest.Value);
        }
        return lease;
    }

    private LeaderboardSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out LeaderboardSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The leaderboard snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(LeaderboardSnapshotLease lease) => StateSessionActive(lease.State);

    private bool StateSessionActive(LeaderboardState state)
    {
        LeaderboardState current = leaderboards.State;
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

    private static bool StatesEquivalent(LeaderboardState left, LeaderboardState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private static LeaderboardSummary Summary(Leaderboard? board) => board is null
        ? new LeaderboardSummary(false, 0, 0, 0, false, false)
        : new LeaderboardSummary(
            true,
            board.Entries.Count,
            board.TotalListSize,
            board.GameTypeId,
            board.HasMoreAbove,
            board.HasMoreBelow);

    private static LeaderboardPeriodView? PeriodView(WeeklyLeaderboardPeriod? value) => value is null
        ? null
        : new LeaderboardPeriodView(
            value.Year,
            value.Week,
            value.MaxOffset,
            value.CurrentOffset,
            value.MinutesUntilReset,
            value.IsCurrentWeek);

    private sealed record LeaderboardSnapshotLease(
        long Revision,
        LeaderboardState State,
        int ViewSize,
        int WindowSize,
        IReadOnlyDictionary<LeaderboardRoute, IReadOnlyList<LeaderboardEntryView>> Entries);
}
