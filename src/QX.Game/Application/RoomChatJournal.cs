using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed class RoomChatJournal : IDisposable
{
    private readonly IConnection _connection;
    private readonly RoomManager _room;
    private readonly TimeProvider _time_provider;
    private readonly int _capacity;
    private readonly Action<Exception>? _observer_error;
    private readonly object _sync = new();
    private readonly Queue<RoomChatEntry> _entries = [];
    private long _next_sequence;
    private bool _disposed;

    public RoomChatJournal(
        IConnection connection,
        RoomManager room,
        TimeProvider? time_provider = null,
        int capacity = 2000,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(room);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");

        _connection = connection;
        _room = room;
        _time_provider = time_provider ?? TimeProvider.System;
        _capacity = capacity;
        _observer_error = observer_error;
        _room.Chat += OnChat;
    }

    public event Action<RoomChatEntry>? Received;

    public RoomChatHistoryPage History(RoomChatHistoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AfterSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.AfterSequence),
                request.AfterSequence,
                "The sequence cursor cannot be negative.");
        }
        if (request.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Limit),
                request.Limit,
                "The history limit must be between 1 and 500.");
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            long oldest = _entries.TryPeek(out RoomChatEntry? first) ? first.Sequence : 0;
            long latest = _next_sequence;
            bool gap = request.AfterSequence > latest ||
                oldest > 0 && request.AfterSequence < oldest - 1;
            var entries = new List<RoomChatEntry>(Math.Min(request.Limit, _entries.Count));
            bool has_more = false;

            foreach (RoomChatEntry entry in _entries)
            {
                if (entry.Sequence <= request.AfterSequence)
                    continue;
                if (entries.Count == request.Limit)
                {
                    has_more = true;
                    break;
                }
                entries.Add(entry);
            }

            long next = entries.Count > 0
                ? entries[^1].Sequence
                : Math.Min(request.AfterSequence, latest);
            return new RoomChatHistoryPage(
                entries,
                request.AfterSequence,
                next,
                oldest,
                latest,
                has_more,
                gap);
        }
    }

    public IDisposable Subscribe(Action<RoomChatEntry> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Received += listener;
        }
        return new Subscription(this, listener);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            Received = null;
        }
        _room.Chat -= OnChat;
    }

    private void OnChat(AvatarChat chat)
    {
        Session? session = _connection.Session;
        if (session is null)
            return;

        RoomSnapshot snapshot = _room.Capture(room =>
        {
            Avatar? speaker = room.AvatarByIndex(chat.Index);
            Id? room_id = room.RoomId == 0 ? null : (Id)room.RoomId;
            return new RoomSnapshot(
                room_id,
                room.Generation,
                chat.Index,
                speaker?.Id,
                speaker?.Name,
                speaker?.Type,
                speaker?.Figure);
        });
        if (!ReferenceEquals(_connection.Session, session))
            return;

        RoomChatEntry entry;
        Action<RoomChatEntry>? received;
        lock (_sync)
        {
            if (_disposed)
                return;

            entry = new RoomChatEntry(
                ++_next_sequence,
                _time_provider.GetUtcNow(),
                session.Client,
                snapshot.RoomId,
                snapshot.RoomGeneration,
                snapshot.SpeakerIndex,
                snapshot.SpeakerId,
                snapshot.SpeakerName,
                snapshot.SpeakerType,
                snapshot.SpeakerFigure,
                chat);
            _entries.Enqueue(entry);
            if (_entries.Count > _capacity)
                _entries.Dequeue();
            received = Received;
        }

        if (received is null)
            return;
        foreach (Action<RoomChatEntry> listener in received.GetInvocationList().Cast<Action<RoomChatEntry>>())
        {
            try
            {
                listener(entry);
            }
            catch (Exception error)
            {
                _observer_error?.Invoke(error);
            }
        }
    }

    private void Unsubscribe(Action<RoomChatEntry> listener)
    {
        lock (_sync)
            Received -= listener;
    }

    private readonly record struct RoomSnapshot(
        Id? RoomId,
        long RoomGeneration,
        int SpeakerIndex,
        Id? SpeakerId,
        string? SpeakerName,
        AvatarType? SpeakerType,
        string? SpeakerFigure);

    private sealed class Subscription(
        RoomChatJournal journal,
        Action<RoomChatEntry> listener) : IDisposable
    {
        private RoomChatJournal? _journal = journal;
        private Action<RoomChatEntry>? _listener = listener;

        public void Dispose()
        {
            RoomChatJournal? journal = Interlocked.Exchange(ref _journal, null);
            Action<RoomChatEntry>? listener = Interlocked.Exchange(ref _listener, null);
            if (journal is not null && listener is not null)
                journal.Unsubscribe(listener);
        }
    }
}
