using Qx.Interception;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Snapshots;

public sealed partial class GameQueryService
{
    private readonly GameState game;
    private readonly IApplicationRuntime application;
    private readonly Func<Session?> session;
    private readonly Func<bool> interceptor_connected;
    private readonly Func<bool> message_catalog_loaded;
    private readonly Func<bool> wire_profile_analyzed;
    private readonly Func<bool> wire_profile_exact;
    private readonly Func<IReadOnlyList<string>> missing_wire_capabilities;
    private readonly TimeProvider time_provider;
    private readonly int max_room_items;
    private readonly int max_inventory_items;
    private readonly int max_heightmap_tiles;

    public GameQueryService(
        GameState game,
        IApplicationRuntime application,
        Func<Session?> session,
        Func<bool>? interceptorConnected = null,
        Func<bool>? messageCatalogLoaded = null,
        Func<bool>? wireProfileAnalyzed = null,
        Func<bool>? wireProfileExact = null,
        Func<IReadOnlyList<string>>? missingWireCapabilities = null,
        TimeProvider? timeProvider = null,
        int maxRoomItems = 200,
        int maxInventoryItems = 500,
        int maxHeightmapTiles = 4096)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRoomItems);
        ArgumentOutOfRangeException.ThrowIfNegative(maxInventoryItems);
        ArgumentOutOfRangeException.ThrowIfNegative(maxHeightmapTiles);

        this.game = game;
        this.application = application;
        this.session = session;
        interceptor_connected = interceptorConnected ?? (() => session() is not null);
        message_catalog_loaded = messageCatalogLoaded ?? (() => session() is not null);
        wire_profile_analyzed = wireProfileAnalyzed ?? (() => false);
        wire_profile_exact = wireProfileExact ?? (() => false);
        missing_wire_capabilities = missingWireCapabilities ?? (() => []);
        time_provider = timeProvider ?? TimeProvider.System;
        max_room_items = maxRoomItems;
        max_inventory_items = maxInventoryItems;
        max_heightmap_tiles = maxHeightmapTiles;
    }

    public QueryEnvelope<ConnectionSnapshot> Connection()
    {
        Session? current_session = session();
        ConnectionSnapshot snapshot = SnapshotFactory.Connection(
            current_session,
            interceptor_connected(),
            message_catalog_loaded(),
            wire_profile_analyzed(),
            wire_profile_exact(),
            missing_wire_capabilities());
        var pending = new List<string>();
        if (!snapshot.InterceptorConnected)
            pending.Add("interceptor");
        if (!snapshot.HotelConnected)
            pending.Add("hotelSession");
        if (snapshot.HotelConnected && !snapshot.MessageCatalogLoaded)
            pending.Add("messageCatalog");
        if (snapshot.HotelConnected &&
            snapshot.MessageCatalogLoaded &&
            !snapshot.WireProfileAnalyzed)
        {
            pending.Add("wireProfileAnalysis");
        }
        bool ready = snapshot.InterceptorConnected &&
                     snapshot.HotelConnected &&
                     snapshot.MessageCatalogLoaded &&
                     snapshot.WireProfileAnalyzed &&
                     pending.Count == 0;
        return Result(
            "connection",
            snapshot,
            ready,
            ready,
            false,
            false,
            pending);
    }

    public QueryEnvelope<RoomSnapshot> Room()
    {
        RoomManager room = game.Room;
        bool connected = Connected;
        QueryRead<RoomSnapshot> read = room.Capture(current =>
        {
            Avatar[] avatars = current.Avatars.ToArray();
            FloorItem[] floor_items = current.FloorItems.ToArray();
            WallItem[] wall_items = current.WallItems.ToArray();
            RoomContentStateSnapshot content = ContentState(current);
            string[] pending = RoomPending(content);
            RoomSnapshot snapshot = new(
                current.IsInRoom,
                current.IsReady,
                current.State.ToString(),
                current.Generation,
                current.IsInRoom ? current.RoomId : null,
                SnapshotFactory.RoomAccess(current),
                current.RoomType,
                current.IsOwner,
                current.HasRights,
                SnapshotFactory.RoomAuthority(current),
                current.Data is { } data ? SnapshotFactory.From(data) : null,
                current.Details is { } details ? SnapshotFactory.From(details) : null,
                SnapshotFactory.RoomEnvironment(current),
                content,
                avatars.Length,
                floor_items.Length,
                wall_items.Length,
                current.Controllers.Count,
                current.FloorPlan is { } floor_plan ? SnapshotFactory.From(floor_plan) : null,
                current.Heightmap is { } heightmap ? SnapshotFactory.HeightmapSummary(heightmap) : null);
            bool ready = connected && current.IsReady;
            return new QueryRead<RoomSnapshot>(
                snapshot,
                ready,
                ready && pending.Length == 0,
                RoomStateIsStale(current, connected),
                false,
                pending);
        });
        return Result("room", read);
    }

    public QueryEnvelope<AvatarCollectionSnapshot> Avatars()
    {
        RoomManager room = game.Room;
        bool connected = Connected;
        QueryRead<AvatarCollectionSnapshot> read = room.Capture(current =>
        {
            AvatarCollectionSnapshot snapshot = SnapshotFactory.Avatars(
                current.Avatars,
                current.IsInRoom ? current.RoomId : null,
                current.Generation);
            bool ready = connected && current.IsReady;
            bool loaded = ready && current.AvatarsAreLoaded;
            return new QueryRead<AvatarCollectionSnapshot>(
                snapshot,
                ready,
                loaded,
                RoomStateIsStale(current, connected),
                false,
                loaded ? [] : ["avatars"]);
        });
        return Result("avatars", read);
    }

    public QueryEnvelope<FurniCollectionSnapshot> Furni()
    {
        RoomManager room = game.Room;
        bool connected = Connected;
        QueryRead<FurniCollectionSnapshot> read = room.Capture(current =>
        {
            FurniCollectionSnapshot snapshot = SnapshotFactory.Furni(
                current.FloorItems,
                current.WallItems,
                game.GameData.Furni,
                max_room_items,
                current.IsInRoom ? current.RoomId : null,
                current.Generation);
            bool ready = connected && current.IsReady;
            var pending = new List<string>();
            if (!current.FloorItemsAreLoaded)
                pending.Add("floorItems");
            if (!current.WallItemsAreLoaded)
                pending.Add("wallItems");
            if (!game.GameData.IsLoaded)
                pending.Add("definitions");
            return new QueryRead<FurniCollectionSnapshot>(
                snapshot,
                ready,
                ready && pending.Count == 0,
                RoomStateIsStale(current, connected),
                snapshot.FloorItemsTruncated || snapshot.WallItemsTruncated,
                pending);
        });
        return Result("furni", read);
    }

    public QueryEnvelope<ProfileSnapshot?> Profile()
    {
        ProfileStateView state = ReadProfileState();
        return ProfileEnvelope(state);
    }

    private ProfileStateView ReadProfileState() =>
        application.Invoke<ProfileStateRequest, ProfileStateView>(
            ApplicationMemberIds.ProfileState,
            new ProfileStateRequest());

    private QueryEnvelope<ProfileSnapshot?> ProfileEnvelope(ProfileStateView state)
    {
        ProfileSnapshot? snapshot = state.Identity is { } profile
            ? new ProfileSnapshot(
                profile.Id,
                profile.Name,
                profile.Figure,
                profile.Gender.ToString(),
                profile.Motto,
                profile.RealName,
                profile.DirectMail,
                profile.RespectTotal,
                profile.RespectLeft,
                profile.PetRespectLeft,
                profile.StreamPublishingAllowed,
                profile.LastAccessDate,
                profile.IsNameChangeable,
                profile.IsSafetyLocked,
                profile.IsTradeLocked,
                profile.NameColor,
                profile.RespectReplenishesLeft,
                profile.MaxRespectPerDay)
            : null;
        bool loaded = snapshot is not null;
        return Result(
            "profile",
            snapshot,
            state.Connected && loaded,
            loaded,
            !state.Connected && loaded,
            false,
            loaded ? [] : ["profile"]);
    }

    public QueryEnvelope<FriendCollectionSnapshot> Friends()
    {
        bool connected = Connected;
        QueryRead<FriendCollectionSnapshot> read = game.Friends.Capture(current =>
        {
            FriendCollectionSnapshot snapshot = SnapshotFactory.Friends(
                current.Friends,
                current.Categories,
                current.UserLimit,
                current.NormalLimit,
                current.ExtendedLimit);
            return new QueryRead<FriendCollectionSnapshot>(
                snapshot,
                connected && current.IsLoaded,
                current.IsLoaded,
                current.IsStale || !connected && snapshot.Total > 0,
                false,
                current.IsLoaded ? [] : ["friends"]);
        });
        return Result("friends", read);
    }

    public QueryEnvelope<InventorySnapshot> Inventory()
    {
        InventoryFurniPage page = InventoryApplicationPages.ReadFurni(
            application,
            max_items: max_inventory_items);
        FurniData? definitions = game.GameData.Furni;
        InventoryItemSnapshot[] items =
        [
            .. page.Items.Select(item => SnapshotFactory.WithDefinition(item, definitions))
        ];
        var snapshot = new InventorySnapshot(
            definitions is not null,
            page.Loading,
            page.Stale,
            page.LoadGeneration,
            page.ExpectedFragments,
            page.ReceivedFragments,
            page.Total,
            items.Length,
            max_inventory_items,
            items.Length < page.Total,
            Array.AsReadOnly(items));
        var pending = new List<string>();
        if (!page.Loaded)
            pending.Add("inventory");
        if (!game.GameData.IsLoaded)
            pending.Add("definitions");
        return Result(
            "inventory",
            snapshot,
            page.Connected && page.Loaded,
            page.Connected && pending.Count == 0,
            page.Stale || !page.Connected && page.Total > 0,
            snapshot.Truncated,
            pending);
    }

    public QueryEnvelope<ControllerCollectionSnapshot> Controllers()
    {
        bool connected = Connected;
        QueryRead<ControllerCollectionSnapshot> read = game.Room.Capture(current =>
        {
            ControllerCollectionSnapshot snapshot = SnapshotFactory.Controllers(
                current.Controllers,
                current.IsInRoom ? current.RoomId : null,
                current.Generation,
                current.IsOwner);
            bool ready = connected && current.IsReady;
            bool loaded = ready && current.ControllersAreLoaded;
            return new QueryRead<ControllerCollectionSnapshot>(
                snapshot,
                ready,
                loaded,
                RoomStateIsStale(current, connected),
                false,
                loaded ? [] : ["controllers"]);
        });
        return Result("controllers", read);
    }

    public QueryEnvelope<CurrencySnapshot> Currencies()
    {
        WalletStateView state = WalletApplicationPages.Read(application);
        IReadOnlyDictionary<int, int> points = state.ActivityPoints.Points.ToDictionary(
            point => point.Type,
            point => point.Amount);
        var snapshot = new CurrencySnapshot(
            state.CreditsLoaded,
            state.Credits,
            state.PointsLoaded,
            state.PointsLoaded ? points.GetValueOrDefault(WalletPointTypes.Diamonds) : null,
            state.PointsLoaded ? points.GetValueOrDefault(WalletPointTypes.Duckets) : null,
            points);
        bool loaded = snapshot.CreditsLoaded && snapshot.PointsLoaded;
        var pending = new List<string>();
        if (!snapshot.CreditsLoaded)
            pending.Add("credits");
        if (!snapshot.PointsLoaded)
            pending.Add("points");
        var read = new QueryRead<CurrencySnapshot>(
            snapshot,
            state.Connected && (snapshot.CreditsLoaded || snapshot.PointsLoaded),
            loaded,
            !state.Connected && (snapshot.CreditsLoaded || snapshot.PointsLoaded),
            false,
            pending);
        return Result("currencies", read);
    }

    public QueryEnvelope<HeightmapSnapshot?> Heightmap()
    {
        bool connected = Connected;
        QueryRead<HeightmapSnapshot?> read = game.Room.Capture(current =>
        {
            HeightmapSnapshot? snapshot = current.Heightmap is not { } heightmap
                ? null
                : SnapshotFactory.Heightmap(
                    heightmap,
                    max_heightmap_tiles,
                    current.IsInRoom ? current.RoomId : null,
                    current.Generation);
            bool ready = connected && current.IsReady;
            bool loaded = ready && current.HeightmapIsLoaded && snapshot is not null;
            return new QueryRead<HeightmapSnapshot?>(
                snapshot,
                ready,
                loaded,
                RoomStateIsStale(current, connected),
                snapshot?.Truncated ?? false,
                loaded ? [] : ["heightmap"]);
        });
        return Result("heightmap", read);
    }

    private bool Connected => session() is not null;

    private RoomContentStateSnapshot ContentState(RoomManager room) =>
        new(
            room.DataIsLoaded,
            room.DetailsAreLoaded,
            room.EntryTileIsLoaded,
            room.PropertiesHaveBeenReceived,
            room.VisualizationSettingsAreLoaded,
            room.ChatSettingsAreLoaded,
            room.RightsAreKnown,
            room.IsSpectating.HasValue,
            room.AvatarsAreLoaded,
            room.FloorItemsAreLoaded,
            room.WallItemsAreLoaded,
            room.ControllersAreLoaded,
            room.FloorPlanIsLoaded,
            room.HeightmapIsLoaded,
            game.GameData.IsLoaded);

    private static bool RoomStateIsStale(RoomManager room, bool connected) =>
        !connected && (room.IsInRoom ||
                       room.Data is not null ||
                       room.Avatars.Count > 0 ||
                       room.FloorItems.Count > 0 ||
                       room.WallItems.Count > 0) ||
        room.IsInRoom && room.Data is { } data && data.Id != room.RoomId;

    private QueryEnvelope<T> Result<T>(
        string query,
        T data,
        bool ready,
        bool loaded,
        bool stale,
        bool truncated,
        IReadOnlyList<string> pending) =>
        QueryResults.Success(
            query,
            data,
            ready,
            loaded,
            stale,
            truncated,
            pending,
            time_provider.GetUtcNow());

    private QueryEnvelope<T> Result<T>(string query, QueryRead<T> read) =>
        Result(
            query,
            read.Data,
            read.Ready,
            read.Loaded,
            read.Stale,
            read.Truncated,
            read.Pending);

    private static string[] RoomPending(RoomContentStateSnapshot content)
    {
        var pending = new List<string>();
        if (!content.DataLoaded)
            pending.Add("roomData");
        if (!content.DetailsLoaded)
            pending.Add("roomDetails");
        if (!content.EntryTileLoaded)
            pending.Add("roomEntryTile");
        if (!content.PropertiesReceived)
            pending.Add("roomProperties");
        if (!content.VisualizationSettingsLoaded)
            pending.Add("roomVisualization");
        if (!content.ChatSettingsLoaded)
            pending.Add("roomChatSettings");
        if (!content.RightsKnown)
            pending.Add("roomRights");
        if (!content.SpectatorKnown)
            pending.Add("roomSpectator");
        if (!content.AvatarsLoaded)
            pending.Add("avatars");
        if (!content.FloorItemsLoaded)
            pending.Add("floorItems");
        if (!content.WallItemsLoaded)
            pending.Add("wallItems");
        if (!content.ControllersLoaded)
            pending.Add("controllers");
        if (!content.FloorPlanLoaded)
            pending.Add("floorPlan");
        if (!content.HeightmapLoaded)
            pending.Add("heightmap");
        if (!content.DefinitionsLoaded)
            pending.Add("definitions");
        return [.. pending];
    }

    private readonly record struct QueryRead<T>(
        T Data,
        bool Ready,
        bool Loaded,
        bool Stale,
        bool Truncated,
        IReadOnlyList<string> Pending);
}
