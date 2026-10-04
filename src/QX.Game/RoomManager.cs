using Qx.Messages;
using Qx.Game.Protocol;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace Qx.Game;

/// <summary>Specifies the state of the user's room session.</summary>
public enum RoomSessionState
{
    /// <summary>No room session.</summary>
    Outside,
    /// <summary>A room session that has begun and is still loading.</summary>
    Entering,
    /// <summary>A room session that the user has entered and the server has reported ready.</summary>
    Ready,
    /// <summary>A room session that is being ended.</summary>
    Leaving
}

/// <summary>Represents a kick of the user from a room by the room owner or staff.</summary>
/// <param name="RoomId">The room the kick was received in, or 0 when no room was tracked.</param>
/// <param name="ErrorCode">The generic error code that carried the kick.</param>
/// <param name="WasEntered">Whether the room had been fully entered when the kick arrived.</param>
public sealed record RoomKick(Id RoomId, int ErrorCode, bool WasEntered);

internal enum RoomPlacementCommitKind
{
    FloorAdded,
    FloorUpdated,
    FloorRemoved,
    WallAdded,
    WallUpdated,
    WallRemoved,
    RoomReset
}

internal sealed record RoomPlacementCommitItem(
    Id RoomItemId,
    ItemType ItemKind,
    Point? FloorPosition,
    int? Direction,
    WallLocation? WallPosition);

internal sealed record RoomPlacementStateCommit(
    RoomPlacementCommitKind Kind,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    RoomPlacementCommitItem? Previous,
    RoomPlacementCommitItem? Current,
    Id? PickerId,
    bool? IsExpired,
    int? Delay);

internal sealed record RoomPickupConfirmationCommit(
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int Category,
    Id RoomItemId,
    string Title,
    string Body);

/// <summary>Manages the state of the room the user is in.</summary>
/// <remarks>
/// <para>
/// The manager follows room access, entry, furni, avatars, rights, settings and Fx bar values from
/// the messages the hotel sends. The state is cleared when the user leaves the room and when the
/// hotel connection closes.
/// </para>
/// <para>
/// All members are safe to call from any thread. Use <see cref="Capture{TResult}"/> to read several
/// members as one consistent view.
/// </para>
/// <para>
/// Events are raised in order after a change has been applied and outside the state lock. Avatars,
/// items, item data and room data passed to handlers are copies taken when the change was applied.
/// </para>
/// </remarks>
public sealed class RoomManager : GameStateManager
{
    private const int KickedByOwnerError = 4008;
    private readonly Dictionary<Id, AreaHideData> pending_hidden_areas = [];

    private readonly ConcurrentDictionary<long, FloorItem> _floorItems = [];
    private readonly ConcurrentDictionary<long, WallItem> _wallItems = [];
    private readonly ConcurrentDictionary<int, Avatar> _avatars = [];
    private readonly Dictionary<(bool IsUserFx, int ConfigId), VariableFxConfigEntry> _variable_fx_configs = [];
    private readonly Dictionary<VariableFxSlot, VariableFxValue> _variable_fx_values = [];
    private readonly ConcurrentDictionary<long, GuestRoomResult> _pending_room_results = [];
    private readonly Dictionary<string, string> _properties = new(StringComparer.Ordinal);
    private readonly Queue<Action> _publication_queue = [];
    private readonly object _publication_sync = new();
    private readonly object _state_sync = new();
    private List<Action>? _staged_publications;
    private bool _publication_draining;
    private long _revision;
    private int _mutation_depth;
    private bool _room_ready_received;
    private RoomKick? _pending_kick;
    private CancellationTokenSource _session_end = new();
    private TaskCompletionSource _next_change = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets whether the user has entered the current room.</summary>
    /// <remarks>Set when the server confirms the entry, before the room has finished loading.</remarks>
    public bool IsInRoom { get; private set; }
    /// <summary>Gets whether the room session is <see cref="RoomSessionState.Ready"/>.</summary>
    public bool IsReady => State is RoomSessionState.Ready;
    /// <summary>Gets the state of the room session.</summary>
    public RoomSessionState State { get; private set; }
    /// <summary>Gets the number of the current room session, which increases each time a room session begins or ends.</summary>
    public long Generation { get; private set; }
    /// <summary>Gets the revision of the room state, which increases with every change the manager applies.</summary>
    /// <remarks>
    /// Movements and Fx bar values record the revision they were applied at, so a revision read
    /// before sending a command tells the changes it caused from older ones.
    /// </remarks>
    public long Revision => Interlocked.Read(ref _revision);

    /// <summary>Gets a token that is canceled when the current room session ends.</summary>
    /// <remarks>
    /// The token is canceled on leaving the room and on entering another or the same room again.
    /// Capture it together with <see cref="Generation"/> through <see cref="Capture{TResult}"/> to
    /// bind work to one session.
    /// </remarks>
    public CancellationToken SessionToken
    {
        get
        {
            lock (_state_sync)
                return _session_end.Token;
        }
    }

    /// <summary>Gets a task that completes after the next change to the room state has been applied and its events have run.</summary>
    /// <remarks>
    /// Every message the manager handles counts as a change, even one that leaves the state as it
    /// was. Reading the property again after the task completes returns the task for the change after that.
    /// </remarks>
    public Task NextChange => Volatile.Read(ref _next_change).Task;

    /// <summary>Gets the user's own avatar, or <see langword="null"/> while it is not in the room.</summary>
    public Avatar? Self => OwnUserId?.Invoke() is { } id
        ? _avatars.Values.FirstOrDefault(avatar => avatar is User && avatar.Id == id)
        : null;
    /// <summary>Gets the id of the current room, or 0 when there is no room session.</summary>
    public long RoomId { get; private set; }
    /// <summary>Gets the state of the user's attempt to enter a room.</summary>
    public RoomAccessState AccessState { get; private set; }
    /// <summary>Gets the id of the room that <see cref="AccessState"/> refers to, or <see langword="null"/> when it refers to none.</summary>
    public Id? AccessRoomId { get; private set; }
    /// <summary>Gets the latest queue status of the room being entered, or <see langword="null"/> when there is none.</summary>
    /// <remarks>Cleared whenever an access state other than <see cref="RoomAccessState.Queued"/> is recorded.</remarks>
    public RoomQueueStatus? QueueStatus { get; private set; }
    /// <summary>Gets the user's position in the active room queue, or <see langword="null"/> when the user is not queued.</summary>
    public int? QueuePosition => QueueStatus?.Position;
    /// <summary>Gets whether the user is waiting at the doorbell of the room being entered.</summary>
    public bool IsRingingDoorbell => AccessState is RoomAccessState.RingingDoorbell;
    /// <summary>Gets whether the user is waiting in the queue of the room being entered.</summary>
    public bool IsInQueue => AccessState is RoomAccessState.Queued;
    /// <summary>Gets the connection failure reported with the current access state, or <see langword="null"/> when the state is not a connection failure.</summary>
    public RoomConnectionFailure? ConnectionFailure { get; private set; }
    /// <summary>Gets how the last room session ended, or <see langword="null"/> when no room session has ended.</summary>
    public RoomExitState? LastExit { get; private set; }
    /// <summary>Gets the most recent kick of the user in the current or a previous room session, or <see langword="null"/> when there was none.</summary>
    /// <remarks>Cleared when a new room session begins.</remarks>
    public RoomKick? LastKick { get; private set; }
    /// <summary>Gets the kick that caused <see cref="LastExit"/>, or <see langword="null"/> when the last room exit was not caused by a kick.</summary>
    public RoomKick? LastExitKick => LastExit?.Kick;
    /// <summary>Gets whether the last room exit was caused by the user being kicked.</summary>
    public bool WasKicked => LastExit?.WasKicked ?? false;
    /// <summary>Gets the room type sent with the room ready message, or an empty string before it arrives.</summary>
    public string RoomType { get; private set; } = "";
    /// <summary>Gets whether the user owns the current room.</summary>
    public bool IsOwner { get; private set; }
    /// <summary>Gets the user's rights level in the current room, or <see langword="null"/> when the server has not sent it.</summary>
    /// <remarks>A revoke of the user's rights sets the level to 0.</remarks>
    public int? RightsLevel { get; private set; }
    /// <summary>Gets whether the user's rights in the current room are known.</summary>
    /// <remarks>They are known when the user owns the room or once a rights level has arrived.</remarks>
    public bool RightsAreKnown => IsOwner || RightsLevel.HasValue;
    /// <summary>Gets whether the user owns the current room or has a rights level above 0.</summary>
    public bool HasRights => IsOwner || RightsLevel is > 0;
    /// <summary>Gets whether the user is spectating the current room, or <see langword="null"/> when the server has not said.</summary>
    public bool? IsSpectating { get; private set; }
    /// <summary>Gets the data of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public RoomData? Data { get; private set; }
    /// <summary>Gets the details of the current room, or <see langword="null"/> while they have not arrived.</summary>
    public RoomResultDetails? Details { get; private set; }
    /// <summary>Gets the entry tile of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public RoomEntryTile? EntryTile { get; private set; }
    /// <summary>Gets the visualization settings of the current room, or <see langword="null"/> while they have not arrived.</summary>
    public RoomVisualizationSettings? VisualizationSettings { get; private set; }
    /// <summary>Gets the chat settings of the current room, or <see langword="null"/> while they have not arrived.</summary>
    /// <remarks>Set from the room details and from the chat settings message, whichever arrives last.</remarks>
    public RoomChatSettings? ChatSettings { get; private set; }
    /// <summary>
    /// Gets the game data used to fill in the identifier and size of room furni, or
    /// <see langword="null"/> until a <see cref="GameState"/> attaches the manager.
    /// </summary>
    /// <remarks>
    /// It is the <see cref="GameState.GameData"/> of that game state. Furni already in the room are
    /// filled in again whenever it loads for the current session. An identifier the server sent is
    /// kept, and sizes are set on floor items only.
    /// </remarks>
    public GameData? GameData { get; internal set; }
    internal Func<Id?>? OwnUserId { get; set; }
    /// <summary>Gets whether the data of the current room has been received.</summary>
    public bool DataIsLoaded { get; private set; }
    /// <summary>Gets whether the details of the current room have been received.</summary>
    public bool DetailsAreLoaded { get; private set; }
    /// <summary>Gets whether the entry tile of the current room has been received.</summary>
    public bool EntryTileIsLoaded { get; private set; }
    /// <summary>Gets whether at least one room property has been received.</summary>
    public bool PropertiesHaveBeenReceived { get; private set; }
    /// <summary>Gets whether the visualization settings of the current room have been received.</summary>
    public bool VisualizationSettingsAreLoaded { get; private set; }
    /// <summary>Gets whether the chat settings of the current room have been received.</summary>
    public bool ChatSettingsAreLoaded { get; private set; }
    /// <summary>Gets whether at least one batch of avatars has been received.</summary>
    public bool AvatarsAreLoaded { get; private set; }
    /// <summary>Gets whether at least one batch of floor items has been received.</summary>
    public bool FloorItemsAreLoaded { get; private set; }
    /// <summary>Gets whether at least one batch of wall items has been received.</summary>
    public bool WallItemsAreLoaded { get; private set; }
    /// <summary>Gets whether the list of users with rights in the current room has been received.</summary>
    public bool ControllersAreLoaded { get; private set; }
    /// <summary>Gets whether the floor plan of the current room has been received.</summary>
    public bool FloorPlanIsLoaded { get; private set; }
    /// <summary>Gets whether the heightmap of the current room has been received.</summary>
    public bool HeightmapIsLoaded { get; private set; }

