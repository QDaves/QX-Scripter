namespace Qx.Game.Application;

internal sealed partial class DailyTaskApplication
{
    private const int maximum_page_size = 500;

    public DailyTaskStateView ReadState(DailyTaskStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshotRevision(request.SnapshotRevision);
        DailyTaskSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        DailyTaskStateView result = StateViewFor(lease);
        RequireLeaseActive(lease);
        return result;
    }

    public DailyTaskPage ReadEntries(DailyTaskPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        DailyTaskSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        DailyTaskPage result = PageFor(lease, request.Offset, request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    private DailyTaskStateView StateViewFor(DailyTaskSnapshotLease lease)
    {
        DailyTaskState state = lease.State;
        bool connected = Connected(state);
        return new DailyTaskStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.TasksRevision,
            state.BaselineRevision,
            state.AddedRevision,
            state.UpdateRevision,
            lease.Revision,
            Summary(state));
    }

    private DailyTaskPage PageFor(
        DailyTaskSnapshotLease lease,
        int offset,
        int limit)
    {
        DailyTaskState state = lease.State;
        IReadOnlyList<DailyTaskView> page = Slice(lease.Tasks, offset, limit);
        bool connected = Connected(state);
        return new DailyTaskPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.TasksRevision,
            state.BaselineRevision,
            lease.Revision,
            Summary(state),
            lease.Tasks.Count,
            offset,
            NextOffset(offset, page.Count, lease.Tasks.Count),
            page);
    }

    private bool Connected(DailyTaskState state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private void RequireLeaseActive(DailyTaskSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the daily task snapshot was being read.");
        }
    }

    private static void ValidatePage(
        int offset,
        int limit,
        long? snapshot_revision)
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

    private static IReadOnlyList<T> Slice<T>(
        IReadOnlyList<T> values,
        int offset,
        int limit)
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
