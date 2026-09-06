namespace Qx.Game.Application;

internal sealed partial class EarningApplication
{
    private const int maximum_page_size = 500;

    public EarningStateView ReadState(EarningStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshotRevision(request.SnapshotRevision);
        EarningSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        EarningStateView result = StateViewFor(lease);
        RequireLeaseActive(lease);
        return result;
    }

    public EarningEntryPage ReadEntries(EarningEntryPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        EarningSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        EarningEntryPage result = EntryPageFor(lease, request.Offset, request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    private EarningStateView StateViewFor(EarningSnapshotLease lease)
    {
        EarningState state = lease.State;
        bool connected = Connected(state);
        return new EarningStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.StatusRevision,
            state.BaselineRevision,
            state.ClaimRevision,
            state.NotificationRevision,
            lease.Revision,
            VaultSummary(state));
    }

    private EarningEntryPage EntryPageFor(
        EarningSnapshotLease lease,
        int offset,
        int limit)
    {
        EarningState state = lease.State;
        IReadOnlyList<EarningEntryView> page = Slice(lease.Entries, offset, limit);
        bool connected = Connected(state);
        return new EarningEntryPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.StatusRevision,
            state.BaselineRevision,
            lease.Revision,
            VaultSummary(state),
            lease.Entries.Count,
            offset,
            NextOffset(offset, page.Count, lease.Entries.Count),
            page);
    }

    private bool Connected(EarningState state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private void RequireLeaseActive(EarningSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the earning snapshot was being read.");
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
