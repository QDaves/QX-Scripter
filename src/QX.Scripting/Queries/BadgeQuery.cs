using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <summary>
/// Represents a query over owned badges.
/// </summary>
/// <remarks>
/// Every filter returns a new query. Code matching ignores case, <see langword="null"/> entries
/// in code lists are skipped, and a <see langword="null"/> argument throws
/// <see cref="ArgumentNullException"/>. Rarity and owner count filters drop badges without
/// rarity data.
/// </remarks>
public sealed class BadgeQuery : QueryCollection<OwnedBadge>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BadgeQuery"/> class over the specified badges.
    /// </summary>
    /// <param name="badges">The owned badges to query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="badges"/> is <see langword="null"/>.</exception>
    public BadgeQuery(IEnumerable<OwnedBadge> badges) : base(badges)
    {
    }

    /// <summary>
    /// Filters the badges with a predicate.
    /// </summary>
    /// <param name="predicate">The condition a badge must meet to be kept.</param>
    /// <returns>A new query with the badges that match <paramref name="predicate"/>.</returns>
    public BadgeQuery Where(Func<OwnedBadge, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    /// <summary>
    /// Filters the badges to those with any of the specified 32-bit badge ids.
    /// </summary>
    /// <remarks>
    /// Badges whose native id does not fit in a 32-bit integer are dropped. Use
    /// <see cref="ByNativeId(IEnumerable{Id})"/> to match those.
    /// </remarks>
    /// <param name="badgeIds">The badge ids to keep.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery ById(params int[] badgeIds) =>
        ById((IEnumerable<int>)badgeIds);

    /// <summary>
    /// Filters the badges to those with any of the specified 32-bit badge ids.
    /// </summary>
    /// <remarks>
    /// Badges whose native id does not fit in a 32-bit integer are dropped. Use
    /// <see cref="ByNativeId(IEnumerable{Id})"/> to match those.
    /// </remarks>
    /// <param name="badgeIds">The badge ids to keep.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery ById(IEnumerable<int> badgeIds)
    {
        HashSet<int> values = QueryValues.Set(badgeIds);
        return Where(badge =>
            (long)badge.NativeBadgeId is >= int.MinValue and <= int.MaxValue &&
            values.Contains(badge.BadgeId));
    }

    /// <summary>
    /// Filters the badges to those with any of the specified native badge ids.
    /// </summary>
    /// <param name="badgeIds">The native badge ids to keep, compared with <see cref="OwnedBadge.NativeBadgeId"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery ByNativeId(params Id[] badgeIds) =>
        ByNativeId((IEnumerable<Id>)badgeIds);

    /// <summary>
    /// Filters the badges to those with any of the specified native badge ids.
    /// </summary>
    /// <param name="badgeIds">The native badge ids to keep, compared with <see cref="OwnedBadge.NativeBadgeId"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery ByNativeId(IEnumerable<Id> badgeIds)
    {
        HashSet<Id> values = QueryValues.Set(badgeIds);
        return Where(badge => values.Contains(badge.NativeBadgeId));
    }

    /// <summary>
    /// Filters the badges to those with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="codes">The badge codes to keep.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery Coded(params string[] codes) =>
        Coded((IEnumerable<string>)codes);

    /// <summary>
    /// Filters the badges to those with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="codes">The badge codes to keep.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery Coded(IEnumerable<string> codes)
    {
        HashSet<string> values = QueryValues.Strings(codes);
        return Where(badge => values.Contains(badge.Code));
    }

    /// <summary>
    /// Filters out the badges with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="codes">The badge codes to drop.</param>
    /// <returns>A new query without the matching badges.</returns>
    public BadgeQuery NotCoded(params string[] codes) =>
        NotCoded((IEnumerable<string>)codes);

    /// <summary>
    /// Filters out the badges with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="codes">The badge codes to drop.</param>
    /// <returns>A new query without the matching badges.</returns>
    public BadgeQuery NotCoded(IEnumerable<string> codes)
    {
        HashSet<string> values = QueryValues.Strings(codes);
        return Where(badge => !values.Contains(badge.Code));
    }

    /// <summary>
    /// Filters the badges to those whose code contains the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The text to search for.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery CodeContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(badge => badge.Code.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the badges to those whose code starts with the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The prefix to match.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery CodeStartsWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(badge => badge.Code.StartsWith(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the badges to those with any of the specified rarity ids.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="rarityIds">The rarity ids to keep, compared with <see cref="OwnedBadge.RarityId"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery OfRarity(params int[] rarityIds) =>
        OfRarity((IEnumerable<int>)rarityIds);

    /// <summary>
    /// Filters the badges to those with any of the specified rarity ids.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="rarityIds">The rarity ids to keep, compared with <see cref="OwnedBadge.RarityId"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery OfRarity(IEnumerable<int> rarityIds)
    {
        HashSet<int> values = QueryValues.Set(rarityIds);
        return Where(badge => badge.HasRarityData && values.Contains(badge.RarityId));
    }

    /// <summary>
    /// Filters the badges by whether they carry rarity data.
    /// </summary>
    /// <param name="value">Whether to keep badges with rarity data instead of badges without it.</param>
    /// <returns>A new query with the matching badges.</returns>
    public BadgeQuery WithRarityData(bool value = true) =>
        Where(badge => badge.HasRarityData == value);

    /// <summary>
    /// Filters the badges to those whose owner count is in the specified range.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="minimum">The lowest owner count to keep, inclusive.</param>
    /// <param name="maximum">The highest owner count to keep, inclusive.</param>
    /// <returns>A new query with the matching badges.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimum"/> is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minimum"/> is greater than <paramref name="maximum"/>.</exception>
    public BadgeQuery OwnerCountBetween(int minimum, int maximum)
    {
        if (minimum < 0)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if (minimum > maximum)
            throw new ArgumentException("Minimum owner count cannot exceed maximum owner count.", nameof(minimum));
        return Where(badge =>
            badge.HasRarityData &&
            badge.OwnerCount >= minimum &&
            badge.OwnerCount <= maximum);
    }

    /// <summary>
    /// Filters the badges to those owned by at most the specified number of users.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="maximum">The highest owner count to keep, inclusive.</param>
    /// <returns>A new query with the matching badges.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximum"/> is negative.</exception>
    public BadgeQuery AtMostOwners(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return Where(badge => badge.HasRarityData && badge.OwnerCount <= maximum);
    }

    /// <summary>
    /// Filters the badges to those owned by at least the specified number of users.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="minimum">The lowest owner count to keep, inclusive.</param>
    /// <returns>A new query with the matching badges.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimum"/> is negative.</exception>
    public BadgeQuery AtLeastOwners(int minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        return Where(badge => badge.HasRarityData && badge.OwnerCount >= minimum);
    }

    private BadgeQuery Next(IEnumerable<OwnedBadge> badges) => new(badges);
}

