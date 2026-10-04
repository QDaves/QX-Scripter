using System.Text.Json.Serialization;
using Qx;
using Qx.Model;

namespace Qx.Game.Snapshots;

/// <summary>
/// Represents the load state that accompanies every payload of the MCP read tools, describing how
/// complete and how trustworthy it is.
/// </summary>
/// <remarks>
/// This is what separates "the room genuinely has no furni" from "the furni packets have
/// not arrived yet". An empty collection with <paramref name="Loaded"/> <see langword="true"/>
/// is an authoritative empty result; the same empty collection with <paramref name="Loaded"/>
/// <see langword="false"/> only means the data is still in flight, and the names of the
/// missing pieces are listed in <paramref name="Pending"/>.
/// </remarks>
/// <param name="Ready">
/// Whether the subsystem is connected and usable at all: a hotel session exists and, for
/// room queries, the room session has reached its ready state. The payload can still be
/// incomplete while this is <see langword="true"/>.
/// </param>
/// <param name="Loaded">
/// Whether every part of the answer has arrived. Implies <paramref name="Ready"/> and an
/// empty <paramref name="Pending"/>. Only when this is <see langword="true"/> may an empty
/// payload be read as "there is nothing".
/// </param>
/// <param name="Stale">
/// Whether the payload was retained from a connection or room session that has since ended,
/// or contradicts the current room. The data is returned as-is but is no longer authoritative.
/// </param>
/// <param name="Truncated">
/// Whether the projection dropped items to stay under its per-query cap. The payload's own
/// total and returned counts say how many were lost.
/// </param>
/// <param name="CapturedAtUtc">The UTC instant at which the underlying state was read.</param>
/// <param name="Pending">
/// The names of the pieces that have not arrived yet, for example <c>"floorItems"</c>,
/// <c>"definitions"</c>, <c>"heightmap"</c>, <c>"credits"</c> or <c>"messageCatalog"</c>.
/// Empty when <paramref name="Loaded"/> is <see langword="true"/>.
/// </param>
public sealed record QueryMetadataSnapshot(
    bool Ready,
    bool Loaded,
    bool Stale,
    bool Truncated,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<string> Pending);

/// <summary>
/// Represents a failed MCP read query, classified so a caller can react without parsing an
/// exception message.
/// </summary>
/// <param name="Code">
/// The stable failure class. One of <c>cancelled</c>, <c>timeout</c>, <c>disconnected</c>,
/// <c>invalid_response</c>, <c>correlation_error</c>, <c>unsupported</c>, <c>not_found</c>,
/// <c>invalid_request</c>, <c>connection_error</c>, <c>unavailable</c> or <c>request_failed</c>.
/// Prefer this over <paramref name="Type"/>.
/// </param>
/// <param name="Type">The full CLR type name of the originating exception.</param>
/// <param name="Message">The exception message, for diagnostics only.</param>
/// <param name="OutgoingName">
/// The name of the request message that was sent, when the failure was a timeout or a
/// disconnect while awaiting a reply; otherwise, <see langword="null"/>.
/// </param>
/// <param name="IncomingName">
/// The name of the reply message that was awaited or that failed to parse; otherwise
/// <see langword="null"/>.
/// </param>
/// <param name="ResponseType">
/// The CLR type the reply was being parsed or matched into, for parse and correlation
/// failures; otherwise, <see langword="null"/>.
/// </param>
/// <param name="TimeoutMs">
/// The timeout in milliseconds that elapsed, present only when <paramref name="Code"/> is
/// <c>timeout</c> and the failure came from a request broker.
/// </param>
/// <param name="ResourceName">
/// The fragmented resource (for example the inventory) whose load was correlated to the
/// wrong request; present only for <c>correlation_error</c>.
/// </param>
/// <param name="RetiredRequestEpoch">
/// The request epoch whose fragments arrived too late; present only for
/// <c>correlation_error</c>.
/// </param>
/// <param name="ActiveRequestEpoch">
/// The request epoch that was current when the stale fragments arrived; present only for
/// <c>correlation_error</c>.
/// </param>
public sealed record QueryErrorSnapshot(
    string Code,
    string Type,
    string Message,
    string? OutgoingName,
    string? IncomingName,
    string? ResponseType,
    int? TimeoutMs,
    string? ResourceName,
    long? RetiredRequestEpoch,
    long? ActiveRequestEpoch);

/// <summary>
/// Represents the wrapper every MCP read query returns: the payload plus its load state, or an
/// error.
/// </summary>
/// <remarks>
/// On success <paramref name="Error"/> is <see langword="null"/>; on failure
/// <paramref name="Data"/> is <see langword="default"/> and every metadata flag is
/// <see langword="false"/>. A <see langword="null"/> <paramref name="Data"/> is not by
/// itself a failure: queries whose payload is nullable (profile, heightmap) return
/// <see langword="null"/> with no error when the value does not exist yet.
/// </remarks>
/// <typeparam name="T">The type of the projected payload.</typeparam>
/// <param name="Query">The query name, for example <c>room</c>, <c>furni</c> or <c>inventory</c>.</param>
/// <param name="Metadata">How complete and how current <paramref name="Data"/> is.</param>
/// <param name="Data">The projected state, or <see langword="default"/> when the query failed.</param>
/// <param name="Error">The failure description, or <see langword="null"/> when the query succeeded.</param>
public sealed record QueryEnvelope<T>(
    string Query,
    QueryMetadataSnapshot Metadata,
    T? Data,
    QueryErrorSnapshot? Error);

/// <summary>
/// Represents the JSON projection of the interceptor link, the hotel session and the analysis of
/// the connected client build for the MCP read tools; scripts use the <c>IsConnected</c> and
/// <c>Session</c> globals.
/// </summary>
/// <param name="InterceptorConnected">Whether the packet interceptor is attached.</param>
/// <param name="HotelConnected">Whether a hotel session is open.</param>
/// <param name="MessageCatalogLoaded">Whether the message name catalog is available.</param>
/// <param name="WireProfileAnalyzed">Whether the connected client build has been analyzed.</param>
/// <param name="WireProfileExact">Whether the analysis matched this build exactly rather than falling back.</param>
/// <param name="MissingWireCapabilities">The wire capabilities the connected build lacks; empty when it lacks none.</param>
/// <param name="Host">The hotel server host name, or <see langword="null"/> when no session is open.</param>
/// <param name="Port">The hotel server port, or <see langword="null"/> when no session is open.</param>
/// <param name="HotelVersion">The hotel client version string, or <see langword="null"/> when no session is open.</param>
/// <param name="ClientIdentifier">
/// The client identifier reported by the interceptor, or <see langword="null"/> when no
/// session is open.
/// </param>
public sealed record ConnectionSnapshot(
    bool InterceptorConnected,
    bool HotelConnected,
    bool MessageCatalogLoaded,
    bool WireProfileAnalyzed,
    bool WireProfileExact,
    IReadOnlyList<string> MissingWireCapabilities,
    string? Host,
    int? Port,
    string? HotelVersion,
    string? ClientIdentifier);

/// <summary>
/// Represents the JSON projection of a point in room space for the MCP read tools; scripts use
/// <see cref="Tile"/>.
/// </summary>
/// <param name="X">The tile column.</param>
/// <param name="Y">The tile row.</param>
/// <param name="Z">
/// The height above the floor in tile units, where one unit is one full tile height.
/// For furni this is the stack height the item sits at.
/// </param>
public sealed record PositionSnapshot(int X, int Y, float Z);

/// <summary>
/// Represents the JSON projection of the rectangle of tiles an object occupies for the MCP read
/// tools; scripts use <see cref="Area"/>.
/// </summary>
/// <param name="Origin">The anchor tile, which is the object's own position.</param>
/// <param name="Width">The extent along X in tiles, already rotated for the object's direction.</param>
/// <param name="Length">The extent along Y in tiles, already rotated for the object's direction.</param>
public sealed record AreaSnapshot(PositionSnapshot Origin, int Width, int Length);

/// <summary>
/// Represents the JSON projection of a room's navigator record used by the MCP read tools and the
/// application-layer results; scripts that read live state use <see cref="RoomData"/>.
/// </summary>
/// <param name="Id">The room identifier.</param>
/// <param name="Name">The room name.</param>
/// <param name="OwnerId">The identifier of the owning user.</param>
/// <param name="OwnerName">The name of the owning user.</param>
/// <param name="DoorMode">
/// Who may walk in, as the numeric value of <see cref="RoomDoorMode"/>: 0 open, 1 doorbell,
/// 2 password, 3 invisible, 4 new users only.
/// </param>
/// <param name="UserCount">How many avatars are inside right now.</param>
/// <param name="MaxUserCount">The capacity of the room.</param>
/// <param name="Description">The room description.</param>
/// <param name="TradeMode">
/// Who may trade inside, as the numeric value of <see cref="RoomTradeMode"/>: 0 nobody,
/// 1 rights holders only, 2 everyone.
/// </param>
/// <param name="Score">The room's like count.</param>
/// <param name="Ranking">The room's position in the hotel ranking.</param>
/// <param name="Category">
/// The navigator category identifier. The numbering is defined per hotel by the navigator
/// configuration, so it must be resolved against that list rather than assumed.
/// </param>
/// <param name="Tags">The room's search tags.</param>
/// <param name="OfficialRoomPicture">
/// The staff-room picture reference, or <see langword="null"/> for an ordinary room.
/// </param>
/// <param name="HasGroup">Whether the room belongs to a group. The group fields are meaningless when this is <see langword="false"/>.</param>
/// <param name="GroupId">The owning group's identifier.</param>
/// <param name="GroupName">The owning group's name.</param>
/// <param name="GroupBadge">The owning group's badge code.</param>
/// <param name="HasEvent">Whether a room event is running. The event fields are meaningless when this is <see langword="false"/>.</param>
/// <param name="EventName">The running event's title.</param>
/// <param name="EventDescription">The running event's description.</param>
/// <param name="EventMinutesRemaining">Minutes left before the event expires.</param>
/// <param name="ShowOwner">Whether the navigator displays the owner's name.</param>
/// <param name="AllowPets">Whether visitors may bring pets in.</param>
/// <param name="DisplayRoomEntryAd">Whether the client shows an entry advertisement for this room.</param>
public sealed record RoomDataSnapshot(
    Id Id,
    string Name,
    Id OwnerId,
    string OwnerName,
    int DoorMode,
    int UserCount,
    int MaxUserCount,
    string Description,
    int TradeMode,
    int Score,
    int Ranking,
    int Category,
    IReadOnlyList<string> Tags,
    string? OfficialRoomPicture,
    bool HasGroup,
    Id GroupId,
    string GroupName,
    string GroupBadge,
    bool HasEvent,
    string EventName,
    string EventDescription,
    int EventMinutesRemaining,
    bool ShowOwner,
    bool AllowPets,
    bool DisplayRoomEntryAd);

/// <summary>
/// Represents the JSON projection of one rectangular hole an area-hider furni cuts into the floor
/// for the MCP read tools; scripts use <see cref="AreaHideData"/>.
/// </summary>
/// <param name="FurniId">The identifier of the furni that owns the hole.</param>
/// <param name="On">Whether the hider is currently switched on.</param>
/// <param name="RootX">The X tile of the rectangle's corner.</param>
/// <param name="RootY">The Y tile of the rectangle's corner.</param>
/// <param name="Width">The rectangle's extent along X in tiles.</param>
/// <param name="Length">The rectangle's extent along Y in tiles.</param>
/// <param name="Invert">
/// The client's <c>furniture_area_hide_invert</c> flag, which flips which side of the
/// rectangle is hidden.
/// </param>
public sealed record HiddenAreaSnapshot(
    Id FurniId,
    bool On,
    int RootX,
    int RootY,
    int Width,
    int Length,
    bool Invert);

/// <summary>
/// Represents the JSON projection of the current room's static floor plan for the MCP read tools;
/// scripts use <see cref="FloorPlan"/>.
/// </summary>
/// <param name="UseLegacyScale">Whether the room is drawn at the legacy 32 pixel tile scale instead of 64.</param>
/// <param name="WallHeight">The fixed wall height the hotel sends with the floor plan.</param>
/// <param name="Map">
/// The raw height map text, one line per row, where <c>x</c> marks a void tile and
/// <c>0</c> to <c>9</c> then <c>a</c> to <c>z</c> give tile heights 0 to 35.
/// </param>
/// <param name="Width">The column count, the length of the longest map line.</param>
/// <param name="Length">The row count, the number of map lines.</param>
/// <param name="Scale">The tile scale in pixels: 32 when <paramref name="UseLegacyScale"/> is set, otherwise 64.</param>
/// <param name="Tiles">
/// The tile heights in row-major order, <paramref name="Width"/> times <paramref name="Length"/>
/// entries, with -1 for void tiles and for positions past the end of a short line.
/// </param>
/// <param name="HiddenAreas">The floor areas hidden by area-hider furni.</param>
/// <param name="HasCameraData">Whether the floor plan carried camera coordinates.</param>
/// <param name="CameraX">The camera X coordinate, or <see langword="null"/> when <paramref name="HasCameraData"/> is <see langword="false"/>.</param>
/// <param name="CameraY">The camera Y coordinate, or <see langword="null"/> when <paramref name="HasCameraData"/> is <see langword="false"/>.</param>
/// <param name="CameraZ">The camera Z coordinate, or <see langword="null"/> when <paramref name="HasCameraData"/> is <see langword="false"/>.</param>
public sealed record FloorPlanSnapshot(
    bool UseLegacyScale,
    int WallHeight,
    string Map,
    int Width,
    int Length,
    int Scale,
    IReadOnlyList<int> Tiles,
    IReadOnlyList<HiddenAreaSnapshot> HiddenAreas,
    bool HasCameraData,
    int? CameraX,
    int? CameraY,
    float? CameraZ);

/// <summary>
/// Represents aggregate counts over the live heightmap, so the MCP read tools can judge walkability
/// without shipping every tile; scripts use <see cref="Heightmap"/>.
/// </summary>
/// <param name="Width">The heightmap's column count.</param>
/// <param name="Length">The heightmap's row count.</param>
/// <param name="TileCount">Every tile in the heightmap, including void tiles.</param>
/// <param name="FloorTileCount">Tiles that are floor at all, blocked or not.</param>
/// <param name="WalkableTileCount">Floor tiles that are currently free to step on.</param>
/// <param name="BlockedTileCount">Floor tiles currently blocked, normally by furni.</param>
/// <param name="NonFloorTileCount">Void tiles; equal to <paramref name="TileCount"/> minus <paramref name="FloorTileCount"/>.</param>
public sealed record HeightmapSummarySnapshot(
    int Width,
    int Length,
    int TileCount,
    int FloorTileCount,
    int WalkableTileCount,
    int BlockedTileCount,
    int NonFloorTileCount);