    /// <summary>Gets the name of the current room, or an empty string while the room data has not arrived.</summary>
    public string Name => Data?.Name ?? "";
    /// <summary>Gets the name of the owner of the current room, or an empty string while the room data has not arrived.</summary>
    public string OwnerName => Data?.OwnerName ?? "";
    /// <summary>Gets the description of the current room, or an empty string while the room data has not arrived.</summary>
    public string Description => Data?.Description ?? "";
    /// <summary>Gets the score of the current room, or 0 while the room data has not arrived.</summary>
    public int Score => Data?.Score ?? 0;
    /// <summary>Gets the id of the group of the current room, or 0 when the room has no group.</summary>
    public Id GroupId => Data is { HasGroup: true } data ? data.GroupId : 0;
    /// <summary>Gets the name of the group of the current room, or an empty string while the room data has not arrived.</summary>
    public string GroupName => Data?.GroupName ?? "";
    /// <summary>Gets whether an event is running in the current room.</summary>
    public bool HasEvent => Data?.HasEvent ?? false;
    /// <summary>Gets the name of the event in the current room, or an empty string while the room data has not arrived.</summary>
    public string EventName => Data?.EventName ?? "";
    /// <summary>Gets the description of the event in the current room, or an empty string while the room data has not arrived.</summary>
    public string EventDescription => Data?.EventDescription ?? "";
    /// <summary>Gets the tags of the current room, or an empty list while the room data has not arrived.</summary>
    public IReadOnlyList<string> Tags => Data?.Tags ?? [];
    /// <summary>Gets the users with rights in the current room, as last listed by the server.</summary>
    public IReadOnlyList<IdName> Controllers { get; private set; } = [];
    /// <summary>Gets the floor plan of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public FloorPlan? FloorPlan { get; private set; }
    /// <summary>Gets the heightmap of the current room, or <see langword="null"/> while it has not arrived.</summary>
    /// <remarks>Height updates from the server are applied to the same instance.</remarks>
    public Heightmap? Heightmap { get; private set; }
    /// <summary>
    /// Gets the user's ownership, rights and spectator state in the current room, together with the
    /// room's mute state and moderation levels, as one value.
    /// </summary>
    /// <remarks>Read it through <see cref="Capture{TResult}"/> to take every member from the same state.</remarks>
    public RoomAuthorityState Authority
    {
        get
        {
            RoomResultDetails? details = Details;
            return new RoomAuthorityState(
                IsOwner,
                RightsLevel,
                RightsAreKnown,
                HasRights,
                IsSpectating,
                details?.IsRoomMuted,
                details?.CanMute,
                details?.Moderation.Mute,
                details?.Moderation.Kick,
                details?.Moderation.Ban);
        }
    }
    /// <summary>Gets the decoration, layout and chat rules of the current room as one value.</summary>
    /// <remarks>
    /// The value is detached: its property map and chat settings are copies that do not change with
    /// the room. Read it through <see cref="Capture{TResult}"/> to take every member from the same state.
    /// </remarks>
    public RoomEnvironmentState Environment => new(
        EntryTile,
        Properties,
        FloorProperty,
        WallpaperProperty,
        LandscapeProperty,
        AnimatedLandscapeProperty,
        VisualizationSettings,
        ChatSettings is { } chat ? RoomObjectSnapshot.Copy(chat) : null);
    /// <summary>Gets a copy of the properties of the current room, such as <c>floor</c>, <c>wallpaper</c> and <c>landscape</c>, keyed by name.</summary>
    public IReadOnlyDictionary<string, string> Properties
    {
        get
        {
            lock (_state_sync)
                return new Dictionary<string, string>(_properties, StringComparer.Ordinal);
        }
    }
    /// <summary>Gets the <c>floor</c> property of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public string? FloorProperty => Property("floor");
    /// <summary>Gets the <c>wallpaper</c> property of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public string? WallpaperProperty => Property("wallpaper");
    /// <summary>Gets the <c>landscape</c> property of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public string? LandscapeProperty => Property("landscape");
    /// <summary>Gets the <c>landscapeanim</c> property of the current room, or <see langword="null"/> while it has not arrived.</summary>
    public string? AnimatedLandscapeProperty => Property("landscapeanim");

    /// <summary>Gets the floor items in the current room.</summary>
    /// <remarks>
    /// The collection is a copy, but the items in it are the instances the manager keeps updating.
    /// An item's <see cref="Qx.Model.Furni.IsRemoved"/> becomes <see langword="true"/> once it leaves the room or is replaced.
    /// </remarks>
    public IReadOnlyCollection<FloorItem> FloorItems => _floorItems.Values.ToArray();
    /// <summary>Gets the wall items in the current room.</summary>
    /// <remarks>
    /// The collection is a copy, but the items in it are the instances the manager keeps updating.
    /// An item's <see cref="Qx.Model.Furni.IsRemoved"/> becomes <see langword="true"/> once it leaves the room or is replaced.
    /// </remarks>
    public IReadOnlyCollection<WallItem> WallItems => _wallItems.Values.ToArray();
    /// <summary>Gets the avatars in the current room, which are users, pets and bots.</summary>
    /// <remarks>
    /// The collection is a copy, but the avatars in it are the instances the manager keeps updating.
    /// An avatar's <see cref="Avatar.IsRemoved"/> becomes <see langword="true"/> once it leaves the room or is replaced.
    /// </remarks>
    public IReadOnlyCollection<Avatar> Avatars => _avatars.Values.ToArray();

    /// <summary>Gets the Fx bar configurations of the current room, for avatars and for furni.</summary>
    public IReadOnlyList<VariableFxConfigEntry> VariableFxConfigs
    {
        get
        {
            lock (_state_sync)
                return [.. _variable_fx_configs.Values];
        }
    }

    /// <summary>Gets every Fx bar value in the current room, bound to its current configuration.</summary>
    public IReadOnlyList<VariableFxValue> VariableFxValues
    {
        get
        {
            lock (_state_sync)
                return [.. _variable_fx_values.Values.Select(BindVariableFx)];
        }
    }