/// <summary>
/// Represents a query over the badges a user wears.
/// </summary>
/// <remarks>
/// Every filter returns a new query. Code matching ignores case, <see langword="null"/> entries
/// in code lists are skipped, and a <see langword="null"/> argument throws
/// <see cref="ArgumentNullException"/>.
/// </remarks>
public sealed class SelectedBadgeQuery : QueryCollection<SelectedBadge>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectedBadgeQuery"/> class over the specified badges.
    /// </summary>
    /// <param name="badges">The selected badges to query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="badges"/> is <see langword="null"/>.</exception>
    public SelectedBadgeQuery(IEnumerable<SelectedBadge> badges) : base(badges)
    {
    }

    /// <summary>
    /// Filters the badges with a predicate.
    /// </summary>
    /// <param name="predicate">The condition a badge must meet to be kept.</param>
    /// <returns>A new query with the badges that match <paramref name="predicate"/>.</returns>
    public SelectedBadgeQuery Where(Func<SelectedBadge, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    /// <summary>
    /// Filters the badges to those in any of the specified slots.
    /// </summary>
    /// <param name="slots">The slot numbers to keep, compared with <see cref="SelectedBadge.Slot"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery InSlot(params int[] slots) =>
        InSlot((IEnumerable<int>)slots);

    /// <summary>
    /// Filters the badges to those in any of the specified slots.
    /// </summary>
    /// <param name="slots">The slot numbers to keep, compared with <see cref="SelectedBadge.Slot"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery InSlot(IEnumerable<int> slots)
    {
        HashSet<int> values = QueryValues.Set(slots);
        return Where(badge => values.Contains(badge.Slot));
    }

    /// <summary>
    /// Filters the badges to those with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="codes">The badge codes to keep.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery Coded(params string[] codes) =>
        Coded((IEnumerable<string>)codes);

    /// <summary>
    /// Filters the badges to those with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="codes">The badge codes to keep.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery Coded(IEnumerable<string> codes)
    {
        HashSet<string> values = QueryValues.Strings(codes);
        return Where(badge => values.Contains(badge.Code));
    }

    /// <summary>
    /// Filters the badges to those whose code contains the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The text to search for.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery CodeContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(badge => badge.Code.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the badges to those with any of the specified rarity ids.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="rarityIds">The rarity ids to keep, compared with <see cref="SelectedBadge.RarityId"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery OfRarity(params int[] rarityIds) =>
        OfRarity((IEnumerable<int>)rarityIds);

    /// <summary>
    /// Filters the badges to those with any of the specified rarity ids.
    /// </summary>
    /// <remarks>
    /// Badges without rarity data are dropped.
    /// </remarks>
    /// <param name="rarityIds">The rarity ids to keep, compared with <see cref="SelectedBadge.RarityId"/>.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery OfRarity(IEnumerable<int> rarityIds)
    {
        HashSet<int> values = QueryValues.Set(rarityIds);
        return Where(badge => badge.HasRarityData && values.Contains(badge.RarityId));
    }

    /// <summary>
    /// Filters the badges by whether they carry rarity data.
    /// </summary>
    /// <param name="value">Whether to keep badges with rarity data instead of badges without it.</param>
    /// <returns>A new query with the matching badges.</returns>
    public SelectedBadgeQuery WithRarityData(bool value = true) =>
        Where(badge => badge.HasRarityData == value);

    private SelectedBadgeQuery Next(IEnumerable<SelectedBadge> badges) => new(badges);
}
