using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed class FriendMessageJournal : IDisposable
{
    private readonly IConnection connection;
    private readonly FriendManager friends;
    private readonly TimeProvider time_provider;
    private readonly int capacity;
    private readonly ApplicationEventSource<FriendMessageEntry> received;
    private readonly object publication_sync = new();
    private readonly object sync = new();
    private readonly Queue<FriendMessageEntry> entries = [];
    private long next_sequence;
    private long cursor_floor;
    private bool disposed;

    public FriendMessageJournal(
        IConnection connection,
        FriendManager friends,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null,
        int capacity = 2000)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(friends);
        ArgumentNullException.ThrowIfNull(time_provider);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.connection = connection;
        this.friends = friends;
        this.time_provider = time_provider;
        this.capacity = capacity;
        received = new ApplicationEventSource<FriendMessageEntry>(observer_error);
        friends.MessageReceived += OnMessage;
        friends.ResetCompleted += OnReset;
    }

    public FriendMessageHistoryPage History(FriendMessageHistoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.AfterSequence);
        if (request.Limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(request.Limit));

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            long oldest = entries.TryPeek(out FriendMessageEntry? first) ? first.Sequence : 0;
            long latest = next_sequence;
            bool gap = request.AfterSequence > latest ||
                request.AfterSequence < cursor_floor ||
                oldest > 0 && request.AfterSequence < oldest - 1;
            var page = new List<FriendMessageEntry>(Math.Min(request.Limit, entries.Count));
            bool has_more = false;
            foreach (FriendMessageEntry entry in entries)
            {
                if (entry.Sequence <= request.AfterSequence)
                    continue;
                if (page.Count == request.Limit)
                {
                    has_more = true;
                    break;
                }
                page.Add(entry);
            }
            long next = page.Count == 0
                ? gap ? latest : Math.Min(request.AfterSequence, latest)
                : page[^1].Sequence;
            return new FriendMessageHistoryPage(
                page,
                request.AfterSequence,
                next,
                oldest,
                latest,
                has_more,
                gap);
        }
    }

    public IDisposable Subscribe(Action<FriendMessageEntry> listener) =>
        received.Subscribe(listener);

    public void Dispose()
    {
        lock (publication_sync)
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                entries.Clear();
            }
            friends.MessageReceived -= OnMessage;
            friends.ResetCompleted -= OnReset;
            received.Dispose();
        }
    }

    private void OnReset()
    {
        lock (publication_sync)
        {
            lock (sync)
            {
                if (disposed)
                    return;
                entries.Clear();
                cursor_floor = next_sequence;
            }
        }
    }

    private void OnMessage(NewConsoleMessage message)
    {
        lock (publication_sync)
        {
            Session? session = connection.Session;
            if (session is null)
                return;
            FriendMessageEntry entry;
            lock (sync)
            {
                if (disposed || !ReferenceEquals(connection.Session, session))
                    return;
                entry = new FriendMessageEntry(
                    ++next_sequence,
                    time_provider.GetUtcNow(),
                    session.Client,
                    message.ChatId,
                    message.Content.Type,
                    message.Content.Text,
                    message.Content.HabbiconId,
                    message.SecondsSinceSent,
                    message.MessageId,
                    message.ConfirmationId,
                    message.SenderId,
                    message.SenderName,
                    message.SenderFigure,
                    message.IsOffline,
                    message.LegacyCompact);
                entries.Enqueue(entry);
                if (entries.Count > capacity)
                    entries.Dequeue();
            }
            received.Publish(entry);
        }
    }
}
