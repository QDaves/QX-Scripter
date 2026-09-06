using System.Text.Json.Serialization;
using Qx;
using Qx.Model;

namespace Qx.Game.Snapshots;

/// <summary>
/// The load state that accompanies every snapshot, describing how complete and how
/// trustworthy the payload next to it is.
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
/// A failed query, classified so a caller can react without parsing an exception message.
/// </summary>
/// <param name="Code">
/// The stable failure class. One of <c>cancelled</c>, <c>timeout</c>, <c>disconnected</c>,
/// <c>invalid_response</c>, <c>correlation_error</c>, <c>unsupported_client</c>,
/// <c>unsupported</c>, <c>not_found</c>, <c>invalid_request</c>, <c>connection_error</c>,
/// <c>unavailable</c> or <c>request_failed</c>. Prefer this over <paramref name="Type"/>.
/// </param>
/// <param name="Type">The full CLR type name of the originating exception.</param>
/// <param name="Message">The exception message, for diagnostics only.</param>
/// <param name="OutgoingName">
/// The name of the request message that was sent, when the failure was a timeout or a
/// disconnect while awaiting a reply; otherwise <see langword="null"/>.
/// </param>
/// <param name="IncomingName">
/// The name of the reply message that was awaited or that failed to parse; otherwise
/// <see langword="null"/>.
/// </param>
/// <param name="ResponseType">
/// The CLR type the reply was being parsed or matched into, for parse and correlation
/// failures; otherwise <see langword="null"/>.
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
/// The wrapper every read query returns: the payload plus its load state, or an error.
/// </summary>
/// <remarks>
/// On success <paramref name="Error"/> is <see langword="null"/>; on failure
/// <paramref name="Data"/> is <see langword="default"/> and every metadata flag is
/// <see langword="false"/>. A <see langword="null"/> <paramref name="Data"/> is not by
/// itself a failure: queries whose payload is nullable (profile, heightmap) return
/// <see langword="null"/> with no error when the value simply does not exist yet.
/// </remarks>
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
/// The state of the link to G-Earth and to the hotel, and what is known about the wire
/// format of the connected client.
/// </summary>
/// <param name="InterceptorConnected">Whether the packet interceptor (G-Earth) is attached.</param>
/// <param name="HotelConnected">Whether a hotel session is currently open.</param>
/// <param name="MessageCatalogLoaded">
/// Whether the message-name catalog is available. Until this is <see langword="true"/>
/// packets can only be addressed by raw header, not by name.
/// </param>
/// <param name="WireProfileAnalyzed">
/// Whether the connected client build has been analysed, which is what lets structure-varying
/// messages (guest room results, marketplace offers, inventory items) be parsed at all.
/// </param>
/// <param name="WireProfileExact">
/// Whether the analysis produced an exact match for this build rather than a best-effort
/// fallback. Messages that require an exact profile throw
/// <see cref="NotSupportedException"/> while this is <see langword="false"/>.
/// </param>
/// <param name="MissingWireCapabilities">
/// The names of wire capabilities the connected build does not provide. Any feature listed
/// here will fail rather than silently misbehave.
/// </param>
/// <param name="Client">
/// The client flavour, <c>Flash</c>, <c>Unity</c> or <c>None</c>; <see langword="null"/>
/// when no session is open.
/// </param>
/// <param name="Host">The hotel host the client connected to, or <see langword="null"/> with no session.</param>
/// <param name="Port">The hotel port, or <see langword="null"/> with no session.</param>
/// <param name="HotelVersion">The hotel build string reported at connect, or <see langword="null"/>.</param>
/// <param name="ClientIdentifier">The client identifier reported at connect, or <see langword="null"/>.</param>
public sealed record ConnectionSnapshot(
    bool InterceptorConnected,
    bool HotelConnected,
    bool MessageCatalogLoaded,
    bool WireProfileAnalyzed,
    bool WireProfileExact,
    IReadOnlyList<string> MissingWireCapabilities,
    string? Client,
    string? Host,
    int? Port,
    string? HotelVersion,
    string? ClientIdentifier);

/// <summary>A point in room space.</summary>
/// <param name="X">The tile column.</param>
/// <param name="Y">The tile row.</param>
/// <param name="Z">
/// The height above the floor in tile units, where one unit is one full tile height.
/// For furni this is the stack height the item sits at.
/// </param>
public sealed record PositionSnapshot(int X, int Y, float Z);