/// <summary>
/// Represents the JSON projection of which pieces of the current room session have arrived for the
/// MCP read tools; scripts use the loaded flags of <see cref="RoomManager"/>.
/// </summary>
/// <remarks>
/// Each flag is the per-piece counterpart of the envelope's <c>Loaded</c> flag: a value of
/// <see langword="false"/> means "not received yet", never "empty".
/// </remarks>
/// <param name="DataLoaded">The navigator record for the room has arrived.</param>
/// <param name="DetailsLoaded">The guest-room result detail block has arrived.</param>
/// <param name="EntryTileLoaded">The door tile and its direction are known.</param>
/// <param name="PropertiesReceived">At least one room property (floor, wallpaper, landscape) has arrived.</param>
/// <param name="VisualizationSettingsLoaded">Wall and floor thickness are known.</param>
/// <param name="ChatSettingsLoaded">The room chat configuration has arrived.</param>
/// <param name="RightsKnown">
/// The local user's rights in this room are settled, either because ownership was confirmed
/// or because a rights level was received.
/// </param>
/// <param name="SpectatorKnown">Whether the local user is spectating has been determined.</param>
/// <param name="AvatarsLoaded">The initial avatar list has arrived.</param>
/// <param name="FloorItemsLoaded">The floor item list has arrived.</param>
/// <param name="WallItemsLoaded">The wall item list has arrived.</param>
/// <param name="ControllersLoaded">The list of users with rights has arrived.</param>
/// <param name="FloorPlanLoaded">The static floor plan has arrived.</param>
/// <param name="HeightmapLoaded">The live heightmap has arrived.</param>
/// <param name="DefinitionsLoaded">
/// The furni definition catalog is available, which is what fills the <c>Definition</c>
/// blocks on item snapshots.
/// </param>
public sealed record RoomContentStateSnapshot(
    bool DataLoaded,
    bool DetailsLoaded,
    bool EntryTileLoaded,
    bool PropertiesReceived,
    bool VisualizationSettingsLoaded,
    bool ChatSettingsLoaded,
    bool RightsKnown,
    bool SpectatorKnown,
    bool AvatarsLoaded,
    bool FloorItemsLoaded,
    bool WallItemsLoaded,
    bool ControllersLoaded,
    bool FloorPlanLoaded,
    bool HeightmapLoaded,
    bool DefinitionsLoaded);

/// <summary>
/// Represents the JSON projection of the tile an avatar is placed on when entering the room for the
/// MCP read tools; scripts use <see cref="Qx.Model.Messages.Incoming.RoomEntryTile"/>.
/// </summary>
/// <param name="X">The door tile's column.</param>
/// <param name="Y">The door tile's row.</param>
/// <param name="Direction">The facing the avatar is given on arrival, 0-7 clockwise from north.</param>
public sealed record RoomEntryTileSnapshot(int X, int Y, int Direction);

/// <summary>
/// Represents the JSON projection of how the room's walls and floor are drawn for the MCP read
/// tools; scripts use <see cref="Qx.Model.Messages.Incoming.RoomVisualizationSettings"/>.
/// </summary>
/// <param name="WallsHidden">Whether the room is rendered without walls.</param>
/// <param name="WallThickness">
/// The numeric value of <see cref="RoomThickness"/>: -2 thinnest, -1 thin, 0 normal, 1 thick.
/// </param>
/// <param name="FloorThickness">
/// The numeric value of <see cref="RoomThickness"/>, using the same -2 to 1 range as
/// <paramref name="WallThickness"/>.
/// </param>
/// <param name="WallThicknessMultiplier">
/// <paramref name="WallThickness"/> resolved to the drawing factor the client applies,
/// which is 2 raised to the thickness: 0.25, 0.5, 1 or 2.
/// </param>
/// <param name="FloorThicknessMultiplier">
/// <paramref name="FloorThickness"/> resolved the same way as <paramref name="WallThicknessMultiplier"/>.
/// </param>
public sealed record RoomVisualizationSettingsSnapshot(
    bool WallsHidden,
    int WallThickness,
    int FloorThickness,
    float WallThicknessMultiplier,
    float FloorThicknessMultiplier);

/// <summary>
/// Represents the JSON projection of the room's chat configuration for the MCP read tools; scripts
/// use <see cref="RoomChatSettings"/>.
/// </summary>
/// <remarks>
/// On the compact Flash guest-room layout the hotel only sends the flood setting; the other
/// four fields then carry their defaults rather than server values.
/// </remarks>
/// <param name="Flow">
/// The numeric value of <see cref="RoomChatFlowMode"/>: 0 free flow, 1 line by line.
/// </param>
/// <param name="BubbleWidth">
/// The numeric value of <see cref="RoomChatBubbleWidth"/>: 0 wide (2000 px), 1 normal
/// (350 px), 2 thin (240 px).
/// </param>
/// <param name="ScrollSpeed">
/// The numeric value of <see cref="RoomChatScrollSpeed"/>: 0 fast (3000 ms bubble lifetime),
/// 1 normal (6000 ms), 2 slow (12000 ms).
/// </param>
/// <param name="TalkHearingDistance">How many tiles away normal chat is still heard; the hotel default is 14.</param>
/// <param name="FloodProtection">
/// The numeric value of <see cref="RoomChatFloodSensitivity"/>: 0 strict, 1 normal, 2 loose.
/// </param>
public sealed record RoomChatSettingsSnapshot(
    int Flow,
    int BubbleWidth,
    int ScrollSpeed,
    int TalkHearingDistance,
    int FloodProtection);

/// <summary>
/// Represents the JSON projection of who is allowed to moderate in the room for the MCP read tools;
/// scripts use <see cref="RoomModerationSettings"/>.
/// </summary>
/// <remarks>
/// Each field is the numeric value of <see cref="RoomModerationPermission"/>: 0 owner only,
/// 1 rights holders, 2 everyone (offered for kick only), 4 group admins and 5 group admins
/// plus rights holders in a group room.
/// </remarks>
/// <param name="Mute">Who may mute other users; the hotel only offers 0 and 1 outside group rooms.</param>
/// <param name="Kick">Who may kick other users; the only permission that can be 2.</param>
/// <param name="Ban">Who may ban other users; the hotel only offers 0 and 1 outside group rooms.</param>
public sealed record RoomModerationSettingsSnapshot(int Mute, int Kick, int Ban);

/// <summary>
/// Represents the JSON projection of a room's navigator thumbnail for the MCP read tools; scripts
/// use <see cref="RoomThumbnailData"/>.
/// </summary>
/// <param name="RoomId">The room the thumbnail belongs to.</param>
/// <param name="Reference">The thumbnail reference as sent by the hotel.</param>
/// <param name="ImageUrl">The URL of the thumbnail image.</param>
public sealed record RoomThumbnailSnapshot(Id RoomId, string Reference, string ImageUrl);

/// <summary>
/// Represents the JSON projection of the detail block the hotel sends with a guest room result for
/// the MCP read tools; scripts use <see cref="RoomResultDetails"/>.
/// </summary>
/// <param name="Forward">Whether the result was sent as a room forward, echoing the request's room-forward flag.</param>
/// <param name="IsStaffPick">Whether the room is a staff pick.</param>
/// <param name="IsGroupMember">Whether the local user is a member of the room's group.</param>
/// <param name="IsRoomMuted">Whether the room is muted for everyone.</param>
/// <param name="Moderation">The room's mute, kick and ban permissions.</param>
/// <param name="CanMute">Whether the local user may mute others in the room.</param>
/// <param name="Chat">The room's chat configuration.</param>
/// <param name="OpeningConnection">
/// The trailing opening-connection flag, which only the compact and the extended full Flash
/// layouts carry; <see langword="null"/> on the layout without it.
/// </param>
public sealed record RoomResultDetailsSnapshot(
    bool Forward,
    bool IsStaffPick,
    bool IsGroupMember,
    bool IsRoomMuted,
    RoomModerationSettingsSnapshot Moderation,
    bool CanMute,
    RoomChatSettingsSnapshot Chat,
    bool? OpeningConnection);

/// <summary>
/// Represents the JSON projection of the decoration and layout of the current room for the MCP read
/// tools; scripts use <see cref="RoomEnvironmentState"/>.
/// </summary>
/// <remarks>
/// Every member is <see langword="null"/>, and the property map empty, until the corresponding
/// packet has arrived.
/// </remarks>
/// <param name="EntryTile">The door tile, or <see langword="null"/> before it is received.</param>
/// <param name="Properties">
/// Every room property keyed exactly as the hotel sends it, for example <c>floor</c>,
/// <c>wallpaper</c>, <c>landscape</c> and <c>landscapeanim</c>. A snapshot copy, not a live view.
/// </param>
/// <param name="Floor">The <c>floor</c> property, the floor pattern identifier.</param>
/// <param name="Wallpaper">The <c>wallpaper</c> property, the wall pattern identifier.</param>
/// <param name="Landscape">The <c>landscape</c> property, the window backdrop identifier.</param>
/// <param name="AnimatedLandscape">The <c>landscapeanim</c> property, the animated backdrop identifier.</param>
/// <param name="Visualization">Wall and floor thickness, or <see langword="null"/> before they are received.</param>
/// <param name="Chat">The room chat configuration, or <see langword="null"/> before it is received.</param>
public sealed record RoomEnvironmentSnapshot(
    RoomEntryTileSnapshot? EntryTile,
    IReadOnlyDictionary<string, string> Properties,
    string? Floor,
    string? Wallpaper,
    string? Landscape,
    string? AnimatedLandscape,
    RoomVisualizationSettingsSnapshot? Visualization,
    RoomChatSettingsSnapshot? Chat);

/// <summary>
/// Represents the JSON projection of what the local user is permitted to do in the current room for
/// the MCP read tools; scripts use <see cref="RoomAuthorityState"/>.
/// </summary>
/// <param name="IsOwner">Whether the local user owns the room.</param>
/// <param name="RightsLevel">
/// The controller level granted to the local user, or <see langword="null"/> while it is
/// still unknown. The client's scale is 0 not a controller, 1 room controller (rights),
/// 2 group member, 3 group admin, 4 room owner, 5 moderator.
/// </param>
/// <param name="RightsKnown">
/// Whether the rights question is settled. When <see langword="false"/> a
/// <paramref name="HasRights"/> of <see langword="false"/> only means "not confirmed yet".
/// </param>
/// <param name="HasRights">Whether the local user owns the room or holds a rights level above 0.</param>
/// <param name="IsSpectating">
/// Whether the local user entered as a spectator, or <see langword="null"/> while unknown.
/// Spectators cannot act in the room.
/// </param>
/// <param name="IsRoomMuted">
/// Whether the room is muted for everyone, taken from the guest room details;
/// <see langword="null"/> until those details arrive.
/// </param>
/// <param name="CanMute">
/// Whether the local user may mute others, taken from the guest room details;
/// <see langword="null"/> until those details arrive.
/// </param>
/// <param name="Moderation">
/// The room's mute, kick and ban permissions, or <see langword="null"/> until the guest
/// room details arrive.
/// </param>
public sealed record RoomAuthoritySnapshot(
    bool IsOwner,
    int? RightsLevel,
    bool RightsKnown,
    bool HasRights,
    bool? IsSpectating,
    bool? IsRoomMuted,
    bool? CanMute,
    RoomModerationSettingsSnapshot? Moderation);

/// <summary>
/// Represents the JSON projection of one line of a door queue for the MCP read tools; scripts use
/// <see cref="Qx.Model.Messages.Incoming.RoomQueueEntry"/>.
/// </summary>
/// <param name="Type">The queue's identifier as sent by the hotel, for example <c>visitors</c>.</param>
/// <param name="Size">
/// The local user's zero-based place in this queue, or a negative value when the hotel
/// reports no place.
/// </param>
public sealed record RoomQueueEntrySnapshot(string Type, int Size);

/// <summary>
/// Represents the JSON projection of a group of door queues sharing one entry target for the MCP
/// read tools; scripts use <see cref="Qx.Model.Messages.Incoming.RoomQueueSet"/>.
/// </summary>
/// <param name="Name">The set's name as sent by the hotel.</param>
/// <param name="Target">
/// What entering this set grants, as the numeric value of <c>RoomQueueTarget</c>:
/// 1 spectator, 2 visitor.
/// </param>
/// <param name="TargetName">The same target rendered as its enumeration name.</param>
/// <param name="IsActive">Whether this is the set the local user is currently waiting in.</param>
/// <param name="Position">
/// The local user's one-based place in the set's first queue, or <see langword="null"/>
/// when the hotel reports no place.
/// </param>
/// <param name="Queues">The individual queues that make up the set.</param>
public sealed record RoomQueueSetSnapshot(
    string Name,
    int Target,
    string TargetName,
    bool IsActive,
    int? Position,
    IReadOnlyList<RoomQueueEntrySnapshot> Queues);

/// <summary>
/// Represents the JSON projection of the door queue the local user waits in for the MCP read tools;
/// scripts use <see cref="Qx.Model.Messages.Incoming.RoomQueueStatus"/>.
/// </summary>
/// <param name="RoomId">The room being queued for.</param>
/// <param name="ActiveTarget">
/// The target of the set currently being waited in, as the numeric value of
/// <c>RoomQueueTarget</c> (1 spectator, 2 visitor), or <see langword="null"/> when no set
/// is active.
/// </param>
/// <param name="ActiveTargetName">The same active target rendered as its enumeration name.</param>
/// <param name="Position">The local user's one-based place in the active queue, or <see langword="null"/>.</param>
/// <param name="Sets">Every queue set the hotel reported for this room.</param>
public sealed record RoomQueueSnapshot(
    Id RoomId,
    int? ActiveTarget,
    string? ActiveTargetName,
    int? Position,
    IReadOnlyList<RoomQueueSetSnapshot> Sets);

/// <summary>
/// Represents the JSON projection of the reason a room could not be entered for the MCP read tools;
/// scripts use <see cref="RoomConnectionFailure"/>.
/// </summary>
/// <param name="Kind">
/// The classified reason: <c>Full</c>, <c>QueueError</c>, <c>Banned</c>, <c>Blocked</c>
/// or <c>Unknown</c>.
/// </param>
/// <param name="ReasonCode">
/// The raw code from the hotel: 1 room full, 3 queue error, 4 banned, 5 blocked. Any other
/// value maps to <c>Unknown</c> and is passed through unchanged.
/// </param>
/// <param name="Parameter">
/// The extra text the hotel attaches to a queue error (reason code 3); empty otherwise.
/// </param>
public sealed record RoomConnectionFailureSnapshot(
    string Kind,
    int ReasonCode,
    string Parameter);

/// <summary>
/// Represents the JSON projection of a kick of the local user out of a room for the MCP read tools;
/// scripts use <see cref="RoomKick"/>.
/// </summary>
/// <param name="RoomId">The room the local user was removed from.</param>
/// <param name="ErrorCode">
/// The generic error code that announced the kick; 4008 is the client's
/// <c>KICKED_BY_OWNER</c>.
/// </param>
/// <param name="WasEntered">Whether the room had been fully entered when the kick landed.</param>
public sealed record RoomKickSnapshot(
    Id RoomId,
    int ErrorCode,
    bool WasEntered);

/// <summary>
/// Represents the JSON projection of how the previous room session ended for the MCP read tools;
/// scripts use <see cref="RoomExitState"/>.
/// </summary>
/// <param name="RoomId">The room that was left.</param>
/// <param name="WasEntered">Whether the room had been fully entered before the exit.</param>
/// <param name="Source">
/// The transport that ended the session: <c>RoomTransition</c>, <c>ConnectionClosed</c>,
/// <c>ClientQuit</c>, <c>Disconnected</c>, <c>AccessFailure</c>,
/// <c>SelfRemoved</c> or <c>Kicked</c>.
/// </param>
/// <param name="Cause">
/// The classified reason, which is <c>Kicked</c> whenever a kick was consumed and otherwise
/// equal to <paramref name="Source"/>. Prefer this over <paramref name="Source"/>.
/// </param>
/// <param name="Reason">
/// The native room-exit reason code, when the transport carried one; otherwise
/// <see langword="null"/>.
/// </param>
/// <param name="WasKicked">Whether this exit was caused by a kick.</param>
/// <param name="Kick">The kick that caused the exit, or <see langword="null"/> when none did.</param>
public sealed record RoomExitSnapshot(
    Id RoomId,
    bool WasEntered,
    string Source,
    string Cause,
    short? Reason,
    bool WasKicked,
    RoomKickSnapshot? Kick);

