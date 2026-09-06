namespace Qx.Game.Application;

internal sealed partial class CraftingApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, CraftingSnapshotLease> snapshot_leases = [];
    private readonly LinkedList<long> snapshot_lease_order = [];
    private long snapshot_lease_revision;

    private CraftingSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            CraftingState state = crafting.State;
            CraftingSnapshotLease lease;
            lock (leases_sync)
            {
                ThrowIfDisposed();
                lease = StoreStateLeaseUnsafe(state);
            }
            if (LeaseActive(lease))
                return lease;
            RemoveLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The crafting state changed while its snapshot was being captured.");
    }

    private CraftingSnapshotLease StoreStateLease(CraftingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the crafting snapshot was stored.");
            }
            return StoreStateLeaseUnsafe(state);
        }
    }

    private CraftingSnapshotLease StoreStateLeaseUnsafe(CraftingState state)
    {
        CraftingSnapshotLease? existing = snapshot_leases.Values.FirstOrDefault(
            lease => EquivalentState(lease.State, state));
        if (existing is not null)
            return existing;
        long revision = checked(++snapshot_lease_revision);
        var lease = new CraftingSnapshotLease(revision, state);
        snapshot_leases.Add(revision, lease);
        snapshot_lease_order.AddLast(revision);
        while (snapshot_leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = snapshot_lease_order.First;
            if (oldest is null)
            {
                throw new InvalidOperationException(
                    "The crafting snapshot lease order is invalid.");
            }
            snapshot_lease_order.RemoveFirst();
            snapshot_leases.Remove(oldest.Value);
        }
        return lease;
    }

    private CraftingSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!snapshot_leases.TryGetValue(
                    revision,
                    out CraftingSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The crafting snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(CraftingSnapshotLease lease) =>
        StateSessionActive(lease.State);

    private bool StateSessionActive(CraftingState state)
    {
        CraftingState current = crafting.State;
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

    private static bool EquivalentState(CraftingState left, CraftingState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.ProductsRevision == right.ProductsRevision &&
        ReferenceEquals(left.Products, right.Products) &&
        left.RecipeRevision == right.RecipeRevision &&
        ReferenceEquals(left.Recipe, right.Recipe) &&
        left.ResultRevision == right.ResultRevision &&
        ReferenceEquals(left.LastResult, right.LastResult) &&
        left.AvailabilityRevision == right.AvailabilityRevision &&
        ReferenceEquals(left.AvailableRecipes, right.AvailableRecipes);

    private sealed record CraftingSnapshotLease(
        long Revision,
        CraftingState State);
}
