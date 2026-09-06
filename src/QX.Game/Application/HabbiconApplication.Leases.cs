using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class HabbiconApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, HabbiconSnapshotLease> leases = [];
    private readonly LinkedList<long> lease_order = [];
    private long lease_revision;

    private HabbiconSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            HabbiconStateData state = habbicons.State;
            HabbiconSnapshotLease lease;
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
            "The habbicon state changed while its snapshot was being captured.");
    }

    private HabbiconSnapshotLease StoreLease(HabbiconStateData state)
    {
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the habbicon snapshot was stored.");
            }
            return StoreLeaseUnsafe(state);
        }
    }

    private HabbiconSnapshotLease StoreLeaseUnsafe(HabbiconStateData state)
    {
        HabbiconSnapshotLease? existing = leases.Values.FirstOrDefault(
            lease => StatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;
        HabbiconEntryView[] entries = state.Collections
            .SelectMany(collection => collection.Habbicons)
            .Select((icon, ordinal) => EntryView(ApplyUserState(icon, state), ordinal))
            .ToArray();
        HabbiconCollectionView[] collections = state.Collections
            .Select((collection, ordinal) => CollectionView(collection, state, ordinal))
            .ToArray();
        long revision = checked(++lease_revision);
        var lease = new HabbiconSnapshotLease(
            revision,
            state,
            Array.AsReadOnly(collections),
            Array.AsReadOnly(entries));
        leases.Add(revision, lease);
        lease_order.AddLast(revision);
        while (leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The habbicon lease order is invalid.");
            lease_order.RemoveFirst();
            leases.Remove(oldest.Value);
        }
        return lease;
    }

    private HabbiconSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out HabbiconSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The habbicon snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(HabbiconSnapshotLease lease) => StateSessionActive(lease.State);

    private bool StateSessionActive(HabbiconStateData state)
    {
        HabbiconStateData current = habbicons.State;
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

    private static bool StatesEquivalent(HabbiconStateData left, HabbiconStateData right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private static Habbicon ApplyUserState(Habbicon value, HabbiconStateData state) =>
        state.UserStates.TryGetValue(value.HabbiconId, out HabbiconState current)
            ? value with { State = current }
            : value with { };

    private static HabbiconEntryView EntryView(Habbicon value, int ordinal) =>
        new(
            ordinal,
            value.HabbiconId,
            value.Name,
            value.CollectionId,
            (int)value.State,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType,
            value.IsOwned,
            value.IsClaimable,
            value.IsPurchasable);

    private static HabbiconCollectionView CollectionView(
        HabbiconCollection value,
        HabbiconStateData state,
        int ordinal) =>
        new(
            ordinal,
            value.CollectionId,
            value.Name,
            value.Completed,
            value.RewardHabbiconId,
            (int)value.RewardState,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType,
            value.RewardIsClaimable,
            value.Habbicons
                .Select((icon, icon_ordinal) => EntryView(
                    ApplyUserState(icon, state),
                    icon_ordinal))
                .ToArray());

    private static HabbiconVaultSummary Summary(HabbiconStateData state)
    {
        Habbicon[] icons = state.Collections
            .SelectMany(collection => collection.Habbicons)
            .Select(icon => ApplyUserState(icon, state))
            .ToArray();
        return new HabbiconVaultSummary(
            state.ShopLoaded,
            state.UserLoaded,
            state.Enabled,
            state.Collections.Count,
            icons.Length,
            icons.Count(icon => icon.IsOwned),
            icons.Count(icon => icon.State is HabbiconState.Favorite),
            icons.Count(icon => icon.IsClaimable),
            state.RecentHabbiconIds.Count);
    }

    private sealed record HabbiconSnapshotLease(
        long Revision,
        HabbiconStateData State,
        IReadOnlyList<HabbiconCollectionView> Collections,
        IReadOnlyList<HabbiconEntryView> Entries);
}
