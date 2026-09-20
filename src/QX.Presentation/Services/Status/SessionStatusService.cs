using Qx.Game;
using Qx.Game.Application;
using Qx.Hosting;
using Qx.Interception.GEarth;
using Qx.Model;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Threading;
using ClientKind = Qx.ClientType;
using GameSession = Qx.Interception.Session;

namespace Qx.Presentation.Services.Status;

public sealed class SessionStatusService : ISessionStatusService, IAlwaysOn, IDisposable
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan ActivityInterval = TimeSpan.FromSeconds(5);

    readonly DesktopRuntime _runtime;
    readonly IGameGateway _gateway;
    readonly IScriptRunRegistry _runs;
    readonly IUiDispatcher _dispatcher;
    readonly ThrottledSignal _refresh;
    readonly CoalescingSignal _profile_changed;
    readonly SerialOperation _profile_reads = new();
    readonly IDisposable _profile_subscription;
    readonly ITimer _activity;
    readonly Lock _gate = new();
    volatile bool _stopped;
    RoomFacts _room;
    volatile bool _room_stale = true;
    string? _user_name;
    bool _mcp_running;
    int _mcp_port;
    string _mcp_failure = "";

    public SessionStatusService(DesktopRuntime runtime, IGameGateway gateway, IScriptRunRegistry runs, IUiDispatcher dispatcher, TimeProvider time)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(time);
        _refresh = new ThrottledSignal(dispatcher, time, RefreshInterval, Apply);
        _profile_changed = new CoalescingSignal(dispatcher, QueueProfileRead);
        GEarthExtension extension = runtime.Extension;
        extension.InterceptorConnected += OnSessionSignal;
        extension.InterceptorDisconnected += OnSessionSignal;
        extension.Connected += OnConnected;
        extension.Disconnected += OnSessionSignal;
        RoomManager room = runtime.Game.Room;
        room.Entered += OnRoomSignal;
        room.Ready += OnRoomSignal;
        room.Left += OnRoomSignal;
        room.RoomDataUpdated += OnRoomData;
        room.AvatarsAdded += OnAvatarsAdded;
        room.AvatarRemoved += OnAvatarChanged;
        room.FloorItemsLoaded += OnRoomSignal;
        room.WallItemsLoaded += OnRoomSignal;
        room.FloorItemAdded += OnFloorItemAdded;
        room.FloorItemRemoved += OnItemRemoved;
        room.WallItemAdded += OnWallItemAdded;
        room.WallItemRemoved += OnItemRemoved;
        runs.Changed += OnRunsChanged;
        gateway.SessionChanged += OnGatewaySession;
        _profile_subscription = gateway.SubscribeSignal(ApplicationMemberIds.ProfileChanged, _profile_changed);
        _activity = time.CreateTimer(OnActivityTick, null, ActivityInterval, ActivityInterval);
        Current = Snapshot();
    }

    public SessionStatus Current { get; private set; }

    public event Action<SessionStatus>? Changed;

    public void RefreshRuntime()
    {
        if (_stopped)
            return;
        RuntimeServiceStatus mcp = _runtime.Status.Mcp;
        int port = _runtime.Mcp.Port;
        lock (_gate)
        {
            _mcp_running = mcp.Phase == RuntimeServicePhase.Running;
            _mcp_port = port;
            _mcp_failure = mcp.Phase switch
            {
                RuntimeServicePhase.Running => "",
                RuntimeServicePhase.Disabled or RuntimeServicePhase.Pending => "",
                _ => mcp.Error?.Message ?? $"MCP server failed to start on port {port}."
            };
        }
        _refresh.Raise();
    }

    public void Dispose()
    {
        GEarthExtension extension = _runtime.Extension;
        extension.InterceptorConnected -= OnSessionSignal;
        extension.InterceptorDisconnected -= OnSessionSignal;
        extension.Connected -= OnConnected;
        extension.Disconnected -= OnSessionSignal;
        RoomManager room = _runtime.Game.Room;
        room.Entered -= OnRoomSignal;
        room.Ready -= OnRoomSignal;
        room.Left -= OnRoomSignal;
        room.RoomDataUpdated -= OnRoomData;
        room.AvatarsAdded -= OnAvatarsAdded;
        room.AvatarRemoved -= OnAvatarChanged;
        room.FloorItemsLoaded -= OnRoomSignal;
        room.WallItemsLoaded -= OnRoomSignal;
        room.FloorItemAdded -= OnFloorItemAdded;
        room.FloorItemRemoved -= OnItemRemoved;
        room.WallItemAdded -= OnWallItemAdded;
        room.WallItemRemoved -= OnItemRemoved;
        _runs.Changed -= OnRunsChanged;
        _gateway.SessionChanged -= OnGatewaySession;
        _stopped = true;
        _profile_subscription.Dispose();
        _activity.Dispose();
        _refresh.Dispose();
    }

    void OnSessionSignal() => OnRoomSignal();

    void OnConnected(GameSession session) => OnRoomSignal();

    void OnRoomSignal()
    {
        _room_stale = true;
        if (!_stopped)
            _refresh.Raise();
    }

    void OnAvatarsAdded(IReadOnlyList<Avatar> avatars) => OnRoomSignal();

    void OnRoomData(RoomData data) => OnRoomSignal();

    void OnAvatarChanged(Avatar avatar) => OnRoomSignal();

    void OnFloorItemAdded(FloorItem item) => OnRoomSignal();

    void OnWallItemAdded(WallItem item) => OnRoomSignal();

    void OnItemRemoved(Id id) => OnRoomSignal();

    void OnRunsChanged()
    {
        if (!_stopped)
            _refresh.Raise();
    }

    void OnGatewaySession()
    {
        if (!_stopped)
            _profile_changed.Raise();
    }

    void OnActivityTick(object? state) => RefreshRuntime();

    void QueueProfileRead() => _dispatcher.Post(RefreshProfileAsync);

    async Task RefreshProfileAsync()
    {
        if (_stopped)
            return;
        OperationLease lease = await _profile_reads.StartAsync(CancellationToken.None);
        try
        {
            string? name = null;
            try
            {
                ProfileStateView view = await _gateway.QueryAsync<ProfileStateRequest, ProfileStateView>(ApplicationMemberIds.ProfileState, new ProfileStateRequest(), lease.Token);
                name = view.Identity?.Name;
            }
            catch (GameUnavailableException)
            {
            }
            catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
            {
                return;
            }
            if (!_profile_reads.IsCurrent(lease) || _stopped)
                return;
            lock (_gate)
                _user_name = string.IsNullOrWhiteSpace(name) ? null : name;
            _refresh.Raise();
        }
        finally
        {
            _profile_reads.Complete(lease);
        }
    }

    void Apply()
    {
        SessionStatus next = Snapshot();
        if (next == Current)
            return;
        Current = next;
        Changed?.Invoke(next);
    }

    SessionStatus Snapshot()
    {
        GEarthExtension extension = _runtime.Extension;
        GameSession? session = extension.Session;
        RoomFacts room = RoomState();
        string? user;
        bool mcp_running;
        int mcp_port;
        string mcp_failure;
        lock (_gate)
        {
            user = _user_name;
            mcp_running = _mcp_running;
            mcp_port = _mcp_port;
            mcp_failure = _mcp_failure;
        }
        int gearth_port = extension.ConnectedPort > 0 ? extension.ConnectedPort : _runtime.Launch.GEarth.Port;
        return new SessionStatus(
            extension.IsInterceptorConnected,
            gearth_port,
            extension.IsConnected,
            ClientName(session?.Client),
            session?.HotelVersion ?? "",
            room.InRoom,
            room.Id,
            room.Name,
            room.Users,
            room.Bots,
            room.Pets,
            room.FloorItems,
            room.WallItems,
            user,
            mcp_running,
            mcp_port,
            mcp_failure,
            _runtime.Mcp.LastRequestUtc,
            _runs.RunningCount);
    }

    RoomFacts RoomState()
    {
        if (!_room_stale)
            return _room;
        _room_stale = false;
        _room = _runtime.Game.Room.Capture(Facts);
        return _room;
    }

    static RoomFacts Facts(RoomManager room)
    {
        int users = 0;
        int bots = 0;
        int pets = 0;
        foreach (Avatar avatar in room.Avatars)
        {
            switch (avatar.Type)
            {
                case AvatarType.User:
                    users++;
                    break;
                case AvatarType.Pet:
                    pets++;
                    break;
                default:
                    bots++;
                    break;
            }
        }
        long id = room.RoomId;
        string name = room.Name.Length > 0 ? room.Name : id > 0 ? "Room" : "";
        return new RoomFacts(room.IsInRoom, id, name, users, bots, pets, room.FloorItems.Count, room.WallItems.Count);
    }

    static string ClientName(ClientKind? client) => client switch
    {
        null or ClientKind.None => "",
        ClientKind.Flash => "Flash",
        _ => client.Value.ToString()
    };

    readonly record struct RoomFacts(bool InRoom, long Id, string Name, int Users, int Bots, int Pets, int FloorItems, int WallItems);
}
