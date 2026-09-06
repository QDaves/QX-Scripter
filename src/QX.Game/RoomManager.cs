using Qx.Messages;
using Qx.Game.Protocol;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using Qx.Protocol;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace Qx.Game;

public enum RoomSessionState
{
    Outside,
    Entering,
    Ready,
    Leaving
}

/// <summary>
/// Describes a forced removal of the local user from a room by the room owner or staff.
/// </summary>
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
    ClientType Client,
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
    ClientType Client,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int Category,
    Id RoomItemId,
    string Title,
    string Body);

public sealed class RoomManager : GameStateManager
{
    private const int KickedByOwnerError = 4008;

    private readonly ConcurrentDictionary<long, FloorItem> _floorItems = [];
    private readonly ConcurrentDictionary<long, WallItem> _wallItems = [];
    private readonly ConcurrentDictionary<int, Avatar> _avatars = [];
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
    private bool _placement_session_bound;
    private ClientType _placement_client;
    private RoomKick? _pending_kick;

    public bool IsInRoom { get; private set; }
    public bool IsReady => State is RoomSessionState.Ready;
    public RoomSessionState State { get; private set; }
    public long Generation { get; private set; }
    public long Revision => Interlocked.Read(ref _revision);
    public long RoomId { get; private set; }
    public RoomAccessState AccessState { get; private set; }
    public Id? AccessRoomId { get; private set; }
    public RoomQueueStatus? QueueStatus { get; private set; }
    public int? QueuePosition => QueueStatus?.Position;
    public bool IsRingingDoorbell => AccessState is RoomAccessState.RingingDoorbell;
    public bool IsInQueue => AccessState is RoomAccessState.Queued;
    public RoomConnectionFailure? ConnectionFailure { get; private set; }
    public RoomExitState? LastExit { get; private set; }
    public RoomExitReason? LastNativeExitReason { get; private set; }
    /// <summary>
    /// The most recent kick observed in the current or a previous room session.
    /// Cleared when a new room session begins.
    /// </summary>
    public RoomKick? LastKick { get; private set; }
    /// <summary>
    /// The kick that caused <see cref="LastExit"/>, or <see langword="null"/> when the
    /// last room exit was not caused by a kick.
    /// </summary>
    public RoomKick? LastExitKick => LastExit?.Kick;
    /// <summary>
    /// Whether the last room exit was caused by the local user being kicked.
    /// </summary>
    public bool WasKicked => LastExit?.WasKicked ?? false;
    public string RoomType { get; private set; } = "";
    public bool IsOwner { get; private set; }
    public int? RightsLevel { get; private set; }
    public bool RightsAreKnown => IsOwner || RightsLevel.HasValue;
    public bool HasRights => IsOwner || RightsLevel is > 0;
    public bool? IsSpectating { get; private set; }
    public RoomData? Data { get; private set; }
    public RoomResultDetails? Details { get; private set; }
    public RoomEntryTile? EntryTile { get; private set; }
    public RoomVisualizationSettings? VisualizationSettings { get; private set; }
    public RoomChatSettings? ChatSettings { get; private set; }
    public GameData? GameData { get; set; }
    internal Func<Id?>? OwnUserId { get; set; }
    public bool DataIsLoaded { get; private set; }
    public bool DetailsAreLoaded { get; private set; }
    public bool EntryTileIsLoaded { get; private set; }
    public bool PropertiesHaveBeenReceived { get; private set; }
    public bool VisualizationSettingsAreLoaded { get; private set; }
    public bool ChatSettingsAreLoaded { get; private set; }
    public bool AvatarsAreLoaded { get; private set; }
    public bool FloorItemsAreLoaded { get; private set; }
    public bool WallItemsAreLoaded { get; private set; }
    public bool ControllersAreLoaded { get; private set; }
    public bool FloorPlanIsLoaded { get; private set; }
    public bool HeightmapIsLoaded { get; private set; }

