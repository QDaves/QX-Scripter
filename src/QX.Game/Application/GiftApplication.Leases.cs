using Qx.Interception;

namespace Qx.Game.Application;

internal sealed partial class GiftApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, GiftSnapshotLease> snapshot_leases = [];
    private readonly LinkedList<long> snapshot_lease_order = [];
    private long snapshot_lease_revision;

    private GiftSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GiftState state = gifts.State;
            GiftSnapshotLease lease = StoreStateLease(state);
            if (LeaseActive(lease))
                return lease;
            RemoveLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The gift state changed while its snapshot was being captured.");
    }

    private GiftSnapshotLease StoreStateLease(GiftState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            GiftSnapshotLease? existing = snapshot_leases.Values.FirstOrDefault(
                lease => EquivalentPagedState(lease.State, state));
            if (existing is not null)
            {
                TouchLease(existing.Revision);
                return existing;
            }
            return StoreLeaseUnsafe(state);
        }
    }

    private GiftSnapshotLease StoreRefreshLease(
        GiftStateUpdate wrapping_update,
        GiftStateUpdate club_info_update)
    {
        ArgumentNullException.ThrowIfNull(wrapping_update);
        ArgumentNullException.ThrowIfNull(club_info_update);
        if (wrapping_update.Kind is not GiftStateChangeKind.Wrapping ||
            club_info_update.Kind is not GiftStateChangeKind.ClubInfo ||
            wrapping_update.State.Wrapping is null ||
            club_info_update.State.ClubInfo is null)
        {
            throw new InvalidOperationException(
                "Gift refresh responses do not contain the required committed snapshots.");
        }
        GiftState wrapping_state = wrapping_update.State;
        GiftState club_info_state = club_info_update.State;
        if (wrapping_state.Session is null ||
            !ReferenceEquals(wrapping_state.Session, club_info_state.Session) ||
            wrapping_state.SessionGeneration != club_info_state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "Gift refresh responses were committed for different hotel sessions.");
        }
        GiftState current = gifts.State;
        if (!ReferenceEquals(current.Session, wrapping_state.Session) ||
            current.SessionGeneration != wrapping_state.SessionGeneration ||
            current.Revision < wrapping_state.Revision ||
            current.Revision < club_info_state.Revision)
        {
            throw new InvalidOperationException(
                "The active gift state no longer contains the refreshed hotel session.");
        }
        GiftState combined = current with
        {
            WrappingRevision = wrapping_state.WrappingRevision,
            Wrapping = wrapping_state.Wrapping,
            ClubInfoRevision = club_info_state.ClubInfoRevision,
            ClubInfo = club_info_state.ClubInfo
        };
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(combined))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the refreshed gift snapshot was stored.");
            }
            return StoreLeaseUnsafe(combined);
        }
    }

    private GiftSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!snapshot_leases.TryGetValue(revision, out GiftSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The gift snapshot is unavailable for the active hotel session.");
            }
            TouchLease(revision);
            return lease;
        }
    }

    private GiftSnapshotLease StoreLeaseUnsafe(GiftState state)
    {
        long revision = checked(++snapshot_lease_revision);
        var lease = new GiftSnapshotLease(revision, state);
        snapshot_leases.Add(revision, lease);
        snapshot_lease_order.AddLast(revision);
        while (snapshot_leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = snapshot_lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The gift snapshot lease order is invalid.");
            snapshot_lease_order.RemoveFirst();
            snapshot_leases.Remove(oldest.Value);
        }
        return lease;
    }

    private void TouchLease(long revision)
    {
        LinkedListNode<long>? node = snapshot_lease_order.Find(revision);
        if (node is null || ReferenceEquals(node, snapshot_lease_order.Last))
            return;
        snapshot_lease_order.Remove(node);
        snapshot_lease_order.AddLast(node);
    }

    private bool LeaseActive(GiftSnapshotLease lease)
        => StateSessionActive(lease.State);

    private bool StateSessionActive(GiftState state)
    {
        GiftState current = gifts.State;
        return ReferenceEquals(current.Session, state.Session) &&
            current.SessionGeneration == state.SessionGeneration &&
            ReferenceEquals(connection.Session, state.Session);
    }

    private void RemoveLease(long revision)
    {
        lock (leases_sync)
        {
            snapshot_leases.Remove(revision);
            LinkedListNode<long>? node = snapshot_lease_order.Find(revision);
            if (node is not null)
                snapshot_lease_order.Remove(node);
        }
    }

    private void ClearLeases()
    {
        lock (leases_sync)
        {
            snapshot_leases.Clear();
            snapshot_lease_order.Clear();
        }
    }

    private static bool EquivalentPagedState(GiftState left, GiftState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.WrappingRevision == right.WrappingRevision &&
        ReferenceEquals(left.Wrapping, right.Wrapping) &&
        left.ClubInfoRevision == right.ClubInfoRevision &&
        ReferenceEquals(left.ClubInfo, right.ClubInfo) &&
        left.ClubSelectedRevision == right.ClubSelectedRevision &&
        ReferenceEquals(left.ClubSelected, right.ClubSelected) &&
        left.NewUserOfferRevision == right.NewUserOfferRevision &&
        ReferenceEquals(left.NewUserOffer, right.NewUserOffer);

    private sealed record GiftSnapshotLease(long Revision, GiftState State);
}