    /// <summary>Occurs when a new room session begins, with the id of the room being entered.</summary>
    public event Action<Id>? Entering;
    /// <summary>Occurs when the server confirms that the user has entered the room.</summary>
    public event Action? Entered;
    /// <summary>Occurs when the server sends the room ready message for the room being entered.</summary>
    public event Action? Ready;
    /// <summary>Occurs when the current room session starts to end.</summary>
    /// <remarks>The room state has already been cleared when handlers run.</remarks>
    public event Action? Leaving;
    /// <summary>Occurs when the current room session has ended.</summary>
    /// <remarks>Raised after <see cref="Exited"/>.</remarks>
    public event Action? Left;
    /// <summary>Occurs when the current room session has ended, with how it ended.</summary>
    public event Action<RoomExitState>? Exited;
    /// <summary>Occurs when the server reports that the user was kicked from the room.</summary>
    /// <remarks>Raised as soon as the kick is reported, which is before the room exit itself arrives.</remarks>
    public event Action<RoomKick>? Kicked;
    /// <summary>Occurs when the data of the current room arrives or changes.</summary>
    public event Action<RoomData>? RoomDataUpdated;
    /// <summary>Occurs when a batch of floor items has been added to the room.</summary>
    /// <remarks>The hotel sends further batches after entry, for example for temporary furni.</remarks>
    public event Action? FloorItemsLoaded;
    /// <summary>Occurs when a batch of wall items has been added to the room.</summary>
    public event Action? WallItemsLoaded;
    /// <summary>Occurs when avatars are added to the room, with the added avatars.</summary>
    /// <remarks>Raised for the avatars already in the room on entry and for every later arrival.</remarks>
    public event Action<IReadOnlyList<Avatar>>? AvatarsAdded;
    /// <summary>Occurs when a floor item is placed in the room.</summary>
    public event Action<FloorItem>? FloorItemAdded;
    /// <summary>Occurs when a floor item is removed from the room, with the id of the item.</summary>
    public event Action<Id>? FloorItemRemoved;
    /// <summary>Occurs when a floor item is removed from the room, with the item as it was.</summary>
    public event Action<FloorItem>? FloorItemRemovedDetailed;
    /// <summary>Occurs when a wall item is placed in the room.</summary>
    public event Action<WallItem>? WallItemAdded;
    /// <summary>Occurs when a wall item is removed from the room, with the id of the item.</summary>
    public event Action<Id>? WallItemRemoved;
    /// <summary>Occurs when a wall item is removed from the room, with the item as it was.</summary>
    public event Action<WallItem>? WallItemRemovedDetailed;
    /// <summary>Occurs when an avatar leaves the room.</summary>
    /// <remarks>Also raised when a user reappears at a new room index and the entry at the old index is dropped.</remarks>
    public event Action<Avatar>? AvatarRemoved;
    /// <summary>Occurs when any tracked state of an avatar is updated, such as its position, dance or figure.</summary>
    public event Action<Avatar>? AvatarUpdated;
    /// <summary>Occurs when a floor item is updated, moved or its data changes.</summary>
    public event Action<FloorItem>? FloorItemUpdated;
    /// <summary>Occurs when a wall item is updated, moved or its data changes.</summary>
    public event Action<WallItem>? WallItemUpdated;
    /// <summary>Occurs when an avatar performs an expression, such as a wave, with the expression id.</summary>
    public event Action<Avatar, int>? AvatarActioned;
    /// <summary>Occurs when an avatar moves to a different tile, with the previous and the current tile.</summary>
    public event Action<Avatar, Tile, Tile>? AvatarMoved;
    /// <summary>Occurs for every walk update, roller slide and wired move of an avatar, with the movement.</summary>
    /// <remarks>Unlike <see cref="AvatarMoved"/>, it is raised even when the avatar stays on the same tile.</remarks>
    public event Action<Avatar, AvatarMovement>? AvatarMovementReceived;
    /// <summary>Occurs for every roller slide, wired move, move and rotation of a floor item, with the movement.</summary>
    /// <remarks>Unlike <see cref="FloorItemMoved"/>, roller and wired moves are reported even when the item stays on the same tile.</remarks>
    public event Action<FloorItem, FloorItemMovement>? FloorItemMovementReceived;
    /// <summary>Occurs when an Fx bar value arrives or the configuration it is drawn with changes, with the current and the previous value.</summary>
    /// <remarks>The previous value is <see langword="null"/> when the value is new.</remarks>
    public event Action<VariableFxValue, VariableFxValue?>? VariableFxChanged;
    /// <summary>Occurs when an Fx bar value is removed, including when its avatar or furni leaves the room.</summary>
    public event Action<VariableFxValue>? VariableFxRemoved;
    /// <summary>Occurs when an avatar's dance changes, with the previous and the current dance.</summary>
    public event Action<Avatar, int, int>? AvatarDanceChanged;
    /// <summary>Occurs when an avatar's effect changes, with the previous and the current effect.</summary>
    public event Action<Avatar, int, int>? AvatarEffectChanged;
    /// <summary>Occurs when an avatar's hand item changes, with the previous and the current hand item.</summary>
    public event Action<Avatar, int, int>? AvatarHandItemChanged;
    /// <summary>Occurs when an avatar falls asleep or wakes up, with the previous and the current idle state.</summary>
    public event Action<Avatar, bool, bool>? AvatarIdleChanged;
    /// <summary>Occurs when an avatar starts or stops typing, with the previous and the current typing state.</summary>
    public event Action<Avatar, bool, bool>? AvatarTypingChanged;
    /// <summary>Occurs when an avatar's figure or motto changes, with the previous figure, the current figure, the previous motto and the current motto.</summary>
    /// <remarks>Also raised when a pet's figure changes, with its motto passed twice.</remarks>
    public event Action<Avatar, string, string, string, string>? AvatarIdentityChanged;
    /// <summary>Occurs when a floor item moves to a different tile, with the previous and the current tile.</summary>
    public event Action<FloorItem, Tile, Tile>? FloorItemMoved;
    /// <summary>Occurs when a wall item moves, with the previous and the current location.</summary>
    public event Action<WallItem, WallLocation, WallLocation>? WallItemMoved;
    /// <summary>Occurs when a floor item's data changes, with the previous and the current data.</summary>
    public event Action<FloorItem, ItemData, ItemData>? FloorItemDataChanged;
    /// <summary>Occurs when a wall item's data changes, with the previous and the current data string.</summary>
    /// <remarks>The item's <see cref="Qx.Model.WallItem.State"/> changes with its data.</remarks>
    public event Action<WallItem, string, string>? WallItemDataChanged;
    /// <summary>Occurs when a user in the room is renamed, with the previous and the new name.</summary>
    public event Action<Avatar, string, string>? AvatarNameChanged;
    /// <summary>Occurs when an avatar talks, shouts or whispers in the room.</summary>
    public event Action<AvatarChat>? Chat;
    /// <summary>Occurs when the user's room access state changes.</summary>
    public event Action<RoomAccessTransition>? AccessStateChanged;
    /// <summary>Occurs when a queue status for the room being entered arrives.</summary>
    public event Action<RoomQueueStatus>? QueueUpdated;
    /// <summary>Occurs when the server refuses a room connection.</summary>
    public event Action<CanNotConnect>? ConnectionFailed;
    /// <summary>Occurs when a doorbell message arrives.</summary>
    /// <remarks>
    /// A message with a user name reports someone ringing at the current room. A message with an
    /// empty user name reports that the user is waiting at the doorbell.
    /// </remarks>
    public event Action<Doorbell>? DoorbellRang;
    /// <summary>Occurs when the server grants room access, to the user or to someone at the doorbell.</summary>
    public event Action<FlatAccessible>? AccessGranted;
    /// <summary>Occurs when the server denies room access, to the user or to someone at the doorbell.</summary>
    public event Action<FlatAccessDenied>? AccessDenied;
    /// <summary>Occurs when the details of the current room arrive.</summary>
    public event Action<RoomResultDetails>? DetailsUpdated;
    /// <summary>Occurs when the entry tile of the current room arrives.</summary>
    public event Action<RoomEntryTile>? EntryTileUpdated;
    /// <summary>Occurs when a property of the current room, such as the floor or wallpaper, arrives.</summary>
    public event Action<FlatProperty>? PropertyUpdated;
    /// <summary>Occurs when the visualization settings of the current room arrive.</summary>
    public event Action<RoomVisualizationSettings>? VisualizationSettingsUpdated;
    /// <summary>Occurs when the chat settings of the current room arrive.</summary>
    public event Action<RoomChatSettings>? ChatSettingsUpdated;
    /// <summary>Occurs when a member of <see cref="Authority"/> changes in the current room, with the new value.</summary>
    /// <remarks>
    /// Besides changes of the user's ownership, rights and spectator state, it is raised when a guest
    /// room result for the current room changes the room's mute state, whether the user may mute
    /// others or the moderation levels. The room result applied as the room is entered and the reset
    /// on leaving do not raise it, so read <see cref="Authority"/> once the room is entered to get the
    /// starting value.
    /// </remarks>
    public event Action<RoomAuthorityState>? AuthorityChanged;
    /// <summary>Occurs when the user's rights level in the current room changes, with the previous and the current level.</summary>
    public event Action<int?, int?>? RightsLevelChanged;
    /// <summary>Occurs when the user's spectator state in the current room changes, with the previous and the current state.</summary>
    public event Action<bool?, bool?>? SpectatingChanged;
    internal event Action<RoomPlacementStateCommit>? PlacementStateCommitted;
    internal event Action<RoomPickupConfirmationCommit>? PickupConfirmationReceived;

    private void Patch(int index, Action<Avatar> update)
    {
        if (!_avatars.TryGetValue(index, out Avatar? avatar))
            return;
        update(avatar);
        Publish(AvatarUpdated, avatar);
    }

    private void PatchPet(int index, Action<Pet> update)
    {
        if (!_avatars.TryGetValue(index, out Avatar? avatar) || avatar is not Pet pet)
            return;
        update(pet);
        Publish(AvatarUpdated, pet);
    }

    private void SetWallItemData(Id id, string data)
    {
        if (!_wallItems.TryGetValue(id, out WallItem? item))
            return;
        string previous = item.Data;
        item.Data = data;
        Publish(WallItemDataChanged, item, previous, item.Data);
        Publish(WallItemUpdated, item);
    }

    private void replace_hidden_areas(IEnumerable<AreaHideData> areas)
    {
        if (FloorPlan is not { } previous) return;
        FloorPlan = new FloorPlan(previous.Map)
        {
            UseLegacyScale = previous.UseLegacyScale,
            WallHeight = previous.WallHeight,
            CameraX = previous.CameraX,
            CameraY = previous.CameraY,
            CameraZ = previous.CameraZ,
            HasCameraData = previous.HasCameraData,
            HiddenAreas = Array.AsReadOnly(areas.ToArray())
        };
    }

    private FloorItem? RemoveFloorItem(Id id)
    {
        if (!_floorItems.Remove(id, out FloorItem? item))
            return null;
        item.IsRemoved = true;
        DropVariableFx(false, id);
        Publish(FloorItemRemoved, id);
        Publish(FloorItemRemovedDetailed, item);
        return item;
    }

    private void ApplyFloorItems(FloorItems message)
    {
        foreach (FloorItem item in message.Items)
        {
            Enrich(item);
            item.IsRemoved = false;
            if (_floorItems.TryGetValue(item.Id, out FloorItem? previous) &&
                !ReferenceEquals(previous, item))
            {
                previous.IsRemoved = true;
            }
            _floorItems[item.Id] = item;
        }
        FloorItemsAreLoaded = true;
        Publish(FloorItemsLoaded);
    }

    private void ApplyHeightmap(Heightmap message)
    {
        Heightmap = message;
        HeightmapIsLoaded = true;
    }

    private WallItem? RemoveWallItem(Id id)
    {
        if (!_wallItems.Remove(id, out WallItem? item))
            return null;
        item.IsRemoved = true;
        Publish(WallItemRemoved, id);
        Publish(WallItemRemovedDetailed, item);
        return item;
    }

