using Qx.Model.Messages.Incoming;

namespace Qx.Game.Snapshots;

public sealed record OwnedBadgeSnapshot(
    Id Id,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData);

public sealed record BadgeInventorySnapshot(
    bool IsLoading,
    bool IsStale,
    long Generation,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total,
    int Returned,
    int MaxBadges,
    bool Truncated,
    IReadOnlyList<OwnedBadgeSnapshot> Badges);

public static partial class SnapshotFactory
{
    public static BadgeInventorySnapshot BadgeInventory(
        IEnumerable<OwnedBadge> badges,
        int maxBadges = 500,
        bool isLoading = false,
        bool isStale = false,
        long generation = 0,
        int expectedFragments = -1,
        int receivedFragments = 0,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        CappedSource<OwnedBadge> inventory = SelectCapped(
            badges,
            maxBadges,
            sourceItemLimit,
            nameof(badges),
            Comparer<OwnedBadge>.Create((left, right) =>
            {
                int comparison = StringComparer.OrdinalIgnoreCase.Compare(left.Code, right.Code);
                return comparison != 0
                    ? comparison
                    : ((long)left.NativeBadgeId).CompareTo((long)right.NativeBadgeId);
            }));
        OwnedBadgeSnapshot[] projected = inventory.Items
            .Select(From)
            .ToArray();

        return new BadgeInventorySnapshot(
            isLoading,
            isStale,
            generation,
            expectedFragments,
            receivedFragments,
            inventory.Total,
            projected.Length,
            maxBadges,
            projected.Length < inventory.Total,
            projected);
    }

    public static OwnedBadgeSnapshot From(OwnedBadge badge) =>
        new(
            badge.NativeBadgeId,
            badge.Code,
            badge.OwnerCount,
            badge.RarityId,
            badge.HasRarityData);
}
