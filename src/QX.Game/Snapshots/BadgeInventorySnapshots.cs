using Qx.Model.Messages.Incoming;

namespace Qx.Game.Snapshots;

/// <summary>
/// Represents the JSON projection of a badge the local user owns used by the MCP read tools and the
/// application-layer results; scripts that read live state use
/// <see cref="Qx.Model.Messages.Incoming.OwnedBadge"/>.
/// </summary>
/// <param name="Id">The badge identifier.</param>
/// <param name="Code">The badge code.</param>
/// <param name="OwnerCount">The number of users who own the badge, or 0 when <paramref name="HasRarityData"/> is <see langword="false"/>.</param>
/// <param name="RarityId">The rarity of the badge, or 0 when <paramref name="HasRarityData"/> is <see langword="false"/>.</param>
/// <param name="HasRarityData">Whether the hotel sent the owner count and rarity for the badge.</param>
public sealed record OwnedBadgeSnapshot(
    Id Id,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData);

/// <summary>
/// Represents the JSON projection of the local user's badge inventory and its load state for the
/// MCP read tools; scripts use <see cref="BadgeInventoryManager"/>.
/// </summary>
/// <param name="IsLoading">Whether a load is in flight right now.</param>
/// <param name="IsStale">
/// Whether the listed badges are left over from a previous load that has been invalidated. They are
/// still returned, but a fresh load is needed before acting on them.
/// </param>
/// <param name="Generation">A counter bumped every time a new load begins.</param>
/// <param name="ExpectedFragments">How many fragments the current load consists of, or -1 while that is not yet known.</param>
/// <param name="ReceivedFragments">How many fragments of the current load have arrived.</param>
/// <param name="Total">The number of badges the user owns.</param>
/// <param name="Returned">The number of badges in <paramref name="Badges"/>.</param>
/// <param name="MaxBadges">The cap applied to the projection.</param>
/// <param name="Truncated">Whether badges were dropped to honor the cap.</param>
/// <param name="Badges">
/// The badges, ordered by code case-insensitively, then by identifier. When truncated these are the
/// first ones in that order.
/// </param>
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
    /// <summary>Projects owned badges into a capped inventory snapshot.</summary>
    /// <param name="badges">The badges to project.</param>
    /// <param name="maxBadges">The maximum number of badges to return.</param>
    /// <param name="isLoading">Whether a load is in flight.</param>
    /// <param name="isStale">Whether the badges are left over from an invalidated load.</param>
    /// <param name="generation">The load counter to stamp onto the snapshot.</param>
    /// <param name="expectedFragments">The number of fragments in the current load, or -1 when not yet known.</param>
    /// <param name="receivedFragments">The number of fragments received so far.</param>
    /// <param name="sourceItemLimit">The safety valve against an unbounded source; a source with more items throws.</param>
    /// <returns>The inventory, ordered by code case-insensitively, then by identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="badges"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxBadges"/> or <paramref name="sourceItemLimit"/> is negative.
    /// </exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// Thrown when the sequence yields more than <paramref name="sourceItemLimit"/> items.
    /// </exception>
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

    /// <summary>Projects one owned badge.</summary>
    /// <param name="badge">The badge to project.</param>
    /// <returns>The badge snapshot.</returns>
    public static OwnedBadgeSnapshot From(OwnedBadge badge) =>
        new(
            badge.NativeBadgeId,
            badge.Code,
            badge.OwnerCount,
            badge.RarityId,
            badge.HasRarityData);
}