/// <summary>The rectangle of tiles an object occupies.</summary>
/// <param name="Origin">The anchor tile, which is the object's own position.</param>
/// <param name="Width">The extent along X in tiles, already rotated for the object's direction.</param>
/// <param name="Length">The extent along Y in tiles, already rotated for the object's direction.</param>
public sealed record AreaSnapshot(PositionSnapshot Origin, int Width, int Length);

/// <summary>
/// The navigator record of a room: everything shown in a room listing or on the room's
/// info card.
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
/// One rectangular hole cut into the floor by an area-hider furni, as fed to the client's
/// floor-hole update.
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
/// The room's static geometry: the tile height map the room was built with, plus the
/// camera hint and any area hiders.
/// </summary>
/// <param name="UseLegacyScale">
/// Whether the room is drawn at the old 32-pixel tile scale instead of 64.
/// </param>
/// <param name="WallHeight">
/// The extra wall height configured for the room, in tile units; -1 means the client
/// derives it from the floor plan.
/// </param>
/// <param name="Map">
/// The raw floor plan text, one line per row. <c>x</c> marks a void tile; digits
/// <c>0</c>-<c>9</c> and letters <c>a</c>-<c>z</c> encode heights 0 to 35.
/// </param>
/// <param name="Width">The number of columns, taken from the longest line of <paramref name="Map"/>.</param>
/// <param name="Length">The number of rows in <paramref name="Map"/>.</param>
/// <param name="Scale">The tile scale in pixels: 32 when <paramref name="UseLegacyScale"/> is set, otherwise 64.</param>
/// <param name="Tiles">
/// The decoded heights in row-major order (<c>y * Width + x</c>), with -1 for void tiles.
/// </param>
/// <param name="HiddenAreas">The area-hider rectangles declared for this room.</param>
/// <param name="HasCameraData">
/// Whether the client sent a camera hint. The three camera fields are
/// <see langword="null"/> when this is <see langword="false"/>, which happens on Unity
/// builds that omit the tail.
/// </param>
/// <param name="CameraX">The X tile the camera centres on.</param>
/// <param name="CameraY">The Y tile the camera centres on.</param>
/// <param name="CameraZ">The camera height in tile units.</param>
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
/// Aggregate counts over the live heightmap, so walkability can be judged without shipping
/// every tile.
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
/// Which pieces of the current room session have arrived. Each flag is the per-piece
/// counterpart of the envelope's <c>Loaded</c> flag: a value of <see langword="false"/>
/// means "not received yet", never "empty".
/// </summary>
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

/// <summary>The tile an avatar is placed on when entering the room.</summary>
/// <param name="X">The door tile's column.</param>
/// <param name="Y">The door tile's row.</param>
/// <param name="Direction">The facing the avatar is given on arrival, 0-7 clockwise from north.</param>
public sealed record RoomEntryTileSnapshot(int X, int Y, int Direction);

/// <summary>How the room's walls and floor are drawn.</summary>
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

/// <summary>The room's chat configuration.</summary>
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
/// Who is allowed to moderate in the room. Each field is the numeric value of
/// <see cref="RoomModerationPermission"/>: 0 owner only, 1 rights holders, 2 everyone
/// (offered for kick only), 4 group admins and 5 group admins plus rights holders in a
/// group room.
/// </summary>
/// <param name="Mute">Who may mute other users; the hotel only offers 0 and 1 outside group rooms.</param>
/// <param name="Kick">Who may kick other users; the only permission that can be 2.</param>
/// <param name="Ban">Who may ban other users; the hotel only offers 0 and 1 outside group rooms.</param>
public sealed record RoomModerationSettingsSnapshot(int Mute, int Kick, int Ban);

/// <summary>A room thumbnail reference, sent by Unity builds with the guest room result.</summary>
/// <param name="RoomId">The room the thumbnail belongs to.</param>
/// <param name="Reference">The hotel-side reference of the stored image.</param>
/// <param name="ImageUrl">The absolute URL the image can be fetched from.</param>
public sealed record RoomThumbnailSnapshot(Id RoomId, string Reference, string ImageUrl);

