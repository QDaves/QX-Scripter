namespace Qx.Game.Application;

internal sealed partial class LeaderboardApplication
{
    private const int maximum_page_size = 500;

    public LeaderboardStateView ReadState(LeaderboardStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateRoute(request.Scope);
        ValidateSnapshotRevision(request.SnapshotRevision);
        LeaderboardSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        LeaderboardStateView result = StateViewFor(lease, request.Scope, request.Weekly);
        RequireLeaseActive(lease);
        return result;
    }

    public LeaderboardEntryPage ReadEntries(LeaderboardEntryPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateRoute(request.Scope);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        LeaderboardSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        LeaderboardEntryPage result = PageFor(
            lease,
            request.Scope,
            request.Weekly,
            request.Offset,
            request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    private LeaderboardStateView StateViewFor(
        LeaderboardSnapshotLease lease,
        LeaderboardScope scope,
        bool weekly)
    {
        LeaderboardState state = lease.State;
        bool connected = Connected(state);
        var route = new LeaderboardRoute(scope, weekly);
        return new LeaderboardStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.BoardsRevision,
            state.SettingsRevision,
            lease.Revision,
            scope,
            weekly,
            Summary(state.Boards.GetValueOrDefault(route)),
            PeriodView(state.Period),
            state.FavouriteGroupId,
            state.WeekOffset,
            lease.ViewSize,
            lease.WindowSize);
    }

    private LeaderboardEntryPage PageFor(
        LeaderboardSnapshotLease lease,
        LeaderboardScope scope,
        bool weekly,
        int offset,
        int limit)
    {
        LeaderboardState state = lease.State;
        var route = new LeaderboardRoute(scope, weekly);
        IReadOnlyList<LeaderboardEntryView> values = lease.Entries.GetValueOrDefault(route) ??
            Array.AsReadOnly(Array.Empty<LeaderboardEntryView>());
        IReadOnlyList<LeaderboardEntryView> page = Slice(values, offset, limit);
        bool connected = Connected(state);
        return new LeaderboardEntryPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.BoardsRevision,
            lease.Revision,
            scope,
            weekly,
            Summary(state.Boards.GetValueOrDefault(route)),
            values.Count,
            offset,
            NextOffset(offset, page.Count, values.Count),
            page);
    }

    private bool Connected(LeaderboardState state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private void RequireLeaseActive(LeaderboardSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the leaderboard snapshot was being read.");
        }
    }

    private static void ValidatePage(int offset, int limit, long? snapshot_revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > maximum_page_size)
            throw new ArgumentOutOfRangeException(nameof(limit));
        ValidateSnapshotRevision(snapshot_revision);
        if (offset > 0 && snapshot_revision is null)
        {
            throw new ArgumentException(
                "A snapshot revision is required after the first page.",
                nameof(snapshot_revision));
        }
    }

    private static void ValidateSnapshotRevision(long? snapshot_revision)
    {
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
    }

    private static void ValidateRoute(LeaderboardScope scope)
    {
        if (scope is not (LeaderboardScope.Total or LeaderboardScope.Friends or LeaderboardScope.Groups))
            throw new ArgumentOutOfRangeException(nameof(scope));
    }

    private static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> values, int offset, int limit)
    {
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }
}
