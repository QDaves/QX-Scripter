using Qx.Interception;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Snapshots;

/// <summary>Provides the read queries that project the live game state into query envelopes.</summary>
/// <remarks>
/// Every query reads the state that has arrived so far and returns it together with a
/// <see cref="QueryMetadataSnapshot"/> that says whether it is complete, current and capped.
/// </remarks>
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

    /// <summary>Initializes a new instance of the <see cref="GameQueryService"/> class.</summary>
    /// <param name="game">The live game state to read from.</param>
    /// <param name="application">The application runtime that serves the profile, inventory, badge, achievement and wallet state.</param>
    /// <param name="session">A function that returns the open hotel session, or <see langword="null"/> when there is none.</param>
    /// <param name="interceptorConnected">
    /// A function that reports whether the interceptor is attached, or <see langword="null"/> to treat an
    /// open session as attached.
    /// </param>
    /// <param name="messageCatalogLoaded">
    /// A function that reports whether the message catalog is loaded, or <see langword="null"/> to treat an
    /// open session as loaded.
    /// </param>
    /// <param name="wireProfileAnalyzed">
    /// A function that reports whether the client build has been analyzed, or <see langword="null"/> to
    /// always report <see langword="false"/>.
    /// </param>
    /// <param name="wireProfileExact">
    /// A function that reports whether the analysis matched the build exactly, or <see langword="null"/> to
    /// always report <see langword="false"/>.
    /// </param>
    /// <param name="missingWireCapabilities">
    /// A function that lists the wire capabilities the build lacks, or <see langword="null"/> to report none.
    /// </param>
    /// <param name="timeProvider">The clock that stamps each envelope, or <see langword="null"/> for the system clock.</param>
    /// <param name="maxRoomItems">The cap on floor items and on wall items returned by <see cref="Furni"/>.</param>
    /// <param name="maxInventoryItems">The cap on items returned by <see cref="Inventory"/>.</param>
    /// <param name="maxHeightmapTiles">The cap on tiles returned by <see cref="Heightmap"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="game"/>, <paramref name="application"/> or <paramref name="session"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a cap is negative.</exception>
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

    /// <summary>Gets the state of the interceptor link, the hotel session and the client build analysis.</summary>
    /// <remarks>
    /// Ready and loaded only when all four are available. Otherwise the pending list names what is
    /// missing: <c>interceptor</c>, <c>hotelSession</c>, <c>messageCatalog</c> or
    /// <c>wireProfileAnalysis</c>.
    /// </remarks>
    /// <returns>The <c>connection</c> query envelope.</returns>
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

    /// <summary>Gets the current room session with its data, environment and content counts.</summary>
    /// <remarks>
    /// Ready once connected and the room session is ready; loaded once every piece in
    /// <see cref="RoomContentStateSnapshot"/> has arrived. Stale when the state is left over from a
    /// closed connection or the room data belongs to another room.
    /// </remarks>
    /// <returns>The <c>room</c> query envelope.</returns>
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

    /// <summary>Gets every avatar in the current room.</summary>
    /// <returns>The <c>avatars</c> query envelope, never truncated.</returns>
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

    /// <summary>Gets the floor and wall items in the current room, with their furni definitions.</summary>
    /// <remarks>
    /// Each list is capped separately at the room item cap given to the constructor. Loaded only once the
    /// floor items, the wall items and the furni definitions have all arrived.
    /// </remarks>
    /// <returns>The <c>furni</c> query envelope.</returns>
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

    /// <summary>Gets the local user's profile.</summary>
    /// <returns>The <c>profile</c> query envelope, whose data is <see langword="null"/> until the profile has arrived.</returns>
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

    /// <summary>Gets the friend list with its categories and limits.</summary>
    /// <returns>The <c>friends</c> query envelope, never truncated.</returns>
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

    /// <summary>Gets the local user's furni inventory, with the furni definition of each item.</summary>
    /// <remarks>
    /// Capped at the inventory item cap given to the constructor. Loaded only once the inventory and the
    /// furni definitions have both arrived.
    /// </remarks>
    /// <returns>The <c>inventory</c> query envelope.</returns>
    public QueryEnvelope<InventorySnapshot> Inventory()
    {
        InventoryFurniPage page = InventoryApplicationPages.ReadFurni(
            application,
            maxItems: max_inventory_items);
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

    /// <summary>Gets the users with rights in the current room.</summary>
    /// <remarks>The hotel only sends this list to the room owner.</remarks>
    /// <returns>The <c>controllers</c> query envelope, never truncated.</returns>
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

    /// <summary>Gets the local user's credits and activity point balances.</summary>
    /// <remarks>Ready once either balance has arrived, loaded once both have.</remarks>
    /// <returns>The <c>currencies</c> query envelope.</returns>
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

    /// <summary>Gets the live heightmap of the current room.</summary>
    /// <remarks>Capped at the heightmap tile cap given to the constructor.</remarks>
    /// <returns>The <c>heightmap</c> query envelope, whose data is <see langword="null"/> until the heightmap has arrived.</returns>
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
