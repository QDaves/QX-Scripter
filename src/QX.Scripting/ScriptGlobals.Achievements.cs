using Qx.Game;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Loads the achievement list from the hotel when it has not been loaded yet.
    /// </summary>
    /// <remarks>
    /// It completes at once when the list is already loaded, and concurrent callers share one
    /// request.
    /// </remarks>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds.</param>
    /// <returns>Every known achievement, including the ones the client does not list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<IReadOnlyList<Achievement>> LoadAchievements(int timeoutMs = 10000) =>
        await Game.Achievements.EnsureLoadedAsync(timeoutMs, Ct);

    /// <summary>
    /// Gets one achievement by code or badge code, loading the list first when needed.
    /// </summary>
    /// <remarks>
    /// The code may be written any way the hotel writes it: <c>RoomEntry</c>, <c>ACH_RoomEntry</c>
    /// and <c>ACH_RoomEntry5</c> all find the same achievement. The comparison ignores case.
    /// </remarks>
    /// <param name="code">The achievement code, with or without prefix and level.</param>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>The achievement, or <see langword="null"/> when no achievement matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<Achievement?> GetAchievement(string code, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(code);
        await LoadAchievements(timeoutMs);
        return Game.Achievements.ByCode(code);
    }

    /// <summary>
    /// Gets one achievement by identifier, loading the list first when needed.
    /// </summary>
    /// <param name="id">The achievement identifier.</param>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>The achievement, or <see langword="null"/> when no achievement has that id.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<Achievement?> GetAchievementById(int id, int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.ById(id);
    }

    /// <summary>
    /// Gets the achievements grouped into categories the way the client groups them, loading the
    /// list first when needed.
    /// </summary>
    /// <remarks>
    /// Hidden and category-less entries are dropped, and archived ones are filed only under
    /// <c>archive</c>. The ordinary categories keep the order in which they first appear, followed
    /// by <c>misc</c> when present, then <c>archive</c> and <c>wired_games</c>, which are always
    /// included, and <c>new</c> when any achievement is marked as new.
    /// </remarks>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>The categories in display order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<IReadOnlyList<AchievementCategory>> GetAchievementCategories(int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Categories;
    }

    /// <summary>
    /// Gets one achievement category by code, loading the list first when needed.
    /// </summary>
    /// <param name="code">The category code, matched without regard to case.</param>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>
    /// The category, or <see langword="null"/> when there is no category with that code. The
    /// <c>archive</c> and <c>wired_games</c> categories are always present, even when empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<AchievementCategory?> GetAchievementCategory(string code, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(code);
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Category(code);
    }

    /// <summary>
    /// Gets the account's achievement score, as the hotel last reported it.
    /// </summary>
    /// <remarks>
    /// The hotel sends the score on its own rather than in answer to a request, so the value is
    /// what has arrived so far and is zero until it does. It is not the sum of the list.
    /// </remarks>
    public int AchievementScore => Game.Achievements.Score;

    /// <summary>Gets the number of achievement levels done across every listed achievement.</summary>
    public int AchievementProgress => Game.Achievements.Progress;

    /// <summary>Gets the number of achievement levels that exist across every listed achievement.</summary>
    public int AchievementMaxProgress => Game.Achievements.MaxProgress;

    /// <summary>Gets how far through every listed achievement this account is, from 0 to 1.</summary>
    /// <remarks>It is 0 when no levels are known.</remarks>
    public double AchievementCompletion => Game.Achievements.Completion;

    /// <summary>
    /// Gets the listed achievements that still have a level to reach, loading the list first when
    /// needed.
    /// </summary>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>The unfinished achievements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<IReadOnlyList<Achievement>> GetUnfinishedAchievements(int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Unfinished;
    }

    /// <summary>
    /// Gets the listed achievements that already have every level, loading the list first when
    /// needed.
    /// </summary>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>The finished achievements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<IReadOnlyList<Achievement>> GetFinishedAchievements(int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Finished;
    }

    /// <summary>
    /// Gets the unfinished achievements closest to their next level, loading the list first when
    /// needed.
    /// </summary>
    /// <remarks>
    /// Ordered by how far through the current level they are, then by how few points are left.
    /// Achievements the client draws without a progress bar are left out, since there is no partial
    /// progress for them to be close to.
    /// </remarks>
    /// <param name="count">The maximum number of achievements to return.</param>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>Up to <paramref name="count"/> achievements, closest first.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is negative, or <paramref name="timeoutMs"/> is zero or negative.
    /// </exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<IReadOnlyList<Achievement>> GetClosestAchievements(
        int count = 10,
        int timeoutMs = 10000)
    {
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Closest(count);
    }

    /// <summary>
    /// Gets the badge an achievement's next level grants and what it takes to get there.
    /// </summary>
    /// <remarks>
    /// The achievement list is loaded first when needed. The point limit comes from the hotel's
    /// badge point limits when they have been fetched with <see cref="GetBadgePointLimits"/>, and
    /// from the achievement's own level limit otherwise.
    /// </remarks>
    /// <param name="code">The achievement code, with or without prefix and level.</param>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>
    /// The next badge, or <see langword="null"/> when the achievement is unknown, already at its
    /// last level, or has a badge code that does not follow the level pattern.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<NextBadge?> GetNextBadge(string code, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(code);
        await LoadAchievements(timeoutMs);
        return Game.Achievements.Next(code);
    }

    /// <summary>
    /// Gets the next badges of the unfinished achievements closest to their next level.
    /// </summary>
    /// <remarks>
    /// The achievements are picked as in <see cref="GetClosestAchievements(int, int)"/> and keep
    /// that order. Achievements without a resolvable next badge are left out, so fewer than
    /// <paramref name="count"/> badges can come back.
    /// </remarks>
    /// <param name="count">The maximum number of achievements to consider.</param>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, when it has not been loaded yet.</param>
    /// <returns>The next badges, closest first.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is negative, or <paramref name="timeoutMs"/> is zero or negative.
    /// </exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list did not arrive within <paramref name="timeoutMs"/>.</exception>
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
    /// Gets the point limits behind every badge, loading them from the hotel when they have not
    /// been loaded yet.
    /// </summary>
    /// <remarks>
    /// It completes at once when the limits are already loaded, and concurrent callers share one
    /// request.
    /// </remarks>
    /// <param name="timeoutMs">The time to wait for the limits, in milliseconds.</param>
    /// <returns>The badge point limits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the limits did not arrive within <paramref name="timeoutMs"/>.</exception>
    public async Task<BadgePointLimits> GetBadgePointLimits(int timeoutMs = 10000) =>
        await Game.Achievements.EnsurePointLimitsLoadedAsync(timeoutMs, Ct);

    /// <summary>Asks the hotel to resend the achievement list.</summary>
    /// <remarks>
    /// It returns without waiting; the list arrives through
    /// <see cref="OnAchievementList(Action{IReadOnlyList{Achievement}})"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void RefreshAchievements() => Game.Achievements.Request();

    /// <summary>Registers a handler that runs whenever the whole achievement list arrives.</summary>
    /// <param name="handler">The handler to call with the list as the hotel sent it.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnAchievementList(Action<IReadOnlyList<Achievement>> handler)
        => Subscribe(
            handler,
            value => Game.Achievements.ListChanged += value,
            value => Game.Achievements.ListChanged -= value);

    /// <summary>
    /// Registers a handler that runs whenever an achievement gains a level.
    /// </summary>
    /// <remarks>
    /// The level change is worked out from the achievement update itself, so the handler runs
    /// whether or not the hotel sends a level-up notification alongside it.
    /// </remarks>
    /// <param name="handler">The handler to call with the achievement as it was and as it now stands.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnAchievementLevelUp(Action<Achievement, Achievement> handler)
        => Subscribe(
            handler,
            value => Game.Achievements.LevelUp += value,
            value => Game.Achievements.LevelUp -= value);

    /// <summary>Registers a handler that runs whenever the hotel reports the achievement score.</summary>
    /// <param name="handler">The handler to call with the new score.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnAchievementScore(Action<int> handler)
        => Subscribe(
            handler,
            value => Game.Achievements.ScoreChanged += value,
            value => Game.Achievements.ScoreChanged -= value);
}