/// <summary>
/// The detail block that accompanies a guest room result, carrying the viewer-specific
/// facts the navigator record does not.
/// </summary>
/// <remarks>
/// The tail differs per client: Flash sends <paramref name="OpeningConnection"/>, Unity
/// sends <paramref name="UnityContextId"/> and optionally <paramref name="UnityThumbnail"/>.
/// The field that does not apply to the connected client stays at its default.
/// </remarks>
/// <param name="Forward">Whether the client should immediately enter the room rather than only display it.</param>
/// <param name="IsStaffPick">Whether the room is currently a staff pick.</param>
/// <param name="IsGroupMember">Whether the local user belongs to the room's group.</param>
/// <param name="IsRoomMuted">Whether the room is muted for everyone right now.</param>
/// <param name="Moderation">Who may mute, kick and ban in this room.</param>
/// <param name="CanMute">Whether the local user is allowed to mute in this room.</param>
/// <param name="Chat">The room's chat configuration.</param>
/// <param name="OpeningConnection">
/// Flash only: whether this result was delivered as part of opening a room connection
/// rather than merely inspecting the room. <see langword="null"/> on Unity.
/// </param>
/// <param name="UnityContextId">Unity only: the navigator context the result was produced for; 0 on Flash.</param>
/// <param name="UnityThumbnail">Unity only: the room thumbnail, when one exists. Always <see langword="null"/> on Flash.</param>
public sealed record RoomResultDetailsSnapshot(
    bool Forward,
    bool IsStaffPick,
    bool IsGroupMember,
    bool IsRoomMuted,
    RoomModerationSettingsSnapshot Moderation,
    bool CanMute,
    RoomChatSettingsSnapshot Chat,
    bool? OpeningConnection,
    Id UnityContextId,
    RoomThumbnailSnapshot? UnityThumbnail);

/// <summary>
/// The decoration and layout of the room the session is inside. Every member is
/// <see langword="null"/> until the corresponding packet has arrived.
/// </summary>
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

/// <summary>What the local user is permitted to do in the current room.</summary>
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

/// <summary>One line of a door queue.</summary>
/// <param name="Type">The queue's identifier as sent by the hotel, for example <c>visitors</c>.</param>
/// <param name="Size">
/// The local user's zero-based place in this queue, or a negative value when the hotel
/// reports no place.
/// </param>
public sealed record RoomQueueEntrySnapshot(string Type, int Size);

/// <summary>A group of door queues sharing one entry target.</summary>
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

/// <summary>The door-queue status for a room the local user is waiting to enter.</summary>
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

/// <summary>Why a room could not be entered.</summary>
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

/// <summary>A kick of the local user out of a room.</summary>
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

/// <summary>How the previous room session ended.</summary>
/// <param name="RoomId">The room that was left.</param>
/// <param name="WasEntered">Whether the room had been fully entered before the exit.</param>
/// <param name="Source">
/// The transport that ended the session: <c>RoomTransition</c>, <c>ConnectionClosed</c>,
/// <c>NativeReason</c>, <c>ClientQuit</c>, <c>Disconnected</c>, <c>AccessFailure</c>,
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
/// <param name="HasNativeReason">Whether a native room exit reason accompanied the exit.</param>
/// <param name="WasKicked">Whether this exit was caused by a kick.</param>
/// <param name="Kick">The kick that caused the exit, or <see langword="null"/> when none did.</param>
public sealed record RoomExitSnapshot(
    Id RoomId,
    bool WasEntered,
    string Source,
    string Cause,
    short? Reason,
    bool HasNativeReason,
    bool WasKicked,
    RoomKickSnapshot? Kick);