/// <summary>
/// Represents the JSON projection of the entry side of the room session, from getting in and
/// waiting at the door to how the last attempt or session ended, for the MCP read tools; scripts
/// use the access members of <see cref="RoomManager"/>.
/// </summary>
/// <param name="State">
/// The access state: <c>Idle</c>, <c>Connecting</c>, <c>RingingDoorbell</c>, <c>Queued</c>,
/// <c>Accessible</c>, <c>Denied</c>, <c>NotFound</c> or <c>ConnectionError</c>.
/// </param>
/// <param name="RoomId">The room the access attempt targets, or <see langword="null"/> when idle.</param>
/// <param name="IsRingingDoorbell">Whether the local user is waiting for a doorbell answer.</param>
/// <param name="IsInQueue">Whether the local user is standing in a door queue.</param>
/// <param name="QueuePosition">The one-based place in the active queue, or <see langword="null"/>.</param>
/// <param name="Queue">The full door-queue status, or <see langword="null"/> when not queued.</param>
/// <param name="ConnectionFailure">Why the last entry attempt failed, or <see langword="null"/>.</param>
/// <param name="LastKick">
/// The most recent kick seen in this or a previous room session, cleared when a new room
/// session starts. Not necessarily the cause of <paramref name="LastExit"/>.
/// </param>
/// <param name="LastExit">How the previous room session ended, or <see langword="null"/> when none has ended yet.</param>
/// <param name="WasKicked">Whether the previous room session ended in a kick.</param>
public sealed record RoomAccessSnapshot(
    string State,
    Id? RoomId,
    bool IsRingingDoorbell,
    bool IsInQueue,
    int? QueuePosition,
    RoomQueueSnapshot? Queue,
    RoomConnectionFailureSnapshot? ConnectionFailure,
    RoomKickSnapshot? LastKick,
    RoomExitSnapshot? LastExit,
    bool WasKicked);

/// <summary>
/// Represents the JSON projection of the whole room session, where it stands, who the local user is
/// in it and how much of the room's content has arrived, for the MCP read tools; scripts use
/// <see cref="RoomManager"/>.
/// </summary>
/// <remarks>
/// This is a point-in-time copy, not a live view. Item and avatar collections are reported
/// only as counts here; use the dedicated avatars, furni, controllers and heightmap queries
/// to get their contents.
/// </remarks>
/// <param name="IsInRoom">Whether the session is currently inside a room.</param>
/// <param name="IsReady">
/// Whether the room session has reached its ready state, which is when acting in the room
/// becomes safe.
/// </param>
/// <param name="State">The session state: <c>Outside</c>, <c>Entering</c> or <c>Ready</c>.</param>
/// <param name="Generation">
/// A counter bumped on every room entry and exit. Two snapshots with the same generation
/// describe the same room visit; a change means everything cached about the room is void.
/// </param>
/// <param name="Id">The current room's identifier, or <see langword="null"/> when not in a room.</param>
/// <param name="Access">Door state, queues and how the previous session ended.</param>
/// <param name="RoomType">The room type string sent with the room-ready packet; empty outside a room.</param>
/// <param name="IsOwner">Whether the local user owns the room; also present on <paramref name="Authority"/>.</param>
/// <param name="HasRights">Whether the local user holds rights; also present on <paramref name="Authority"/>.</param>
/// <param name="Authority">The full permission picture for the local user in this room.</param>
/// <param name="Data">The navigator record, or <see langword="null"/> before it arrives.</param>
/// <param name="Details">The guest-room detail block, or <see langword="null"/> before it arrives.</param>
/// <param name="Environment">Decoration, door tile and chat configuration.</param>
/// <param name="Content">Which pieces of the room have arrived so far.</param>
/// <param name="AvatarCount">How many avatars are tracked in the room right now.</param>
/// <param name="FloorItemCount">How many floor items are tracked right now.</param>
/// <param name="WallItemCount">How many wall items are tracked right now.</param>
/// <param name="ControllerCount">How many users with rights are tracked right now.</param>
/// <param name="FloorPlan">The static geometry, or <see langword="null"/> before it arrives.</param>
/// <param name="Heightmap">Walkability counts over the live heightmap, or <see langword="null"/> before it arrives.</param>
public sealed record RoomSnapshot(
    bool IsInRoom,
    bool IsReady,
    string State,
    long Generation,
    Id? Id,
    RoomAccessSnapshot Access,
    string RoomType,
    bool IsOwner,
    bool HasRights,
    RoomAuthoritySnapshot Authority,
    RoomDataSnapshot? Data,
    RoomResultDetailsSnapshot? Details,
    RoomEnvironmentSnapshot Environment,
    RoomContentStateSnapshot Content,
    int AvatarCount,
    int FloorItemCount,
    int WallItemCount,
    int ControllerCount,
    FloorPlanSnapshot? FloorPlan,
    HeightmapSummarySnapshot? Heightmap);

/// <summary>
/// Represents the JSON projection of the decoded last status update of an avatar for the MCP read
/// tools; scripts use <see cref="AvatarStatus"/>.
/// </summary>
/// <param name="StatusId">
/// The extra integer of the status entry: <see cref="TargetId"/> when that is not 0, otherwise
/// <see cref="JumpingPower"/>.
/// </param>
/// <param name="Position">The tile the status places the avatar on, including its height.</param>
/// <param name="Direction">The body facing, 0-7 clockwise from north.</param>
/// <param name="HeadDirection">The head facing, 0-7 clockwise from north.</param>
/// <param name="Raw">
/// The fragment string recompiled in the client's own <c>/name args/name args/</c> form.
/// </param>
/// <param name="Stance">The posture derived from the fragments: <c>Sit</c>, <c>Lay</c> or <c>Stand</c>.</param>
/// <param name="IsController">Whether the status carries the <c>flatctrl</c> fragment.</param>
/// <param name="RightsLevel">The level from the <c>flatctrl</c> fragment, or 0 when the fragment is absent or not a number.</param>
/// <param name="IsTrading">Whether the status carries the <c>trd</c> fragment.</param>
/// <param name="SittingOnFloor">Whether the <c>sit</c> fragment's second argument is <c>1</c>, which marks sitting on the floor.</param>
/// <param name="Sign">The sign from the <c>sign</c> fragment, or 0 when the fragment is absent or not a number.</param>
/// <param name="MovingTo">
/// The tile from the <c>mv</c> fragment the avatar is stepping onto, or <see langword="null"/>
/// when it is not walking.
/// </param>
/// <param name="Fragments">
/// Every fragment keyed case-insensitively by its name, with its space-separated arguments, so
/// fragments that are not modeled here are still reachable.
/// </param>
public sealed record AvatarStatusSnapshot(
    int StatusId,
    PositionSnapshot Position,
    int Direction,
    int HeadDirection,
    string Raw,
    string Stance,
    bool IsController,
    int RightsLevel,
    bool IsTrading,
    bool SittingOnFloor,
    int Sign,
    PositionSnapshot? MovingTo,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Fragments)
{
    /// <summary>Gets the integer the status entry carries after the facings, read as the jumping power.</summary>
    public int JumpingPower { get; init; }

    /// <summary>Gets the target identifier of the status, or 0 when none is set.</summary>
    /// <remarks>
    /// Status updates read from the wire leave this at 0; when it is set it takes precedence
    /// over <see cref="JumpingPower"/> in <see cref="StatusId"/>.
    /// </remarks>
    public int TargetId { get; init; }

    /// <summary>Gets the height offset carried by the <c>sit</c> or <c>lay</c> fragment, in tile units.</summary>
    /// <remarks>
    /// <see langword="null"/> when standing or when the fragment carries no usable number.
    /// </remarks>
    public double? ActionHeight { get; init; }
}

/// <summary>
/// Represents the JSON projection of the fields that only a user avatar has for the MCP read tools;
/// scripts use <see cref="User"/>.
/// </summary>
/// <param name="Gender">
/// The user's gender: <c>Male</c>, <c>Female</c>, <c>Unisex</c>, or <c>None</c> when the hotel
/// sent an unrecognized value.
/// </param>
/// <param name="GroupId">The identifier of the user's favorite group, or -1 when the user displays none.</param>
/// <param name="GroupStatus">The favorite group membership status as sent with the room user.</param>
/// <param name="GroupName">The name of the user's favorite group, empty when the user displays none.</param>
/// <param name="FigureExtra">The secondary figure string the hotel sends with the room user, empty when none.</param>
/// <param name="AchievementScore">The user's achievement score.</param>
/// <param name="IsModerator">Whether the hotel flags the user as staff.</param>
/// <param name="BadgeCode">
/// The user's badge code; the room user packets do not carry it, so it is normally empty.
/// </param>
/// <param name="GroupBadge">
/// The favorite group's badge code; the room user packets do not carry it, so it is normally
/// empty.
/// </param>
/// <param name="GroupPayload">
/// The favorite group's badge parts as a flat list of integers; the room user packets do not
/// carry it, so it is normally empty.
/// </param>
/// <param name="BadgeRank">The badge rank the hotel sends with the user, or -1 when none was sent.</param>
/// <param name="RightsLevel">
/// The controller level from the user's last status update, or 0 when no status has been
/// received or it carries no <c>flatctrl</c> fragment.
/// </param>
/// <param name="HasRights">Whether <paramref name="RightsLevel"/> is above 0.</param>
public sealed record UserAvatarSnapshot(
    string Gender,
    Id GroupId,
    int GroupStatus,
    string GroupName,
    string FigureExtra,
    int AchievementScore,
    bool IsModerator,
    string BadgeCode,
    string GroupBadge,
    IReadOnlyList<int> GroupPayload,
    int BadgeRank,
    int RightsLevel,
    bool HasRights)
{
    /// <summary>Initializes a new instance for compatibility, taking the group identifier as a plain <see cref="long"/>.</summary>
    /// <param name="Gender">The user's gender.</param>
    /// <param name="GroupId">The identifier of the user's favorite group, or -1 when the user displays none.</param>
    /// <param name="GroupStatus">The favorite group membership status.</param>
    /// <param name="GroupName">The name of the user's favorite group.</param>
    /// <param name="FigureExtra">The secondary figure string.</param>
    /// <param name="AchievementScore">The user's achievement score.</param>
    /// <param name="IsModerator">Whether the hotel flags the user as staff.</param>
    /// <param name="BadgeCode">The user's badge code.</param>
    /// <param name="GroupBadge">The favorite group's badge code.</param>
    /// <param name="GroupPayload">The favorite group's badge parts as a flat list of integers.</param>
    /// <param name="BadgeRank">The badge rank, or -1 when none was sent.</param>
    /// <param name="RightsLevel">The controller level from the user's last status update.</param>
    /// <param name="HasRights">Whether the user holds a controller level above 0.</param>
    public UserAvatarSnapshot(
        string Gender,
        long GroupId,
        int GroupStatus,
        string GroupName,
        string FigureExtra,
        int AchievementScore,
        bool IsModerator,
        string BadgeCode,
        string GroupBadge,
        IReadOnlyList<int> GroupPayload,
        int BadgeRank,
        int RightsLevel,
        bool HasRights)
        : this(
            Gender,
            (Id)GroupId,
            GroupStatus,
            GroupName,
            FigureExtra,
            AchievementScore,
            IsModerator,
            BadgeCode,
            GroupBadge,
            GroupPayload,
            BadgeRank,
            RightsLevel,
            HasRights)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, yielding the group identifier as a plain <see cref="long"/>.</summary>
    /// <param name="Gender">The user's gender.</param>
    /// <param name="GroupId">The identifier of the user's favorite group as a plain <see cref="long"/>.</param>
    /// <param name="GroupStatus">The favorite group membership status.</param>
    /// <param name="GroupName">The name of the user's favorite group.</param>
    /// <param name="FigureExtra">The secondary figure string.</param>
    /// <param name="AchievementScore">The user's achievement score.</param>
    /// <param name="IsModerator">Whether the hotel flags the user as staff.</param>
    /// <param name="BadgeCode">The user's badge code.</param>
    /// <param name="GroupBadge">The favorite group's badge code.</param>
    /// <param name="GroupPayload">The favorite group's badge parts.</param>
    /// <param name="BadgeRank">The badge rank.</param>
    /// <param name="RightsLevel">The controller level from the user's last status update.</param>
    /// <param name="HasRights">Whether the user holds a controller level above 0.</param>
    public void Deconstruct(
        out string Gender,
        out long GroupId,
        out int GroupStatus,
        out string GroupName,
        out string FigureExtra,
        out int AchievementScore,
        out bool IsModerator,
        out string BadgeCode,
        out string GroupBadge,
        out IReadOnlyList<int> GroupPayload,
        out int BadgeRank,
        out int RightsLevel,
        out bool HasRights)
    {
        Gender = this.Gender;
        GroupId = this.GroupId;
        GroupStatus = this.GroupStatus;
        GroupName = this.GroupName;
        FigureExtra = this.FigureExtra;
        AchievementScore = this.AchievementScore;
        IsModerator = this.IsModerator;
        BadgeCode = this.BadgeCode;
        GroupBadge = this.GroupBadge;
        GroupPayload = this.GroupPayload;
        BadgeRank = this.BadgeRank;
        RightsLevel = this.RightsLevel;
        HasRights = this.HasRights;
    }
}

/// <summary>
/// Represents the JSON projection of the fields that only a pet avatar has for the MCP read tools;
/// scripts use <see cref="Pet"/>.
/// </summary>
/// <remarks>
/// The pet's breed variant is not on the room entity; it only comes from a pet info
/// request. <see cref="PetType"/> plus that breed together resolve the displayed breed.
/// </remarks>
/// <param name="Breed">
/// The pet type identifier, serialized as <see cref="PetType"/>. This is what kind of animal
/// it is (16 is the monsterplant), not the breed variant within that kind.
/// </param>
/// <param name="OwnerId">The owning user's identifier, or -1 when the hotel sent none.</param>
/// <param name="OwnerName">The owning user's name.</param>
/// <param name="RarityLevel">The pet's rarity tier as sent by the hotel.</param>
/// <param name="HasSaddle">Whether the pet is wearing a saddle.</param>
/// <param name="IsRiding">Whether a user is currently riding the pet.</param>
/// <param name="CanBreed">Whether the pet may be bred right now.</param>
/// <param name="CanHarvest">Whether the pet may be harvested right now.</param>
/// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
/// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
/// <param name="Level">The pet's level.</param>
/// <param name="Posture">The pet's current posture string as sent by the hotel, for example <c>ded</c>.</param>
public sealed record PetAvatarSnapshot(
    [property: JsonIgnore] int Breed,
    Id OwnerId,
    string OwnerName,
    int RarityLevel,
    bool HasSaddle,
    bool IsRiding,
    bool CanBreed,
    bool CanHarvest,
    bool CanRevive,
    bool HasBreedingPermission,
    int Level,
    string Posture)
{
    /// <summary>Gets the pet type identifier, the serialized name of <see cref="Breed"/>.</summary>
    /// <remarks>Identifies the kind of animal, not the breed variant.</remarks>
    public int PetType => Breed;

    /// <summary>Initializes a new instance for compatibility, taking the owner identifier as a plain <see cref="long"/>.</summary>
    /// <param name="Breed">The pet type identifier, which is what kind of animal it is.</param>
    /// <param name="OwnerId">The owning user's identifier, or -1 when the hotel sent none.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="RarityLevel">The pet's rarity tier.</param>
    /// <param name="HasSaddle">Whether the pet is wearing a saddle.</param>
    /// <param name="IsRiding">Whether a user is currently riding the pet.</param>
    /// <param name="CanBreed">Whether the pet may be bred right now.</param>
    /// <param name="CanHarvest">Whether the pet may be harvested right now.</param>
    /// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
    /// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
    /// <param name="Level">The pet's level.</param>
    /// <param name="Posture">The pet's current posture string.</param>
    public PetAvatarSnapshot(
        int Breed,
        long OwnerId,
        string OwnerName,
        int RarityLevel,
        bool HasSaddle,
        bool IsRiding,
        bool CanBreed,
        bool CanHarvest,
        bool CanRevive,
        bool HasBreedingPermission,
        int Level,
        string Posture)
        : this(
            Breed,
            (Id)OwnerId,
            OwnerName,
            RarityLevel,
            HasSaddle,
            IsRiding,
            CanBreed,
            CanHarvest,
            CanRevive,
            HasBreedingPermission,
            Level,
            Posture)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, yielding the owner identifier as a plain <see cref="long"/>.</summary>
    /// <param name="Breed">The pet type identifier.</param>
    /// <param name="OwnerId">The owning user's identifier as a plain <see cref="long"/>.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="RarityLevel">The pet's rarity tier.</param>
    /// <param name="HasSaddle">Whether the pet is wearing a saddle.</param>
    /// <param name="IsRiding">Whether a user is currently riding the pet.</param>
    /// <param name="CanBreed">Whether the pet may be bred right now.</param>
    /// <param name="CanHarvest">Whether the pet may be harvested right now.</param>
    /// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
    /// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
    /// <param name="Level">The pet's level.</param>
    /// <param name="Posture">The pet's current posture string.</param>
    public void Deconstruct(
        out int Breed,
        out long OwnerId,
        out string OwnerName,
        out int RarityLevel,
        out bool HasSaddle,
        out bool IsRiding,
        out bool CanBreed,
        out bool CanHarvest,
        out bool CanRevive,
        out bool HasBreedingPermission,
        out int Level,
        out string Posture)
    {
        Breed = this.Breed;
        OwnerId = this.OwnerId;
        OwnerName = this.OwnerName;
        RarityLevel = this.RarityLevel;
        HasSaddle = this.HasSaddle;
        IsRiding = this.IsRiding;
        CanBreed = this.CanBreed;
        CanHarvest = this.CanHarvest;
        CanRevive = this.CanRevive;
        HasBreedingPermission = this.HasBreedingPermission;
        Level = this.Level;
        Posture = this.Posture;
    }
}

