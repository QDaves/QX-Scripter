using Qx.Game;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Everything the hotel says about achievements: the list, the score, the categories and the
    /// point limits behind each badge.
    /// </summary>
    public AchievementManager AchievementState => Game.Achievements;

    /// <summary>
    /// Every achievement, fetching the list from the hotel on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Achievement>> LoadAchievements(int timeoutMs = 10000) =>
        await Game.Achievements.EnsureLoadedAsync(timeoutMs, Ct);

    /// <summary>
    /// One achievement by code or badge code, fetching the list on first use.
    /// </summary>
    /// <remarks>
    /// The code may be written any way the hotel writes it: <c>RoomEntry</c>, <c>ACH_RoomEntry</c>
    /// and <c>ACH_RoomEntry5</c> all find the same achievement.
    /// </remarks>
    /// <param name="code">The achievement code, with or without prefix and level.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<Achievement?> GetAchievement(string code, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(code);
        await LoadAchievements(timeoutMs);
        return Game.Achievements.ByCode(code);
    }

    /// <summary>
    /// One achievement by identifier, fetching the list on first use.
    /// </summary>
    /// <param name="id">The achievement identifier.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<Achievement?> GetAchievementById(int id, int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.ById(id);
    }

    /// <summary>
    /// The achievements grouped into categories the way the client groups them.
    /// </summary>
    /// <remarks>
    /// Hidden and category-less entries are dropped, retired ones move into <c>archive</c>,
    /// <c>misc</c> comes last of the ordinary categories, and the wired and new buckets follow it.
    /// </remarks>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<AchievementCategory>> GetAchievementCategories(int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Categories;
    }

    /// <summary>
    /// One category by code, or <see langword="null"/> when it holds nothing.
    /// </summary>
    /// <param name="code">The category code, matched without regard to case.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<AchievementCategory?> GetAchievementCategory(string code, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(code);
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Category(code);
    }

    /// <summary>
    /// The account's achievement score, as the hotel last reported it.
    /// </summary>
    /// <remarks>
    /// The hotel sends this on its own rather than in answer to a request, so this is what has
    /// arrived so far and is zero until it does. It is not the sum of the list.
    /// </remarks>
    public int AchievementScore => Game.Achievements.Score;

    /// <summary>How many achievement levels are done across every listed achievement.</summary>
    public int AchievementProgress => Game.Achievements.Progress;

    /// <summary>How many achievement levels exist across every listed achievement.</summary>
    public int AchievementMaxProgress => Game.Achievements.MaxProgress;

    /// <summary>How far through every listed achievement this account is, from 0 to 1.</summary>
    public double AchievementCompletion => Game.Achievements.Completion;

    /// <summary>
    /// The achievements that still have a level to reach, fetching the list on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Achievement>> GetUnfinishedAchievements(int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Unfinished;
    }

    /// <summary>
    /// The achievements that already have every level, fetching the list on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Achievement>> GetFinishedAchievements(int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Finished;
    }

    /// <summary>
    /// The unfinished achievements closest to their next level.
    /// </summary>
    /// <remarks>
    /// Ordered by how far through the current level they are, then by how few points are left.
    /// Achievements the client draws without a progress bar are left out, since there is no partial
    /// progress for them to be close to.
    /// </remarks>
    /// <param name="count">How many to return.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Achievement>> GetClosestAchievements(
        int count = 10,
        int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Closest(count);
    }

    /// <summary>
    /// The badge an achievement's next level grants and what it takes to get there.
    /// </summary>
    /// <remarks>
    /// The point limit comes from the hotel's badge point limits when they have been fetched with
    /// <see cref="GetBadgePointLimits"/>, and from the achievement's own level limit otherwise.
    /// Answers <see langword="null"/> when the achievement is unknown or already at its last level.
    /// </remarks>
    /// <param name="code">The achievement code, with or without prefix and level.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<NextBadge?> GetNextBadge(string code, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(code);
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Next(code);
    }

    /// <summary>
    /// The badge every unfinished achievement is working towards, ordered by how close it is.
    /// </summary>
    /// <param name="count">How many to return.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<NextBadge>> GetNextBadges(int count = 10, int timeoutMs = 10000)
    {
        IReadOnlyList<Achievement> closest = await GetClosestAchievements(count, timeoutMs);
        return
        [
            .. closest
                .Select(achievement => Game.Achievements.Next(achievement.Code))
                .Where(next => next is not null)
                .Select(next => next!)
        ];
    }

    /// <summary>
    /// The point limits behind every badge, fetching them from the hotel on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<BadgePointLimits> GetBadgePointLimits(int timeoutMs = 10000) =>
        await Game.Achievements.EnsurePointLimitsLoadedAsync(timeoutMs, Ct);

    /// <summary>Asks the hotel to resend the achievement list.</summary>
    public void RefreshAchievements() => Game.Achievements.Request();

    /// <summary>Runs a callback whenever the whole achievement list arrives.</summary>
    /// <param name="handler">Receives the list.</param>
    public void OnAchievementList(Action<IReadOnlyList<Achievement>> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.Achievements.ListChanged += value,
            value => Game.Achievements.ListChanged -= value);
    }

    /// <summary>
    /// Runs a callback whenever an achievement gains a level.
    /// </summary>
    /// <remarks>
    /// Worked out from the achievement update itself, so it fires whether or not the hotel sends a
    /// level-up notification alongside it.
    /// </remarks>
    /// <param name="handler">Receives the achievement as it was and as it now stands.</param>
    public void OnAchievementLevelUp(Action<Achievement, Achievement> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.Achievements.LevelUp += value,
            value => Game.Achievements.LevelUp -= value);
    }

    /// <summary>Runs a callback whenever the hotel reports the achievement score.</summary>
    /// <param name="handler">Receives the score.</param>
    public void OnAchievementScore(Action<int> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.Achievements.ScoreChanged += value,
            value => Game.Achievements.ScoreChanged -= value);
    }
}
