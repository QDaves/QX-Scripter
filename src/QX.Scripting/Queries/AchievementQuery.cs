using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public sealed class AchievementQuery : QueryCollection<Achievement>
{
    public AchievementQuery(IEnumerable<Achievement> achievements) : base(achievements)
    {
    }

    public AchievementQuery Where(Func<Achievement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public AchievementQuery ById(params int[] ids) =>
        ById((IEnumerable<int>)ids);

    public AchievementQuery ById(IEnumerable<int> ids)
    {
        HashSet<int> values = QueryValues.Set(ids);
        return Where(achievement => values.Contains(achievement.Id));
    }

    public AchievementQuery WithBadgeCode(params string[] badge_codes) =>
        WithBadgeCode((IEnumerable<string>)badge_codes);

    public AchievementQuery WithBadgeCode(IEnumerable<string> badge_codes)
    {
        HashSet<string> values = QueryValues.Strings(badge_codes);
        return Where(achievement => values.Contains(achievement.BadgeCode));
    }

    public AchievementQuery InCategory(params string[] categories) =>
        InCategory((IEnumerable<string>)categories);

    public AchievementQuery InCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(achievement => values.Contains(achievement.Category));
    }

    public AchievementQuery InSubcategory(params string[] subcategories) =>
        InSubcategory((IEnumerable<string>)subcategories);

    public AchievementQuery InSubcategory(IEnumerable<string> subcategories)
    {
        HashSet<string> values = QueryValues.Strings(subcategories);
        return Where(achievement => values.Contains(achievement.Subcategory));
    }

    public AchievementQuery Complete(bool value = true) =>
        Where(achievement => achievement.IsComplete == value);

    public AchievementQuery AtLevel(params int[] levels) =>
        AtLevel((IEnumerable<int>)levels);

    public AchievementQuery AtLevel(IEnumerable<int> levels)
    {
        HashSet<int> values = QueryValues.Set(levels);
        return Where(achievement => values.Contains(achievement.Level));
    }

    public AchievementQuery LevelBetween(int minimum, int maximum)
    {
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum level cannot be below minimum level.");
        return Where(achievement => achievement.Level >= minimum && achievement.Level <= maximum);
    }

    public AchievementQuery ProgressAtLeast(int minimum) =>
        Where(achievement => achievement.CurrentProgress >= minimum);

    public AchievementQuery WithRewardPointType(params int[] point_types) =>
        WithRewardPointType((IEnumerable<int>)point_types);

    public AchievementQuery WithRewardPointType(IEnumerable<int> point_types)
    {
        HashSet<int> values = QueryValues.Set(point_types);
        return Where(achievement => values.Contains(achievement.LevelRewardPointType));
    }

    public AchievementQuery OrderByCategory() =>
        Next(Items
            .OrderBy(achievement => achievement.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Id));

    public AchievementQuery OrderByProgress(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(achievement => achievement.CurrentProgress)
            : Items.OrderBy(achievement => achievement.CurrentProgress));

    private static AchievementQuery Next(IEnumerable<Achievement> achievements) => new(achievements);
}