    private void PublishPlacement(
        RoomPlacementCommitKind kind,
        long session_generation,
        RoomPlacementCommitItem? previous,
        RoomPlacementCommitItem? current,
        Id? picker_id = null,
        bool? is_expired = null,
        int? delay = null)
    {
        Publish(PlacementStateCommitted, new RoomPlacementStateCommit(
            kind,
            session_generation,
            RoomId,
            Generation,
            checked(_revision + 1),
            previous,
            current,
            picker_id,
            is_expired,
            delay));
    }

    private void PublishPickupConfirmation(PickupConfirmation message, long session_generation)
    {
        Publish(PickupConfirmationReceived, new RoomPickupConfirmationCommit(
            session_generation,
            RoomId,
            Generation,
            checked(_revision + 1),
            message.Category,
            message.ItemId,
            message.Title,
            message.Body));
    }

    private static RoomPlacementCommitItem PlacementItem(FloorItem item) => new(
        item.Id,
        ItemType.Floor,
        new Point(item.X, item.Y),
        item.Direction,
        null);

    private static RoomPlacementCommitItem PlacementItem(WallItem item) => new(
        item.Id,
        ItemType.Wall,
        null,
        null,
        item.Location);

    /// <summary>
    /// Applies a state-only furni update, which the client performs by writing the new state and
    /// replacing the furni's stuff data with an empty one. QX derives
    /// <see cref="Model.FloorItem.State"/> from the data instead, so the state is carried in a
    /// fresh <see cref="LegacyData"/> value.
    /// </summary>
    /// <param name="id">The floor item to update.</param>
    /// <param name="state">The state the server reported.</param>
    private void SetFloorItemState(Id id, int state)
    {
        if (!_floorItems.TryGetValue(id, out FloorItem? item))
            return;
        ItemData previous = item.Data;
        item.Data = new LegacyData { Value = state.ToString(CultureInfo.InvariantCulture) };
        Publish(FloorItemDataChanged, item, previous, item.Data);
        Publish(FloorItemUpdated, item);
    }

    private void Mutate(Action mutation)
    {
        bool drain_publications = false;
        bool completed = false;
        ExceptionDispatchInfo? mutation_failure = null;
        lock (_state_sync)
        {
            bool outer = _mutation_depth++ == 0;
            if (outer)
            {
                Interlocked.Increment(ref _revision);
                _staged_publications = [];
            }
            try
            {
                mutation();
            }
            catch (Exception error)
            {
                mutation_failure = ExceptionDispatchInfo.Capture(error);
            }
            finally
            {
                if (--_mutation_depth == 0)
                {
                    Interlocked.Increment(ref _revision);
                    drain_publications = QueuePublications(_staged_publications!);
                    _staged_publications = null;
                    completed = true;
                }
            }
        }

        ExceptionDispatchInfo? publication_failure = null;
        if (drain_publications)
        {
            try
            {
                DrainPublications();
            }
            catch (Exception error)
            {
                publication_failure = ExceptionDispatchInfo.Capture(error);
            }
        }

        if (completed)
        {
            Interlocked.Exchange(
                ref _next_change,
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
        }

        if (mutation_failure is not null && publication_failure is not null)
            throw new AggregateException(mutation_failure.SourceException, publication_failure.SourceException);
        mutation_failure?.Throw();
        publication_failure?.Throw();
    }

    private bool QueuePublications(IReadOnlyList<Action> publications)
    {
        if (publications.Count == 0)
            return false;

        lock (_publication_sync)
        {
            foreach (Action publication in publications)
                _publication_queue.Enqueue(publication);
            if (_publication_draining)
                return false;
            _publication_draining = true;
            return true;
        }
    }

    private void DrainPublications()
    {
        ExceptionDispatchInfo? failure = null;
        while (true)
        {
            Action publication;
            lock (_publication_sync)
            {
                if (_publication_queue.Count == 0)
                {
                    _publication_draining = false;
                    break;
                }
                publication = _publication_queue.Dequeue();
            }

            try
            {
                publication();
            }
            catch (Exception error)
            {
                failure ??= ExceptionDispatchInfo.Capture(error);
            }
        }
        failure?.Throw();
    }

    private void Publish(Action? listeners)
    {
        if (listeners is not null)
            (_staged_publications ?? throw new InvalidOperationException()).Add(listeners);
    }

    private void Publish<T>(Action<T>? listeners, T value)
    {
        if (listeners is not null)
        {
            T snapshot = EventSnapshot(value);
            Publish(() => listeners(snapshot));
        }
    }

    private void Publish<T1, T2>(Action<T1, T2>? listeners, T1 first, T2 second)
    {
        if (listeners is not null)
        {
            T1 first_snapshot = EventSnapshot(first);
            T2 second_snapshot = EventSnapshot(second);
            Publish(() => listeners(first_snapshot, second_snapshot));
        }
    }

    private void Publish<T1, T2, T3>(
        Action<T1, T2, T3>? listeners,
        T1 first,
        T2 second,
        T3 third)
    {
        if (listeners is not null)
        {
            T1 first_snapshot = EventSnapshot(first);
            T2 second_snapshot = EventSnapshot(second);
            T3 third_snapshot = EventSnapshot(third);
            Publish(() => listeners(first_snapshot, second_snapshot, third_snapshot));
        }
    }

    private void Publish<T1, T2, T3, T4, T5>(
        Action<T1, T2, T3, T4, T5>? listeners,
        T1 first,
        T2 second,
        T3 third,
        T4 fourth,
        T5 fifth)
    {
        if (listeners is not null)
        {
            T1 first_snapshot = EventSnapshot(first);
            T2 second_snapshot = EventSnapshot(second);
            T3 third_snapshot = EventSnapshot(third);
            T4 fourth_snapshot = EventSnapshot(fourth);
            T5 fifth_snapshot = EventSnapshot(fifth);
            Publish(() => listeners(
                first_snapshot,
                second_snapshot,
                third_snapshot,
                fourth_snapshot,
                fifth_snapshot));
        }
    }

    private static T EventSnapshot<T>(T value)
    {
        object? snapshot = value switch
        {
            Avatar avatar => RoomObjectSnapshot.Copy(avatar),
            FloorItem item => RoomObjectSnapshot.Copy(item),
            WallItem item => RoomObjectSnapshot.Copy(item),
            ItemData data => RoomObjectSnapshot.Copy(data),
            IReadOnlyList<Avatar> avatars => avatars.Select(RoomObjectSnapshot.Copy).ToArray(),
            RoomData data => RoomObjectSnapshot.Copy(data),
            RoomResultDetails details => RoomObjectSnapshot.Copy(details),
            RoomChatSettings settings => RoomObjectSnapshot.Copy(settings),
            _ => value
        };
        return (T)snapshot!;
    }

    private void OnRoomIncoming<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        OnIncoming<T>(name, message => Mutate(() => handler(message)));

    private void OnRoomIncoming<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        OnIncoming(contract, message => Mutate(() => handler(message)));

    private void OnRoomState<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        OnRoomIncoming<T>(name, message =>
        {
            if (State is RoomSessionState.Entering or RoomSessionState.Ready)
                handler(message);
        });

    private void OnRoomState<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        OnRoomState(contract, (message, _) => handler(message));

    private void OnRoomState<T>(MessageContract<T> contract, Action<T, long> handler)
        where T : IParserComposer<T> =>
        OnIncoming(contract, (message, state_generation) => Mutate(() =>
        {
            if (State is RoomSessionState.Entering or RoomSessionState.Ready)
                handler(message, state_generation);
        }));

    private void OnRoomOutgoing(string name, Action handler) =>
        OnOutgoing(name, () => Mutate(handler));

    private void OnRoomOutgoing<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        OnOutgoing<T>(name, message => Mutate(() => handler(message)));

    private void OnRoomOutgoing<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        OnOutgoing(contract, message => Mutate(() => handler(message)));

    /// <summary>Reads the room state through a projection while holding the state lock, so every member it reads belongs to the same state.</summary>
    /// <remarks>Changes wait until the projection returns, so it should be short and must not wait for room events.</remarks>
    /// <typeparam name="TResult">The type of the value the projection returns.</typeparam>
    /// <param name="projection">The function that reads the manager and returns a value.</param>
    /// <returns>The value returned by <paramref name="projection"/>.</returns>
    public TResult Capture<TResult>(Func<RoomManager, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        lock (_state_sync)
            return projection(this);
    }

    private string? Property(string key)
    {
        lock (_state_sync)
            return _properties.GetValueOrDefault(key);
    }

    private void SetRoomResult(GuestRoomResult message, bool notify)
    {
        RoomAuthorityState previous_authority = Authority;
        bool data_changed = Data is null || !RoomDataMatches(Data, message.Data);
        Data = message.Data;
        DataIsLoaded = true;
        Details = message.Details;
        DetailsAreLoaded = message.Details is not null;
        if (message.Details is { } details)
        {
            ChatSettings = details.Chat;
            ChatSettingsAreLoaded = true;
            if (notify)
            {
                Publish(DetailsUpdated, details);
                Publish(ChatSettingsUpdated, details.Chat);
            }
        }
        else
        {
            ChatSettings = null;
            ChatSettingsAreLoaded = false;
        }
        if (data_changed)
            Publish(RoomDataUpdated, message.Data);
        RoomAuthorityState current_authority = Authority;
        if (notify && previous_authority != current_authority)
            Publish(AuthorityChanged, current_authority);
    }

    private static bool RoomDataMatches(RoomData left, RoomData right) =>
        left.Id == right.Id &&
        left.Name == right.Name &&
        left.OwnerId == right.OwnerId &&
        left.OwnerName == right.OwnerName &&
        left.DoorMode == right.DoorMode &&
        left.UserCount == right.UserCount &&
        left.MaxUserCount == right.MaxUserCount &&
        left.Description == right.Description &&
        left.TradeMode == right.TradeMode &&
        left.Score == right.Score &&
        left.Ranking == right.Ranking &&
        left.Category == right.Category &&
        left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal) &&
        left.OfficialRoomPicRef == right.OfficialRoomPicRef &&
        left.HasGroup == right.HasGroup &&
        left.GroupId == right.GroupId &&
        left.GroupName == right.GroupName &&
        left.GroupBadge == right.GroupBadge &&
        left.HasEvent == right.HasEvent &&
        left.EventName == right.EventName &&
        left.EventDescription == right.EventDescription &&
        left.EventMinutesRemaining == right.EventMinutesRemaining &&
        left.ShowOwner == right.ShowOwner &&
        left.AllowPets == right.AllowPets &&
        left.DisplayRoomEntryAd == right.DisplayRoomEntryAd;