/// <summary>
/// Represents the JSON projection of the fields that only a bot avatar has for the MCP read tools;
/// scripts use <see cref="Bot"/>.
/// </summary>
/// <remarks>
/// Public bots carry no owner or skills: the hotel only sends those for private (rentable)
/// bots, so on a public bot the owner is -1, the name empty and the skill list empty.
/// </remarks>
/// <param name="IsPublic">Whether this is a hotel-owned public bot.</param>
/// <param name="IsPrivate">Whether this is a user-owned rentable bot.</param>
/// <param name="Gender">
/// The bot's gender: <c>Male</c>, <c>Female</c>, <c>Unisex</c>, or <c>None</c> when the hotel
/// sent an unrecognized value.
/// </param>
/// <param name="OwnerId">The owning user's identifier, or -1 for a public bot.</param>
/// <param name="OwnerName">The owning user's name; empty for a public bot.</param>
/// <param name="Skills">The bot's enabled skill identifiers as sent by the hotel; empty for a public bot.</param>
public sealed record BotAvatarSnapshot(
    bool IsPublic,
    bool IsPrivate,
    string Gender,
    Id OwnerId,
    string OwnerName,
    IReadOnlyList<short> Skills)
{
    /// <summary>Initializes a new instance for compatibility, taking the owner identifier as a plain <see cref="long"/>.</summary>
    /// <param name="IsPublic">Whether this is a hotel-owned public bot.</param>
    /// <param name="IsPrivate">Whether this is a user-owned rentable bot.</param>
    /// <param name="Gender">The bot's gender.</param>
    /// <param name="OwnerId">The owning user's identifier, or -1 for a public bot.</param>
    /// <param name="OwnerName">The owning user's name; empty for a public bot.</param>
    /// <param name="Skills">The bot's enabled skill identifiers; empty for a public bot.</param>
    public BotAvatarSnapshot(
        bool IsPublic,
        bool IsPrivate,
        string Gender,
        long OwnerId,
        string OwnerName,
        IReadOnlyList<short> Skills)
        : this(IsPublic, IsPrivate, Gender, (Id)OwnerId, OwnerName, Skills)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, yielding the owner identifier as a plain <see cref="long"/>.</summary>
    /// <param name="IsPublic">Whether this is a hotel-owned public bot.</param>
    /// <param name="IsPrivate">Whether this is a user-owned rentable bot.</param>
    /// <param name="Gender">The bot's gender.</param>
    /// <param name="OwnerId">The owning user's identifier as a plain <see cref="long"/>.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="Skills">The bot's enabled skill identifiers.</param>
    public void Deconstruct(
        out bool IsPublic,
        out bool IsPrivate,
        out string Gender,
        out long OwnerId,
        out string OwnerName,
        out IReadOnlyList<short> Skills)
    {
        IsPublic = this.IsPublic;
        IsPrivate = this.IsPrivate;
        Gender = this.Gender;
        OwnerId = this.OwnerId;
        OwnerName = this.OwnerName;
        Skills = this.Skills;
    }
}

/// <summary>
/// Represents the JSON projection of a user, pet or bot standing in the room for the MCP read
/// tools; scripts use <see cref="Avatar"/>.
/// </summary>
/// <remarks>
/// Exactly one of <paramref name="User"/>, <paramref name="Pet"/> and <paramref name="Bot"/> is
/// populated, matching <paramref name="Type"/>.
/// </remarks>
/// <param name="Type">The entity kind: <c>User</c>, <c>Pet</c>, <c>PublicBot</c> or <c>PrivateBot</c>.</param>
/// <param name="IsRemoved">
/// Whether this snapshot describes an avatar that has already left. Set on the copy handed
/// to removal events; always <see langword="false"/> for avatars read out of the live room.
/// </param>
/// <param name="Id">
/// The entity's own identifier: a user identifier for users, a pet identifier for pets, a
/// bot identifier for bots. Not usable to address the avatar inside the room.
/// </param>
/// <param name="Index">
/// The room-local index. This is the value room packets use to address the avatar, and it
/// is only valid within the current room session.
/// </param>
/// <param name="Name">The displayed name.</param>
/// <param name="Motto">The displayed motto.</param>
/// <param name="Figure">The figure string; for pets this is the pet's appearance string.</param>
/// <param name="Position">The tile the avatar stands on, including its height.</param>
/// <param name="Area">The avatar's footprint, always one tile by one tile.</param>
/// <param name="Direction">The body facing, 0-7 clockwise from north.</param>
/// <param name="HeadDirection">The head facing, 0-7 clockwise from north.</param>
/// <param name="Dance">The dance identifier; 0 when not dancing.</param>
/// <param name="Effect">The avatar effect identifier currently applied; 0 when none.</param>
/// <param name="HandItem">The carry-item identifier currently held; 0 when empty-handed.</param>
/// <param name="IsIdle">Whether the hotel has flagged the avatar as idle.</param>
/// <param name="IsTyping">Whether the avatar is showing the typing indicator.</param>
/// <param name="CurrentStatus">
/// The decoded last status update, or <see langword="null"/> when no status has been
/// received for this avatar yet. This is where posture, walking target and rights live.
/// </param>
/// <param name="User">The user-only fields, or <see langword="null"/> when this is not a user.</param>
/// <param name="Pet">The pet-only fields, or <see langword="null"/> when this is not a pet.</param>
/// <param name="Bot">The bot-only fields, or <see langword="null"/> when this is not a bot.</param>
public sealed record AvatarSnapshot(
    string Type,
    bool IsRemoved,
    Id Id,
    int Index,
    string Name,
    string Motto,
    string Figure,
    PositionSnapshot Position,
    AreaSnapshot Area,
    int Direction,
    int HeadDirection,
    int Dance,
    int Effect,
    int HandItem,
    bool IsIdle,
    bool IsTyping,
    AvatarStatusSnapshot? CurrentStatus,
    UserAvatarSnapshot? User,
    PetAvatarSnapshot? Pet,
    BotAvatarSnapshot? Bot)
{
    /// <summary>
    /// Initializes a new instance for compatibility, taking the identifier as a plain <see cref="long"/> and
    /// fixing <see cref="IsRemoved"/> to <see langword="false"/>.
    /// </summary>
    /// <param name="Type">The entity kind: <c>User</c>, <c>Pet</c>, <c>PublicBot</c> or <c>PrivateBot</c>.</param>
    /// <param name="Id">The entity's own identifier as a plain <see cref="long"/>.</param>
    /// <param name="Index">The room-local index that room packets use to address the avatar.</param>
    /// <param name="Name">The displayed name.</param>
    /// <param name="Motto">The displayed motto.</param>
    /// <param name="Figure">The figure string.</param>
    /// <param name="Position">The tile the avatar stands on, including its height.</param>
    /// <param name="Area">The avatar's footprint.</param>
    /// <param name="Direction">The body facing, 0-7 clockwise from north.</param>
    /// <param name="HeadDirection">The head facing, 0-7 clockwise from north.</param>
    /// <param name="Dance">The dance identifier; 0 when not dancing.</param>
    /// <param name="Effect">The avatar effect identifier; 0 when none.</param>
    /// <param name="HandItem">The carry-item identifier; 0 when empty-handed.</param>
    /// <param name="IsIdle">Whether the hotel has flagged the avatar as idle.</param>
    /// <param name="IsTyping">Whether the avatar is showing the typing indicator.</param>
    /// <param name="CurrentStatus">The decoded last status update, or <see langword="null"/> when none has been received.</param>
    /// <param name="User">The user-only fields, or <see langword="null"/> when this is not a user.</param>
    /// <param name="Pet">The pet-only fields, or <see langword="null"/> when this is not a pet.</param>
    /// <param name="Bot">The bot-only fields, or <see langword="null"/> when this is not a bot.</param>
    public AvatarSnapshot(
        string Type,
        long Id,
        int Index,
        string Name,
        string Motto,
        string Figure,
        PositionSnapshot Position,
        AreaSnapshot Area,
        int Direction,
        int HeadDirection,
        int Dance,
        int Effect,
        int HandItem,
        bool IsIdle,
        bool IsTyping,
        AvatarStatusSnapshot? CurrentStatus,
        UserAvatarSnapshot? User,
        PetAvatarSnapshot? Pet,
        BotAvatarSnapshot? Bot)
        : this(
            Type,
            false,
            (Qx.Id)Id,
            Index,
            Name,
            Motto,
            Figure,
            Position,
            Area,
            Direction,
            HeadDirection,
            Dance,
            Effect,
            HandItem,
            IsIdle,
            IsTyping,
            CurrentStatus,
            User,
            Pet,
            Bot)
    {
    }

    /// <summary>
    /// Deconstructs the instance for compatibility, yielding the identifier as a plain <see cref="long"/>
    /// and omitting <see cref="IsRemoved"/>.
    /// </summary>
    /// <param name="Type">The entity kind.</param>
    /// <param name="Id">The entity's own identifier as a plain <see cref="long"/>.</param>
    /// <param name="Index">The room-local index.</param>
    /// <param name="Name">The displayed name.</param>
    /// <param name="Motto">The displayed motto.</param>
    /// <param name="Figure">The figure string.</param>
    /// <param name="Position">The tile the avatar stands on.</param>
    /// <param name="Area">The avatar's footprint.</param>
    /// <param name="Direction">The body facing.</param>
    /// <param name="HeadDirection">The head facing.</param>
    /// <param name="Dance">The dance identifier.</param>
    /// <param name="Effect">The avatar effect identifier.</param>
    /// <param name="HandItem">The carry-item identifier.</param>
    /// <param name="IsIdle">Whether the hotel has flagged the avatar as idle.</param>
    /// <param name="IsTyping">Whether the avatar is showing the typing indicator.</param>
    /// <param name="CurrentStatus">The decoded last status update, or <see langword="null"/>.</param>
    /// <param name="User">The user-only fields, or <see langword="null"/>.</param>
    /// <param name="Pet">The pet-only fields, or <see langword="null"/>.</param>
    /// <param name="Bot">The bot-only fields, or <see langword="null"/>.</param>
    public void Deconstruct(
        out string Type,
        out long Id,
        out int Index,
        out string Name,
        out string Motto,
        out string Figure,
        out PositionSnapshot Position,
        out AreaSnapshot Area,
        out int Direction,
        out int HeadDirection,
        out int Dance,
        out int Effect,
        out int HandItem,
        out bool IsIdle,
        out bool IsTyping,
        out AvatarStatusSnapshot? CurrentStatus,
        out UserAvatarSnapshot? User,
        out PetAvatarSnapshot? Pet,
        out BotAvatarSnapshot? Bot)
    {
        Type = this.Type;
        Id = this.Id;
        Index = this.Index;
        Name = this.Name;
        Motto = this.Motto;
        Figure = this.Figure;
        Position = this.Position;
        Area = this.Area;
        Direction = this.Direction;
        HeadDirection = this.HeadDirection;
        Dance = this.Dance;
        Effect = this.Effect;
        HandItem = this.HandItem;
        IsIdle = this.IsIdle;
        IsTyping = this.IsTyping;
        CurrentStatus = this.CurrentStatus;
        User = this.User;
        Pet = this.Pet;
        Bot = this.Bot;
    }
}

/// <summary>
/// Represents the JSON projection of every avatar in the room, ordered by room index, for the MCP
/// read tools; scripts use <see cref="RoomManager.Avatars"/>.
/// </summary>
/// <param name="RoomId">The room the avatars belong to, or <see langword="null"/> when not in a room.</param>
/// <param name="Generation">
/// The room session counter the projection was taken under. Compare it against a later
/// snapshot to detect that the room changed underneath.
/// </param>
/// <param name="Total">The number of entries in <paramref name="Avatars"/>. This projection is not capped.</param>
/// <param name="Avatars">The avatars, ordered ascending by their room index.</param>
public sealed record AvatarCollectionSnapshot(
    Id? RoomId,
    long Generation,
    int Total,
    IReadOnlyList<AvatarSnapshot> Avatars)
{
    /// <summary>Initializes a new instance for compatibility with callers that have no room context.</summary>
    /// <param name="Total">The number of entries in <paramref name="Avatars"/>.</param>
    /// <param name="Avatars">The avatars, ordered ascending by their room index.</param>
    public AvatarCollectionSnapshot(int Total, IReadOnlyList<AvatarSnapshot> Avatars)
        : this(null, 0, Total, Avatars)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, omitting the room context.</summary>
    /// <param name="Total">The number of avatars.</param>
    /// <param name="Avatars">The avatars, ordered ascending by their room index.</param>
    public void Deconstruct(out int Total, out IReadOnlyList<AvatarSnapshot> Avatars)
    {
        Total = this.Total;
        Avatars = this.Avatars;
    }
}

