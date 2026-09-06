using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class EarningApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, EarningSnapshotLease> leases = [];
    private readonly LinkedList<long> lease_order = [];
    private long lease_revision;

    private EarningSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            EarningState state = earnings.State;
            EarningSnapshotLease lease;
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
            "The earning state changed while its snapshot was being captured.");
    }

    private EarningSnapshotLease StoreLease(EarningState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the earning snapshot was stored.");
            }
            return StoreLeaseUnsafe(state);
        }
    }

    private EarningSnapshotLease StoreLeaseUnsafe(EarningState state)
    {
        EarningSnapshotLease? existing = leases.Values.FirstOrDefault(
            lease => StatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;
        EarningEntryView[] entries = state.Status.Entries
            .Select((entry, ordinal) => EntryView(entry, ordinal))
            .ToArray();
        long revision = checked(++lease_revision);
        var lease = new EarningSnapshotLease(
            revision,
            state,
            Array.AsReadOnly(entries));
        leases.Add(revision, lease);
        lease_order.AddLast(revision);
        while (leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The earning snapshot lease order is invalid.");
            lease_order.RemoveFirst();
            leases.Remove(oldest.Value);
        }
        return lease;
    }

    private EarningSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out EarningSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The earning snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(EarningSnapshotLease lease) => StateSessionActive(lease.State);

    private bool StateSessionActive(EarningState state)
    {
        EarningState current = earnings.State;
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

    private static bool StatesEquivalent(EarningState left, EarningState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private static EarningEntryView EntryView(EarningEntry entry, int ordinal) => new(
        ordinal,
        EarningsManager.NormalizeCategory(entry.Category),
        unchecked((sbyte)(int)entry.Kind),
        entry.Amount,
        entry.ProductCode,
        entry.IsProduct);

    private static EarningVaultSummary VaultSummary(EarningState state)
    {
        EarningStatus status = state.Status;
        var categories = new HashSet<int>();
        foreach (EarningEntry entry in status.Entries)
            categories.Add(EarningsManager.NormalizeCategory(entry.Category));
        return new EarningVaultSummary(
            state.Loaded,
            status.Entries.Count,
            categories.Count,
            status.Credits(),
            status.Duckets(),
            status.Products(),
            status.HasClaimable());
    }

    private sealed record EarningSnapshotLease(
        long Revision,
        EarningState State,
        IReadOnlyList<EarningEntryView> Entries);
}