    private void SetOwner(bool owner)
    {
        RoomAuthorityState previous = Authority;
        IsOwner = owner;
        RoomAuthorityState current = Authority;
        if (previous != current)
            Publish(AuthorityChanged, current);
    }

    private void SetRightsLevel(int? rights_level)
    {
        RoomAuthorityState previous_authority = Authority;
        int? previous_level = RightsLevel;
        RightsLevel = rights_level;
        if (previous_level != RightsLevel)
            Publish(RightsLevelChanged, previous_level, RightsLevel);
        RoomAuthorityState current_authority = Authority;
        if (previous_authority != current_authority)
            Publish(AuthorityChanged, current_authority);
    }

    private void SetSpectating(bool? spectating)
    {
        RoomAuthorityState previous_authority = Authority;
        bool? previous = IsSpectating;
        IsSpectating = spectating;
        if (previous != IsSpectating)
            Publish(SpectatingChanged, previous, IsSpectating);
        RoomAuthorityState current_authority = Authority;
        if (previous_authority != current_authority)
            Publish(AuthorityChanged, current_authority);
    }

    private void SetAccessState(
        RoomAccessState state,
        Id? room_id,
        RoomConnectionFailure? failure = null)
    {
        RoomAccessState previous_state = AccessState;
        Id? previous_room_id = AccessRoomId;
        AccessState = state;
        AccessRoomId = room_id;
        ConnectionFailure = failure;
        if (state is not RoomAccessState.Queued)
            QueueStatus = null;
        if (previous_state != state || previous_room_id != room_id || failure is not null)
        {
            Publish(AccessStateChanged, new RoomAccessTransition(
                previous_state,
                state,
                previous_room_id,
                room_id,
                failure));
        }
    }

    private void FailRoomAccess(
        RoomAccessState state,
        Id? room_id,
        RoomConnectionFailure? failure = null)
    {
        Id? target_room_id = room_id ?? AccessRoomId ?? (RoomId == 0 ? null : RoomId);
        if (State is not RoomSessionState.Outside || IsInRoom || RoomId != 0)
        {
            LeaveRoom(
                preserve_access: true,
                source: RoomExitSource.AccessFailure);
        }
        SetAccessState(state, target_room_id, failure);
    }

    /// <summary>
    /// Records an access failure that carries a room identifier, and tears the room session down
    /// only when the failure concerns the session this manager is tracking.
    /// </summary>
    /// <remarks>
    /// The Flash client scopes the same way: <c>RoomSessionManager.sessionUpdate</c> resolves the
    /// session by <c>flatId</c> and disposes nothing when no session matches, and its
    /// <c>onNoSuchFlat</c> handlers are empty. The access state itself is recorded unconditionally
    /// so that <see cref="RoomEntryCoordinator"/> can still resolve an attempt for another room.
    /// </remarks>
    /// <param name="state">The terminal access state to record.</param>
    /// <param name="room_id">The room the failure names.</param>
    private void FailRoomAccessFor(RoomAccessState state, Id room_id)
    {
        bool concerns_session = RoomId != 0
            ? RoomId == room_id
            : AccessRoomId is not { } access_room_id || access_room_id == room_id;
        if (concerns_session)
            FailRoomAccess(state, room_id);
        else
            SetAccessState(state, room_id);
    }

    private bool HasTerminalAccessState() => AccessState is
        RoomAccessState.Denied or
        RoomAccessState.NotFound or
        RoomAccessState.ConnectionError;

    private void BeginRoom(Id room_id, bool new_entry = false)
    {
        bool new_room = RoomId != room_id ||
            State is RoomSessionState.Outside or RoomSessionState.Leaving ||
            new_entry && State is RoomSessionState.Ready;
        if (new_room)
        {
            GuestRoomResult? pending_result = TakePendingRoomResult(room_id);
            LeaveRoom(source: RoomExitSource.RoomTransition);
            LastKick = null;
            AdvanceGeneration();
            RoomId = room_id;
            State = RoomSessionState.Entering;
            if (pending_result is not null)
                SetRoomResult(pending_result, false);
            Publish(Entering, room_id);
        }
    }

    private void AdvanceGeneration()
    {
        Generation++;
        CancellationTokenSource ended = _session_end;
        _session_end = new CancellationTokenSource();
        Publish(ended.Cancel);
    }

    private bool IsSelf(Avatar avatar) =>
        avatar is User && OwnUserId?.Invoke() is { } id && avatar.Id == id;

    private void PublishMovement(
        Avatar avatar,
        Tile from,
        Tile to,
        Tile? moving_to,
        MovementSource source,
        int duration)
    {
        if (AvatarMovementReceived is null)
            return;
        Publish(AvatarMovementReceived, avatar, new AvatarMovement(
            avatar.Index,
            IsSelf(avatar),
            from,
            to,
            moving_to,
            source,
            duration,
            checked(_revision + 1),
            Stopwatch.GetTimestamp()));
    }

    private void PublishMovement(
        FloorItem item,
        Tile from,
        MovementSource source,
        int duration)
    {
        if (FloorItemMovementReceived is null)
            return;
        Publish(FloorItemMovementReceived, item, new FloorItemMovement(
            item.Id,
            from,
            item.Location,
            item.Direction,
            source,
            duration,
            checked(_revision + 1),
            Stopwatch.GetTimestamp()));
    }

    private VariableFxValue BindVariableFx(VariableFxValue value) =>
        value with { Config = _variable_fx_configs.GetValueOrDefault((value.IsUser, value.ConfigId)) };

    private void ChangeVariableFxConfig(bool is_user_fx, int config_id, VariableFxConfigEntry? config)
    {
        bool changed = !SameVariableFxConfig(_variable_fx_configs.GetValueOrDefault((is_user_fx, config_id)), config);
        VariableFxValue[] previous = changed
            ?
            [
                .. _variable_fx_values.Values
                    .Where(value => value.IsUser == is_user_fx && value.ConfigId == config_id)
                    .Select(BindVariableFx)
            ]
            : [];
        if (config is null)
            _variable_fx_configs.Remove((is_user_fx, config_id));
        else
            _variable_fx_configs[(is_user_fx, config_id)] = config;
        foreach (VariableFxValue value in previous)
            Publish(VariableFxChanged, BindVariableFx(value), value);
    }

    private static bool SameVariableFxConfig(VariableFxConfigEntry? left, VariableFxConfigEntry? right) =>
        left is null || right is null
            ? left is null && right is null
            : left with { Extra = right.Extra } == right &&
              left.Extra.Count == right.Extra.Count &&
              left.Extra.All(pair => right.Extra.TryGetValue(pair.Key, out string? value) && value == pair.Value);

    private void ApplyVariableFx(VariableFxStatusUpdate message)
    {
        long timestamp = Stopwatch.GetTimestamp();
        foreach (VariableFxStatusEntry entry in message.Statuses)
        {
            VariableFxSlot slot = entry.Slot;
            if (slot.IsUserEntity ? !_avatars.ContainsKey(slot.EntityId) : !_floorItems.ContainsKey(slot.EntityId))
                continue;
            _variable_fx_values.TryGetValue(slot, out VariableFxValue? previous);
            var current = new VariableFxValue(
                slot,
                entry.Value,
                entry.MinValue,
                entry.MaxValue,
                entry.Extra,
                entry.IsInitialize || message.IsInitialize,
                null,
                checked(_revision + 1),
                timestamp);
            _variable_fx_values[slot] = current;
            Publish(VariableFxChanged, BindVariableFx(current), previous is null ? null : BindVariableFx(previous));
        }
    }

    private void RemoveVariableFx(VariableFxStatusRemoval message)
    {
        foreach (VariableFxSlot slot in message.Slots)
        {
            if (_variable_fx_values.Remove(slot, out VariableFxValue? removed))
                Publish(VariableFxRemoved, BindVariableFx(removed));
        }
    }

    private void DropVariableFx(bool is_user, long entity_id)
    {
        foreach (VariableFxValue removed in _variable_fx_values.Values
            .Where(value => value.IsUser == is_user && value.EntityId == entity_id)
            .ToArray())
        {
            _variable_fx_values.Remove(removed.Slot);
            Publish(VariableFxRemoved, BindVariableFx(removed));
        }
    }

    private void CompleteEntry()
    {
        bool entered = !IsInRoom;
        IsInRoom = true;
        State = _room_ready_received
            ? RoomSessionState.Ready
            : RoomSessionState.Entering;
        if (entered)
            Publish(Entered);
    }

    private void LeaveRoom(
        bool preserve_access = false,
        RoomExitSource source = RoomExitSource.ConnectionClosed,
        short? reason = null)
    {
        bool had_state = IsInRoom || RoomId != 0 || State is not RoomSessionState.Outside;
        RoomExitState? exit = null;
        if (had_state)
        {
            exit = new RoomExitState(
                RoomId,
                IsInRoom,
                source,
                reason,
                _pending_kick);
            LastExit = exit;
            _pending_kick = null;
            State = RoomSessionState.Leaving;
            Publish(Leaving);
            PublishPlacement(
                RoomPlacementCommitKind.RoomReset,
                CurrentStateGeneration,
                null,
                null);
        }

        bool left = had_state;
        ClearRoom();
        State = RoomSessionState.Outside;
        if (had_state)
            AdvanceGeneration();
        if (!preserve_access)
            SetAccessState(RoomAccessState.Idle, null);
        if (left)
        {
            Publish(Exited, exit!);
            Publish(Left);
        }
    }