/// <summary>
/// Represents the JSON projection of the local user's account data for the MCP read tools; scripts
/// use <see cref="UserData"/>.
/// </summary>
/// <param name="Id">The user identifier.</param>
/// <param name="Name">The user name.</param>
/// <param name="Figure">The figure string.</param>
/// <param name="Gender">
/// The gender: <c>Male</c>, <c>Female</c>, <c>Unisex</c>, or <c>None</c> when the hotel sent an
/// unrecognized value.
/// </param>
/// <param name="Motto">The motto.</param>
/// <param name="RealName">The real name, empty unless the hotel discloses it.</param>
/// <param name="DirectMail">Whether the user opted into direct mail.</param>
/// <param name="RespectTotal">How much respect the user has received in total.</param>
/// <param name="RespectLeft">How many respects the user may still give out today.</param>
/// <param name="PetRespectLeft">How many pet scratches the user may still give out today.</param>
/// <param name="StreamPublishingAllowed">Whether the user may publish streams.</param>
/// <param name="LastAccessDate">
/// The last login timestamp exactly as the hotel formats it; the format is hotel-specific
/// and is not parsed.
/// </param>
/// <param name="IsNameChangeable">Whether the user may still change their name.</param>
/// <param name="IsSafetyLocked">Whether the account is under a safety lock.</param>
/// <param name="IsTradeLocked">Whether the account is barred from trading. Sent only by newer hotels; otherwise, <see langword="false"/>.</param>
/// <param name="NameColor">The name color the hotel assigns. Sent only by newer hotels; otherwise empty.</param>
/// <param name="RespectReplenishesLeft">How many daily respect replenishments remain. Sent only by newer hotels; otherwise 0.</param>
/// <param name="MaxRespectPerDay">The daily respect allowance. Sent only by newer hotels; otherwise 0.</param>
public sealed record ProfileSnapshot(
    Id Id,
    string Name,
    string Figure,
    string Gender,
    string Motto,
    string RealName,
    bool DirectMail,
    int RespectTotal,
    int RespectLeft,
    int PetRespectLeft,
    bool StreamPublishingAllowed,
    string LastAccessDate,
    bool IsNameChangeable,
    bool IsSafetyLocked,
    bool IsTradeLocked,
    string NameColor,
    int RespectReplenishesLeft,
    int MaxRespectPerDay)
{
    /// <summary>Initializes a new instance for compatibility, taking the identifier as a plain <see cref="long"/>.</summary>
    /// <param name="Id">The user identifier.</param>
    /// <param name="Name">The user name.</param>
    /// <param name="Figure">The figure string.</param>
    /// <param name="Gender">The gender.</param>
    /// <param name="Motto">The motto.</param>
    /// <param name="RealName">The real name, empty unless the hotel discloses it.</param>
    /// <param name="DirectMail">Whether the user opted into direct mail.</param>
    /// <param name="RespectTotal">The total respect the user has received.</param>
    /// <param name="RespectLeft">The respects the user may still give out today.</param>
    /// <param name="PetRespectLeft">The pet scratches the user may still give out today.</param>
    /// <param name="StreamPublishingAllowed">Whether the user may publish streams.</param>
    /// <param name="LastAccessDate">The last login timestamp exactly as the hotel formats it.</param>
    /// <param name="IsNameChangeable">Whether the user may still change their name.</param>
    /// <param name="IsSafetyLocked">Whether the account is under a safety lock.</param>
    /// <param name="IsTradeLocked">Whether the account is barred from trading.</param>
    /// <param name="NameColor">The name color the hotel assigns.</param>
    /// <param name="RespectReplenishesLeft">The daily respect replenishments that remain.</param>
    /// <param name="MaxRespectPerDay">The daily respect allowance.</param>
    public ProfileSnapshot(
        long Id,
        string Name,
        string Figure,
        string Gender,
        string Motto,
        string RealName,
        bool DirectMail,
        int RespectTotal,
        int RespectLeft,
        int PetRespectLeft,
        bool StreamPublishingAllowed,
        string LastAccessDate,
        bool IsNameChangeable,
        bool IsSafetyLocked,
        bool IsTradeLocked,
        string NameColor,
        int RespectReplenishesLeft,
        int MaxRespectPerDay)
        : this(
            (Qx.Id)Id,
            Name,
            Figure,
            Gender,
            Motto,
            RealName,
            DirectMail,
            RespectTotal,
            RespectLeft,
            PetRespectLeft,
            StreamPublishingAllowed,
            LastAccessDate,
            IsNameChangeable,
            IsSafetyLocked,
            IsTradeLocked,
            NameColor,
            RespectReplenishesLeft,
            MaxRespectPerDay)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, yielding the identifier as a plain <see cref="long"/>.</summary>
    /// <param name="Id">The user identifier as a plain <see cref="long"/>.</param>
    /// <param name="Name">The user name.</param>
    /// <param name="Figure">The figure string.</param>
    /// <param name="Gender">The gender.</param>
    /// <param name="Motto">The motto.</param>
    /// <param name="RealName">The real name.</param>
    /// <param name="DirectMail">Whether the user opted into direct mail.</param>
    /// <param name="RespectTotal">The total respect the user has received.</param>
    /// <param name="RespectLeft">The respects the user may still give out today.</param>
    /// <param name="PetRespectLeft">The pet scratches the user may still give out today.</param>
    /// <param name="StreamPublishingAllowed">Whether the user may publish streams.</param>
    /// <param name="LastAccessDate">The last login timestamp exactly as the hotel formats it.</param>
    /// <param name="IsNameChangeable">Whether the user may still change their name.</param>
    /// <param name="IsSafetyLocked">Whether the account is under a safety lock.</param>
    /// <param name="IsTradeLocked">Whether the account is barred from trading.</param>
    /// <param name="NameColor">The name color the hotel assigns.</param>
    /// <param name="RespectReplenishesLeft">The daily respect replenishments that remain.</param>
    /// <param name="MaxRespectPerDay">The daily respect allowance.</param>
    public void Deconstruct(
        out long Id,
        out string Name,
        out string Figure,
        out string Gender,
        out string Motto,
        out string RealName,
        out bool DirectMail,
        out int RespectTotal,
        out int RespectLeft,
        out int PetRespectLeft,
        out bool StreamPublishingAllowed,
        out string LastAccessDate,
        out bool IsNameChangeable,
        out bool IsSafetyLocked,
        out bool IsTradeLocked,
        out string NameColor,
        out int RespectReplenishesLeft,
        out int MaxRespectPerDay)
    {
        Id = this.Id;
        Name = this.Name;
        Figure = this.Figure;
        Gender = this.Gender;
        Motto = this.Motto;
        RealName = this.RealName;
        DirectMail = this.DirectMail;
        RespectTotal = this.RespectTotal;
        RespectLeft = this.RespectLeft;
        PetRespectLeft = this.PetRespectLeft;
        StreamPublishingAllowed = this.StreamPublishingAllowed;
        LastAccessDate = this.LastAccessDate;
        IsNameChangeable = this.IsNameChangeable;
        IsSafetyLocked = this.IsSafetyLocked;
        IsTradeLocked = this.IsTradeLocked;
        NameColor = this.NameColor;
        RespectReplenishesLeft = this.RespectReplenishesLeft;
        MaxRespectPerDay = this.MaxRespectPerDay;
    }
}

/// <summary>
/// Represents the JSON projection of one entry of the friend list used by the MCP read tools and the
/// application-layer results; scripts that read live state use <see cref="Friend"/>.
/// </summary>
/// <param name="Id">The friend's user identifier.</param>
/// <param name="Name">The friend's user name.</param>
/// <param name="Figure">The friend's figure string.</param>
/// <param name="Gender">
/// The friend's gender: <c>Female</c> (0), <c>Male</c> (1), <c>Unisex</c> (2) or <c>None</c> (-1);
/// any other wire value appears as its number.
/// </param>
/// <param name="Motto">The friend's motto.</param>
/// <param name="RealName">The friend's real name, empty unless the hotel discloses it.</param>
/// <param name="IsOnline">Whether the friend is online right now.</param>
/// <param name="CanFollow">Whether the local user may follow the friend into their room.</param>
/// <param name="CategoryId">The friend-list category the friend is filed under, matching <see cref="FriendCategorySnapshot.Id"/>.</param>
/// <param name="FacebookId">The Facebook identifier the hotel sends, empty when none.</param>
/// <param name="IsAcceptingOfflineMessages">Whether the friend accepts messages while offline.</param>
/// <param name="IsVipMember">Whether the friend is a club member.</param>
/// <param name="IsPocketHabboUser">Whether the friend uses the Pocket Habbo client.</param>
/// <param name="Relation">The relationship status the local user set: <c>None</c>, <c>Heart</c>, <c>Smile</c> or <c>Skull</c>.</param>
/// <param name="LastOnline">
/// The last-online value, serialized exactly rather than as a JSON number. The Flash friend
/// packet does not carry it, so it is normally 0.
/// </param>
public sealed record FriendSnapshot(
    Id Id,
    string Name,
    string Figure,
    string Gender,
    string Motto,
    string RealName,
    bool IsOnline,
    bool CanFollow,
    int CategoryId,
    string FacebookId,
    bool IsAcceptingOfflineMessages,
    bool IsVipMember,
    bool IsPocketHabboUser,
    string Relation,
    [property: JsonConverter(typeof(ExactInt64JsonConverter))]
    long LastOnline)
{
    /// <summary>Initializes a new instance for compatibility, taking the original friend fields and the identifier as a plain <see cref="long"/>.</summary>
    /// <remarks>
    /// The fields it does not take are set to their defaults: the category is 0, the Facebook
    /// identifier is empty, the three flags are <see langword="false"/> and the last-online
    /// value is 0.
    /// </remarks>
    /// <param name="Id">The friend's user identifier.</param>
    /// <param name="Name">The friend's user name.</param>
    /// <param name="Figure">The friend's figure string.</param>
    /// <param name="Gender">The friend's gender.</param>
    /// <param name="Motto">The friend's motto.</param>
    /// <param name="RealName">The friend's real name.</param>
    /// <param name="IsOnline">Whether the friend is online right now.</param>
    /// <param name="CanFollow">Whether the local user may follow the friend into their room.</param>
    /// <param name="Relation">The relationship status the local user set.</param>
    public FriendSnapshot(
    long Id,
    string Name,
    string Figure,
    string Gender,
    string Motto,
    string RealName,
    bool IsOnline,
    bool CanFollow,
    string Relation)
    : this(
        (Qx.Id)Id,
        Name,
        Figure,
        Gender,
        Motto,
        RealName,
        IsOnline,
        CanFollow,
        0,
        string.Empty,
        false,
        false,
        false,
        Relation,
        0)
    {
    }

    /// <summary>Deconstructs the instance for compatibility into the original friend snapshot fields.</summary>
    /// <param name="Id">The friend's user identifier as a plain <see cref="long"/>.</param>
    /// <param name="Name">The friend's user name.</param>
    /// <param name="Figure">The friend's figure string.</param>
    /// <param name="Gender">The friend's gender.</param>
    /// <param name="Motto">The friend's motto.</param>
    /// <param name="RealName">The friend's real name.</param>
    /// <param name="IsOnline">Whether the friend is online right now.</param>
    /// <param name="CanFollow">Whether the local user may follow the friend into their room.</param>
    /// <param name="Relation">The relationship status the local user set.</param>
    public void Deconstruct(
        out long Id,
        out string Name,
        out string Figure,
        out string Gender,
        out string Motto,
        out string RealName,
        out bool IsOnline,
        out bool CanFollow,
        out string Relation)
    {
        Id = this.Id;
        Name = this.Name;
        Figure = this.Figure;
        Gender = this.Gender;
        Motto = this.Motto;
        RealName = this.RealName;
        IsOnline = this.IsOnline;
        CanFollow = this.CanFollow;
        Relation = this.Relation;
    }
}

/// <summary>
/// Represents the JSON projection of a user-defined friend list category used by the MCP read tools
/// and the application-layer results; scripts that read live state use <see cref="FriendCategory"/>.
/// </summary>
/// <param name="Id">The category identifier, matched by <see cref="FriendSnapshot.CategoryId"/>.</param>
/// <param name="Name">The category name the user chose.</param>
public sealed record FriendCategorySnapshot(Id Id, string Name);

/// <summary>
/// Represents the JSON projection of the friend list and its capacity limits for the MCP read
/// tools; scripts use <see cref="FriendManager"/>.
/// </summary>
/// <param name="Total">The number of entries in <paramref name="Friends"/>. This projection is not capped.</param>
/// <param name="Online">How many of those friends are online right now.</param>
/// <param name="UserLimit">The friend slots this account actually has; 0 when the hotel has not reported it.</param>
/// <param name="NormalLimit">The friend slots a non-club account gets; 0 when not reported.</param>
/// <param name="ExtendedLimit">The friend slots a club account gets; 0 when not reported.</param>
/// <param name="Categories">The user's friend-list categories, ordered by name.</param>
/// <param name="Friends">The friends, online first, then by name case-insensitively.</param>
public sealed record FriendCollectionSnapshot(
    int Total,
    int Online,
    int UserLimit,
    int NormalLimit,
    int ExtendedLimit,
    IReadOnlyList<FriendCategorySnapshot> Categories,
    IReadOnlyList<FriendSnapshot> Friends)
{
    /// <summary>Initializes a new instance for compatibility, without categories or capacity limits.</summary>
    /// <param name="Total">The number of entries in <paramref name="Friends"/>.</param>
    /// <param name="Online">The number of those friends that are online.</param>
    /// <param name="Friends">The friends.</param>
    public FriendCollectionSnapshot(
        int Total,
        int Online,
        IReadOnlyList<FriendSnapshot> Friends)
        : this(Total, Online, 0, 0, 0, [], Friends)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, omitting categories and capacity limits.</summary>
    /// <param name="Total">The number of friends.</param>
    /// <param name="Online">The number of friends that are online.</param>
    /// <param name="Friends">The friends, online first, then by name case-insensitively.</param>
    public void Deconstruct(
        out int Total,
        out int Online,
        out IReadOnlyList<FriendSnapshot> Friends)
    {
        Total = this.Total;
        Online = this.Online;
        Friends = this.Friends;
    }
}

/// <summary>
/// Represents the JSON projection of the local user's balances for the MCP read tools; scripts use
/// the <c>Credits</c>, <c>Diamonds</c>, <c>Duckets</c> and <c>Points</c> globals.
/// </summary>
/// <remarks>
/// Each amount is <see langword="null"/> until the hotel has sent the matching packet, which is
/// what the two loaded flags distinguish from a real zero.
/// </remarks>
/// <param name="CreditsLoaded">Whether a credit balance has been received.</param>
/// <param name="Credits">The credit balance, or <see langword="null"/> while <paramref name="CreditsLoaded"/> is <see langword="false"/>.</param>
/// <param name="PointsLoaded">Whether the activity-point packet has been received.</param>
/// <param name="Diamonds">The diamond balance (activity point type 5), or <see langword="null"/> while points are unloaded.</param>
/// <param name="Duckets">The ducket balance (activity point type 0), or <see langword="null"/> while points are unloaded.</param>
/// <param name="ActivityPoints">
/// Every activity-point balance keyed by its hotel currency type, including the two broken
/// out above. Type 0 is duckets and type 5 is diamonds; the remaining types are seasonal
/// currencies defined per hotel.
/// </param>
public sealed record CurrencySnapshot(
    bool CreditsLoaded,
    int? Credits,
    bool PointsLoaded,
    int? Diamonds,
    int? Duckets,
    IReadOnlyDictionary<int, int> ActivityPoints)
{
    /// <summary>Initializes a new instance for compatibility, reporting no activity-point map.</summary>
    /// <param name="CreditsLoaded">Whether a credit balance has been received.</param>
    /// <param name="Credits">The credit balance, or <see langword="null"/> while it is unloaded.</param>
    /// <param name="PointsLoaded">Whether the activity-point packet has been received.</param>
    /// <param name="Diamonds">The diamond balance, or <see langword="null"/> while points are unloaded.</param>
    /// <param name="Duckets">The ducket balance, or <see langword="null"/> while points are unloaded.</param>
    public CurrencySnapshot(
        bool CreditsLoaded,
        int? Credits,
        bool PointsLoaded,
        int? Diamonds,
        int? Duckets)
        : this(
            CreditsLoaded,
            Credits,
            PointsLoaded,
            Diamonds,
            Duckets,
            new Dictionary<int, int>())
    {
    }

    /// <summary>Deconstructs the instance for compatibility, omitting the activity-point map.</summary>
    /// <param name="CreditsLoaded">Whether a credit balance has been received.</param>
    /// <param name="Credits">The credit balance, or <see langword="null"/> while it is unloaded.</param>
    /// <param name="PointsLoaded">Whether the activity-point packet has been received.</param>
    /// <param name="Diamonds">The diamond balance, or <see langword="null"/> while points are unloaded.</param>
    /// <param name="Duckets">The ducket balance, or <see langword="null"/> while points are unloaded.</param>
    public void Deconstruct(
        out bool CreditsLoaded,
        out int? Credits,
        out bool PointsLoaded,
        out int? Diamonds,
        out int? Duckets)
    {
        CreditsLoaded = this.CreditsLoaded;
        Credits = this.Credits;
        PointsLoaded = this.PointsLoaded;
        Diamonds = this.Diamonds;
        Duckets = this.Duckets;
    }
}

