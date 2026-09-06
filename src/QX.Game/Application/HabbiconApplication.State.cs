namespace Qx.Game.Application;

internal sealed partial class HabbiconApplication
{
    private const int maximum_page_size = 500;

    public HabbiconStateView ReadState(HabbiconStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshotRevision(request.SnapshotRevision);
        HabbiconSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        HabbiconStateData state = lease.State;
        bool connected = Connected(state);
        HabbiconStateView result = new(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ShopRevision,
            state.UserRevision,
            state.StatusRevision,
            state.InfoRevision,
            state.RoomRevision,
            state.SettingsRevision,
            lease.Revision,
            Summary(state),
            state.RecentHabbiconIds,
            state.LastInfo is null ? null : EntryView(state.LastInfo, 0),
            state.LastRoomUse is null
                ? null
                : new HabbiconRoomUseView(
                    state.LastRoomUse.RoomIndex,
                    state.LastRoomUse.HabbiconId));
        RequireLeaseActive(lease);
        return result;
    }

    public HabbiconCollectionPage ReadCollections(HabbiconCollectionPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        HabbiconSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        IReadOnlyList<HabbiconCollectionView> page = Slice(
            lease.Collections,
            request.Offset,
            request.Limit);
        HabbiconStateData state = lease.State;
        bool connected = Connected(state);
        HabbiconCollectionPage result = new(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ShopRevision,
            state.UserRevision,
            lease.Revision,
            Summary(state),
            lease.Collections.Count,
            request.Offset,
            NextOffset(request.Offset, page.Count, lease.Collections.Count),
            page);
        RequireLeaseActive(lease);
        return result;
    }

    public HabbiconEntryPage ReadEntries(HabbiconEntryPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        HabbiconSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        HabbiconEntryPage result = EntryPageFor(lease, request.Offset, request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    private HabbiconEntryPage EntryPageFor(HabbiconSnapshotLease lease, int offset, int limit)
    {
        IReadOnlyList<HabbiconEntryView> page = Slice(lease.Entries, offset, limit);
        HabbiconStateData state = lease.State;
        bool connected = Connected(state);
        return new HabbiconEntryPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ShopRevision,
            state.UserRevision,
            lease.Revision,
            Summary(state),
            lease.Entries.Count,
            offset,
            NextOffset(offset, page.Count, lease.Entries.Count),
            page);
    }

    private HabbiconCollectionPage CollectionPageFor(
        HabbiconSnapshotLease lease,
        int offset,
        int limit)
    {
        IReadOnlyList<HabbiconCollectionView> page = Slice(
            lease.Collections,
            offset,
            limit);
        HabbiconStateData state = lease.State;
        bool connected = Connected(state);
        return new HabbiconCollectionPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ShopRevision,
            state.UserRevision,
            lease.Revision,
            Summary(state),
            lease.Collections.Count,
            offset,
            NextOffset(offset, page.Count, lease.Collections.Count),
            page);
    }

    private bool Connected(HabbiconStateData state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private void RequireLeaseActive(HabbiconSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the habbicon snapshot was being read.");
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