/// <summary>
/// The entry side of the room session: getting in, waiting at the door, and how the last
/// attempt or the last session ended.
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
/// The whole room session in one object: where the session stands, who the local user is
/// in it, and how much of the room's content has arrived.
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
/// The decoded contents of an avatar's last status update: where it is, what it is doing,
/// and the raw status string those facts were parsed out of.
/// </summary>
/// <param name="StatusId">
/// The single integer the status packet carries besides the fragments. On Flash it is the
/// avatar's jumping power; on Unity builds that send it, it is a target identifier.
/// </param>
/// <param name="Position">The tile the avatar occupies, including its height.</param>
/// <param name="Direction">The body facing, 0-7 clockwise from north.</param>
/// <param name="HeadDirection">The head facing, 0-7 clockwise from north.</param>
/// <param name="Raw">
/// The recompiled status string, in the wire form <c>/key arg arg/key/</c>. Useful when a
/// fragment QX does not model needs to be read.
/// </param>
/// <param name="Stance">
/// The posture derived from the fragments: <c>Sit</c> when a <c>sit</c> fragment is present,
/// <c>Lay</c> for <c>lay</c>, otherwise <c>Stand</c>.
/// </param>
/// <param name="IsController">Whether the avatar carries a <c>flatctrl</c> fragment, meaning it holds rights.</param>
/// <param name="RightsLevel">
/// The argument of the <c>flatctrl</c> fragment, 0 when absent. The client's scale is
/// 0 not a controller, 1 room controller, 2 group member, 3 group admin, 4 room owner,
/// 5 moderator.
/// </param>
/// <param name="IsTrading">Whether the avatar carries the <c>trd</c> fragment, meaning it is in a trade.</param>
/// <param name="SittingOnFloor">
/// Whether the <c>sit</c> fragment's second argument is <c>1</c>, meaning the avatar sits on
/// the floor rather than on furni.
/// </param>
/// <param name="Sign">
/// The number of the hand sign the avatar is holding up, from the <c>sign</c> fragment;
/// 0 when no sign is shown.
/// </param>
/// <param name="MovingTo">
/// The destination tile from the <c>mv</c> fragment while the avatar is walking, or
/// <see langword="null"/> when it is standing still.
/// </param>
/// <param name="Fragments">
/// Every status fragment keyed case-insensitively by its name, with its space-separated
/// arguments. A snapshot copy, not a live view.
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
    /// <summary>
    /// The jumping-power field of the status packet. Flash only; 0 on Unity, where the same
    /// wire slot carries <see cref="TargetId"/> instead.
    /// </summary>
    public int JumpingPower { get; init; }

    /// <summary>
    /// The target identifier of the status packet. Unity only, and only on builds that send
    /// the field; 0 on Flash.
    /// </summary>
    public int TargetId { get; init; }

    /// <summary>
    /// The height offset carried by the <c>sit</c> or <c>lay</c> fragment, in tile units.
    /// <see langword="null"/> when standing or when the fragment carries no usable number.
    /// </summary>
    public double? ActionHeight { get; init; }
}

/// <summary>The fields that only a user avatar has.</summary>
/// <param name="Gender">The avatar's gender: <c>Male</c>, <c>Female</c> or <c>Unisex</c>.</param>
/// <param name="GroupId">The favourite group's identifier, or -1 when no group is worn.</param>
/// <param name="GroupStatus">The user's membership status in the worn group as sent by the hotel.</param>
/// <param name="GroupName">The worn group's name; empty when none.</param>
/// <param name="FigureExtra">The extra figure string the hotel attaches, for example a swim outfit.</param>
/// <param name="AchievementScore">The user's total achievement score.</param>
/// <param name="IsModerator">Whether the hotel flags this user as staff.</param>
/// <param name="BadgeCode">Unity only: the worn badge code; empty on Flash.</param>
/// <param name="GroupBadge">Unity only: the worn group badge code; empty on Flash.</param>
/// <param name="GroupPayload">
/// Unity only: the flat group tail, three integers per group in the order the hotel sent
/// them. Empty on Flash.
/// </param>
/// <param name="BadgeRank">Flash only: the number of badges shown next to the avatar; -1 on Unity.</param>
/// <param name="RightsLevel">
/// The controller level taken from the avatar's current status fragment, 0 when it carries
/// none. Same scale as <see cref="AvatarStatusSnapshot.RightsLevel"/>.
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
    /// <summary>Compatibility overload that accepts the group identifier as a plain <see cref="long"/>.</summary>
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

    /// <summary>Compatibility deconstruction that yields the group identifier as a plain <see cref="long"/>.</summary>
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

/// <summary>The fields that only a pet avatar has.</summary>
/// <remarks>
/// The pet's breed variant is not on the room entity; it only comes from a pet info
/// request. <see cref="PetType"/> plus that breed together resolve the displayed breed.
/// </remarks>
/// <param name="Breed">
/// The pet type identifier, serialised as <see cref="PetType"/>. This is what kind of animal
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
    /// <summary>
    /// The pet type identifier, the serialised name of <see cref="Breed"/>. Identifies the
    /// kind of animal, not the breed variant.
    /// </summary>
    public int PetType => Breed;

    /// <summary>Compatibility overload that accepts the owner identifier as a plain <see cref="long"/>.</summary>
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

    /// <summary>Compatibility deconstruction that yields the owner identifier as a plain <see cref="long"/>.</summary>
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