/// <summary>
/// Represents the JSON projection of a user who holds rights in the room for the MCP read tools;
/// scripts use <see cref="Qx.Model.Messages.Incoming.IdName"/>.
/// </summary>
/// <param name="Id">The user identifier.</param>
/// <param name="Name">The user name.</param>
public sealed record ControllerSnapshot(Id Id, string Name);

/// <summary>
/// Represents the JSON projection of the room's rights list for the MCP read tools; scripts use
/// <see cref="RoomManager.Controllers"/>.
/// </summary>
/// <remarks>
/// The hotel only sends this list to the room owner, so on a room the local user does not
/// own it stays empty and the room content state reports controllers as not loaded.
/// </remarks>
/// <param name="RoomId">The room the list belongs to, or <see langword="null"/> when not in a room.</param>
/// <param name="Generation">The room session counter the projection was taken under.</param>
/// <param name="IsOwner">Whether the local user owns this room, which is the precondition for the list arriving.</param>
/// <param name="Total">The number of entries in <paramref name="Controllers"/>. This projection is not capped.</param>
/// <param name="Controllers">The rights holders, ordered by name case-insensitively, then by identifier.</param>
public sealed record ControllerCollectionSnapshot(
    Id? RoomId,
    long Generation,
    bool IsOwner,
    int Total,
    IReadOnlyList<ControllerSnapshot> Controllers);

/// <summary>
/// Represents the JSON projection of the furni data definition behind a furni kind used by the MCP
/// read tools and the application-layer results; scripts that read live state use
/// <see cref="FurniInfo"/>.
/// </summary>
/// <remarks>Present on an item snapshot only once definitions are loaded.</remarks>
/// <param name="Type">Whether the definition describes a <c>Floor</c> or a <c>Wall</c> item.</param>
/// <param name="Kind">The furni kind identifier, which is what room and inventory packets carry.</param>
/// <param name="Identifier">
/// The full class name from the furni data, including a <c>*</c> color suffix such as
/// <c>rare_dragonlamp*4</c> when the kind has one; empty when the entry has none.
/// </param>
/// <param name="Name">The localized display name, empty when the entry has none.</param>
/// <param name="Width">The footprint along X in tiles at direction 0, at least 1.</param>
/// <param name="Length">The footprint along Y in tiles at direction 0, at least 1.</param>
/// <param name="Category">The category string from the furni data, empty when the entry has none.</param>
/// <param name="Line">The furni line (collection) this kind belongs to, empty when the entry has none.</param>
public sealed record FurniDefinitionSnapshot(
    string Type,
    int Kind,
    string Identifier,
    string Name,
    int Width,
    int Length,
    string Category,
    string Line)
{
    /// <summary>Gets the class name without the <c>*</c> color suffix of <see cref="Identifier"/>.</summary>
    public string ClassName { get; init; } = Identifier;

    /// <summary>Gets the asset revision the client downloads for this kind.</summary>
    public int Revision { get; init; }

    /// <summary>Gets the direction the kind is placed at by default, 0-7 clockwise from north.</summary>
    public int DefaultDirection { get; init; }

    /// <summary>Gets the part colors from the furni data, empty when the kind lists none.</summary>
    public IReadOnlyList<string> PartColors { get; init; } = [];

    /// <summary>Gets the localized catalog description, empty when the entry has none.</summary>
    public string Description { get; init; } = "";

    /// <summary>Gets the advertisement URL attached to the kind, empty when none.</summary>
    public string AdUrl { get; init; } = "";

    /// <summary>Gets the catalog offer identifier from the furni data.</summary>
    public int OfferId { get; init; }

    /// <summary>Gets the furni data's <c>buyout</c> flag for the catalog offer.</summary>
    public bool BuyOut { get; init; }

    /// <summary>Gets the rental offer identifier from the furni data.</summary>
    public int RentOfferId { get; init; }

    /// <summary>Gets the furni data's <c>rentbuyout</c> flag for the rental offer.</summary>
    public bool RentBuyOut { get; init; }

    /// <summary>Gets whether this kind is a Builders Club item.</summary>
    public bool IsBuildersClub { get; init; }

    /// <summary>Gets the Builders Club offer identifier from the furni data.</summary>
    public int BuildersClubOfferId { get; init; }

    /// <summary>Gets the furni data's <c>excludeddynamic</c> flag.</summary>
    public bool ExcludedDynamic { get; init; }

    /// <summary>Gets the free-form parameter string the hotel attaches to the kind, empty when none.</summary>
    public string CustomParams { get; init; } = "";

    /// <summary>Gets the special behavior of this kind, as <see cref="FurniCategory"/>.</summary>
    /// <remarks>
    /// This is the furni data's <c>specialtype</c> field, the value that identifies presents,
    /// trophies, pet products, seeds and chests.
    /// </remarks>
    public FurniCategory SpecialType { get; init; }

    /// <summary>Gets whether avatars may stand on this kind.</summary>
    public bool CanStandOn { get; init; }

    /// <summary>Gets whether avatars may sit on this kind.</summary>
    public bool CanSitOn { get; init; }

    /// <summary>Gets whether avatars may lie on this kind.</summary>
    public bool CanLayOn { get; init; }

    /// <summary>Gets whether other furni may be stacked on this kind.</summary>
    public bool CanPutStuffOn { get; init; }

    /// <summary>Gets the kind's own height in tile units, which is what stacking adds on top of.</summary>
    public double Height { get; init; }

    /// <summary>Gets the environment tag the hotel gives the kind, empty when none.</summary>
    public string Environment { get; init; } = "";

    /// <summary>Gets whether the hotel marks this kind as rare.</summary>
    public bool IsRare { get; init; }

    /// <summary>Gets whether items of this kind may be traded.</summary>
    public bool Tradeable { get; init; }

    /// <summary>Gets whether items of this kind may be recycled.</summary>
    public bool Recyclable { get; init; }

    /// <summary>Gets whether <see cref="Identifier"/> carries a numeric <c>*</c> color suffix.</summary>
    public bool HasIndexedColor { get; init; }

    /// <summary>
    /// Gets the numeric color suffix of <see cref="Identifier"/>, or 0 when
    /// <see cref="HasIndexedColor"/> is <see langword="false"/>.
    /// </summary>
    public int ColorIndex { get; init; }

    /// <summary>Gets whether an avatar can occupy the tile at all, that is stand, sit or lie on it.</summary>
    public bool IsWalkable { get; init; }

    /// <summary>Gets the negation of <see cref="IsWalkable"/>.</summary>
    public bool IsUnwalkable { get; init; }
}

/// <summary>
/// Represents the JSON projection of one row of a game furni's high-score table used by the MCP read
/// tools and the application-layer results; scripts that read live state use <see cref="HighScore"/>.
/// </summary>
/// <param name="Score">The score achieved.</param>
/// <param name="Names">The names of the users who achieved it, since a score can be shared by a team.</param>
public sealed record HighScoreSnapshot(
    int Score,
    IReadOnlyList<string> Names);

/// <summary>
/// Represents the JSON projection of a furni payload, flattened into one record whose
/// shape-specific members are set only where they apply, used by the MCP read tools and the
/// application-layer results; scripts that read live state use <see cref="ItemData"/>.
/// </summary>
/// <remarks>
/// The map, string list, integer list, vote, high-score and crackable members are
/// <see langword="null"/> unless the payload is of the matching type, and are then omitted
/// from JSON.
/// </remarks>
/// <param name="Type">
/// The payload shape: <c>Legacy</c>, <c>Map</c>, <c>StringArray</c>, <c>VoteResult</c>,
/// <c>Empty</c>, <c>IntArray</c>, <c>HighScore</c> or <c>CrackableFurni</c>.
/// </param>
/// <param name="Flags">
/// The modifier bits from the payload header, as the numeric value of <c>ItemDataFlags</c>;
/// the flag value 1 marks a limited rare.
/// </param>
/// <param name="Value">
/// The payload's string value, which is the furni state for most items. Empty for the map,
/// string list, integer list and empty shapes.
/// </param>
/// <param name="State">
/// <paramref name="Value"/> read as a state: 0 for <c>C</c>, <c>FALSE</c> or <c>OFF</c>, 1 for
/// <c>O</c>, <c>TRUE</c> or <c>ON</c>, any other integer as sent, and -1 when it is not a number.
/// </param>
/// <param name="IsLimitedRare">Whether the item belongs to a numbered limited series.</param>
/// <param name="UniqueSerialNumber">The item's number within its limited series, or 0 when it is not a limited rare.</param>
/// <param name="UniqueSeriesSize">The size of the item's limited series, or 0 when it is not a limited rare.</param>
/// <param name="UniqueLimitedData">
/// Additional limited-edition text; the payload parser does not fill it, so it is normally
/// empty.
/// </param>
/// <param name="MapEntries">The key and value pairs of a <c>Map</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="StringValues">The strings of a <c>StringArray</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="IntValues">The integers of an <c>IntArray</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="VoteResult">The vote tally of a <c>VoteResult</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="ScoreType">The scoring mode of a <c>HighScore</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="ClearType">The clearing mode of a <c>HighScore</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="HighScores">The score table of a <c>HighScore</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="Hits">The hit count of a <c>CrackableFurni</c> payload; otherwise, <see langword="null"/>.</param>
/// <param name="Target">The hits a <c>CrackableFurni</c> payload needs in total; otherwise, <see langword="null"/>.</param>
public sealed record ItemDataSnapshot(
    string Type,
    int Flags,
    string Value,
    int State,
    bool IsLimitedRare,
    int UniqueSerialNumber,
    int UniqueSeriesSize,
    string UniqueLimitedData,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? MapEntries,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? StringValues,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<int>? IntValues,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? VoteResult,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ScoreType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ClearType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<HighScoreSnapshot>? HighScores,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Hits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Target);

/// <summary>
/// Represents the JSON projection of a furni standing on the room floor for the MCP read tools;
/// scripts use <see cref="FloorItem"/>.
/// </summary>
/// <param name="Id">The item identifier, unique within the hotel.</param>
/// <param name="IsRemoved">
/// Whether this snapshot describes an item that has already been picked up. Set on the copy
/// handed to removal events; always <see langword="false"/> for items read out of the live room.
/// </param>
/// <param name="Kind">The furni kind identifier. A negative kind means the identity is carried by <paramref name="Identifier"/> instead.</param>
/// <param name="Identifier">
/// The furni class name, taken from the packet when it carries one and otherwise from the
/// definition catalog. <see langword="null"/> when neither source has it.
/// </param>
/// <param name="Definition">The catalog definition, or <see langword="null"/> when definitions are not loaded.</param>
/// <param name="OwnerId">The owning user's identifier.</param>
/// <param name="OwnerName">The owning user's name; the hotel sends it empty in many room packets.</param>
/// <param name="Position">The tile the item's anchor sits on, including its stack height.</param>
/// <param name="Area">
/// The tiles the item covers, already rotated for its direction. Falls back to the item's
/// own size when no definition is available.
/// </param>
/// <param name="Direction">The item's facing, 0-7 clockwise from north.</param>
/// <param name="Height">The item's own height in tile units, as sent with the item.</param>
/// <param name="Extra">
/// The extra identifier the hotel attaches, most often the linked item for stacked or paired
/// furni. Serialized exactly rather than as a JSON number so no precision is lost.
/// </param>
/// <param name="Data">The item's payload, which is where furni state and game data live.</param>
/// <param name="State">
/// The item's state, derived from <paramref name="Data"/>: 0 off, 1 on, any other integer as
/// sent, and -1 when the payload is not a state at all.
/// </param>
/// <param name="SecondsToExpiration">Seconds until a rented item expires; -1 when it does not expire.</param>
/// <param name="Usage">
/// Who may use the item: <c>None</c> (0), <c>Rights</c> (1) for rights holders only, or
/// <c>Anyone</c> (2).
/// </param>
/// <param name="IsHidden">Whether the client hides the item from view.</param>
public sealed record FloorItemSnapshot(
    Id Id,
    bool IsRemoved,
    int Kind,
    string? Identifier,
    FurniDefinitionSnapshot? Definition,
    Id OwnerId,
    string OwnerName,
    PositionSnapshot Position,
    AreaSnapshot Area,
    int Direction,
    float Height,
    [property: JsonConverter(typeof(ExactInt64JsonConverter))]
    long Extra,
    ItemDataSnapshot Data,
    int State,
    int SecondsToExpiration,
    string Usage,
    bool IsHidden)
{
    /// <summary>
    /// Initializes a new instance for compatibility, taking identifiers as plain <see cref="long"/> values and
    /// fixing <see cref="IsRemoved"/> to <see langword="false"/>.
    /// </summary>
    /// <param name="Id">The item identifier.</param>
    /// <param name="Kind">The furni kind identifier.</param>
    /// <param name="Identifier">The furni class name, or <see langword="null"/> when unknown.</param>
    /// <param name="Definition">The catalog definition, or <see langword="null"/> when definitions are not loaded.</param>
    /// <param name="OwnerId">The owning user's identifier.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="Position">The tile the item's anchor sits on, including its stack height.</param>
    /// <param name="Area">The tiles the item covers, already rotated for its direction.</param>
    /// <param name="Direction">The item's facing, 0-7 clockwise from north.</param>
    /// <param name="Height">The item's own height in tile units.</param>
    /// <param name="Extra">The extra identifier the hotel attaches.</param>
    /// <param name="Data">The item's payload.</param>
    /// <param name="State">The item's state derived from <paramref name="Data"/>, or -1 when the payload is not a state.</param>
    /// <param name="SecondsToExpiration">The seconds until a rented item expires; -1 when it does not expire.</param>
    /// <param name="Usage">Who may use the item: <c>None</c>, <c>Rights</c> or <c>Anyone</c>.</param>
    /// <param name="IsHidden">Whether the client hides the item from view.</param>
    public FloorItemSnapshot(
        long Id,
        int Kind,
        string? Identifier,
        FurniDefinitionSnapshot? Definition,
        long OwnerId,
        string OwnerName,
        PositionSnapshot Position,
        AreaSnapshot Area,
        int Direction,
        float Height,
        long Extra,
        ItemDataSnapshot Data,
        int State,
        int SecondsToExpiration,
        string Usage,
        bool IsHidden)
        : this(
            (Qx.Id)Id,
            false,
            Kind,
            Identifier,
            Definition,
            (Qx.Id)OwnerId,
            OwnerName,
            Position,
            Area,
            Direction,
            Height,
            Extra,
            Data,
            State,
            SecondsToExpiration,
            Usage,
            IsHidden)
    {
    }

    /// <summary>
    /// Deconstructs the instance for compatibility, yielding identifiers as plain <see cref="long"/>
    /// values and omitting <see cref="IsRemoved"/>.
    /// </summary>
    /// <param name="Id">The item identifier as a plain <see cref="long"/>.</param>
    /// <param name="Kind">The furni kind identifier.</param>
    /// <param name="Identifier">The furni class name, or <see langword="null"/> when unknown.</param>
    /// <param name="Definition">The catalog definition, or <see langword="null"/>.</param>
    /// <param name="OwnerId">The owning user's identifier as a plain <see cref="long"/>.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="Position">The tile the item's anchor sits on.</param>
    /// <param name="Area">The tiles the item covers.</param>
    /// <param name="Direction">The item's facing.</param>
    /// <param name="Height">The item's own height in tile units.</param>
    /// <param name="Extra">The extra identifier the hotel attaches.</param>
    /// <param name="Data">The item's payload.</param>
    /// <param name="State">The item's state.</param>
    /// <param name="SecondsToExpiration">The seconds until a rented item expires; -1 when it does not expire.</param>
    /// <param name="Usage">Who may use the item.</param>
    /// <param name="IsHidden">Whether the client hides the item from view.</param>
    public void Deconstruct(
        out long Id,
        out int Kind,
        out string? Identifier,
        out FurniDefinitionSnapshot? Definition,
        out long OwnerId,
        out string OwnerName,
        out PositionSnapshot Position,
        out AreaSnapshot Area,
        out int Direction,
        out float Height,
        out long Extra,
        out ItemDataSnapshot Data,
        out int State,
        out int SecondsToExpiration,
        out string Usage,
        out bool IsHidden)
    {
        Id = this.Id;
        Kind = this.Kind;
        Identifier = this.Identifier;
        Definition = this.Definition;
        OwnerId = this.OwnerId;
        OwnerName = this.OwnerName;
        Position = this.Position;
        Area = this.Area;
        Direction = this.Direction;
        Height = this.Height;
        Extra = this.Extra;
        Data = this.Data;
        State = this.State;
        SecondsToExpiration = this.SecondsToExpiration;
        Usage = this.Usage;
        IsHidden = this.IsHidden;
    }
}

