namespace Qx.Game.Application;

internal sealed partial class QuestApplication
{
    private const int maximum_page_size = 500;

    public QuestStateView ReadState(QuestStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshotRevision(request.SnapshotRevision);
        QuestSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        QuestStateView result = StateViewFor(lease);
        RequireLeaseActive(lease);
        return result;
    }

    public QuestEntryPage ReadEntries(QuestEntryPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateCollection(request.Collection);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        QuestSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        QuestEntryPage result = EntryPageFor(
            lease,
            request.Collection,
            request.Offset,
            request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    private QuestStateView StateViewFor(QuestSnapshotLease lease)
    {
        QuestState state = lease.State;
        bool connected = Connected(state);
        return new QuestStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.AvailableRevision,
            state.SeasonalRevision,
            state.CurrentRevision,
            state.CompletionRevision,
            state.CancellationRevision,
            state.DailyRevision,
            lease.Revision,
            Summary(state),
            state.Current is null ? null : View(state.Current),
            state.LastCompletion is null ? null : View(state.LastCompletion),
            state.LastCancellation is null ? null : View(state.LastCancellation),
            state.Daily is null ? null : View(state.Daily));
    }

    private QuestEntryPage EntryPageFor(
        QuestSnapshotLease lease,
        QuestCollection collection,
        int offset,
        int limit)
    {
        QuestState state = lease.State;
        IReadOnlyList<QuestEntryView> entries = Entries(lease, collection);
        IReadOnlyList<QuestEntryView> page = Slice(entries, offset, limit);
        bool connected = Connected(state);
        return new QuestEntryPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.AvailableRevision,
            state.SeasonalRevision,
            lease.Revision,
            Summary(state),
            collection,
            entries.Count,
            offset,
            NextOffset(offset, page.Count, entries.Count),
            page);
    }

    private static IReadOnlyList<QuestEntryView> Entries(
        QuestSnapshotLease lease,
        QuestCollection collection) => collection switch
    {
        QuestCollection.Available => lease.Available,
        QuestCollection.Seasonal => lease.Seasonal,
        QuestCollection.Combined => lease.Combined,
        _ => throw new ArgumentOutOfRangeException(nameof(collection))
    };

    private bool Connected(QuestState state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private void RequireLeaseActive(QuestSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the quest snapshot was being read.");
        }
    }

    private static void ValidateCollection(QuestCollection collection)
    {
        if (!Enum.IsDefined(collection))
            throw new ArgumentOutOfRangeException(nameof(collection));
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