/// <summary>The fields that only a bot avatar has.</summary>
/// <remarks>
/// Public bots carry no owner or skills: the hotel only sends those for private (rentable)
/// bots, so on a public bot the owner is -1, the name empty and the skill list empty.
/// </remarks>
/// <param name="IsPublic">Whether this is a hotel-owned public bot.</param>
/// <param name="IsPrivate">Whether this is a user-owned rentable bot.</param>
/// <param name="Gender">The bot's gender: <c>Male</c>, <c>Female</c> or <c>Unisex</c>.</param>
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
    /// <summary>Compatibility overload that accepts the owner identifier as a plain <see cref="long"/>.</summary>
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

    /// <summary>Compatibility deconstruction that yields the owner identifier as a plain <see cref="long"/>.</summary>
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
/// One entity standing in the room: a user, a pet or a bot. Exactly one of
/// <paramref name="User"/>, <paramref name="Pet"/> and <paramref name="Bot"/> is populated,
/// matching <paramref name="Type"/>.
/// </summary>
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
    /// Compatibility overload that accepts the identifier as a plain <see cref="long"/> and
    /// fixes <see cref="IsRemoved"/> to <see langword="false"/>.
    /// </summary>
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
    /// Compatibility deconstruction that yields the identifier as a plain <see cref="long"/>
    /// and omits <see cref="IsRemoved"/>.
    /// </summary>
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

/// <summary>Every avatar currently tracked in the room, ordered by room index.</summary>
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
    /// <summary>Compatibility overload for callers that have no room context.</summary>
    public AvatarCollectionSnapshot(int Total, IReadOnlyList<AvatarSnapshot> Avatars)
        : this(null, 0, Total, Avatars)
    {
    }

    /// <summary>Compatibility deconstruction that omits the room context.</summary>
    public void Deconstruct(out int Total, out IReadOnlyList<AvatarSnapshot> Avatars)
    {
        Total = this.Total;
        Avatars = this.Avatars;
    }
}

/// <summary>
/// A user's profile card as returned by a profile request, which is more than the room
/// avatar carries.
/// </summary>
/// <param name="Id">The user identifier.</param>
/// <param name="Name">The user name.</param>
/// <param name="Figure">The figure string.</param>
/// <param name="Gender">The gender: <c>Male</c>, <c>Female</c> or <c>Unisex</c>.</param>
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
/// <param name="IsTradeLocked">Whether the account is barred from trading. Sent only by newer hotels; otherwise <see langword="false"/>.</param>
/// <param name="NameColor">The name colour the hotel assigns. Sent only by newer hotels; otherwise empty.</param>
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
    /// <summary>Compatibility overload that accepts the identifier as a plain <see cref="long"/>.</summary>
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

    /// <summary>Compatibility deconstruction that yields the identifier as a plain <see cref="long"/>.</summary>
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

/// <summary>One entry of the messenger friend list.</summary>
/// <remarks>
/// The last five fields only exist on Unity builds; Flash leaves them at their defaults.
/// </remarks>
/// <param name="Id">The friend's user identifier.</param>
/// <param name="Name">The friend's user name.</param>
/// <param name="Figure">The friend's figure string.</param>
/// <param name="Gender">The friend's gender: <c>Male</c>, <c>Female</c> or <c>Unisex</c>.</param>
/// <param name="Motto">The friend's motto. Unity only; empty on Flash.</param>
/// <param name="RealName">The friend's real name, when the hotel discloses it.</param>
/// <param name="IsOnline">Whether the friend is online right now.</param>
/// <param name="CanFollow">Whether the friend may be followed into their room.</param>
/// <param name="CategoryId">The friend-list category this friend is filed under; 0 means uncategorised.</param>
/// <param name="FacebookId">The linked Facebook identifier; empty when none.</param>
/// <param name="IsAcceptingOfflineMessages">Whether offline messages may be sent to this friend. Unity only.</param>
/// <param name="IsVipMember">Whether the friend holds a VIP membership. Unity only.</param>
/// <param name="IsPocketHabboUser">Whether the friend is connected from the mobile client. Unity only.</param>
/// <param name="Relation">The relationship tag: <c>None</c>, <c>Heart</c>, <c>Smile</c> or <c>Skull</c>.</param>
/// <param name="LastOnline">
/// When the friend was last online, as the hotel's own epoch value. Unity only; 0 on Flash.
/// Serialised exactly rather than as a JSON number so no precision is lost.
/// </param>
/// <param name="UnityStatus">Unity only: the raw presence code the hotel sends; 0 on Flash.</param>
/// <param name="UnityPlatform">Unity only: the raw platform code the hotel sends; 0 on Flash.</param>
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
    long LastOnline,
    short UnityStatus,
    short UnityPlatform)
{
    /// <summary>
    /// Compatibility overload carrying only the fields both clients send; every Unity-only
    /// field is left at its default.
    /// </summary>
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
            0,
            0,
            0)
    {
    }

    /// <summary>Compatibility deconstruction that yields only the fields both clients send.</summary>
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

