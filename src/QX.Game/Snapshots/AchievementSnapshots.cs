using Qx.Model.Messages.Incoming;

namespace Qx.Game.Snapshots;

/// <summary>
/// Represents the JSON projection of one of the local user's achievements for the MCP read tools;
/// scripts use <see cref="Qx.Model.Messages.Incoming.Achievement"/>.
/// </summary>
/// <param name="Id">The achievement identifier.</param>
/// <param name="Level">The level being worked on, or the final level once <paramref name="IsComplete"/> is <see langword="true"/>.</param>
/// <param name="BadgeCode">The badge code of the level, such as <c>ACH_RoomEntry5</c>.</param>
/// <param name="BaseProgress">The progress score at which the current level started.</param>
/// <param name="MaxProgress">The progress score that completes the current level.</param>
/// <param name="LevelRewardPoints">The number of reward points given for the level.</param>
/// <param name="LevelRewardPointType">The activity point type of the level reward.</param>
/// <param name="CurrentProgress">The current progress score, on the same scale as <paramref name="BaseProgress"/> and <paramref name="MaxProgress"/>.</param>
/// <param name="IsComplete">Whether the final level is reached, so no further level exists.</param>
/// <param name="Category">The category the client lists the achievement under.</param>
/// <param name="Subcategory">The subcategory within <paramref name="Category"/>.</param>
/// <param name="MaxLevel">The number of levels the achievement has.</param>
/// <param name="DisplayMethod">
/// How the client draws the progress, one of the <see cref="Qx.Model.Messages.Incoming.AchievementDisplay"/> values: 0 draws a
/// progress bar and 1 draws none.
/// </param>
/// <param name="State">
/// Where the client files the achievement, one of the <see cref="Qx.Model.Messages.Incoming.AchievementState"/> values: 0 normal,
/// 2 archived and 4 hidden.
/// </param>
public sealed record AchievementSnapshot(
    int Id,
    int Level,
    string BadgeCode,
    int BaseProgress,
    int MaxProgress,
    int LevelRewardPoints,
    int LevelRewardPointType,
    int CurrentProgress,
    bool IsComplete,
    string Category,
    string Subcategory,
    int MaxLevel,
    int DisplayMethod,
    short State);

/// <summary>
/// Represents the JSON projection of the local user's achievements, capped to a maximum count, for
/// the MCP read tools; scripts use <see cref="AchievementManager"/>.
/// </summary>
/// <param name="DefaultCategory">The default category the hotel sent with the achievement list.</param>
/// <param name="Total">The number of achievements the user has.</param>
/// <param name="Completed">The number of achievements at their final level, counted over all of them.</param>
/// <param name="Returned">The number of achievements in <paramref name="Achievements"/>.</param>
/// <param name="MaxItems">The cap applied to the projection.</param>
/// <param name="Truncated">Whether achievements were dropped to honor the cap.</param>
/// <param name="Achievements">
/// The achievements, ordered by category and subcategory case-insensitively, then by identifier. When
/// truncated these are the first ones in that order.
/// </param>
public sealed record AchievementCollectionSnapshot(
    string DefaultCategory,
    int Total,
    int Completed,
    int Returned,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<AchievementSnapshot> Achievements);

public static partial class SnapshotFactory
{
    /// <summary>Projects achievements into a capped collection.</summary>
    /// <param name="achievements">The achievements to project.</param>
    /// <param name="defaultCategory">The default category to report.</param>
    /// <param name="maxItems">The maximum number of achievements to return.</param>
    /// <param name="sourceItemLimit">The safety valve against an unbounded source; a source with more items throws.</param>
    /// <returns>
    /// The collection, ordered by category and subcategory case-insensitively, then by identifier. The
    /// total and completed counts cover the whole source.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="achievements"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxItems"/> or <paramref name="sourceItemLimit"/> is negative.
    /// </exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// Thrown when the sequence yields more than <paramref name="sourceItemLimit"/> items.
    /// </exception>
    public static AchievementCollectionSnapshot Achievements(
        IEnumerable<Achievement> achievements,
        string defaultCategory = "",
        int maxItems = 500,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        CappedSource<Achievement> source = SelectCapped(
            achievements,
            maxItems,
            sourceItemLimit,
            nameof(achievements),
            Comparer<Achievement>.Create((left, right) =>
            {
                int comparison = StringComparer.OrdinalIgnoreCase.Compare(
                    left.Category,
                    right.Category);
                if (comparison != 0)
                    return comparison;

                comparison = StringComparer.OrdinalIgnoreCase.Compare(
                    left.Subcategory,
                    right.Subcategory);
                return comparison != 0
                    ? comparison
                    : left.Id.CompareTo(right.Id);
            }),
            achievement => achievement.IsComplete);
        AchievementSnapshot[] projected = source.Items
            .Select(achievement => new AchievementSnapshot(
                achievement.Id,
                achievement.Level,
                achievement.BadgeCode,
                achievement.BaseProgress,
                achievement.MaxProgress,
                achievement.LevelRewardPoints,
                achievement.LevelRewardPointType,
                achievement.CurrentProgress,
                achievement.IsComplete,
                achievement.Category,
                achievement.Subcategory,
                achievement.MaxLevel,
                achievement.DisplayMethod,
                achievement.State))
            .ToArray();

        return new AchievementCollectionSnapshot(
            defaultCategory,
            source.Total,
            source.Completed,
            projected.Length,
            maxItems,
            projected.Length < source.Total,
            projected);
    }
}