/// <summary>
/// Represents the JSON projection of the position of a wall item on the wall for the MCP read
/// tools; scripts use <see cref="WallLocation"/>.
/// </summary>
/// <param name="WallX">The wall segment's column.</param>
/// <param name="WallY">The wall segment's row.</param>
/// <param name="OffsetX">The horizontal offset within that segment, in wall pixels.</param>
/// <param name="OffsetY">The vertical offset within that segment, in wall pixels.</param>
/// <param name="Orientation">Which wall the item hangs on: <c>l</c> for the left wall, <c>r</c> for the right.</param>
/// <param name="Raw">
/// The location in the client's own text form, <c>:w=wx,wy l=lx,ly o</c>. This is the exact
/// string a placement packet expects.
/// </param>
public sealed record WallLocationSnapshot(
    int WallX,
    int WallY,
    int OffsetX,
    int OffsetY,
    string Orientation,
    string Raw);

/// <summary>
/// Represents the JSON projection of a furni hanging on a room wall for the MCP read tools; scripts
/// use <see cref="WallItem"/>.
/// </summary>
/// <param name="Id">The item identifier, unique within the hotel.</param>
/// <param name="IsRemoved">
/// Whether this snapshot describes an item that has already been picked up. Set on the copy
/// handed to removal events; always <see langword="false"/> for items read out of the live room.
/// </param>
/// <param name="Kind">The furni kind identifier.</param>
/// <param name="Identifier">
/// The furni class name from the definition catalog; wall item packets never carry one
/// themselves. <see langword="null"/> when definitions are not loaded.
/// </param>
/// <param name="Definition">The catalog definition, or <see langword="null"/> when definitions are not loaded.</param>
/// <param name="OwnerId">The owning user's identifier.</param>
/// <param name="OwnerName">The owning user's name; the hotel sends it empty in many room packets.</param>
/// <param name="Location">Where on the wall the item hangs.</param>
/// <param name="Data">
/// The item's payload as a plain string. Wall items are not sent with the structured payload
/// floor items get; for a sticky or a photo this is the raw content.
/// </param>
/// <param name="State">
/// <paramref name="Data"/> parsed as an integer, or -1 when it does not parse. Unlike floor
/// items, wall item data has no <c>ON</c>/<c>OFF</c> spelling.
/// </param>
/// <param name="SecondsToExpiration">Seconds until a rented item expires; -1 when it does not expire.</param>
/// <param name="Usage">
/// Who may use the item: <c>None</c> (0), <c>Rights</c> (1) for rights holders only, or
/// <c>Anyone</c> (2).
/// </param>
/// <param name="IsHidden">Whether the client hides the item from view.</param>
public sealed record WallItemSnapshot(
    Id Id,
    bool IsRemoved,
    int Kind,
    string? Identifier,
    FurniDefinitionSnapshot? Definition,
    Id OwnerId,
    string OwnerName,
    WallLocationSnapshot Location,
    string Data,
    int State,
    int SecondsToExpiration,
    string Usage,
    bool IsHidden)
{
    /// <summary>
    /// Initializes a new instance for compatibility, taking identifiers as plain <see cref="long"/> values and
    /// fixing <see cref="IsRemoved"/> to <see langword="false"/>.
    /// </summary>
    /// <param name="Id">The item identifier.</param>
    /// <param name="Kind">The furni kind identifier.</param>
    /// <param name="Identifier">The furni class name, or <see langword="null"/> when definitions are not loaded.</param>
    /// <param name="Definition">The catalog definition, or <see langword="null"/> when definitions are not loaded.</param>
    /// <param name="OwnerId">The owning user's identifier.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="Location">Where on the wall the item hangs.</param>
    /// <param name="Data">The item's payload as a plain string.</param>
    /// <param name="State"><paramref name="Data"/> parsed as an integer, or -1 when it does not parse.</param>
    /// <param name="SecondsToExpiration">The seconds until a rented item expires; -1 when it does not expire.</param>
    /// <param name="Usage">Who may use the item: <c>None</c>, <c>Rights</c> or <c>Anyone</c>.</param>
    /// <param name="IsHidden">Whether the client hides the item from view.</param>
    public WallItemSnapshot(
        long Id,
        int Kind,
        string? Identifier,
        FurniDefinitionSnapshot? Definition,
        long OwnerId,
        string OwnerName,
        WallLocationSnapshot Location,
        string Data,
        int State,
        int SecondsToExpiration,
        string Usage,
        bool IsHidden)
        : this(
            (Qx.Id)Id,
            false,
            Kind,
            Identifier,
            Definition,
            (Qx.Id)OwnerId,
            OwnerName,
            Location,
            Data,
            State,
            SecondsToExpiration,
            Usage,
            IsHidden)
    {
    }

    /// <summary>
    /// Deconstructs the instance for compatibility, yielding identifiers as plain <see cref="long"/>
    /// values and omitting <see cref="IsRemoved"/>.
    /// </summary>
    /// <param name="Id">The item identifier as a plain <see cref="long"/>.</param>
    /// <param name="Kind">The furni kind identifier.</param>
    /// <param name="Identifier">The furni class name, or <see langword="null"/>.</param>
    /// <param name="Definition">The catalog definition, or <see langword="null"/>.</param>
    /// <param name="OwnerId">The owning user's identifier as a plain <see cref="long"/>.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="Location">Where on the wall the item hangs.</param>
    /// <param name="Data">The item's payload as a plain string.</param>
    /// <param name="State">The item's state.</param>
    /// <param name="SecondsToExpiration">The seconds until a rented item expires; -1 when it does not expire.</param>
    /// <param name="Usage">Who may use the item.</param>
    /// <param name="IsHidden">Whether the client hides the item from view.</param>
    public void Deconstruct(
        out long Id,
        out int Kind,
        out string? Identifier,
        out FurniDefinitionSnapshot? Definition,
        out long OwnerId,
        out string OwnerName,
        out WallLocationSnapshot Location,
        out string Data,
        out int State,
        out int SecondsToExpiration,
        out string Usage,
        out bool IsHidden)
    {
        Id = this.Id;
        Kind = this.Kind;
        Identifier = this.Identifier;
        Definition = this.Definition;
        OwnerId = this.OwnerId;
        OwnerName = this.OwnerName;
        Location = this.Location;
        Data = this.Data;
        State = this.State;
        SecondsToExpiration = this.SecondsToExpiration;
        Usage = this.Usage;
        IsHidden = this.IsHidden;
    }
}

/// <summary>
/// Represents the JSON projection of the furni in the current room, floor and wall items each
/// capped on their own, for the MCP read tools; scripts use <see cref="RoomManager.FloorItems"/>
/// and <see cref="RoomManager.WallItems"/>.
/// </summary>
/// <remarks>
/// When a truncation flag is set the corresponding list holds the items with the lowest
/// identifiers, not an arbitrary subset, so paging by identifier is meaningful. The counts
/// always describe the whole room even when the lists do not.
/// </remarks>
/// <param name="RoomId">The room the items belong to, or <see langword="null"/> when not in a room.</param>
/// <param name="Generation">The room session counter the projection was taken under.</param>
/// <param name="DefinitionsLoaded">
/// Whether the furni definition catalog was available. When <see langword="false"/> every
/// item's <c>Definition</c> is <see langword="null"/> and its area falls back to the size in
/// the room packet.
/// </param>
/// <param name="FloorItemCount">How many floor items the room actually has.</param>
/// <param name="WallItemCount">How many wall items the room actually has.</param>
/// <param name="ReturnedFloorItemCount">How many floor items are in <paramref name="FloorItems"/>.</param>
/// <param name="ReturnedWallItemCount">How many wall items are in <paramref name="WallItems"/>.</param>
/// <param name="MaxItemsPerType">The cap applied to each list separately.</param>
/// <param name="FloorItemsTruncated">Whether floor items were dropped to honor the cap.</param>
/// <param name="WallItemsTruncated">Whether wall items were dropped to honor the cap.</param>
/// <param name="FloorItems">The floor items, ordered ascending by identifier.</param>
/// <param name="WallItems">The wall items, ordered ascending by identifier.</param>
public sealed record FurniCollectionSnapshot(
    Id? RoomId,
    long Generation,
    bool DefinitionsLoaded,
    int FloorItemCount,
    int WallItemCount,
    int ReturnedFloorItemCount,
    int ReturnedWallItemCount,
    int MaxItemsPerType,
    bool FloorItemsTruncated,
    bool WallItemsTruncated,
    IReadOnlyList<FloorItemSnapshot> FloorItems,
    IReadOnlyList<WallItemSnapshot> WallItems)
{
    /// <summary>Initializes a new instance for compatibility, without room context or definition state.</summary>
    /// <param name="FloorItemCount">The number of floor items the room actually has.</param>
    /// <param name="WallItemCount">The number of wall items the room actually has.</param>
    /// <param name="ReturnedFloorItemCount">The number of floor items in <paramref name="FloorItems"/>.</param>
    /// <param name="ReturnedWallItemCount">The number of wall items in <paramref name="WallItems"/>.</param>
    /// <param name="MaxItemsPerType">The cap applied to each list separately.</param>
    /// <param name="FloorItemsTruncated">Whether floor items were dropped to honor the cap.</param>
    /// <param name="WallItemsTruncated">Whether wall items were dropped to honor the cap.</param>
    /// <param name="FloorItems">The floor items, ordered ascending by identifier.</param>
    /// <param name="WallItems">The wall items, ordered ascending by identifier.</param>
    public FurniCollectionSnapshot(
        int FloorItemCount,
        int WallItemCount,
        int ReturnedFloorItemCount,
        int ReturnedWallItemCount,
        int MaxItemsPerType,
        bool FloorItemsTruncated,
        bool WallItemsTruncated,
        IReadOnlyList<FloorItemSnapshot> FloorItems,
        IReadOnlyList<WallItemSnapshot> WallItems)
        : this(
            null,
            0,
            false,
            FloorItemCount,
            WallItemCount,
            ReturnedFloorItemCount,
            ReturnedWallItemCount,
            MaxItemsPerType,
            FloorItemsTruncated,
            WallItemsTruncated,
            FloorItems,
            WallItems)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, omitting room context and definition state.</summary>
    /// <param name="FloorItemCount">The number of floor items the room actually has.</param>
    /// <param name="WallItemCount">The number of wall items the room actually has.</param>
    /// <param name="ReturnedFloorItemCount">The number of floor items returned.</param>
    /// <param name="ReturnedWallItemCount">The number of wall items returned.</param>
    /// <param name="MaxItemsPerType">The cap applied to each list separately.</param>
    /// <param name="FloorItemsTruncated">Whether floor items were dropped to honor the cap.</param>
    /// <param name="WallItemsTruncated">Whether wall items were dropped to honor the cap.</param>
    /// <param name="FloorItems">The floor items, ordered ascending by identifier.</param>
    /// <param name="WallItems">The wall items, ordered ascending by identifier.</param>
    public void Deconstruct(
        out int FloorItemCount,
        out int WallItemCount,
        out int ReturnedFloorItemCount,
        out int ReturnedWallItemCount,
        out int MaxItemsPerType,
        out bool FloorItemsTruncated,
        out bool WallItemsTruncated,
        out IReadOnlyList<FloorItemSnapshot> FloorItems,
        out IReadOnlyList<WallItemSnapshot> WallItems)
    {
        FloorItemCount = this.FloorItemCount;
        WallItemCount = this.WallItemCount;
        ReturnedFloorItemCount = this.ReturnedFloorItemCount;
        ReturnedWallItemCount = this.ReturnedWallItemCount;
        MaxItemsPerType = this.MaxItemsPerType;
        FloorItemsTruncated = this.FloorItemsTruncated;
        WallItemsTruncated = this.WallItemsTruncated;
        FloorItems = this.FloorItems;
        WallItems = this.WallItems;
    }
}

/// <summary>
/// Represents the JSON projection of one item in the local user's inventory used by the MCP read
/// tools and the application-layer results; scripts that read live state use
/// <see cref="InventoryItem"/>.
/// </summary>
/// <param name="ItemId">
/// The inventory item identifier, which inventory requests such as placement address and which
/// the inventory snapshot orders by.
/// </param>
/// <param name="Type">Whether the item is a <c>Floor</c> or a <c>Wall</c> item.</param>
/// <param name="Id">The furni identifier the hotel sends alongside the inventory item identifier.</param>
/// <param name="Kind">The furni kind identifier.</param>
/// <param name="Definition">The catalog definition, or <see langword="null"/> when definitions are not loaded or the kind is unknown.</param>
/// <param name="Category">The category the item is filed under, as the numeric value of <see cref="FurniCategory"/>.</param>
/// <param name="Data">The item's payload.</param>
/// <param name="IsRecyclable">Whether the item may be recycled.</param>
/// <param name="IsTradeable">Whether the item may be traded.</param>
/// <param name="IsGroupable">Whether the item may be grouped with identical items in the inventory.</param>
/// <param name="IsSellable">Whether the item may be sold on the marketplace.</param>
/// <param name="SecondsToExpiration">The seconds until a rented item expires; -1 when it does not expire.</param>
/// <param name="HasRentPeriodStarted">Whether the rental period of a rented item has started.</param>
/// <param name="RoomId">The room identifier the hotel sends with the item.</param>
/// <param name="SlotId">The slot identifier the hotel sends with a floor item; empty for a wall item.</param>
/// <param name="Extra">
/// The extra value the hotel sends with a floor item, serialized exactly rather than as a JSON
/// number; 0 for a wall item.
/// </param>
public sealed record InventoryItemSnapshot(
    Id ItemId,
    string Type,
    Id Id,
    int Kind,
    FurniDefinitionSnapshot? Definition,
    int Category,
    ItemDataSnapshot Data,
    bool IsRecyclable,
    bool IsTradeable,
    bool IsGroupable,
    bool IsSellable,
    int SecondsToExpiration,
    bool HasRentPeriodStarted,
    Id RoomId,
    string SlotId,
    [property: JsonConverter(typeof(ExactInt64JsonConverter))]
    long Extra);

