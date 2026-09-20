using Qx.Game;
using Qx.Model;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Chat;

public sealed class ActivityLog : IAlwaysOn, IDisposable
{
    public const int Capacity = 2000;

    readonly IGameGateway _gateway;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly List<ActivityEntry> _entries = [];
    readonly Dictionary<int, (Id UserId, bool Active)> _trades = [];
    long _trade_generation = -1;
    long _sequence;
    bool _disposed;

    public ActivityLog(IGameGateway gateway, IUiDispatcher dispatcher, TimeProvider time)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        RoomManager room = gateway.Game.Room;
        room.Entered += OnEntered;
        room.RoomDataUpdated += OnRoomData;
        room.AvatarsAdded += OnArrived;
        room.AvatarRemoved += OnLeft;
        room.AvatarUpdated += OnAvatarUpdated;
    }

    public event Action<ActivityEntry>? Added;

    public event Action<ActivityEntry>? Changed;

    public event Action? Cleared;

    public IReadOnlyList<ActivityEntry> Entries => _entries;

    public static string EnteredText(string? room_name, string? owner_name)
    {
        string room = string.IsNullOrEmpty(room_name) ? "room" : room_name;
        return string.IsNullOrEmpty(owner_name) ? $"Entered {room}" : $"Entered {room} · owned by {owner_name}";
    }

    public void Enter(long room_generation, long room_id, string? room_name, string? owner_name, DateTimeOffset at) =>
        Add(new ActivityEntry(++_sequence, at, ActivityKind.Entered, EnteredText(room_name, owner_name), room_generation, room_id));

    public void Arrive(string name, DateTimeOffset at) =>
        Add(new ActivityEntry(++_sequence, at, ActivityKind.Arrived, $"{name} came in", 0, 0));

    public void Leave(string name, DateTimeOffset at) =>
        Add(new ActivityEntry(++_sequence, at, ActivityKind.Left, $"{name} left", 0, 0));

    public void ApplyRoomData(long room_generation, long room_id, string? room_name, string? owner_name)
    {
        for (int index = _entries.Count - 1; index >= 0; index--)
        {
            ActivityEntry entry = _entries[index];
            if (entry.Kind != ActivityKind.Entered || entry.RoomGeneration != room_generation || entry.RoomId != room_id)
                continue;
            ActivityEntry corrected = entry with { Text = EnteredText(room_name, owner_name) };
            _entries[index] = corrected;
            Changed?.Invoke(corrected);
            return;
        }
    }

    public void Clear()
    {
        if (_entries.Count == 0)
            return;
        _entries.Clear();
        Cleared?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        RoomManager room = _gateway.Game.Room;
        room.Entered -= OnEntered;
        room.RoomDataUpdated -= OnRoomData;
        room.AvatarsAdded -= OnArrived;
        room.AvatarRemoved -= OnLeft;
        room.AvatarUpdated -= OnAvatarUpdated;
    }

    void Add(ActivityEntry entry)
    {
        _entries.Add(entry);
        while (_entries.Count > Capacity)
            _entries.RemoveAt(0);
        Added?.Invoke(entry);
    }

    void OnEntered()
    {
        (long Generation, long RoomId, string Name, string OwnerName) snapshot = Visit();
        DateTimeOffset at = _time.GetUtcNow();
        _dispatcher.Post(() => EnterRecorded(snapshot, at));
    }

    void EnterRecorded((long Generation, long RoomId, string Name, string OwnerName) snapshot, DateTimeOffset at)
    {
        if (_disposed)
            return;
        (long Generation, long RoomId, string Name, string OwnerName) current = Visit();
        bool same_room = current.Generation == snapshot.Generation && current.RoomId == snapshot.RoomId;
        Enter(
            snapshot.Generation,
            snapshot.RoomId,
            same_room ? current.Name : snapshot.Name,
            same_room ? current.OwnerName : snapshot.OwnerName,
            at);
    }

    void OnRoomData(RoomData data)
    {
        (long Generation, long RoomId) scope = _gateway.Game.Room.Capture(room => (room.Generation, room.RoomId));
        if (scope.RoomId != data.Id)
            return;
        string name = data.Name;
        string owner_name = data.OwnerName;
        _dispatcher.Post(() =>
        {
            if (!_disposed)
                ApplyRoomData(scope.Generation, scope.RoomId, name, owner_name);
        });
    }

    void OnArrived(IReadOnlyList<Avatar> avatars)
    {
        if (!_gateway.Game.Room.Capture(room => room.IsReady))
            return;
        string[] users = [.. avatars.OfType<User>().Select(user => user.Name)];
        if (users.Length == 0)
            return;
        DateTimeOffset at = _time.GetUtcNow();
        _dispatcher.Post(() =>
        {
            if (_disposed)
                return;
            foreach (string name in users)
                Arrive(name, at);
        });
    }

    void OnLeft(Avatar avatar)
    {
        if (avatar is not User user)
            return;
        string name = user.Name;
        int index = user.Index;
        Id id = user.Id;
        DateTimeOffset at = _time.GetUtcNow();
        _dispatcher.Post(() =>
        {
            if (_disposed)
                return;
            if (_trades.TryGetValue(index, out var trade) && trade.UserId == id)
                _trades.Remove(index);
            Leave(name, at);
        });
    }

    void OnAvatarUpdated(Avatar avatar)
    {
        if (avatar is not User || avatar.CurrentUpdate is not { } status)
            return;
        (long generation, bool ready) = _gateway.Game.Room.Capture(room => (room.Generation, room.IsReady));
        int index = avatar.Index;
        Id id = avatar.Id;
        string name = avatar.Name;
        bool active = status.IsTrading;
        DateTimeOffset at = _time.GetUtcNow();
        _dispatcher.Post(() =>
        {
            if (_disposed)
                return;
            if (_trade_generation != generation)
            {
                _trades.Clear();
                _trade_generation = generation;
            }
            bool changed = _trades.TryGetValue(index, out var previous) &&
                previous.UserId == id && previous.Active != active;
            _trades[index] = (id, active);
            if (ready && changed)
                Add(new ActivityEntry(++_sequence, at, ActivityKind.Trade,
                    $"{name} {(active ? "started" : "stopped")} trading", generation, 0));
        });
    }

    (long Generation, long RoomId, string Name, string OwnerName) Visit() =>
        _gateway.Game.Room.Capture(room => (room.Generation, room.RoomId, room.Name, room.OwnerName));
}
