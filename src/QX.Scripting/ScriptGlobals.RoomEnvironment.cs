using Qx.Game;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// The current room's decoration, layout, chat rules and the local user's authority in it.
/// <para>
/// Everything here is read from the room tracker and reflects what the server pushed while
/// entering and staying in the room. Nothing is requested: a member stays <see langword="null"/>
/// until the packet that carries it has arrived, and every value is reset on leaving the room.
/// </para>
/// <para>
/// The plain properties read the live tracker; the snapshot properties take a consistent copy
/// under the tracker's lock and are the safe choice when several related values have to agree.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// The extra room flags the server sends alongside the room data: forward-on-enter, staff
    /// pick, group membership, room mute, the moderation permission levels, whether the local user
    /// may mute, and the chat settings. <see langword="null"/> until the room result has arrived.
    /// </summary>
    public RoomResultDetails? RoomDetails => Room.Details;

    /// <summary>
    /// The room's door tile: where avatars appear on entering and which way they face.
    /// <see langword="null"/> before the heightmap arrives.
    /// </summary>
    public RoomEntryTile? RoomEntryTile => Room.EntryTile;

    /// <summary>
    /// Every room property keyed exactly as the hotel sends it, for example <c>floor</c>,
    /// <c>wallpaper</c>, <c>landscape</c> and <c>landscapeanim</c>.
    /// </summary>
    /// <returns>A snapshot copy taken under the room lock, not a live view.</returns>
    public IReadOnlyDictionary<string, string> RoomProperties => Room.Properties;

    /// <summary>
    /// The <c>floor</c> property — the floor pattern identifier — or <see langword="null"/> when
    /// the room has not set one.
    /// </summary>
    public string? RoomFloor => Room.FloorProperty;

    /// <summary>
    /// The <c>wallpaper</c> property — the wall pattern identifier — or <see langword="null"/> when
    /// the room has not set one.
    /// </summary>
    public string? RoomWallpaper => Room.WallpaperProperty;

    /// <summary>
    /// The <c>landscape</c> property — the window backdrop identifier — or <see langword="null"/>
    /// when the room has not set one.
    /// </summary>
    public string? RoomLandscape => Room.LandscapeProperty;

    /// <summary>
    /// The <c>landscapeanim</c> property — the animated backdrop identifier — or
    /// <see langword="null"/> when the room has not set one.
    /// </summary>
    public string? RoomAnimatedLandscape => Room.AnimatedLandscapeProperty;

    /// <summary>
    /// Whether the walls are hidden and how thick the walls and floor are drawn.
    /// <see langword="null"/> before the visualization settings arrive.
    /// </summary>
    public RoomVisualizationSettings? RoomVisualization => Room.VisualizationSettings;

    /// <summary>
    /// The room's chat rules: bubble flow mode, bubble width, scroll speed, hearing distance in
    /// tiles and flood-filter strength. <see langword="null"/> before the room result arrives.
    /// </summary>
    /// <remarks>
    /// One Flash wire layout carries only the flood-filter setting; on such a build the other
    /// fields hold their defaults rather than the room's real values.
    /// </remarks>
    public RoomChatSettings? RoomChatSettings => Room.ChatSettings;

    /// <summary>
    /// The controller level the server granted the local user in this room, or
    /// <see langword="null"/> while it is still unknown. The client's scale is 0 not a controller,
    /// 1 room controller (rights), 2 group member, 3 group admin, 4 room owner, 5 moderator.
    /// </summary>
    public int? RoomRightsLevel => Room.RightsLevel;

    /// <summary>
    /// Whether the rights question is settled — either the local user is known to own the room or
    /// a controller level has arrived. While this is <see langword="false"/>, a rights check of
    /// <see langword="false"/> only means "not confirmed yet".
    /// </summary>
    public bool RoomRightsAreKnown => Room.RightsAreKnown;

    /// <summary>
    /// Whether the local user owns the room or holds a controller level above 0. This is the check
    /// to make before attempting anything that needs rights.
    /// </summary>
    public bool HasRoomRights => Room.HasRights;

    /// <summary>
    /// Whether the local user entered as a spectator, or <see langword="null"/> while unknown.
    /// Spectators cannot act in the room.
    /// </summary>
    public bool? IsRoomSpectating => Room.IsSpectating;

    /// <summary>
    /// Who may mute, kick and ban in this room, as the three permission levels the server sent.
    /// <see langword="null"/> before the room result arrives.
    /// </summary>
    public RoomModerationSettings? RoomModeration => Room.Details?.Moderation;

    /// <summary>
    /// Whether the local user may mute others in this room, or <see langword="null"/> while the
    /// room details have not been loaded — which is deliberately different from a loaded
    /// <see langword="false"/>.
    /// </summary>
    public bool? CanMuteInRoom => Room.DetailsAreLoaded ? Room.Details?.CanMute : null;

    /// <summary>
    /// The room's decoration and layout captured in one consistent snapshot: door tile, every room
    /// property, the four well-known decoration properties, the visualization settings and the chat
    /// settings.
    /// </summary>
    /// <returns>An immutable copy taken under the room lock.</returns>
    public RoomEnvironmentSnapshot RoomEnvironment =>
        Room.Capture(SnapshotFactory.RoomEnvironment);

    /// <summary>
    /// What the local user is permitted to do in this room, captured in one consistent snapshot:
    /// ownership, controller level, whether rights are known, the effective rights flag, spectator
    /// status, room mute state, mute permission and the moderation levels.
    /// </summary>
    /// <returns>An immutable copy taken under the room lock.</returns>
    public RoomAuthoritySnapshot RoomAuthority =>
        Room.Capture(SnapshotFactory.RoomAuthority);

    /// <summary>
    /// The room detail flags as an immutable snapshot, or <see langword="null"/> when the room
    /// result has not arrived.
    /// </summary>
    /// <returns>An immutable copy taken under the room lock.</returns>
    public RoomResultDetailsSnapshot? RoomDetailsSnapshot =>
        Room.Capture(current => current.Details is { } details
            ? SnapshotFactory.From(details)
            : null);

    /// <summary>
    /// Raised when the room's detail flags arrive or change, which happens on entering a room and
    /// whenever the server re-sends the room result.
    /// </summary>
    /// <param name="handler">Receives the details.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomDetailsUpdated(Action<RoomResultDetails> handler)
        => Subscribe(
            handler,
            value => Room.DetailsUpdated += value,
            value => Room.DetailsUpdated -= value);

    /// <summary>Raised when the room's door tile is set or moved.</summary>
    /// <param name="handler">Receives the door tile position and direction.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomEntryTileUpdated(Action<RoomEntryTile> handler)
        => Subscribe(
            handler,
            value => Room.EntryTileUpdated += value,
            value => Room.EntryTileUpdated -= value);

    /// <summary>
    /// Raised for each room property the server sets or changes — floor, wallpaper, landscape and
    /// anything else the hotel keys into the property map.
    /// </summary>
    /// <param name="handler">Receives the property key and its new value.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomPropertyUpdated(Action<FlatProperty> handler)
        => Subscribe(
            handler,
            value => Room.PropertyUpdated += value,
            value => Room.PropertyUpdated -= value);

    /// <summary>Raised when the wall-hiding or wall/floor thickness settings change.</summary>
    /// <param name="handler">Receives the new visualization settings.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomVisualizationUpdated(Action<RoomVisualizationSettings> handler)
        => Subscribe(
            handler,
            value => Room.VisualizationSettingsUpdated += value,
            value => Room.VisualizationSettingsUpdated -= value);

    /// <summary>Raised when the room's chat rules change.</summary>
    /// <param name="handler">Receives the new chat settings.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomChatSettingsUpdated(Action<RoomChatSettings> handler)
        => Subscribe(
            handler,
            value => Room.ChatSettingsUpdated += value,
            value => Room.ChatSettingsUpdated -= value);

    /// <summary>
    /// Raised whenever anything about the local user's authority in the room changes — ownership,
    /// controller level or spectator status — carrying the whole new authority state rather than
    /// just the field that moved.
    /// </summary>
    /// <param name="handler">Receives the new authority state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomAuthorityChanged(Action<RoomAuthorityState> handler)
        => Subscribe(
            handler,
            value => Room.AuthorityChanged += value,
            value => Room.AuthorityChanged -= value);

    /// <summary>
    /// Raised when the local user's controller level changes, including the first time it becomes
    /// known.
    /// </summary>
    /// <param name="handler">
    /// Receives the previous level then the new one; either may be <see langword="null"/> for
    /// "unknown". The scale is 0 not a controller, 1 rights, 2 group member, 3 group admin,
    /// 4 owner, 5 moderator.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomRightsLevelChanged(Action<int?, int?> handler)
        => Subscribe(
            handler,
            value => Room.RightsLevelChanged += value,
            value => Room.RightsLevelChanged -= value);

    /// <summary>Raised when the local user's spectator status changes.</summary>
    /// <param name="handler">
    /// Receives the previous value then the new one; either may be <see langword="null"/> for
    /// "unknown".
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomSpectatingChanged(Action<bool?, bool?> handler)
        => Subscribe(
            handler,
            value => Room.SpectatingChanged += value,
            value => Room.SpectatingChanged -= value);
}