/// <summary>A user-defined friend-list category.</summary>
/// <param name="Id">The category identifier, matched by <see cref="FriendSnapshot.CategoryId"/>.</param>
/// <param name="Name">The category name the user chose.</param>
public sealed record FriendCategorySnapshot(Id Id, string Name);

/// <summary>The messenger friend list with its capacity limits.</summary>
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
    /// <summary>Compatibility overload without categories or capacity limits.</summary>
    public FriendCollectionSnapshot(
        int Total,
        int Online,
        IReadOnlyList<FriendSnapshot> Friends)
        : this(Total, Online, 0, 0, 0, [], Friends)
    {
    }

    /// <summary>Compatibility deconstruction that omits categories and capacity limits.</summary>
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
/// The local user's balances. Each amount is <see langword="null"/> until the hotel has sent
/// the matching packet, which is what the two loaded flags distinguish from a real zero.
/// </summary>
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
    /// <summary>Compatibility overload that reports no activity-point map.</summary>
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

    /// <summary>Compatibility deconstruction that omits the activity-point map.</summary>
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

/// <summary>A user who holds rights in the room.</summary>
/// <param name="Id">The user identifier.</param>
/// <param name="Name">The user name.</param>
public sealed record ControllerSnapshot(Id Id, string Name);

/// <summary>The room's rights list.</summary>
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
/// The catalog definition behind a furni kind, loaded from the hotel's furni data rather
/// than from the room packets. Present on an item snapshot only once definitions are loaded.
/// </summary>
/// <param name="Type">Whether the definition describes a <c>Floor</c> or a <c>Wall</c> item.</param>
/// <param name="Kind">The furni kind identifier, which is what room and inventory packets carry.</param>
/// <param name="Identifier">The furni's class name, the stable textual identity of the kind.</param>
/// <param name="Name">The localised display name.</param>
/// <param name="Width">The footprint along X in tiles at direction 0.</param>
/// <param name="Length">The footprint along Y in tiles at direction 0.</param>
/// <param name="Category">The catalog category string the hotel files this kind under.</param>
/// <param name="Line">The furni line (collection) this kind belongs to.</param>
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
    /// <summary>The furni class name; the same value as <see cref="Identifier"/>.</summary>
    public string ClassName { get; init; } = Identifier;

    /// <summary>The asset revision the client downloads for this kind.</summary>
    public int Revision { get; init; }

    /// <summary>The direction the kind is placed at by default, 0-7 clockwise from north.</summary>
    public int DefaultDirection { get; init; }

    /// <summary>The colourway names this kind offers, empty when it has none.</summary>
    public IReadOnlyList<string> PartColors { get; init; } = [];

    /// <summary>The localised catalog description.</summary>
    public string Description { get; init; } = "";

    /// <summary>The advertisement URL attached to the kind, empty when none.</summary>
    public string AdUrl { get; init; } = "";

    /// <summary>The catalog offer this kind is sold under; 0 when it is not sold.</summary>
    public int OfferId { get; init; }

    /// <summary>Whether the offer may be bought outright.</summary>
    public bool BuyOut { get; init; }

    /// <summary>The catalog offer this kind is rented under; 0 when it is not rentable.</summary>
    public int RentOfferId { get; init; }

    /// <summary>Whether the rental offer may be bought outright.</summary>
    public bool RentBuyOut { get; init; }

    /// <summary>Whether this kind is a Builders Club item.</summary>
    public bool IsBuildersClub { get; init; }

    /// <summary>The Builders Club offer identifier; 0 when there is none.</summary>
    public int BuildersClubOfferId { get; init; }

    /// <summary>Whether the kind is excluded from dynamic catalog listings.</summary>
    public bool ExcludedDynamic { get; init; }

    /// <summary>The free-form parameter string the hotel attaches to the kind, empty when none.</summary>
    public string CustomParams { get; init; } = "";

    /// <summary>
    /// The special behaviour of this kind, as <see cref="FurniCategory"/>. This is the
    /// value that identifies presents, trophies, pet products, seeds and chests.
    /// </summary>
    public FurniCategory SpecialType { get; init; }

    /// <summary>Whether avatars may stand on this kind.</summary>
    public bool CanStandOn { get; init; }

    /// <summary>Whether avatars may sit on this kind.</summary>
    public bool CanSitOn { get; init; }

    /// <summary>Whether avatars may lie on this kind.</summary>
    public bool CanLayOn { get; init; }

    /// <summary>Whether other furni may be stacked on this kind.</summary>
    public bool CanPutStuffOn { get; init; }

    /// <summary>The kind's own height in tile units, which is what stacking adds on top of.</summary>
    public double Height { get; init; }

    /// <summary>The environment tag the hotel gives the kind, empty when none.</summary>
    public string Environment { get; init; } = "";

    /// <summary>Whether the hotel marks this kind as rare.</summary>
    public bool IsRare { get; init; }

    /// <summary>Whether items of this kind may be traded.</summary>
    public bool Tradeable { get; init; }

    /// <summary>Whether items of this kind may be recycled.</summary>
    public bool Recyclable { get; init; }

    /// <summary>Whether the kind renders with an indexed colour rather than its own palette.</summary>
    public bool HasIndexedColor { get; init; }

    /// <summary>The colour index used when <see cref="HasIndexedColor"/> is set.</summary>
    public int ColorIndex { get; init; }

    /// <summary>Whether an avatar can occupy the tile at all, that is stand, sit or lie on it.</summary>
    public bool IsWalkable { get; init; }

    /// <summary>The negation of <see cref="IsWalkable"/>.</summary>
    public bool IsUnwalkable { get; init; }
}

