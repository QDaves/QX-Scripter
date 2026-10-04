using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <summary>
/// Represents a query over achievements.
/// </summary>
/// <remarks>
/// Every filter and sort returns a new query. Text matching ignores case,
/// <see langword="null"/> entries in text lists are skipped, and a <see langword="null"/>
/// argument throws <see cref="ArgumentNullException"/>.
/// </remarks>
public sealed class AchievementQuery : QueryCollection<Achievement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementQuery"/> class over the specified achievements.
    /// </summary>
    /// <param name="achievements">The achievements to query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="achievements"/> is <see langword="null"/>.</exception>
    public AchievementQuery(IEnumerable<Achievement> achievements) : base(achievements)
    {
    }

    /// <summary>
    /// Filters the achievements with a predicate.
    /// </summary>
    /// <param name="predicate">The condition an achievement must meet to be kept.</param>
    /// <returns>A new query with the achievements that match <paramref name="predicate"/>.</returns>
    public AchievementQuery Where(Func<Achievement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    /// <summary>
    /// Filters the achievements to those with any of the specified ids.
    /// </summary>
    /// <param name="ids">The achievement ids to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery ById(params int[] ids) =>
        ById((IEnumerable<int>)ids);

    /// <summary>
    /// Filters the achievements to those with any of the specified ids.
    /// </summary>
    /// <param name="ids">The achievement ids to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery ById(IEnumerable<int> ids)
    {
        HashSet<int> values = QueryValues.Set(ids);
        return Where(achievement => values.Contains(achievement.Id));
    }

    /// <summary>
    /// Filters the achievements to those with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="badgeCodes">The badge codes to keep, compared with <see cref="Achievement.BadgeCode"/>.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery WithBadgeCode(params string[] badgeCodes) =>
        WithBadgeCode((IEnumerable<string>)badgeCodes);

    /// <summary>
    /// Filters the achievements to those with any of the specified badge codes, ignoring case.
    /// </summary>
    /// <param name="badgeCodes">The badge codes to keep, compared with <see cref="Achievement.BadgeCode"/>.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery WithBadgeCode(IEnumerable<string> badgeCodes)
    {
        HashSet<string> values = QueryValues.Strings(badgeCodes);
        return Where(achievement => values.Contains(achievement.BadgeCode));
    }

    /// <summary>
    /// Filters the achievements to those in any of the specified categories, ignoring case.
    /// </summary>
    /// <param name="categories">The categories to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery InCategory(params string[] categories) =>
        InCategory((IEnumerable<string>)categories);

    /// <summary>
    /// Filters the achievements to those in any of the specified categories, ignoring case.
    /// </summary>
    /// <param name="categories">The categories to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery InCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(achievement => values.Contains(achievement.Category));
    }

    /// <summary>
    /// Filters the achievements to those in any of the specified subcategories, ignoring case.
    /// </summary>
    /// <param name="subcategories">The subcategories to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery InSubcategory(params string[] subcategories) =>
        InSubcategory((IEnumerable<string>)subcategories);

    /// <summary>
    /// Filters the achievements to those in any of the specified subcategories, ignoring case.
    /// </summary>
    /// <param name="subcategories">The subcategories to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery InSubcategory(IEnumerable<string> subcategories)
    {
        HashSet<string> values = QueryValues.Strings(subcategories);
        return Where(achievement => values.Contains(achievement.Subcategory));
    }

    /// <summary>
    /// Filters the achievements by whether they are complete.
    /// </summary>
    /// <param name="value">Whether to keep complete achievements instead of incomplete ones.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery Complete(bool value = true) =>
        Where(achievement => achievement.IsComplete == value);

    /// <summary>
    /// Filters the achievements to those at any of the specified levels.
    /// </summary>
    /// <param name="levels">The levels to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery AtLevel(params int[] levels) =>
        AtLevel((IEnumerable<int>)levels);

    /// <summary>
    /// Filters the achievements to those at any of the specified levels.
    /// </summary>
    /// <param name="levels">The levels to keep.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery AtLevel(IEnumerable<int> levels)
    {
        HashSet<int> values = QueryValues.Set(levels);
        return Where(achievement => values.Contains(achievement.Level));
    }

    /// <summary>
    /// Filters the achievements to those with a level in the specified range.
    /// </summary>
    /// <param name="minimum">The lowest level to keep, inclusive.</param>
    /// <param name="maximum">The highest level to keep, inclusive.</param>
    /// <returns>A new query with the matching achievements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximum"/> is less than <paramref name="minimum"/>.</exception>
    public AchievementQuery LevelBetween(int minimum, int maximum)
    {
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum level cannot be below minimum level.");
        return Where(achievement => achievement.Level >= minimum && achievement.Level <= maximum);
    }

    /// <summary>
    /// Filters the achievements to those whose current progress is at least the specified value.
    /// </summary>
    /// <param name="minimum">The lowest progress to keep, inclusive.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery ProgressAtLeast(int minimum) =>
        Where(achievement => achievement.CurrentProgress >= minimum);

    /// <summary>
    /// Filters the achievements to those whose level reward is paid in any of the specified point types.
    /// </summary>
    /// <param name="pointTypes">The point types to keep, compared with <see cref="Achievement.LevelRewardPointType"/>.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery WithRewardPointType(params int[] pointTypes) =>
        WithRewardPointType((IEnumerable<int>)pointTypes);

    /// <summary>
    /// Filters the achievements to those whose level reward is paid in any of the specified point types.
    /// </summary>
    /// <param name="pointTypes">The point types to keep, compared with <see cref="Achievement.LevelRewardPointType"/>.</param>
    /// <returns>A new query with the matching achievements.</returns>
    public AchievementQuery WithRewardPointType(IEnumerable<int> pointTypes)
    {
        HashSet<int> values = QueryValues.Set(pointTypes);
        return Where(achievement => values.Contains(achievement.LevelRewardPointType));
    }

    /// <summary>
    /// Sorts the achievements by category, then subcategory, then id.
    /// </summary>
    /// <remarks>
    /// Categories and subcategories are compared ignoring case.
    /// </remarks>
    /// <returns>A new query with the sorted achievements.</returns>
    public AchievementQuery OrderByCategory() =>
        Next(Items
            .OrderBy(achievement => achievement.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Id));

    /// <summary>
    /// Sorts the achievements by current progress.
    /// </summary>
    /// <remarks>
    /// Achievements with equal progress keep their current order.
    /// </remarks>
    /// <param name="descending">Whether to sort from the most progress instead of the least.</param>
    /// <returns>A new query with the sorted achievements.</returns>
    public AchievementQuery OrderByProgress(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(achievement => achievement.CurrentProgress)
            : Items.OrderBy(achievement => achievement.CurrentProgress));

    private static AchievementQuery Next(IEnumerable<Achievement> achievements) => new(achievements);
}
