using Qx.Game;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Room;

public sealed class RoomSnapshotSource : IRoomSnapshotSource, IDisposable
{
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(250);

    readonly IGameGateway _gateway;
    readonly Debouncer _settle;
    bool _attached;
    bool _disposed;

    public RoomSnapshotSource(IGameGateway gateway, IUiDispatcher dispatcher, TimeProvider time)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        _settle = new Debouncer(dispatcher, time, SettleDelay, Refresh);
    }

    public RoomSnapshot Current { get; private set; } = RoomSnapshot.Empty;

    public event Action<RoomSnapshot>? Updated;

    public void Start()
    {
        if (_disposed || _attached)
            return;
        Attach();
        _attached = true;
        Refresh();
    }

    public void Stop()
    {
        if (!_attached)
            return;
        _attached = false;
        Detach();
        _settle.Cancel();
    }

    public void Settle()
    {
        if (!_disposed && _attached)
            _settle.Trigger();
    }

    public void Refresh()
    {
        if (_disposed)
            return;
        Current = RoomReading.Read(_gateway.Game, _gateway.IsHotelConnected);
        Updated?.Invoke(Current);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
        _settle.Dispose();
    }

    void Attach()
    {
        GameState game = _gateway.Game;
        RoomManager room = game.Room;
        room.Entered += OnRoom;
        room.Ready += OnRoom;
        room.Left += OnRoom;
        room.RoomDataUpdated += OnRoomData;
        room.DetailsUpdated += OnDetails;
        room.ChatSettingsUpdated += OnChatSettings;
        room.EntryTileUpdated += OnEntryTile;
        room.VisualizationSettingsUpdated += OnVisuals;
        room.RightsLevelChanged += OnRights;
        room.AuthorityChanged += OnAuthority;
        room.AvatarsAdded += OnAvatars;
        room.AvatarRemoved += OnAvatar;
        room.AvatarUpdated += OnAvatar;
        room.FloorItemsLoaded += OnRoom;
        room.WallItemsLoaded += OnRoom;
        room.FloorItemAdded += OnFloorItem;
        room.FloorItemUpdated += OnFloorItem;
        room.FloorItemRemoved += OnItemId;
        room.WallItemAdded += OnWallItem;
        room.WallItemUpdated += OnWallItem;
        room.WallItemRemoved += OnItemId;
        game.Visitors.Changed += OnRoom;
        game.RoomActions.VisibilityChanged += OnFurni;
        game.GameData.Loaded += OnRoom;
    }

    void Detach()
    {
        GameState game = _gateway.Game;
        RoomManager room = game.Room;
        room.Entered -= OnRoom;
        room.Ready -= OnRoom;
        room.Left -= OnRoom;
        room.RoomDataUpdated -= OnRoomData;
        room.DetailsUpdated -= OnDetails;
        room.ChatSettingsUpdated -= OnChatSettings;
        room.EntryTileUpdated -= OnEntryTile;
        room.VisualizationSettingsUpdated -= OnVisuals;
        room.RightsLevelChanged -= OnRights;
        room.AuthorityChanged -= OnAuthority;
        room.AvatarsAdded -= OnAvatars;
        room.AvatarRemoved -= OnAvatar;
        room.AvatarUpdated -= OnAvatar;
        room.FloorItemsLoaded -= OnRoom;
        room.WallItemsLoaded -= OnRoom;
        room.FloorItemAdded -= OnFloorItem;
        room.FloorItemUpdated -= OnFloorItem;
        room.FloorItemRemoved -= OnItemId;
        room.WallItemAdded -= OnWallItem;
        room.WallItemUpdated -= OnWallItem;
        room.WallItemRemoved -= OnItemId;
        game.Visitors.Changed -= OnRoom;
        game.RoomActions.VisibilityChanged -= OnFurni;
        game.GameData.Loaded -= OnRoom;
    }

    void OnRoom() => Settle();

    void OnRoomData(RoomData data) => Settle();

    void OnDetails(RoomResultDetails details) => Settle();

    void OnChatSettings(RoomChatSettings settings) => Settle();

    void OnEntryTile(RoomEntryTile tile) => Settle();

    void OnVisuals(RoomVisualizationSettings settings) => Settle();

    void OnRights(int? previous, int? current) => Settle();

    void OnAuthority(RoomAuthorityState authority) => Settle();

    void OnAvatars(IReadOnlyList<Avatar> avatars) => Settle();

    void OnAvatar(Avatar avatar) => Settle();

    void OnFloorItem(FloorItem item) => Settle();

    void OnWallItem(WallItem item) => Settle();

    void OnItemId(Id id) => Settle();

    void OnFurni(Furni item) => Settle();
}
