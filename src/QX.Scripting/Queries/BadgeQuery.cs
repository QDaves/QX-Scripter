using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public sealed class BadgeQuery : QueryCollection<OwnedBadge>
{
    public BadgeQuery(IEnumerable<OwnedBadge> badges) : base(badges)
    {
    }

    public BadgeQuery Where(Func<OwnedBadge, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public BadgeQuery ById(params int[] badge_ids) =>
        ById((IEnumerable<int>)badge_ids);

    public BadgeQuery ById(IEnumerable<int> badge_ids)
    {
        HashSet<int> values = QueryValues.Set(badge_ids);
        return Where(badge =>
            (long)badge.NativeBadgeId is >= int.MinValue and <= int.MaxValue &&
            values.Contains(badge.BadgeId));
    }

    public BadgeQuery ByNativeId(params Id[] badge_ids) =>
        ByNativeId((IEnumerable<Id>)badge_ids);

    public BadgeQuery ByNativeId(IEnumerable<Id> badge_ids)
    {
        HashSet<Id> values = QueryValues.Set(badge_ids);
        return Where(badge => values.Contains(badge.NativeBadgeId));
    }

    public BadgeQuery Coded(params string[] codes) =>
        Coded((IEnumerable<string>)codes);

    public BadgeQuery Coded(IEnumerable<string> codes)
    {
        HashSet<string> values = QueryValues.Strings(codes);
        return Where(badge => values.Contains(badge.Code));
    }

    public BadgeQuery NotCoded(params string[] codes) =>
        NotCoded((IEnumerable<string>)codes);

    public BadgeQuery NotCoded(IEnumerable<string> codes)
    {
        HashSet<string> values = QueryValues.Strings(codes);
        return Where(badge => !values.Contains(badge.Code));
    }

    public BadgeQuery CodeContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(badge => badge.Code.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public BadgeQuery CodeStartsWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(badge => badge.Code.StartsWith(value, StringComparison.OrdinalIgnoreCase));
    }

    public BadgeQuery OfRarity(params int[] rarity_ids) =>
        OfRarity((IEnumerable<int>)rarity_ids);

    public BadgeQuery OfRarity(IEnumerable<int> rarity_ids)
    {
        HashSet<int> values = QueryValues.Set(rarity_ids);
        return Where(badge => badge.HasRarityData && values.Contains(badge.RarityId));
    }

    public BadgeQuery WithRarityData(bool value = true) =>
        Where(badge => badge.HasRarityData == value);

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

    public BadgeQuery AtMostOwners(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return Where(badge => badge.HasRarityData && badge.OwnerCount <= maximum);
    }

    public BadgeQuery AtLeastOwners(int minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        return Where(badge => badge.HasRarityData && badge.OwnerCount >= minimum);
    }

    private BadgeQuery Next(IEnumerable<OwnedBadge> badges) => new(badges);
}

public sealed class SelectedBadgeQuery : QueryCollection<SelectedBadge>
{
    public SelectedBadgeQuery(IEnumerable<SelectedBadge> badges) : base(badges)
    {
    }

    public SelectedBadgeQuery Where(Func<SelectedBadge, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public SelectedBadgeQuery InSlot(params int[] slots) =>
        InSlot((IEnumerable<int>)slots);

    public SelectedBadgeQuery InSlot(IEnumerable<int> slots)
    {
        HashSet<int> values = QueryValues.Set(slots);
        return Where(badge => values.Contains(badge.Slot));
    }

    public SelectedBadgeQuery Coded(params string[] codes) =>
        Coded((IEnumerable<string>)codes);

    public SelectedBadgeQuery Coded(IEnumerable<string> codes)
    {
        HashSet<string> values = QueryValues.Strings(codes);
        return Where(badge => values.Contains(badge.Code));
    }

    public SelectedBadgeQuery CodeContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(badge => badge.Code.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public SelectedBadgeQuery OfRarity(params int[] rarity_ids) =>
        OfRarity((IEnumerable<int>)rarity_ids);

    public SelectedBadgeQuery OfRarity(IEnumerable<int> rarity_ids)
    {
        HashSet<int> values = QueryValues.Set(rarity_ids);
        return Where(badge => badge.HasRarityData && values.Contains(badge.RarityId));
    }

    public SelectedBadgeQuery WithRarityData(bool value = true) =>
        Where(badge => badge.HasRarityData == value);

    private SelectedBadgeQuery Next(IEnumerable<SelectedBadge> badges) => new(badges);
}
