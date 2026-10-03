using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the game leaderboard manager, which tracks the all-time and weekly boards for players,
    /// friends and groups.
    /// </summary>
    public LeaderboardManager Leaderboards => Game.Leaderboards;

    /// <summary>
    /// Requests a leaderboard and waits for the window to come back.
    /// </summary>
    /// <remarks>
    /// The window is centered on the local user's own rank, which is what the client asks for when
    /// a board is first opened. Page through it with <see cref="NextLeaderboardPage"/> and
    /// <see cref="PreviousLeaderboardPage"/>. The request is sent once without a retry, and the
    /// reply is not blocked from the game client.
    /// </remarks>
    /// <param name="gameTypeId">The game whose board to request.</param>
    /// <param name="scope">The slice of the board: everyone, friends, or groups.</param>
    /// <param name="weekly"><see langword="true"/> to request the weekly board; otherwise, <see langword="false"/> for the all-time one.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The window of rows the hotel sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the leaderboard state changed while it was read.</exception>
    /// <exception cref="TimeoutException">Thrown when the hotel did not answer in time.</exception>
    public async Task<Leaderboard> GetLeaderboard(
        int gameTypeId,
        LeaderboardScope scope = LeaderboardScope.Total,
        bool weekly = false,
        int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        LeaderboardStateView state = await ReadLeaderboardState(scope, weekly).ConfigureAwait(false);
        LeaderboardRefreshResult result = await _application
            .InvokeAsync<LeaderboardRefreshRequest, LeaderboardRefreshResult>(
                ApplicationMemberIds.LeaderboardsRefresh,
                new LeaderboardRefreshRequest(
                    gameTypeId,
                    scope,
                    weekly,
                    Limit: 500,
                    TimeoutMilliseconds: timeoutMs,
                    ExpectedSessionGeneration: state.SessionGeneration),
                Ct)
            .ConfigureAwait(false);
        ValidateLeaderboardRefresh(result, state.SessionGeneration, scope, weekly);
        return BoardFrom(await ReadCompleteLeaderboardPage(
            result.FirstPage,
            state.SessionGeneration,
            scope,
            weekly).ConfigureAwait(false));
    }

    /// <summary>
    /// Requests the rows after the window last received and waits for them.
    /// </summary>
    /// <remarks>
    /// The request is for the same game as the window last received for that slice.
    /// </remarks>
    /// <param name="scope">The slice of the board.</param>
    /// <param name="weekly"><see langword="true"/> for the weekly board; otherwise, <see langword="false"/>.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// The next window, or <see langword="null"/> when the board already ends at the window held.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the leaderboard state changed while it was read.</exception>
    /// <exception cref="TimeoutException">Thrown when the hotel did not answer in time.</exception>
    public async Task<Leaderboard?> NextLeaderboardPage(
        LeaderboardScope scope = LeaderboardScope.Total,
        bool weekly = false,
        int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        LeaderboardStateView state = await ReadLeaderboardState(scope, weekly).ConfigureAwait(false);
        if (!state.Board.HasMoreBelow)
            return null;
        LeaderboardEntryPage page = await ReadLeaderboardEntries(
            state,
            scope,
            weekly).ConfigureAwait(false);
        int start_rank = page.Entries.Count > 0 ? page.Entries[^1].Rank + 1 : -1;
        LeaderboardRefreshResult result = await _application
            .InvokeAsync<LeaderboardRefreshRequest, LeaderboardRefreshResult>(
                ApplicationMemberIds.LeaderboardsRefresh,
                new LeaderboardRefreshRequest(
                    state.Board.GameTypeId,
                    scope,
                    weekly,
                    start_rank,
                    0,
                    500,
                    timeoutMs,
                    state.SessionGeneration),
                Ct)
            .ConfigureAwait(false);
        ValidateLeaderboardRefresh(result, state.SessionGeneration, scope, weekly);
        return BoardFrom(await ReadCompleteLeaderboardPage(
            result.FirstPage,
            state.SessionGeneration,
            scope,
            weekly).ConfigureAwait(false));
    }

    /// <summary>Requests the rows before the window last received and waits for them.</summary>
    /// <remarks>
    /// The request is for the same game as the window last received for that slice.
    /// </remarks>
    /// <param name="scope">The slice of the board.</param>
    /// <param name="weekly"><see langword="true"/> for the weekly board; otherwise, <see langword="false"/>.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// The previous window, or <see langword="null"/> when the window held already starts at the top.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the leaderboard state changed while it was read.</exception>
    /// <exception cref="TimeoutException">Thrown when the hotel did not answer in time.</exception>
    public async Task<Leaderboard?> PreviousLeaderboardPage(
        LeaderboardScope scope = LeaderboardScope.Total,
        bool weekly = false,
        int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        LeaderboardStateView state = await ReadLeaderboardState(scope, weekly).ConfigureAwait(false);
        if (!state.Board.HasMoreAbove)
            return null;
        LeaderboardEntryPage page = await ReadLeaderboardEntries(
            state,
            scope,
            weekly).ConfigureAwait(false);
        int start_rank = page.Entries.Count > 0
            ? Math.Max(1, page.Entries[0].Rank - state.WindowSize)
            : -1;
        LeaderboardRefreshResult result = await _application
            .InvokeAsync<LeaderboardRefreshRequest, LeaderboardRefreshResult>(
                ApplicationMemberIds.LeaderboardsRefresh,
                new LeaderboardRefreshRequest(
                    state.Board.GameTypeId,
                    scope,
                    weekly,
                    start_rank,
                    1,
                    500,
                    timeoutMs,
                    state.SessionGeneration),
                Ct)
            .ConfigureAwait(false);
        ValidateLeaderboardRefresh(result, state.SessionGeneration, scope, weekly);
        return BoardFrom(await ReadCompleteLeaderboardPage(
            result.FirstPage,
            state.SessionGeneration,
            scope,
            weekly).ConfigureAwait(false));
    }

    /// <summary>
    /// Walks a whole leaderboard and returns its rows, up to a limit.
    /// </summary>
    /// <remarks>
    /// The hotel only ever sends a window, so this pages until the rows run out. The opening
    /// window sits around the local user, so the walk goes up to the top first and then down.
    /// Boards can be long: <paramref name="maxRows"/> caps the walk so a script cannot page
    /// forever against a board that keeps growing underneath it.
    /// </remarks>
    /// <param name="gameTypeId">The game whose board to walk.</param>
    /// <param name="scope">The slice of the board.</param>
    /// <param name="weekly"><see langword="true"/> for the weekly board; otherwise, <see langword="false"/>.</param>
    /// <param name="maxRows">The maximum number of rows to collect.</param>
    /// <param name="timeoutMs">The timeout in milliseconds for each request.</param>
    /// <returns>The rows ordered by rank, at most <paramref name="maxRows"/> of them.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxRows"/> or <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the leaderboard state changed while it was read.</exception>
    /// <exception cref="TimeoutException">Thrown when the hotel did not answer in time.</exception>
    public async Task<IReadOnlyList<LeaderboardEntry>> GetFullLeaderboard(
        int gameTypeId,
        LeaderboardScope scope = LeaderboardScope.Total,
        bool weekly = false,
        int maxRows = 500,
        int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);

        Leaderboard page = await GetLeaderboard(gameTypeId, scope, weekly, timeoutMs);
        var rows = new SortedDictionary<int, LeaderboardEntry>();
        foreach (LeaderboardEntry entry in page.Entries)
            rows[entry.Rank] = entry;

        // Walk up to the top first, then down, because the opening window sits around the local
        // user rather than at rank one.
        while (rows.Count < maxRows)
        {
            Leaderboard? previous = await PreviousLeaderboardPage(scope, weekly, timeoutMs);
            if (previous is null || previous.Entries.Count == 0)
                break;
            int previous_count = rows.Count;
            foreach (LeaderboardEntry entry in previous.Entries)
                rows[entry.Rank] = entry;
            if (rows.Count == previous_count)
                break;
        }

        while (rows.Count < maxRows)
        {
            Leaderboard? next = await NextLeaderboardPage(scope, weekly, timeoutMs);
            if (next is null || next.Entries.Count == 0)
                break;
            int previous_count = rows.Count;
            foreach (LeaderboardEntry entry in next.Entries)
                rows[entry.Rank] = entry;
            if (rows.Count == previous_count)
                break;
        }

        return rows.Values.Take(maxRows).ToArray();
    }

    /// <summary>
    /// Sets which week the weekly boards ask for, zero being the running week.
    /// </summary>
    /// <remarks>
    /// No request is sent; the offset applies to the next weekly board request.
    /// </remarks>
    /// <param name="offset">
    /// The number of weeks back, capped at the maximum offset of the last weekly period received.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is negative.</exception>
    public void SetLeaderboardWeek(int offset) =>
        _application.Invoke<LeaderboardWeekOffsetRequest, LeaderboardWeekOffsetResult>(
            ApplicationMemberIds.LeaderboardsWeekOffsetSet,
            new LeaderboardWeekOffsetRequest(offset),
            Ct);

    /// <summary>
    /// Gets the week the last weekly board covered, or <see langword="null"/> when none was received.
    /// </summary>
    public WeeklyLeaderboardPeriod? LeaderboardWeek => Game.Leaderboards.Period;

    /// <summary>Registers a handler that runs whenever any leaderboard window arrives.</summary>
    /// <param name="handler">The handler to call with the slice, whether it was weekly, and the window.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnLeaderboard(Action<LeaderboardScope, bool, Leaderboard> handler)
        => Subscribe(
            handler,
            value => Game.Leaderboards.BoardReceived += value,
            value => Game.Leaderboards.BoardReceived -= value);

    private async Task<LeaderboardStateView> ReadLeaderboardState(
        LeaderboardScope scope,
        bool weekly)
    {
        LeaderboardStateView state = await _application
            .InvokeAsync<LeaderboardStateRequest, LeaderboardStateView>(
                ApplicationMemberIds.LeaderboardsState,
                new LeaderboardStateRequest(scope, weekly),
                Ct)
            .ConfigureAwait(false);
        if (!state.Connected || state.SessionGeneration <= 0)
            throw new InvalidOperationException("An active leaderboard session is required.");
        return state;
    }

    private async Task<LeaderboardEntryPage> ReadLeaderboardEntries(
        LeaderboardStateView state,
        LeaderboardScope scope,
        bool weekly)
    {
        LeaderboardEntryPage page = await _application
            .InvokeAsync<LeaderboardEntryPageRequest, LeaderboardEntryPage>(
                ApplicationMemberIds.LeaderboardsEntriesList,
                new LeaderboardEntryPageRequest(
                    scope,
                    weekly,
                    Limit: 500,
                    SnapshotRevision: state.SnapshotRevision),
                Ct)
            .ConfigureAwait(false);
        return await ReadCompleteLeaderboardPage(
            page,
            state.SessionGeneration,
            scope,
            weekly).ConfigureAwait(false);
    }

    private static void ValidateLeaderboardRefresh(
        LeaderboardRefreshResult result,
        long session_generation,
        LeaderboardScope scope,
        bool weekly)
    {
        LeaderboardEntryPage page = result.FirstPage;
        if (result.SessionGeneration != session_generation ||
            page.SessionGeneration != session_generation ||
            page.StateRevision != result.StateRevision ||
            page.BoardsRevision != result.BoardsRevision ||
            page.SnapshotRevision != result.SnapshotRevision ||
            page.Scope != scope ||
            page.Weekly != weekly ||
            page.Offset != 0)
        {
            throw new InvalidOperationException("The leaderboard application returned an invalid refresh.");
        }
    }

    private async Task<LeaderboardEntryPage> ReadCompleteLeaderboardPage(
        LeaderboardEntryPage first,
        long session_generation,
        LeaderboardScope scope,
        bool weekly)
    {
        ValidateLeaderboardPage(first, first, session_generation, scope, weekly, 0);
        var entries = new List<LeaderboardEntryView>(first.Total);
        entries.AddRange(first.Entries);
        int? next_offset = first.NextOffset;
        while (next_offset is int offset)
        {
            LeaderboardEntryPage page = await _application
                .InvokeAsync<LeaderboardEntryPageRequest, LeaderboardEntryPage>(
                    ApplicationMemberIds.LeaderboardsEntriesList,
                    new LeaderboardEntryPageRequest(
                        scope,
                        weekly,
                        offset,
                        500,
                        first.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateLeaderboardPage(page, first, session_generation, scope, weekly, offset);
            entries.AddRange(page.Entries);
            next_offset = page.NextOffset;
        }
        if (entries.Count != first.Total)
            throw new InvalidOperationException("The leaderboard application returned an incomplete page set.");
        return first with
        {
            NextOffset = null,
            Entries = Array.AsReadOnly(entries.ToArray())
        };
    }

    private static void ValidateLeaderboardPage(
        LeaderboardEntryPage page,
        LeaderboardEntryPage first,
        long session_generation,
        LeaderboardScope scope,
        bool weekly,
        int expected_offset)
    {
        if (page.Board is null || first.Board is null || page.Entries is null ||
            page.Total is < 0 or > 65535)
        {
            throw new InvalidOperationException("The leaderboard application returned an invalid page.");
        }
        int end_offset = checked(page.Offset + page.Entries.Count);
        int? expected_next = end_offset < page.Total ? end_offset : null;
        if (!page.Connected ||
            page.SessionGeneration != session_generation ||
            page.StateRevision != first.StateRevision ||
            page.BoardsRevision != first.BoardsRevision ||
            page.SnapshotRevision != first.SnapshotRevision ||
            page.Scope != scope ||
            page.Weekly != weekly ||
            page.Board != first.Board ||
            page.Total != first.Total ||
            page.Total != page.Board.EntryCount ||
            page.Offset != expected_offset ||
            page.Offset < 0 ||
            page.Entries.Count > 500 ||
            end_offset > page.Total ||
            (expected_next is not null && end_offset == page.Offset) ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException("The leaderboard application returned an invalid page.");
        }
    }

    private static Leaderboard BoardFrom(LeaderboardEntryPage page)
    {
        LeaderboardEntry[] entries = page.Entries
            .Select(entry => new LeaderboardEntry(
                entry.UserId,
                entry.Score,
                entry.Rank,
                entry.Name,
                entry.Figure,
                entry.Gender))
            .ToArray();
        return new Leaderboard(entries, page.Board.TotalListSize, page.Board.GameTypeId);
    }
}