    public string Name => Data?.Name ?? "";
    public string OwnerName => Data?.OwnerName ?? "";
    public string Description => Data?.Description ?? "";
    public int Score => Data?.Score ?? 0;
    public Id GroupId => Data is { HasGroup: true } data ? data.GroupId : 0;
    public string GroupName => Data?.GroupName ?? "";
    public bool HasEvent => Data?.HasEvent ?? false;
    public string EventName => Data?.EventName ?? "";
    public string EventDescription => Data?.EventDescription ?? "";
    public IReadOnlyList<string> Tags => Data?.Tags ?? [];
    public IReadOnlyList<IdName> Controllers { get; private set; } = [];
    public FloorPlan? FloorPlan { get; private set; }
    public Heightmap? Heightmap { get; private set; }
    public RoomAuthorityState Authority => new(
        IsOwner,
        RightsLevel,
        RightsAreKnown,
        HasRights,
        IsSpectating);
    public IReadOnlyDictionary<string, string> Properties
    {
        get
        {
            lock (_state_sync)
                return new Dictionary<string, string>(_properties, StringComparer.Ordinal);
        }
    }
    public string? FloorProperty => Property("floor");
    public string? WallpaperProperty => Property("wallpaper");
    public string? LandscapeProperty => Property("landscape");
    public string? AnimatedLandscapeProperty => Property("landscapeanim");

    public IReadOnlyCollection<FloorItem> FloorItems => _floorItems.Values.ToArray();
    public IReadOnlyCollection<WallItem> WallItems => _wallItems.Values.ToArray();
    public IReadOnlyCollection<Avatar> Avatars => _avatars.Values.ToArray();

    public event Action<Id>? Entering;
    public event Action? Entered;
    public event Action? Ready;
    public event Action? Leaving;
    public event Action? Left;
    public event Action<RoomExitState>? Exited;
    /// <summary>
    /// Raised as soon as the server signals that the local user was kicked, which is
    /// before the room exit itself arrives.
    /// </summary>
    public event Action<RoomKick>? Kicked;
    public event Action<RoomData>? RoomDataUpdated;
    public event Action? FloorItemsLoaded;
    public event Action? WallItemsLoaded;
    public event Action<IReadOnlyList<Avatar>>? AvatarsAdded;
    public event Action<FloorItem>? FloorItemAdded;
    public event Action<Id>? FloorItemRemoved;
    public event Action<FloorItem>? FloorItemRemovedDetailed;
    public event Action<WallItem>? WallItemAdded;
    public event Action<Id>? WallItemRemoved;
    public event Action<WallItem>? WallItemRemovedDetailed;
    public event Action<Avatar>? AvatarRemoved;
    public event Action<Avatar>? AvatarUpdated;
    public event Action<FloorItem>? FloorItemUpdated;
    public event Action<WallItem>? WallItemUpdated;
    public event Action<Avatar, int>? AvatarActioned;
    public event Action<Avatar, Tile, Tile>? AvatarMoved;
    public event Action<Avatar, int, int>? AvatarDanceChanged;
    public event Action<Avatar, int, int>? AvatarEffectChanged;
    public event Action<Avatar, int, int>? AvatarHandItemChanged;
    public event Action<Avatar, bool, bool>? AvatarIdleChanged;
    public event Action<Avatar, bool, bool>? AvatarTypingChanged;
    public event Action<Avatar, string, string, string, string>? AvatarIdentityChanged;
    public event Action<FloorItem, Tile, Tile>? FloorItemMoved;
    public event Action<WallItem, WallLocation, WallLocation>? WallItemMoved;
    public event Action<FloorItem, ItemData, ItemData>? FloorItemDataChanged;
    /// <summary>
    /// Raised when a wall item's data - and with it its <see cref="WallItem.State"/> - changes,
    /// carrying the previous and the current data string.
    /// </summary>
    public event Action<WallItem, string, string>? WallItemDataChanged;
    /// <summary>
    /// Raised when a user standing in the room is renamed, carrying the previous and the new name.
    /// </summary>
    public event Action<Avatar, string, string>? AvatarNameChanged;
    public event Action<AvatarChat>? Chat;
    public event Action<RoomAccessTransition>? AccessStateChanged;
    public event Action<RoomQueueStatus>? QueueUpdated;
    public event Action<CanNotConnect>? ConnectionFailed;
    public event Action<Doorbell>? DoorbellRang;
    public event Action<FlatAccessible>? AccessGranted;
    public event Action<FlatAccessDenied>? AccessDenied;
    public event Action<RoomResultDetails>? DetailsUpdated;
    public event Action<RoomEntryTile>? EntryTileUpdated;
    public event Action<FlatProperty>? PropertyUpdated;
    public event Action<RoomVisualizationSettings>? VisualizationSettingsUpdated;
    public event Action<RoomChatSettings>? ChatSettingsUpdated;
    public event Action<RoomAuthorityState>? AuthorityChanged;
    public event Action<int?, int?>? RightsLevelChanged;
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