    private void ClearRoom()
    {
        IsInRoom = false;
        RoomId = 0;
        RoomType = "";
        IsOwner = false;
        RightsLevel = null;
        IsSpectating = null;
        Data = null;
        Details = null;
        EntryTile = null;
        VisualizationSettings = null;
        ChatSettings = null;
        DataIsLoaded = false;
        DetailsAreLoaded = false;
        EntryTileIsLoaded = false;
        PropertiesHaveBeenReceived = false;
        VisualizationSettingsAreLoaded = false;
        ChatSettingsAreLoaded = false;
        _properties.Clear();
        Controllers = [];
        ControllersAreLoaded = false;
        FloorPlan = null;
        pending_hidden_areas.Clear();
        FloorPlanIsLoaded = false;
        Heightmap = null;
        HeightmapIsLoaded = false;
        FloorItemsAreLoaded = false;
        WallItemsAreLoaded = false;
        AvatarsAreLoaded = false;
        _room_ready_received = false;
        foreach (FloorItem item in _floorItems.Values)
            item.IsRemoved = true;
        foreach (WallItem item in _wallItems.Values)
            item.IsRemoved = true;
        foreach (Avatar avatar in _avatars.Values)
            avatar.IsRemoved = true;
        _floorItems.Clear();
        _wallItems.Clear();
        _avatars.Clear();
        _variable_fx_configs.Clear();
        _variable_fx_values.Clear();
    }

    private GuestRoomResult? TakePendingRoomResult(Id room_id)
    {
        _pending_room_results.TryRemove(room_id, out GuestRoomResult? result);
        _pending_room_results.Clear();
        return result;
    }

    private void Enrich(Furni item)
    {
        if (GameData?.Furni is not { } furni || furni.GetInfo(item.Type, item.Kind) is not { } info)
            return;
        if (string.IsNullOrEmpty(item.Identifier))
            item.Identifier = info.Identifier;
        if (item is FloorItem floor)
        {
            floor.SizeX = info.Width;
            floor.SizeZ = info.Length;
        }
    }

    internal void EnrichFurni()
    {
        Mutate(() =>
        {
            foreach (FloorItem item in _floorItems.Values)
                Enrich(item);
            foreach (WallItem item in _wallItems.Values)
                Enrich(item);
        });
    }

    /// <summary>Gets the Fx bar values of one avatar, by room index, or of one furni, by item id.</summary>
    /// <param name="isUser"><see langword="true"/> to read the values of an avatar; <see langword="false"/> to read the values of a furni.</param>
    /// <param name="entityId">The avatar's room index, or the furni's item id.</param>
    /// <returns>The values bound to their current configuration, or an empty list when there are none.</returns>
    public IReadOnlyList<VariableFxValue> VariableFxOf(bool isUser, long entityId)
    {
        lock (_state_sync)
        {
            return [.. _variable_fx_values.Values
                .Where(value => value.IsUser == isUser && value.EntityId == entityId)
                .Select(BindVariableFx)];
        }
    }

    /// <summary>Gets the floor item with the specified id, or <see langword="null"/> when it is not in the room.</summary>
    /// <param name="id">The id of the floor item.</param>
    public FloorItem? FloorItem(Id id) => _floorItems.GetValueOrDefault(id);
    /// <summary>Gets the wall item with the specified id, or <see langword="null"/> when it is not in the room.</summary>
    /// <param name="id">The id of the wall item.</param>
    public WallItem? WallItem(Id id) => _wallItems.GetValueOrDefault(id);
    /// <summary>Gets the avatar at the specified room index, or <see langword="null"/> when there is none.</summary>
    /// <param name="index">The room index of the avatar.</param>
    public Avatar? AvatarByIndex(int index) => _avatars.GetValueOrDefault(index);
    /// <summary>Gets the first avatar with the specified id, or <see langword="null"/> when there is none.</summary>
    /// <remarks>Users, pets and bots are all searched, so the match can be of any kind. Use <see cref="AvatarByIndex"/> to read one specific avatar.</remarks>
    /// <param name="id">The id of the avatar.</param>
    public Avatar? AvatarById(Id id) => _avatars.Values.FirstOrDefault(a => a.Id == id);
    /// <summary>Gets the user with the specified name, ignoring case, or <see langword="null"/> when there is none.</summary>
    /// <param name="name">The name of the user.</param>
    public User? UserByName(string name) =>
        _avatars.Values.OfType<User>().FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        OnRoomOutgoing(MessageContracts.Room.Access.OpenRequest, message =>
        {
            BeginRoom(message.RoomId, new_entry: true);
            SetAccessState(RoomAccessState.Connecting, message.RoomId);
        });

        OnRoomIncoming(MessageContracts.Room.Access.OpenConfirmed, message =>
        {
            BeginRoom(message.RoomId, new_entry: true);
            SetAccessState(RoomAccessState.Connecting, message.RoomId);
        });

        OnRoomIncoming(MessageContracts.Room.Access.Doorbell, message =>
        {
            Publish(DoorbellRang, message);
            if (string.IsNullOrEmpty(message.UserName))
            {
                Id? room_id = AccessRoomId ?? (RoomId == 0 ? null : RoomId);
                SetAccessState(RoomAccessState.RingingDoorbell, room_id);
            }
        });

        OnRoomIncoming(MessageContracts.Room.Access.QueueStatus, message =>
        {
            if (RoomId == 0 || State is RoomSessionState.Outside)
                BeginRoom(message.RoomId);
            if (RoomId != message.RoomId)
                return;
            SetAccessState(
                message.ActiveSet is null ? RoomAccessState.Connecting : RoomAccessState.Queued,
                message.RoomId);
            QueueStatus = message;
            Publish(QueueUpdated, message);
        });

        OnRoomIncoming(MessageContracts.Room.Access.Granted, message =>
        {
            Publish(AccessGranted, message);
            if (!message.IsSelf)
                return;
            BeginRoom(message.RoomId);
            SetAccessState(RoomAccessState.Accessible, message.RoomId);
        });

        OnRoomIncoming(MessageContracts.Room.Access.Denied, message =>
        {
            Publish(AccessDenied, message);
            if (message.IsSelf)
                FailRoomAccessFor(RoomAccessState.Denied, message.RoomId);
        });

        OnRoomIncoming(MessageContracts.Room.Access.NotFound, message =>
            FailRoomAccessFor(RoomAccessState.NotFound, message.RoomId));

        OnRoomIncoming(MessageContracts.Room.Access.ConnectionFailed, message =>
        {
            Publish(ConnectionFailed, message);
            FailRoomAccess(
                RoomAccessState.ConnectionError,
                null,
                new RoomConnectionFailure(message.Kind, message.ReasonCode, message.Parameter));
        });

        OnRoomIncoming(MessageContracts.Room.Lifecycle.Ready, message =>
        {
            BeginRoom(message.RoomId, new_entry: true);
            SetAccessState(RoomAccessState.Accessible, message.RoomId);
            RoomType = message.RoomType;
            _room_ready_received = true;
            if (IsInRoom)
                State = RoomSessionState.Ready;
            Publish(Ready);
        });

        OnRoomIncoming(MessageContracts.Room.Lifecycle.Entry, info =>
        {
            BeginRoom(info.GuestRoomId);
            SetAccessState(RoomAccessState.Accessible, info.GuestRoomId);
            SetOwner(info.Owner);
            CompleteEntry();
        });

        OnRoomIncoming(MessageContracts.Room.Lifecycle.Forward, message =>
        {
            SetAccessState(RoomAccessState.Connecting, message.RoomId);
        });

        OnRoomIncoming(MessageContracts.Errors.Generic, message =>
        {
            if (message.ErrorCode != KickedByOwnerError)
                return;
            if (State is RoomSessionState.Outside && !IsInRoom && RoomId == 0)
                return;
            var kick = new RoomKick(RoomId, message.ErrorCode, IsInRoom);
            _pending_kick = kick;
            LastKick = kick;
            Publish(Kicked, kick);
        });

        OnRoomIncoming(MessageContracts.Room.Lifecycle.ConnectionClosed, message =>
        {
            if (State is RoomSessionState.Outside && HasTerminalAccessState())
                return;
            LeaveRoom(
                source: RoomExitSource.ConnectionClosed,
                reason: message.Reason);
        });

        OnRoomOutgoing(MessageContracts.Room.Lifecycle.Quit, _ =>
            LeaveRoom(
                preserve_access: State is RoomSessionState.Outside && HasTerminalAccessState(),
                source: RoomExitSource.ClientQuit));

        OnRoomIncoming(MessageContracts.Room.Snapshot, message =>
        {
            if (message.Data.Id == RoomId && State is not RoomSessionState.Outside)
            {
                SetRoomResult(message, true);
            }
            else if (message.EnterRoom)
            {
                _pending_room_results[message.Data.Id] = message;
            }
        });

        OnRoomState(MessageContracts.Room.Environment.EntryTile, message =>
        {
            EntryTile = message;
            EntryTileIsLoaded = true;
            Publish(EntryTileUpdated, message);
        });

        OnRoomState(MessageContracts.Room.Environment.Property, message =>
        {
            _properties[message.Key] = message.Value;
            PropertiesHaveBeenReceived = true;
            Publish(PropertyUpdated, message);
        });

        OnRoomState(MessageContracts.Room.Environment.Visualization, message =>
        {
            VisualizationSettings = message;
            VisualizationSettingsAreLoaded = true;
            Publish(VisualizationSettingsUpdated, message);
        });

        OnRoomState(MessageContracts.Room.Environment.ChatSettings, message =>
        {
            ChatSettings = message;
            ChatSettingsAreLoaded = true;
            Publish(ChatSettingsUpdated, message);
        });