/// <summary>One row of a game furni's high-score table.</summary>
/// <param name="Score">The score achieved.</param>
/// <param name="Names">The names of the users who achieved it, since a score can be shared by a team.</param>
public sealed record HighScoreSnapshot(
    int Score,
    IReadOnlyList<string> Names);

/// <summary>
/// The per-item payload a furni carries. Which of the optional members are populated is
/// decided by <paramref name="Type"/>; the ones that do not apply are omitted from JSON.
/// </summary>
/// <param name="Type">
/// The payload shape: <c>Legacy</c> (0), <c>Map</c> (1), <c>StringArray</c> (2),
/// <c>VoteResult</c> (3), <c>Empty</c> (4), <c>IntArray</c> (5), <c>HighScore</c> (6) or
/// <c>CrackableFurni</c> (7).
/// </param>
/// <param name="Flags">
/// The numeric value of <see cref="ItemDataFlags"/>; bit 0 (value 1) marks a limited rare.
/// </param>
/// <param name="Value">
/// The payload's primary string. For <c>Legacy</c> this is the whole payload; for the other
/// shapes it is the leading string the shape carries.
/// </param>
/// <param name="State">
/// <paramref name="Value"/> interpreted as a furni state: <c>C</c>, <c>FALSE</c> and
/// <c>OFF</c> map to 0, <c>O</c>, <c>TRUE</c> and <c>ON</c> map to 1, any other integer text
/// maps to itself, and anything unparseable maps to -1.
/// </param>
/// <param name="IsLimitedRare">Whether the limited-rare flag is set, which is what makes the three unique fields meaningful.</param>
/// <param name="UniqueSerialNumber">This copy's number within the limited series; 0 when not a limited rare.</param>
/// <param name="UniqueSeriesSize">How many copies the limited series has; 0 when not a limited rare.</param>
/// <param name="UniqueLimitedData">Unity only: the extra limited-edition string; empty on Flash.</param>
/// <param name="MapEntries">The key-value payload; present only for <c>Map</c>.</param>
/// <param name="StringValues">The string list payload; present only for <c>StringArray</c>.</param>
/// <param name="IntValues">The integer list payload; present only for <c>IntArray</c>.</param>
/// <param name="VoteResult">The vote tally; present only for <c>VoteResult</c>.</param>
/// <param name="ScoreType">
/// How the game furni scores, as the hotel's own code; present only for <c>HighScore</c>.
/// </param>
/// <param name="ClearType">
/// When the game furni clears its table, as the hotel's own code; present only for
/// <c>HighScore</c>.
/// </param>
/// <param name="HighScores">The score table; present only for <c>HighScore</c>.</param>
/// <param name="Hits">How many hits the crackable furni has taken; present only for <c>CrackableFurni</c>.</param>
/// <param name="Target">How many hits the crackable furni needs in total; present only for <c>CrackableFurni</c>.</param>
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

/// <summary>A furni standing on the room floor.</summary>
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
/// furni. Serialised exactly rather than as a JSON number so no precision is lost.
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
    /// Compatibility overload that accepts identifiers as plain <see cref="long"/> values and
    /// fixes <see cref="IsRemoved"/> to <see langword="false"/>.
    /// </summary>
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
    /// Compatibility deconstruction that yields identifiers as plain <see cref="long"/>
    /// values and omits <see cref="IsRemoved"/>.
    /// </summary>
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

/// <summary>Where a wall item hangs.</summary>
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

