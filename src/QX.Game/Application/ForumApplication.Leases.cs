using Qx.Interception;

namespace Qx.Game.Application;

internal sealed partial class ForumApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, ForumSnapshotLease> leases = [];
    private readonly LinkedList<long> lease_order = [];
    private long lease_revision;

    private ForumSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Session? session = connection.Session;
            Session? forum_session = forums.Session;
            long generation = forums.SessionGeneration;
            ForumSnapshot snapshot = forums.Snapshot;
            if (!ReferenceEquals(session, forum_session))
                continue;
            ForumSnapshotLease lease;
            lock (leases_sync)
            {
                ThrowIfDisposed();
                RemoveInactiveLeasesUnsafe();
                lease = StoreLeaseUnsafe(session, generation, snapshot);
            }
            if (LeaseActive(lease))
                return lease;
            RemoveLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The forum state changed while its snapshot was being captured.");
    }

    private ForumSnapshotLease StoreLeaseUnsafe(
        Session? session,
        long generation,
        ForumSnapshot snapshot)
    {
        ForumSnapshotLease? existing = leases.Values.FirstOrDefault(
            lease => ReferenceEquals(lease.Session, session) &&
                lease.SessionGeneration == generation &&
                ReferenceEquals(lease.Snapshot, snapshot));
        if (existing is not null)
            return existing;
        long revision = checked(++lease_revision);
        var lease = new ForumSnapshotLease(revision, session, generation, snapshot);
        leases.Add(revision, lease);
        lease_order.AddLast(revision);
        while (leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The forum snapshot lease order is invalid.");
            lease_order.RemoveFirst();
            leases.Remove(oldest.Value);
        }
        return lease;
    }

    private ForumSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out ForumSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The forum snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(ForumSnapshotLease lease) =>
        ReferenceEquals(connection.Session, lease.Session) &&
        ReferenceEquals(forums.Session, lease.Session) &&
        forums.SessionGeneration == lease.SessionGeneration;

    private void RequireLeaseActive(ForumSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the forum snapshot was being read.");
        }
    }

    private void RemoveInactiveLeasesUnsafe()
    {
        LinkedListNode<long>? node = lease_order.First;
        while (node is not null)
        {
            LinkedListNode<long>? next = node.Next;
            if (!leases.TryGetValue(node.Value, out ForumSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                leases.Remove(node.Value);
                lease_order.Remove(node);
            }
            node = next;
        }
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

    private sealed record ForumSnapshotLease(
        long Revision,
        Session? Session,
        long SessionGeneration,
        ForumSnapshot Snapshot);
}