        OnRoomIncoming(MessageContracts.Room.Authority.ControllerGranted, message =>
        {
            if (RoomId != message.RoomId ||
                State is not (RoomSessionState.Entering or RoomSessionState.Ready))
            {
                return;
            }
            SetRightsLevel(message.RightsLevel);
        });

        OnRoomIncoming(MessageContracts.Room.Authority.ControllerRevoked, message =>
        {
            if (RoomId != message.RoomId ||
                State is not (RoomSessionState.Entering or RoomSessionState.Ready))
            {
                return;
            }
            SetRightsLevel(0);
        });

        OnRoomIncoming(MessageContracts.Room.Authority.Owner, message =>
        {
            if (RoomId != message.RoomId ||
                State is not (RoomSessionState.Entering or RoomSessionState.Ready))
            {
                return;
            }
            SetOwner(true);
        });

        OnRoomIncoming(MessageContracts.Room.Authority.SpectatorGranted, message =>
        {
            if (RoomId != message.RoomId ||
                State is not (RoomSessionState.Entering or RoomSessionState.Ready))
            {
                return;
            }
            SetSpectating(true);
        });

        OnRoomIncoming(MessageContracts.Room.Authority.SpectatorRevoked, message =>
        {
            if (RoomId != message.RoomId ||
                State is not (RoomSessionState.Entering or RoomSessionState.Ready))
            {
                return;
            }
            SetSpectating(false);
        });

        // The hotel does not only send this once on entry: it also delivers later furni in further
        // batches, which is how temporary furni arrive. The client treats every batch the same way
        // it treats a single ObjectAdd - onObjects and onObjectAdd both call addActiveObject and
        // neither drops what is already placed. Replacing the collection here emptied the room down
        // to whatever the newest batch carried. Leaving the room is what clears it, in Reset.
        OnRoomState(MessageContracts.Room.Objects, ApplyFloorItems);

        // Same batching as Objects above: the client's onItems adds each element through the same
        // path as a single ItemAdd and never clears.
        OnRoomState(MessageContracts.Room.WallItems, message =>
        {
            foreach (WallItem item in message.Items)
            {
                Enrich(item);
                item.IsRemoved = false;
                if (_wallItems.TryGetValue(item.Id, out WallItem? previous) &&
                    !ReferenceEquals(previous, item))
                {
                    previous.IsRemoved = true;
                }
                _wallItems[item.Id] = item;
            }
            WallItemsAreLoaded = true;
            Publish(WallItemsLoaded);
        });

        OnRoomState(MessageContracts.Room.Occupants.Snapshot, message =>
        {
            foreach (Avatar avatar in message.Avatars)
            {
                if (avatar is User)
                {
                    foreach ((int index, Avatar cached) in _avatars)
                    {
                        if (index == avatar.Index || cached is not User || cached.Id != avatar.Id)
                            continue;
                        _avatars.TryRemove(index, out _);
                        cached.IsRemoved = true;
                        DropVariableFx(true, index);
                        Publish(AvatarRemoved, cached);
                    }
                }
                avatar.IsRemoved = false;
                if (_avatars.TryGetValue(avatar.Index, out Avatar? previous) &&
                    !ReferenceEquals(previous, avatar))
                {
                    previous.IsRemoved = true;
                }
                _avatars[avatar.Index] = avatar;
            }
            AvatarsAreLoaded = true;
            Publish(AvatarsAdded, message.Avatars);
        });

        OnRoomState(MessageContracts.Room.FloorItem.Added, (message, state_generation) =>
        {
            Enrich(message.Item);
            message.Item.IsRemoved = false;
            _floorItems[message.Item.Id] = message.Item;
            Publish(FloorItemAdded, message.Item);
            PublishPlacement(
                RoomPlacementCommitKind.FloorAdded,
                state_generation,
                null,
                PlacementItem(message.Item));
        });

        OnRoomState(MessageContracts.Room.FloorItem.Removed, (message, state_generation) =>
        {
            FloorItem? item = RemoveFloorItem(message.Id);
            if (item is not null)
            {
                PublishPlacement(
                    RoomPlacementCommitKind.FloorRemoved,
                    state_generation,
                    PlacementItem(item),
                    null,
                    message.PickerId,
                    message.IsExpired,
                    message.Delay);
            }
        });

        OnRoomState(MessageContracts.Room.FloorItem.RemovedMultiple, message =>
        {
            foreach (Id id in message.Ids)
                RemoveFloorItem(id);
        });

        OnRoomState(MessageContracts.Room.WallItem.Added, (message, state_generation) =>
        {
            Enrich(message.Item);
            message.Item.IsRemoved = false;
            _wallItems[message.Item.Id] = message.Item;
            Publish(WallItemAdded, message.Item);
            PublishPlacement(
                RoomPlacementCommitKind.WallAdded,
                state_generation,
                null,
                PlacementItem(message.Item));
        });

        OnRoomState(MessageContracts.Room.WallItem.Removed, (message, state_generation) =>
        {
            WallItem? item = RemoveWallItem(message.Id);
            if (item is not null)
            {
                PublishPlacement(
                    RoomPlacementCommitKind.WallRemoved,
                    state_generation,
                    PlacementItem(item),
                    null,
                    message.PickerId);
            }
        });

        OnRoomState(MessageContracts.Room.WallItem.RemovedMultiple, message =>
        {
            foreach (Id id in message.Ids)
                RemoveWallItem(id);
        });

        OnRoomState(MessageContracts.Room.WallItem.DataUpdated, message =>
            SetWallItemData(message.Id, message.ItemData));

        OnRoomState(MessageContracts.Room.WallItem.DataBatchUpdated, message =>
        {
            foreach (ItemStateUpdate item in message.Items)
                SetWallItemData(item.Id, item.ItemData);
        });

        OnRoomState(MessageContracts.Room.Occupants.Removed, message =>
        {
            if (_avatars.Remove(message.Index, out Avatar? avatar))
            {
                avatar.IsRemoved = true;
                DropVariableFx(true, message.Index);
                Publish(AvatarRemoved, avatar);
            }
        });

        OnRoomState(MessageContracts.Room.Occupants.Status, message =>
        {
            foreach (AvatarStatus status in message.Updates)
            {
                if (!_avatars.TryGetValue(status.Index, out Avatar? avatar))
                    continue;
                Tile previous = avatar.Location;
                avatar.Location = status.Location;
                avatar.Direction = status.Direction;
                avatar.HeadDirection = status.HeadDirection;
                avatar.CurrentUpdate = status;
                avatar.MovingTo = status.MovingTo;
                if (previous != avatar.Location)
                    Publish(AvatarMoved, avatar, previous, avatar.Location);
                PublishMovement(avatar, previous, avatar.Location, avatar.MovingTo, MovementSource.Walk, 0);
                Publish(AvatarUpdated, avatar);
            }
        });