/// <summary>
/// Represents the JSON projection of the local user's inventory and its load state for the MCP read
/// tools; scripts use the <c>InventoryItems</c> global.
/// </summary>
/// <remarks>
/// The inventory arrives in fragments. A snapshot taken mid-load returns whatever fragments
/// landed so far, which is why <paramref name="IsLoading"/> and the fragment counters matter
/// more here than for other queries.
/// </remarks>
/// <param name="DefinitionsLoaded">
/// Whether the furni definition catalog was available. When <see langword="false"/> every
/// item's <c>Definition</c> is <see langword="null"/>.
/// </param>
/// <param name="IsLoading">Whether a load is in flight right now.</param>
/// <param name="IsStale">
/// Whether the listed items are left over from a previous load that has been invalidated.
/// They are still returned, but a fresh load is needed before acting on them.
/// </param>
/// <param name="Generation">
/// A counter bumped every time a new load begins. Items from two different generations must
/// never be mixed.
/// </param>
/// <param name="ExpectedFragments">
/// How many fragments the current load consists of, or -1 while that is not yet known.
/// </param>
/// <param name="ReceivedFragments">How many fragments of the current load have arrived.</param>
/// <param name="Total">How many items the hand actually holds.</param>
/// <param name="Returned">How many items are in <paramref name="Items"/>.</param>
/// <param name="MaxItems">The cap applied to the projection.</param>
/// <param name="Truncated">Whether items were dropped to honor the cap.</param>
/// <param name="Items">
/// The items, ordered ascending by inventory slot identifier. When truncated these are the
/// lowest identifiers, so paging by identifier is meaningful.
/// </param>
public sealed record InventorySnapshot(
    bool DefinitionsLoaded,
    bool IsLoading,
    bool IsStale,
    long Generation,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total,
    int Returned,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<InventoryItemSnapshot> Items);

/// <summary>
/// Represents the JSON projection of one tile of the live heightmap for the MCP read tools; scripts
/// use <see cref="HeightmapTile"/>.
/// </summary>
/// <param name="X">The tile column.</param>
/// <param name="Y">The tile row.</param>
/// <param name="Value">
/// The raw wire value. Negative means the tile is not floor; otherwise bit 14 (0x4000) is
/// the blocked flag and the low 14 bits are the height in 1/256 tile units.
/// </param>
/// <param name="IsFloor">Whether the tile is floor at all, that is <paramref name="Value"/> is not negative.</param>
/// <param name="IsBlocked">Whether the blocked bit is set, which normally means furni occupies the tile.</param>
/// <param name="IsWalkable">Whether the tile is floor and not blocked, which is the condition for stepping on it.</param>
/// <param name="Height">The stack height in tile units, or -1 for a non-floor tile.</param>
public sealed record HeightmapTileSnapshot(
    int X,
    int Y,
    short Value,
    bool IsFloor,
    bool IsBlocked,
    bool IsWalkable,
    double Height);

/// <summary>
/// Represents the JSON projection of the live heightmap of the current room, with per-tile detail
/// and aggregate counts, for the MCP read tools; scripts use <see cref="Heightmap"/>.
/// </summary>
/// <remarks>
/// Truncation here keeps the first tiles in the heightmap's own row-major order and drops the
/// tail, so a truncated snapshot covers the top of the room and not the bottom. The counts
/// are computed over every tile regardless of the cap.
/// </remarks>
/// <param name="RoomId">The room the heightmap belongs to, or <see langword="null"/> when not in a room.</param>
/// <param name="Generation">The room session counter the projection was taken under.</param>
/// <param name="Width">The heightmap's column count.</param>
/// <param name="Length">The heightmap's row count.</param>
/// <param name="TileCount">How many tiles the heightmap actually has.</param>
/// <param name="ReturnedTileCount">How many tiles are in <paramref name="Tiles"/>.</param>
/// <param name="MaxTiles">The cap applied to the projection.</param>
/// <param name="Truncated">Whether tiles were dropped to honor the cap.</param>
/// <param name="FloorTileCount">Tiles that are floor at all, blocked or not.</param>
/// <param name="WalkableTileCount">Floor tiles that are currently free to step on.</param>
/// <param name="BlockedTileCount">Floor tiles currently blocked.</param>
/// <param name="NonFloorTileCount">Void tiles; equal to <paramref name="TileCount"/> minus <paramref name="FloorTileCount"/>.</param>
/// <param name="Tiles">The tiles in the heightmap's own row-major order.</param>
public sealed record HeightmapSnapshot(
    Id? RoomId,
    long Generation,
    int Width,
    int Length,
    int TileCount,
    int ReturnedTileCount,
    int MaxTiles,
    bool Truncated,
    int FloorTileCount,
    int WalkableTileCount,
    int BlockedTileCount,
    int NonFloorTileCount,
    IReadOnlyList<HeightmapTileSnapshot> Tiles);

/// <summary>
/// Represents the JSON projection of the detailed statistics of one pet for the MCP read tools;
/// scripts use <see cref="PetInfo"/>.
/// </summary>
/// <remarks>
/// The request does not carry the pet type, only the breed variant. What kind of animal it
/// is lives on the room entity, which is why <see cref="PetType"/> is a separate optional
/// field filled in by the caller when the room knows it.
/// </remarks>
/// <param name="Id">The pet identifier.</param>
/// <param name="Name">The pet's name.</param>
/// <param name="Level">The pet's current level.</param>
/// <param name="MaxLevel">The highest level this pet can reach.</param>
/// <param name="Experience">Experience accumulated towards the next level.</param>
/// <param name="MaxExperience">Experience needed for the next level.</param>
/// <param name="Energy">Current energy; a pet with no energy sleeps.</param>
/// <param name="MaxEnergy">The energy cap.</param>
/// <param name="Happiness">Current nutrition; the client calls this field <c>nutrition</c>.</param>
/// <param name="MaxHappiness">The nutrition cap; the client calls this field <c>maxNutrition</c>.</param>
/// <param name="Scratches">Respect received; the client calls this field <c>respect</c>.</param>
/// <param name="OwnerId">The owning user's identifier.</param>
/// <param name="Age">The pet's age in days.</param>
/// <param name="OwnerName">The owning user's name.</param>
/// <param name="BreedId">
/// The breed variant within the pet type, not the pet type itself. Pet types with no
/// variants, notably the monsterplant (type 16), report 0 here, so 0 must not be read as
/// "unknown pet".
/// </param>
/// <param name="HasFreeSaddle">Whether the pet's saddle is unlocked without a purchase.</param>
/// <param name="IsRiding">Whether a user is currently riding the pet.</param>
/// <param name="SkillThresholds">The experience thresholds at which the pet unlocks its skills.</param>
/// <param name="AccessRights">
/// The hotel's own access-rights code controlling who may command the pet. The numbering is
/// hotel-specific and is passed through unchanged.
/// </param>
/// <param name="CanBreed">Whether the pet may be bred right now.</param>
/// <param name="CanHarvest">Whether the pet may be harvested right now.</param>
/// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
/// <param name="RarityLevel">The pet's rarity tier as sent by the hotel.</param>
/// <param name="MaxWellbeingSeconds">The full duration of the wellbeing timer, in seconds.</param>
/// <param name="RemainingWellbeingSeconds">Seconds of wellbeing left before the pet suffers.</param>
/// <param name="RemainingGrowingSeconds">Seconds left in the current growth stage; relevant for monsterplants.</param>
/// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
public sealed record PetInfoSnapshot(
    Id Id,
    string Name,
    int Level,
    int MaxLevel,
    int Experience,
    int MaxExperience,
    int Energy,
    int MaxEnergy,
    int Happiness,
    int MaxHappiness,
    int Scratches,
    Id OwnerId,
    int Age,
    string OwnerName,
    int BreedId,
    bool HasFreeSaddle,
    bool IsRiding,
    IReadOnlyList<int> SkillThresholds,
    int AccessRights,
    bool CanBreed,
    bool CanHarvest,
    bool CanRevive,
    int RarityLevel,
    int MaxWellbeingSeconds,
    int RemainingWellbeingSeconds,
    int RemainingGrowingSeconds,
    bool HasBreedingPermission)
{
    /// <summary>
    /// Gets what kind of animal this is, supplied from the room entity because the pet info
    /// message does not carry it.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/>, and omitted from JSON, when the pet is not in the room. Together
    /// with <see cref="BreedId"/> this resolves the displayed breed.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PetType { get; init; }

    /// <summary>Initializes a new instance for compatibility, taking identifiers as plain <see cref="long"/> values.</summary>
    /// <param name="Id">The pet identifier.</param>
    /// <param name="Name">The pet's name.</param>
    /// <param name="Level">The pet's current level.</param>
    /// <param name="MaxLevel">The highest level this pet can reach.</param>
    /// <param name="Experience">The experience accumulated towards the next level.</param>
    /// <param name="MaxExperience">The experience needed for the next level.</param>
    /// <param name="Energy">The current energy.</param>
    /// <param name="MaxEnergy">The energy cap.</param>
    /// <param name="Happiness">The current nutrition.</param>
    /// <param name="MaxHappiness">The nutrition cap.</param>
    /// <param name="Scratches">The respect received.</param>
    /// <param name="OwnerId">The owning user's identifier.</param>
    /// <param name="Age">The pet's age in days.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="BreedId">The breed variant within the pet type.</param>
    /// <param name="HasFreeSaddle">Whether the pet's saddle is unlocked without a purchase.</param>
    /// <param name="IsRiding">Whether a user is currently riding the pet.</param>
    /// <param name="SkillThresholds">The experience thresholds at which the pet unlocks its skills.</param>
    /// <param name="AccessRights">The hotel's access-rights code controlling who may command the pet.</param>
    /// <param name="CanBreed">Whether the pet may be bred right now.</param>
    /// <param name="CanHarvest">Whether the pet may be harvested right now.</param>
    /// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
    /// <param name="RarityLevel">The pet's rarity tier.</param>
    /// <param name="MaxWellbeingSeconds">The full duration of the wellbeing timer, in seconds.</param>
    /// <param name="RemainingWellbeingSeconds">The seconds of wellbeing left.</param>
    /// <param name="RemainingGrowingSeconds">The seconds left in the current growth stage.</param>
    /// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
    public PetInfoSnapshot(
        long Id,
        string Name,
        int Level,
        int MaxLevel,
        int Experience,
        int MaxExperience,
        int Energy,
        int MaxEnergy,
        int Happiness,
        int MaxHappiness,
        int Scratches,
        long OwnerId,
        int Age,
        string OwnerName,
        int BreedId,
        bool HasFreeSaddle,
        bool IsRiding,
        IReadOnlyList<int> SkillThresholds,
        int AccessRights,
        bool CanBreed,
        bool CanHarvest,
        bool CanRevive,
        int RarityLevel,
        int MaxWellbeingSeconds,
        int RemainingWellbeingSeconds,
        int RemainingGrowingSeconds,
        bool HasBreedingPermission)
        : this(
            (Qx.Id)Id,
            Name,
            Level,
            MaxLevel,
            Experience,
            MaxExperience,
            Energy,
            MaxEnergy,
            Happiness,
            MaxHappiness,
            Scratches,
            (Qx.Id)OwnerId,
            Age,
            OwnerName,
            BreedId,
            HasFreeSaddle,
            IsRiding,
            SkillThresholds,
            AccessRights,
            CanBreed,
            CanHarvest,
            CanRevive,
            RarityLevel,
            MaxWellbeingSeconds,
            RemainingWellbeingSeconds,
            RemainingGrowingSeconds,
            HasBreedingPermission)
    {
    }

    /// <summary>Deconstructs the instance for compatibility, yielding identifiers as plain <see cref="long"/> values.</summary>
    /// <param name="Id">The pet identifier as a plain <see cref="long"/>.</param>
    /// <param name="Name">The pet's name.</param>
    /// <param name="Level">The pet's current level.</param>
    /// <param name="MaxLevel">The highest level this pet can reach.</param>
    /// <param name="Experience">The experience accumulated towards the next level.</param>
    /// <param name="MaxExperience">The experience needed for the next level.</param>
    /// <param name="Energy">The current energy.</param>
    /// <param name="MaxEnergy">The energy cap.</param>
    /// <param name="Happiness">The current nutrition.</param>
    /// <param name="MaxHappiness">The nutrition cap.</param>
    /// <param name="Scratches">The respect received.</param>
    /// <param name="OwnerId">The owning user's identifier as a plain <see cref="long"/>.</param>
    /// <param name="Age">The pet's age in days.</param>
    /// <param name="OwnerName">The owning user's name.</param>
    /// <param name="BreedId">The breed variant within the pet type.</param>
    /// <param name="HasFreeSaddle">Whether the pet's saddle is unlocked without a purchase.</param>
    /// <param name="IsRiding">Whether a user is currently riding the pet.</param>
    /// <param name="SkillThresholds">The experience thresholds at which the pet unlocks its skills.</param>
    /// <param name="AccessRights">The hotel's access-rights code controlling who may command the pet.</param>
    /// <param name="CanBreed">Whether the pet may be bred right now.</param>
    /// <param name="CanHarvest">Whether the pet may be harvested right now.</param>
    /// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
    /// <param name="RarityLevel">The pet's rarity tier.</param>
    /// <param name="MaxWellbeingSeconds">The full duration of the wellbeing timer, in seconds.</param>
    /// <param name="RemainingWellbeingSeconds">The seconds of wellbeing left.</param>
    /// <param name="RemainingGrowingSeconds">The seconds left in the current growth stage.</param>
    /// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
    public void Deconstruct(
        out long Id,
        out string Name,
        out int Level,
        out int MaxLevel,
        out int Experience,
        out int MaxExperience,
        out int Energy,
        out int MaxEnergy,
        out int Happiness,
        out int MaxHappiness,
        out int Scratches,
        out long OwnerId,
        out int Age,
        out string OwnerName,
        out int BreedId,
        out bool HasFreeSaddle,
        out bool IsRiding,
        out IReadOnlyList<int> SkillThresholds,
        out int AccessRights,
        out bool CanBreed,
        out bool CanHarvest,
        out bool CanRevive,
        out int RarityLevel,
        out int MaxWellbeingSeconds,
        out int RemainingWellbeingSeconds,
        out int RemainingGrowingSeconds,
        out bool HasBreedingPermission)
    {
        Id = this.Id;
        Name = this.Name;
        Level = this.Level;
        MaxLevel = this.MaxLevel;
        Experience = this.Experience;
        MaxExperience = this.MaxExperience;
        Energy = this.Energy;
        MaxEnergy = this.MaxEnergy;
        Happiness = this.Happiness;
        MaxHappiness = this.MaxHappiness;
        Scratches = this.Scratches;
        OwnerId = this.OwnerId;
        Age = this.Age;
        OwnerName = this.OwnerName;
        BreedId = this.BreedId;
        HasFreeSaddle = this.HasFreeSaddle;
        IsRiding = this.IsRiding;
        SkillThresholds = this.SkillThresholds;
        AccessRights = this.AccessRights;
        CanBreed = this.CanBreed;
        CanHarvest = this.CanHarvest;
        CanRevive = this.CanRevive;
        RarityLevel = this.RarityLevel;
        MaxWellbeingSeconds = this.MaxWellbeingSeconds;
        RemainingWellbeingSeconds = this.RemainingWellbeingSeconds;
        RemainingGrowingSeconds = this.RemainingGrowingSeconds;
        HasBreedingPermission = this.HasBreedingPermission;
    }
}
