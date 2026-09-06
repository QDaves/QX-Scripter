using Qx.Game.Application;

namespace Qx.Game.Snapshots;

public sealed partial class GameQueryService
{
    public QueryEnvelope<BadgeInventorySnapshot> BadgeInventory(int maxBadges = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBadges);

        if (maxBadges == 0)
        {
            BadgeStateView state = application.Invoke<BadgeStateRequest, BadgeStateView>(
                ApplicationMemberIds.BadgesState,
                new BadgeStateRequest());
            BadgeInventorySummary empty_inventory = state.Inventory;
            var empty = new BadgeInventorySnapshot(
                empty_inventory.Loading,
                empty_inventory.Stale,
                empty_inventory.LoadGeneration,
                empty_inventory.ExpectedFragments,
                empty_inventory.ReceivedFragments,
                empty_inventory.OwnedCount,
                0,
                0,
                empty_inventory.OwnedCount > 0,
                []);
            return Result(
                "badge_inventory",
                new QueryRead<BadgeInventorySnapshot>(
                    empty,
                    state.Connected && empty_inventory.Loaded,
                    empty_inventory.Loaded,
                    empty_inventory.Stale || !state.Connected && empty_inventory.OwnedCount > 0,
                    empty.Truncated,
                    empty_inventory.Loaded ? [] : ["badgeInventory"]));
        }

        const int page_limit = 500;
        OwnedBadgePage first = application.Invoke<OwnedBadgePageRequest, OwnedBadgePage>(
            ApplicationMemberIds.BadgesOwnedList,
            new OwnedBadgePageRequest(Limit: page_limit));
        ValidateBadgePage(first, 0, page_limit, null, null);
        var badges = new List<OwnedBadgeSnapshot>(first.Total);
        badges.AddRange(first.Badges);
        OwnedBadgePage current = first;
        while (current.NextOffset is int offset)
        {
            current = application.Invoke<OwnedBadgePageRequest, OwnedBadgePage>(
                ApplicationMemberIds.BadgesOwnedList,
                new OwnedBadgePageRequest(offset, page_limit, first.SnapshotRevision));
            ValidateBadgePage(current, offset, page_limit, first.SnapshotRevision, first);
            badges.AddRange(current.Badges);
        }
        if (badges.Count != first.Total)
            throw new InvalidOperationException("The badge application returned an incomplete snapshot.");

        BadgeInventorySummary inventory = first.Inventory;
        OwnedBadgeSnapshot[] selected = badges
            .OrderBy(badge => badge.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(badge => (long)badge.Id)
            .Take(maxBadges)
            .ToArray();
        bool truncated = selected.Length < first.Total;
        var snapshot = new BadgeInventorySnapshot(
            inventory.Loading,
            inventory.Stale,
            inventory.LoadGeneration,
            inventory.ExpectedFragments,
            inventory.ReceivedFragments,
            first.Total,
            selected.Length,
            maxBadges,
            truncated,
            Array.AsReadOnly(selected));
        var read = new QueryRead<BadgeInventorySnapshot>(
            snapshot,
            first.Connected && inventory.Loaded,
            inventory.Loaded,
            inventory.Stale || !first.Connected && first.Total > 0,
            truncated,
            inventory.Loaded ? [] : ["badgeInventory"]);
        return Result("badge_inventory", read);
    }

    private static void ValidateBadgePage(
        OwnedBadgePage page,
        int offset,
        int limit,
        long? snapshot_revision,
        OwnedBadgePage? first)
    {
        int consumed = checked(offset + page.Badges.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Inventory.OwnedCount != page.Total ||
            page.Badges.Count > limit ||
            consumed > page.Total ||
            consumed < page.Total && page.Badges.Count == 0 ||
            page.NextOffset != expected_next ||
            snapshot_revision is long revision && page.SnapshotRevision != revision ||
            first is not null &&
            (page.Connected != first.Connected ||
             page.Client != first.Client ||
             page.SessionGeneration != first.SessionGeneration ||
             page.StateRevision != first.StateRevision ||
             page.InventoryRevision != first.InventoryRevision ||
             page.BaselineRevision != first.BaselineRevision ||
             page.Total != first.Total ||
             page.Inventory != first.Inventory))
        {
            throw new InvalidOperationException("The badge application returned an invalid snapshot page.");
        }
    }
}