        OnRoomState(MessageContracts.Room.Occupants.Action.Dance, message => Patch(message.Index, avatar =>
        {
            int previous = avatar.Dance;
            avatar.Dance = message.Dance;
            if (previous != avatar.Dance)
                Publish(AvatarDanceChanged, avatar, previous, avatar.Dance);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Action.Effect, message => Patch(message.Index, avatar =>
        {
            int previous = avatar.Effect;
            avatar.Effect = message.Effect;
            if (previous != avatar.Effect)
                Publish(AvatarEffectChanged, avatar, previous, avatar.Effect);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Action.Carry, message => Patch(message.Index, avatar =>
        {
            int previous = avatar.HandItem;
            avatar.HandItem = message.ItemType;
            if (previous != avatar.HandItem)
                Publish(AvatarHandItemChanged, avatar, previous, avatar.HandItem);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Action.Sleep, message => Patch(message.Index, avatar =>
        {
            bool previous = avatar.IsIdle;
            avatar.IsIdle = message.Sleeping;
            if (previous != avatar.IsIdle)
                Publish(AvatarIdleChanged, avatar, previous, avatar.IsIdle);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Action.Typing, message => Patch(message.Index, avatar =>
        {
            bool previous = avatar.IsTyping;
            avatar.IsTyping = message.Typing;
            if (previous != avatar.IsTyping)
                Publish(AvatarTypingChanged, avatar, previous, avatar.IsTyping);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Action.Expression, message =>
        {
            if (_avatars.TryGetValue(message.Index, out Avatar? avatar))
                Publish(AvatarActioned, avatar, message.Action);
        });

        OnRoomState(MessageContracts.Room.Occupants.Identity.Appearance, message => Patch(message.Index, avatar =>
        {
            string previous_figure = avatar.Figure;
            string previous_motto = avatar.Motto;
            avatar.Figure = message.Figure;
            avatar.Motto = message.Motto;
            if (avatar is User user)
            {
                user.Gender = Genders.Parse(message.Gender);
                user.AchievementScore = message.AchievementScore;
                if (message.BadgesRank >= 0)
                    user.BadgeRank = message.BadgesRank;
            }
            if (previous_figure != avatar.Figure || previous_motto != avatar.Motto)
                Publish(AvatarIdentityChanged, avatar, previous_figure, avatar.Figure, previous_motto, avatar.Motto);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Identity.Name, message => Patch(message.Index, avatar =>
        {
            string previous = avatar.Name;
            avatar.Name = message.NewName;
            if (previous != avatar.Name)
                Publish(AvatarNameChanged, avatar, previous, avatar.Name);
        }));

        OnRoomState(MessageContracts.Room.Occupants.Identity.FavoriteGroup, message =>
            Patch(message.RoomIndex, avatar =>
            {
                if (avatar is not User user)
                    return;
                user.GroupId = message.GroupId;
                user.GroupName = message.GroupName;
            }));

        OnRoomState(MessageContracts.Room.Occupants.Pet.Figure, message =>
            PatchPet(message.Index, pet =>
            {
                string previous_figure = pet.Figure;
                pet.Figure = message.Figure.FigureString;
                pet.HasSaddle = message.HasSaddle;
                pet.IsRiding = message.IsRiding;
                if (previous_figure != pet.Figure)
                    Publish(AvatarIdentityChanged, pet, previous_figure, pet.Figure, pet.Motto, pet.Motto);
            }));

        OnRoomState(MessageContracts.Room.Occupants.Pet.Status, message => PatchPet(message.Index, pet =>
        {
            pet.CanBreed = message.CanBreed;
            pet.CanHarvest = message.CanHarvest;
            pet.CanRevive = message.CanRevive;
            pet.HasBreedingPermission = message.HasBreedingPermission;
        }));

        OnRoomState(MessageContracts.Room.Occupants.Pet.Level, message =>
            PatchPet(message.Index, pet => pet.Level = message.Level));

        OnRoomState(MessageContracts.Room.FloorItem.DiceValue, message =>
            SetFloorItemState(message.ItemId, message.Value));

        OnRoomState(MessageContracts.Room.FloorItem.OneWayDoorStatus, message =>
            SetFloorItemState(message.ItemId, message.Status));

        OnRoomState(MessageContracts.Room.FloorItem.Updated, (message, state_generation) =>
        {
            Enrich(message.Item);
            _floorItems.TryGetValue(message.Item.Id, out FloorItem? previous_item);
            RoomPlacementCommitItem? previous = previous_item is null
                ? null
                : PlacementItem(previous_item);
            _floorItems[message.Item.Id] = message.Item;
            if (previous_item is not null)
            {
                previous_item.IsRemoved = true;
                if (previous_item.Location != message.Item.Location)
                    Publish(FloorItemMoved, message.Item, previous_item.Location, message.Item.Location);
                if (previous_item.Location != message.Item.Location || previous_item.Direction != message.Item.Direction)
                    PublishMovement(message.Item, previous_item.Location, MovementSource.Update, 0);
                if (!ReferenceEquals(previous_item.Data, message.Item.Data))
                    Publish(FloorItemDataChanged, message.Item, previous_item.Data, message.Item.Data);
            }
            Publish(FloorItemUpdated, message.Item);
            PublishPlacement(
                RoomPlacementCommitKind.FloorUpdated,
                state_generation,
                previous,
                PlacementItem(message.Item));
        });

        OnRoomState(MessageContracts.Room.WallItem.Updated, (message, state_generation) =>
        {
            Enrich(message.Item);
            _wallItems.TryGetValue(message.Item.Id, out WallItem? previous_item);
            RoomPlacementCommitItem? previous = previous_item is null
                ? null
                : PlacementItem(previous_item);
            _wallItems[message.Item.Id] = message.Item;
            if (previous_item is not null)
            {
                previous_item.IsRemoved = true;
                if (previous_item.Location != message.Item.Location)
                    Publish(WallItemMoved, message.Item, previous_item.Location, message.Item.Location);
            }
            Publish(WallItemUpdated, message.Item);
            PublishPlacement(
                RoomPlacementCommitKind.WallUpdated,
                state_generation,
                previous,
                PlacementItem(message.Item));
        });

        OnRoomState(MessageContracts.Room.Item.PickupConfirmation, PublishPickupConfirmation);

        OnRoomState(MessageContracts.Room.FloorItem.DataUpdated, message =>
        {
            if (_floorItems.TryGetValue(message.Id, out FloorItem? item))
            {
                ItemData previous = item.Data;
                item.Data = message.Data;
                Publish(FloorItemDataChanged, item, previous, item.Data);
                Publish(FloorItemUpdated, item);
            }
        });

        OnRoomState(MessageContracts.Room.FloorItem.DataBatchUpdated, message =>
        {
            foreach (FloorDataEntry entry in message.Items)
                if (_floorItems.TryGetValue(entry.Id, out FloorItem? item))
                {
                    ItemData previous = item.Data;
                    item.Data = entry.Data;
                    Publish(FloorItemDataChanged, item, previous, item.Data);
                    Publish(FloorItemUpdated, item);
                }
        });

        OnRoomState(MessageContracts.Room.Authority.ControllersSnapshot, message =>
        {
            if (message.RoomId != RoomId)
                return;
            Controllers = message.Users.ToArray();
            ControllersAreLoaded = true;
        });

        OnRoomState(MessageContracts.Room.Environment.FloorPlan, message =>
        {
            FloorPlan = message;
            foreach (AreaHideData area in pending_hidden_areas.Values)
                replace_hidden_areas(FloorPlan.HiddenAreas.Where(value => value.FurniId != area.FurniId).Append(area));
            pending_hidden_areas.Clear();
            FloorPlanIsLoaded = true;
        });

        OnRoomState(MessageContracts.Room.Environment.AreaHide, message =>
        {
            if (FloorPlan is null)
                pending_hidden_areas[message.FurniId] = message;
            else
                replace_hidden_areas(FloorPlan.HiddenAreas.Where(value => value.FurniId != message.FurniId).Append(message));
        });

        OnRoomState(MessageContracts.Room.Heightmap.Snapshot, ApplyHeightmap);

        OnRoomState(MessageContracts.Room.Heightmap.Diff, message =>
        {
            if (Heightmap is { } map)
            {
                foreach (HeightmapDiff diff in message.Updates)
                    map.Apply(diff.X, diff.Y, diff.Value);
            }
        });

        OnRoomState(MessageContracts.Room.Movement.Slide, message =>
        {
            foreach (SlideObject slide in message.Objects)
                if (_floorItems.TryGetValue(slide.Id, out FloorItem? item))
                {
                    Tile previous = item.Location;
                    item.Location = new Tile(message.To.X, message.To.Y, slide.ToZ);
                    if (previous != item.Location)
                        Publish(FloorItemMoved, item, previous, item.Location);
                    PublishMovement(item, previous, MovementSource.Roller, 0);
                    Publish(FloorItemUpdated, item);
                }

            if (message.Avatar is { } avatar)
            {
                if (!_avatars.TryGetValue(unchecked((int)(long)avatar.Index), out Avatar? mover))
                    return;
                Tile previous = mover.Location;
                mover.Location = new Tile(message.To.X, message.To.Y, avatar.ToZ);
                mover.MovingTo = null;
                if (previous != mover.Location)
                    Publish(AvatarMoved, mover, previous, mover.Location);
                PublishMovement(
                    mover,
                    new Tile(message.From.X, message.From.Y, avatar.FromZ),
                    mover.Location,
                    null,
                    MovementSource.Roller,
                    0);
                Publish(AvatarUpdated, mover);
            }
        });

        OnRoomState(MessageContracts.Room.Movement.Wired, message =>
        {
            foreach (WiredMovement move in message.Movements)
                switch (move)
                {
                    case AvatarWiredMovement av when _avatars.TryGetValue(av.AvatarIndex, out Avatar? a):
                        Tile previous_avatar_location = a.Location;
                        a.Location = av.Destination;
                        a.Direction = av.BodyDirection;
                        a.HeadDirection = av.HeadDirection;
                        a.MovingTo = null;
                        if (previous_avatar_location != a.Location)
                            Publish(AvatarMoved, a, previous_avatar_location, a.Location);
                        PublishMovement(a, av.Source, a.Location, null, MovementSource.Wired, av.AnimationTime);
                        Publish(AvatarUpdated, a);
                        break;
                    case AvatarDirectionWiredMovement ad when _avatars.TryGetValue(ad.AvatarIndex, out Avatar? a):
                        a.Direction = ad.BodyDirection;
                        a.HeadDirection = ad.HeadDirection;
                        Publish(AvatarUpdated, a);
                        break;
                    case FloorItemWiredMovement fm when _floorItems.TryGetValue(fm.ItemId, out FloorItem? item):
                        Tile previous_floor_location = item.Location;
                        item.Location = fm.Destination;
                        item.Direction = fm.Rotation;
                        if (previous_floor_location != item.Location)
                            Publish(FloorItemMoved, item, previous_floor_location, item.Location);
                        PublishMovement(item, previous_floor_location, MovementSource.Wired, fm.AnimationTime);
                        Publish(FloorItemUpdated, item);
                        break;
                    case WallItemWiredMovement wm when _wallItems.TryGetValue(wm.ItemId, out WallItem? item):
                        WallLocation previous_wall_location = item.Location;
                        item.Location = wm.Destination;
                        if (previous_wall_location != item.Location)
                            Publish(WallItemMoved, item, previous_wall_location, item.Location);
                        Publish(WallItemUpdated, item);
                        break;
                }
        });

        OnRoomState(MessageContracts.Wired.VariableFx.Configs, message =>
        {
            foreach (VariableFxConfigEntry config in message.Configs)
                ChangeVariableFxConfig(config.IsUserFx, config.ConfigId, config);
        });

        OnRoomState(MessageContracts.Wired.VariableFx.ConfigsRemoved, message =>
        {
            foreach (int config_id in message.ConfigIds)
            {
                ChangeVariableFxConfig(true, config_id, null);
                ChangeVariableFxConfig(false, config_id, null);
            }
        });

        OnRoomState(MessageContracts.Wired.VariableFx.Statuses, ApplyVariableFx);

        OnRoomState(MessageContracts.Wired.VariableFx.StatusesRemoved, RemoveVariableFx);

        OnRoomState(MessageContracts.Room.Chat.Talk, message =>
            Publish(Chat, message with { Type = ChatType.Talk }));
        OnRoomState(MessageContracts.Room.Chat.Shout, message =>
            Publish(Chat, message with { Type = ChatType.Shout }));
        OnRoomState(MessageContracts.Room.Chat.Whisper, message =>
            Publish(Chat, message with { Type = ChatType.Whisper }));
    }

    /// <inheritdoc/>
    protected override void Reset()
    {
        Mutate(() =>
        {
            _pending_room_results.Clear();
            LeaveRoom(source: RoomExitSource.Disconnected);
        });
    }
}
