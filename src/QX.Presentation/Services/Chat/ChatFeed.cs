using Qx.Diagnostics;
using Qx.Game.Application;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Chat;

public sealed class ChatFeed : IDisposable
{
    public const int Capacity = 2000;
    public const int PageSize = 500;
    public const int PagesPerDrain = 4;

    readonly IGameGateway _gateway;
    readonly IUiDispatcher _dispatcher;
    readonly HashSet<long> _sequences = [];
    readonly Queue<long> _order = new();
    readonly IDisposable _subscription;
    long _signals;
    long _cursor;
    long _hidden;
    int _scheduled;
    int _disposed;

    public ChatFeed(IGameGateway gateway, IUiDispatcher dispatcher)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _subscription = gateway.Subscribe<RoomChatEntry>(ApplicationMemberIds.RoomChatReceived, OnReceived);
    }

    public event Action<IReadOnlyList<RoomChatEntry>>? Added;

    public void Start() => Schedule(signal: false);

    public async Task ClearAsync(CancellationToken cancellation_token)
    {
        try
        {
            RoomChatHistoryPage? latest = await _gateway.QueryAsync<RoomChatHistoryRequest, RoomChatHistoryPage>(
                ApplicationMemberIds.RoomChatHistory,
                new RoomChatHistoryRequest(long.MaxValue, 1),
                cancellation_token);
            if (latest is not null)
            {
                _hidden = latest.Latest;
                _cursor = _hidden;
            }
        }
        finally
        {
            _sequences.Clear();
            _order.Clear();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _subscription.Dispose();
    }

    void OnReceived(RoomChatEntry entry) => Schedule(signal: true);

    void Schedule(bool signal)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (signal)
            Interlocked.Increment(ref _signals);
        if (Interlocked.CompareExchange(ref _scheduled, 1, 0) != 0)
            return;
        _dispatcher.Post(DrainAsync, UiPriority.Background);
    }

    async Task DrainAsync()
    {
        long observed = Volatile.Read(ref _signals);
        bool more = false;
        try
        {
            if (Volatile.Read(ref _disposed) == 0)
                more = await ReadAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"The chat log could not be read: {error.Message}", "ui");
        }
        finally
        {
            Interlocked.Exchange(ref _scheduled, 0);
            if (Volatile.Read(ref _disposed) == 0 && (more || observed != Volatile.Read(ref _signals)))
                Schedule(signal: false);
        }
    }

    async Task<bool> ReadAsync()
    {
        bool more = false;
        for (int page_index = 0; page_index < PagesPerDrain; page_index++)
        {
            RoomChatHistoryPage? page = await _gateway.QueryAsync<RoomChatHistoryRequest, RoomChatHistoryPage>(
                ApplicationMemberIds.RoomChatHistory,
                new RoomChatHistoryRequest(_cursor, PageSize));
            if (page is null)
                return false;
            var accepted = new List<RoomChatEntry>();
            foreach (RoomChatEntry entry in page.Entries)
            {
                if (Accept(entry))
                    accepted.Add(entry);
            }
            long previous = _cursor;
            _cursor = page.Next;
            more = page.HasMore && _cursor > previous;
            if (accepted.Count > 0)
                Added?.Invoke(accepted);
            if (!more)
                break;
        }
        return more;
    }

    bool Accept(RoomChatEntry entry)
    {
        if (entry.Sequence <= _hidden || !_sequences.Add(entry.Sequence))
            return false;
        _order.Enqueue(entry.Sequence);
        while (_order.Count > Capacity)
            _sequences.Remove(_order.Dequeue());
        return true;
    }
}
