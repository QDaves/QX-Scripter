using Qx.Game.Application;

namespace Qx.Game.Snapshots;

public sealed partial class GameQueryService
{
    public QueryEnvelope<AchievementCollectionSnapshot> Achievements(int maxItems = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxItems);

        if (maxItems == 0)
        {
            AchievementStateView state = application.Invoke<
                AchievementStateRequest,
                AchievementStateView>(
                    ApplicationMemberIds.AchievementsState,
                    new AchievementStateRequest());
            var empty = new AchievementCollectionSnapshot(
                state.List.DefaultCategory,
                state.List.Total,
                state.List.Completed,
                0,
                0,
                state.List.Total > 0,
                []);
            return Result(
                "achievements",
                new QueryRead<AchievementCollectionSnapshot>(
                    empty,
                    state.Connected && state.List.Loaded,
                    state.List.Loaded,
                    !state.Connected && state.List.Total > 0,
                    empty.Truncated,
                    state.List.Loaded ? [] : ["achievements"]));
        }

        const int page_limit = 500;
        AchievementPage first = application.Invoke<AchievementPageRequest, AchievementPage>(
            ApplicationMemberIds.AchievementsList,
            new AchievementPageRequest(Limit: page_limit));
        ValidateAchievementPage(first, 0, page_limit, null, null);
        var achievements = new List<AchievementSnapshot>(first.Total);
        AddAchievements(first, achievements);
        AchievementPage current = first;
        while (current.NextOffset is int offset)
        {
            current = application.Invoke<AchievementPageRequest, AchievementPage>(
                ApplicationMemberIds.AchievementsList,
                new AchievementPageRequest(offset, page_limit, first.SnapshotRevision));
            ValidateAchievementPage(
                current,
                offset,
                page_limit,
                first.SnapshotRevision,
                first);
            AddAchievements(current, achievements);
        }
        if (achievements.Count != first.Total)
            throw new InvalidOperationException("The achievement application returned an incomplete snapshot.");

        AchievementSnapshot[] selected = achievements
            .OrderBy(achievement => achievement.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Id)
            .Take(maxItems)
            .ToArray();
        bool truncated = selected.Length < first.Total;
        var snapshot = new AchievementCollectionSnapshot(
            first.DefaultCategory,
            first.Total,
            first.Completed,
            selected.Length,
            maxItems,
            truncated,
            Array.AsReadOnly(selected));
        var read = new QueryRead<AchievementCollectionSnapshot>(
            snapshot,
            first.Connected && first.Loaded,
            first.Loaded,
            !first.Connected && snapshot.Total > 0,
            truncated,
            first.Loaded ? [] : ["achievements"]);
        return Result("achievements", read);
    }

    private static void AddAchievements(
        AchievementPage page,
        List<AchievementSnapshot> achievements)
    {
        achievements.AddRange(page.Achievements.Select(achievement => new AchievementSnapshot(
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
            achievement.State)));
    }

    private static void ValidateAchievementPage(
        AchievementPage page,
        int offset,
        int limit,
        long? snapshot_revision,
        AchievementPage? first)
    {
        int consumed = checked(offset + page.Achievements.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Completed < 0 ||
            page.Completed > page.Total ||
            page.Achievements.Count > limit ||
            consumed > page.Total ||
            consumed < page.Total && page.Achievements.Count == 0 ||
            page.NextOffset != expected_next ||
            snapshot_revision is long revision && page.SnapshotRevision != revision ||
            first is not null &&
            (page.Connected != first.Connected ||
             page.Client != first.Client ||
             page.SessionGeneration != first.SessionGeneration ||
             page.StateRevision != first.StateRevision ||
             page.ListRevision != first.ListRevision ||
             page.BaselineRevision != first.BaselineRevision ||
             page.NewCodesRevision != first.NewCodesRevision ||
             page.Loaded != first.Loaded ||
             page.Total != first.Total ||
             page.Completed != first.Completed ||
             page.DefaultCategory != first.DefaultCategory))
        {
            throw new InvalidOperationException("The achievement application returned an invalid snapshot page.");
        }
    }
}