/// <summary>A furni hanging on a room wall.</summary>
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
    /// Compatibility overload that accepts identifiers as plain <see cref="long"/> values and
    /// fixes <see cref="IsRemoved"/> to <see langword="false"/>.
    /// </summary>
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
    /// Compatibility deconstruction that yields identifiers as plain <see cref="long"/>
    /// values and omits <see cref="IsRemoved"/>.
    /// </summary>
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
/// The furni in the current room, floor and wall items projected separately and each capped
/// on its own.
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
/// <param name="FloorItemsTruncated">Whether floor items were dropped to honour the cap.</param>
/// <param name="WallItemsTruncated">Whether wall items were dropped to honour the cap.</param>
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
    /// <summary>Compatibility overload without room context or definition state.</summary>
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

    /// <summary>Compatibility deconstruction that omits room context and definition state.</summary>
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

/// <summary>One item in the local user's hand (inventory).</summary>
/// <param name="ItemId">
/// The inventory slot identifier. This is what inventory and placement packets address, and
/// it is the value the collection is ordered and paged by.
/// </param>
/// <param name="Type">Whether the item is a <c>Floor</c> or a <c>Wall</c> item.</param>
/// <param name="Id">
/// The item's own identifier, which is the identifier it takes once placed in a room. Often
/// equal to <paramref name="ItemId"/> but not guaranteed to be.
/// </param>
/// <param name="Kind">The furni kind identifier.</param>
/// <param name="Definition">The catalog definition, or <see langword="null"/> when definitions are not loaded.</param>
/// <param name="Category">
/// The furni's special category, matching <see cref="FurniCategory"/>: 1 default,
/// 9 present, 11 trophy, 19 monsterplant seed and so on.
/// </param>
/// <param name="Data">The item's payload, which is where limited-rare and game data live.</param>
/// <param name="IsRecyclable">Whether the item may be recycled.</param>
/// <param name="IsTradeable">Whether the item may be traded.</param>
/// <param name="IsGroupable">Whether the client stacks this item with identical ones in the hand view.</param>
/// <param name="IsSellable">Whether the item may be listed on the marketplace.</param>
/// <param name="SecondsToExpiration">Seconds until a rented item expires; -1 when it does not expire.</param>
/// <param name="HasRentPeriodStarted">Whether the rental clock has already started running.</param>
/// <param name="RoomId">The room a rented item is bound to; 0 when it is not bound.</param>
/// <param name="IsUnseen">Unity only: whether the item is still marked as new; <see langword="false"/> on Flash.</param>
/// <param name="Timestamp">
/// Unity only: when the item entered the hand, as the hotel's own epoch value; 0 on Flash.
/// Serialised exactly rather than as a JSON number so no precision is lost.
/// </param>
/// <param name="IsNft">Unity only, and only on builds that send the extended tail: whether the item is an NFT.</param>
/// <param name="NftName">The NFT name, present only when <paramref name="IsNft"/> is set.</param>
/// <param name="IsExternalImage">Unity only: whether the item renders an externally hosted image.</param>
/// <param name="SlotId">
/// The hotel's grouping slot for identical floor items; empty for wall items, which do not
/// carry it.
/// </param>
/// <param name="Extra">
/// The extra identifier attached to floor items; 0 for wall items. Serialised exactly rather
/// than as a JSON number so no precision is lost.
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
    bool IsUnseen,
    [property: JsonConverter(typeof(ExactInt64JsonConverter))]
    long Timestamp,
    bool IsNft,
    string NftName,
    bool IsExternalImage,
    string SlotId,
    [property: JsonConverter(typeof(ExactInt64JsonConverter))]
    long Extra);

/// <summary>
/// The local user's hand, together with the fragmented-load bookkeeping that says whether it
/// can be trusted yet.
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
/// <param name="Truncated">Whether items were dropped to honour the cap.</param>
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

/// <summary>One tile of the live heightmap, which is what walkability must be judged from.</summary>
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
/// The live heightmap of the current room, with per-tile detail and aggregate counts.
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
/// <param name="Truncated">Whether tiles were dropped to honour the cap.</param>
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
/// The detailed statistics of one pet, as returned by a pet info request.
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
    /// What kind of animal this is, supplied from the room entity because the pet info
    /// message does not carry it. <see langword="null"/>, and omitted from JSON, when the
    /// pet is not in the room. Together with <see cref="BreedId"/> this resolves the
    /// displayed breed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PetType { get; init; }

    /// <summary>Compatibility overload that accepts identifiers as plain <see cref="long"/> values.</summary>
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

    /// <summary>Compatibility deconstruction that yields identifiers as plain <see cref="long"/> values.</summary>
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
