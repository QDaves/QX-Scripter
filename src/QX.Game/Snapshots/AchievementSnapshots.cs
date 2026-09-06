using Qx.Model.Messages.Incoming;

namespace Qx.Game.Snapshots;

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
