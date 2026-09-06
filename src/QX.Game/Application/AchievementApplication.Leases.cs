using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class AchievementApplication
{
    private const int achievement_snapshot_lease_limit = 4;
    private const int badge_snapshot_lease_limit = 4;
    private readonly object achievement_leases_sync = new();
    private readonly object badge_leases_sync = new();
    private readonly Dictionary<long, AchievementSnapshotLease> achievement_leases = [];
    private readonly Dictionary<long, BadgeSnapshotLease> badge_leases = [];
    private readonly LinkedList<long> achievement_lease_order = [];
    private readonly LinkedList<long> badge_lease_order = [];
    private long achievement_lease_revision;
    private long badge_lease_revision;

    private AchievementSnapshotLease StoreCurrentAchievementLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            AchievementState state = achievements.State;
            AchievementSnapshotLease lease;
            lock (achievement_leases_sync)
            {
                ThrowIfDisposed();
                lease = StoreAchievementLeaseUnsafe(state);
            }
            if (AchievementLeaseActive(lease))
                return lease;
            RemoveAchievementLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The achievement state changed while its snapshot was being captured.");
    }

    private AchievementSnapshotLease StoreAchievementLease(AchievementState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (achievement_leases_sync)
        {
            ThrowIfDisposed();
            if (!AchievementStateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the achievement snapshot was stored.");
            }
            return StoreAchievementLeaseUnsafe(state);
        }
    }

    private AchievementSnapshotLease StoreAchievementLeaseUnsafe(AchievementState state)
    {
        AchievementSnapshotLease? existing = achievement_leases.Values.FirstOrDefault(
            lease => AchievementStatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;

        var new_codes = new HashSet<string>(state.NewCodes, StringComparer.Ordinal);
        AchievementApplicationItem[] items = state.Achievements
            .Select(value => AchievementItem(value, new_codes.Contains(value.Code)))
            .ToArray();
        AchievementPointLimitItem[] limits = state.PointLimits.Limits
            .OrderBy(value => value.AchievementCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Level)
            .ThenBy(value => value.BadgeCode, StringComparer.OrdinalIgnoreCase)
            .Select(value => new AchievementPointLimitItem(
                value.AchievementCode,
                value.Level,
                value.Limit,
                value.BadgeCode))
            .ToArray();
        long revision = checked(++achievement_lease_revision);
        var lease = new AchievementSnapshotLease(
            revision,
            state,
            Array.AsReadOnly(items),
            Array.AsReadOnly(limits));
        achievement_leases.Add(revision, lease);
        achievement_lease_order.AddLast(revision);
        while (achievement_leases.Count > achievement_snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = achievement_lease_order.First;
            if (oldest is null)
            {
                throw new InvalidOperationException(
                    "The achievement snapshot lease order is invalid.");
            }
            achievement_lease_order.RemoveFirst();
            achievement_leases.Remove(oldest.Value);
        }
        return lease;
    }

    private AchievementSnapshotLease ReadAchievementLease(long revision)
    {
        lock (achievement_leases_sync)
        {
            if (!achievement_leases.TryGetValue(
                    revision,
                    out AchievementSnapshotLease? lease) ||
                !AchievementLeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The achievement snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool AchievementLeaseActive(AchievementSnapshotLease lease) =>
        AchievementStateSessionActive(lease.State);

    private bool AchievementStateSessionActive(AchievementState state)
    {
        AchievementState current = achievements.State;
        return ReferenceEquals(current.Session, state.Session) &&
            current.SessionGeneration == state.SessionGeneration &&
            ReferenceEquals(connection.Session, state.Session);
    }

    private void RemoveAchievementLease(long revision)
    {
        lock (achievement_leases_sync)
        {
            achievement_leases.Remove(revision);
            LinkedListNode<long>? node = achievement_lease_order.Find(revision);
            if (node is not null)
                achievement_lease_order.Remove(node);
        }
    }

    private BadgeSnapshotLease StoreCurrentBadgeLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            BadgeInventoryState state = badges.State;
            BadgeSnapshotLease lease;
            lock (badge_leases_sync)
            {
                ThrowIfDisposed();
                lease = StoreBadgeLeaseUnsafe(state);
            }
            if (BadgeLeaseActive(lease))
                return lease;
            RemoveBadgeLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The badge state changed while its snapshot was being captured.");
    }

    private BadgeSnapshotLease StoreBadgeLease(BadgeInventoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (badge_leases_sync)
        {
            ThrowIfDisposed();
            if (!BadgeStateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the badge snapshot was stored.");
            }
            return StoreBadgeLeaseUnsafe(state);
        }
    }

    private BadgeSnapshotLease StoreBadgeLeaseUnsafe(BadgeInventoryState state)
    {
        BadgeSnapshotLease? existing = badge_leases.Values.FirstOrDefault(
            lease => BadgeStatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;

        OwnedBadgeSnapshot[] owned = state.OwnedBadges
            .Select(SnapshotFactory.From)
            .ToArray();
        BadgeSelectedLeaseSet[] selected = state.SelectedBadgeSets
            .OrderBy(value => (long)value.Value.UserId)
            .Select(value => new BadgeSelectedLeaseSet(
                value.Value.UserId,
                value.Revision,
                Array.AsReadOnly(value.Value.Badges
                    .Select(badge => new SelectedBadgeSnapshot(
                        badge.Slot,
                        badge.Code,
                        badge.OwnerCount,
                        badge.RarityId,
                        badge.HasRarityData))
                    .ToArray())))
            .ToArray();
        long revision = checked(++badge_lease_revision);
        var lease = new BadgeSnapshotLease(
            revision,
            state,
            Array.AsReadOnly(owned),
            Array.AsReadOnly(selected));
        badge_leases.Add(revision, lease);
        badge_lease_order.AddLast(revision);
        while (badge_leases.Count > badge_snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = badge_lease_order.First;
            if (oldest is null)
            {
                throw new InvalidOperationException(
                    "The badge snapshot lease order is invalid.");
            }
            badge_lease_order.RemoveFirst();
            badge_leases.Remove(oldest.Value);
        }
        return lease;
    }

    private BadgeSnapshotLease ReadBadgeLease(long revision)
    {
        lock (badge_leases_sync)
        {
            if (!badge_leases.TryGetValue(revision, out BadgeSnapshotLease? lease) ||
                !BadgeLeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The badge snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool BadgeLeaseActive(BadgeSnapshotLease lease) =>
        BadgeStateSessionActive(lease.State);

    private bool BadgeStateSessionActive(BadgeInventoryState state)
    {
        BadgeInventoryState current = badges.State;
        return ReferenceEquals(current.Session, state.Session) &&
            current.SessionGeneration == state.SessionGeneration &&
            ReferenceEquals(connection.Session, state.Session);
    }

    private void RemoveBadgeLease(long revision)
    {
        lock (badge_leases_sync)
        {
            badge_leases.Remove(revision);
            LinkedListNode<long>? node = badge_lease_order.Find(revision);
            if (node is not null)
                badge_lease_order.Remove(node);
        }
    }

    private void ClearLeases()
    {
        ClearAchievementLeases();
        ClearBadgeLeases();
    }

    private void ClearAchievementLeases()
    {
        lock (achievement_leases_sync)
        {
            achievement_leases.Clear();
            achievement_lease_order.Clear();
        }
    }

    private void ClearBadgeLeases()
    {
        lock (badge_leases_sync)
        {
            badge_leases.Clear();
            badge_lease_order.Clear();
        }
    }

    private static bool AchievementStatesEquivalent(
        AchievementState left,
        AchievementState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private static bool BadgeStatesEquivalent(
        BadgeInventoryState left,
        BadgeInventoryState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private sealed record AchievementSnapshotLease(
        long Revision,
        AchievementState State,
        IReadOnlyList<AchievementApplicationItem> Achievements,
        IReadOnlyList<AchievementPointLimitItem> PointLimits);

    private sealed record BadgeSelectedLeaseSet(
        Id UserId,
        long Revision,
        IReadOnlyList<SelectedBadgeSnapshot> Badges);

    private sealed record BadgeSnapshotLease(
        long Revision,
        BadgeInventoryState State,
        IReadOnlyList<OwnedBadgeSnapshot> OwnedBadges,
        IReadOnlyList<BadgeSelectedLeaseSet> SelectedSets);
}