    private FloorItem? RemoveFloorItem(Id id)
    {
        if (!_floorItems.Remove(id, out FloorItem? item))
            return null;
        item.IsRemoved = true;
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
        if (!TryPlacementClient(out ClientType client))
            return;
        Publish(PlacementStateCommitted, new RoomPlacementStateCommit(
            kind,
            client,
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
        if (!TryPlacementClient(out ClientType client))
            return;
        Publish(PickupConfirmationReceived, new RoomPickupConfirmationCommit(
            client,
            session_generation,
            RoomId,
            Generation,
            checked(_revision + 1),
            message.Category,
            message.ItemId,
            message.Title,
            message.Body));
    }

    private bool TryPlacementClient(out ClientType client)
    {
        if (CurrentSession is { } session)
        {
            _placement_client = session.Client;
            _placement_session_bound = true;
        }
        client = _placement_client;
        return _placement_session_bound;
    }

    private void BindPlacementClient(ClientType client)
    {
        lock (_state_sync)
        {
            _placement_client = client;
            _placement_session_bound = true;
        }
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

    private void OnRoomState<T>(MessageKey key, Action<T> handler) where T : IParserComposer<T> =>
        OnIncoming<T>(key, message => Mutate(() =>
        {
            if (State is RoomSessionState.Entering or RoomSessionState.Ready)
                handler(message);
        }));

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

    private void OnRoomIncoming<T>(ClientType client, string name, Action<T> handler)
        where T : IParserComposer<T> =>
        OnIncoming<T>(client, name, message => Mutate(() => handler(message)));

    private void OnRoomState<T>(ClientType client, string name, Action<T> handler)
        where T : IParserComposer<T> =>
        OnRoomIncoming<T>(client, name, message =>
        {
            if (State is RoomSessionState.Entering or RoomSessionState.Ready)
                handler(message);
        });

    private void OnRoomOutgoing(string name, Action handler) =>
        OnOutgoing(name, () => Mutate(handler));

    private void OnRoomOutgoing<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        OnOutgoing<T>(name, message => Mutate(() => handler(message)));

    private void OnRoomOutgoing<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        OnOutgoing(contract, message => Mutate(() => handler(message)));

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

    private void BeginRoom(Id room_id)
    {
        bool new_room = RoomId != room_id || State is RoomSessionState.Outside or RoomSessionState.Leaving;
        if (new_room)
        {
            GuestRoomResult? pending_result = TakePendingRoomResult(room_id);
            LeaveRoom(source: RoomExitSource.RoomTransition);
            LastKick = null;
            Generation++;
            RoomId = room_id;
            State = RoomSessionState.Entering;
            if (pending_result is not null)
                SetRoomResult(pending_result, false);
            Publish(Entering, room_id);
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
        short? reason = null,
        RoomExitReason? native_reason = null)
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
                native_reason is not null,
                _pending_kick);
            LastExit = exit;
            LastNativeExitReason = native_reason;
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
            Generation++;
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

    public void EnrichFurni()
    {
        Mutate(() =>
        {
            foreach (FloorItem item in _floorItems.Values)
                Enrich(item);
            foreach (WallItem item in _wallItems.Values)
                Enrich(item);
        });
    }

    public FloorItem? FloorItem(Id id) => _floorItems.GetValueOrDefault(id);
    public WallItem? WallItem(Id id) => _wallItems.GetValueOrDefault(id);
    public Avatar? AvatarByIndex(int index) => _avatars.GetValueOrDefault(index);
    public Avatar? AvatarById(Id id) => _avatars.Values.FirstOrDefault(a => a.Id == id);
    public User? UserByName(string name) =>
        _avatars.Values.OfType<User>().FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));

    protected override void OnAttach()
    {
        if (CurrentSession is { } active_session)
            BindPlacementClient(active_session.Client);
        OnConnected(session => BindPlacementClient(session.Client));

        OnRoomOutgoing(MessageContracts.Room.Access.OpenRequest, message =>
        {
            BeginRoom(message.RoomId);
            SetAccessState(RoomAccessState.Connecting, message.RoomId);
        });

        OnRoomIncoming(MessageContracts.Room.Access.OpenConfirmed, message =>
        {
            BeginRoom(message.RoomId);
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
            BeginRoom(message.RoomId);
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

        OnRoomIncoming(MessageContracts.Room.Lifecycle.NativeExit, message =>
        {
            if (RoomId != 0 && message.RoomId == RoomId)
            {
                LeaveRoom(
                    source: RoomExitSource.NativeReason,
                    reason: message.Reason,
                    native_reason: message);
            }
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

        OnRoomIncoming(MessageContracts.Room.Authority.SpectatingEnded, _ =>
        {
            if (State is not (RoomSessionState.Entering or RoomSessionState.Ready))
                return;
            SetSpectating(false);
        });

        // The hotel does not only send this once on entry: it also delivers later furni in further
        // batches, which is how temporary furni arrive. The client treats every batch the same way
        // it treats a single ObjectAdd - onObjects and onObjectAdd both call addActiveObject and
        // neither drops what is already placed. Replacing the collection here emptied the room down
        // to whatever the newest batch carried. Leaving the room is what clears it, in Reset.
        OnRoomState<FloorItems>(MessageKeys.Room.Objects, ApplyFloorItems);

        // Same batching as Objects above: the client's onItems adds each element through the same
        // path as a single ItemAdd and never clears.
        OnRoomState<WallItems>(MessageKeys.Room.WallItems, message =>
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

        OnRoomState<FloorItemsRemove>(MessageKeys.Room.FloorItem.RemovedMultiple, message =>
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

        OnRoomState<WallItemsRemove>(MessageKeys.Room.WallItem.RemovedMultiple, message =>
        {
            foreach (Id id in message.Ids)
                RemoveWallItem(id);
        });

        OnRoomState<ItemStateUpdate>(MessageKeys.Room.WallItem.DataUpdated, message =>
            SetWallItemData(message.Id, message.ItemData));

        OnRoomState<WallItemsStateUpdate>(MessageKeys.Room.WallItem.DataBatchUpdated, message =>
        {
            foreach (ItemStateUpdate item in message.Items)
                SetWallItemData(item.Id, item.ItemData);
        });

        OnRoomState(MessageContracts.Room.Occupants.Removed, message =>
        {
            if (_avatars.Remove(message.Index, out Avatar? avatar))
            {
                avatar.IsRemoved = true;
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
                if (previous != avatar.Location)
                    Publish(AvatarMoved, avatar, previous, avatar.Location);
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
            Patch(message.Index, avatar =>
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

        OnRoomState(MessageContracts.Room.ItemPickupConfirmation, PublishPickupConfirmation);

        OnRoomState<FloorItemDataUpdate>(MessageKeys.Room.FloorItem.DataUpdated, message =>
        {
            if (_floorItems.TryGetValue(message.Id, out FloorItem? item))
            {
                ItemData previous = item.Data;
                item.Data = message.Data;
                Publish(FloorItemDataChanged, item, previous, item.Data);
                Publish(FloorItemUpdated, item);
            }
        });

        OnRoomState<FloorItemsDataUpdate>(MessageKeys.Room.FloorItem.DataBatchUpdated, message =>
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
            FloorPlanIsLoaded = true;
        });

        OnRoomState<Heightmap>(MessageKeys.Room.Heightmap.Snapshot, ApplyHeightmap);

        OnRoomState<HeightmapUpdate>(MessageKeys.Room.Heightmap.Diff, message =>
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
                    Publish(FloorItemUpdated, item);
                }

            if (message.Avatar is { } avatar)
            {
                if (!_avatars.TryGetValue(unchecked((int)(long)avatar.Index), out Avatar? mover))
                    return;
                Tile previous = mover.Location;
                mover.Location = new Tile(message.To.X, message.To.Y, avatar.ToZ);
                if (previous != mover.Location)
                    Publish(AvatarMoved, mover, previous, mover.Location);
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
                        if (previous_avatar_location != a.Location)
                            Publish(AvatarMoved, a, previous_avatar_location, a.Location);
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

        OnRoomState(MessageContracts.Room.Chat.Talk, message =>
            Publish(Chat, message with { Type = ChatType.Talk }));
        OnRoomState(MessageContracts.Room.Chat.Shout, message =>
            Publish(Chat, message with { Type = ChatType.Shout }));
        OnRoomState(MessageContracts.Room.Chat.Whisper, message =>
            Publish(Chat, message with { Type = ChatType.Whisper }));
    }

    protected override void Reset()
    {
        Mutate(() =>
        {
            _pending_room_results.Clear();
            LeaveRoom(source: RoomExitSource.Disconnected);
        });
    }
}
