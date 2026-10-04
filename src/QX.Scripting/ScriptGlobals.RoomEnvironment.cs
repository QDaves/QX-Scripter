using Qx.Game;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// The current room's decoration, layout, chat rules and the local user's authority in it.
/// <para>
/// Everything here is read from the room tracker and reflects what the server pushed while
/// entering and staying in the room. Nothing is requested: a value stays <see langword="null"/>
/// until the packet that carries it has arrived, and every value is reset on leaving the room.
/// </para>
/// <para>
/// Each property takes a consistent copy under the tracker's lock, so related values always
/// agree. The single live values are on <see cref="Room"/>, for example
/// <see cref="RoomManager.EntryTile"/> or <see cref="RoomManager.ChatSettings"/>.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the room's decoration, layout and chat rules captured in one consistent value.
    /// </summary>
    /// <remarks>
    /// The value holds the door tile, every room property, the four well known decoration
    /// properties, the visualization settings and the chat settings. Every read returns a new
    /// detached copy taken under the room lock.
    /// </remarks>
    public RoomEnvironmentState RoomEnvironment =>
        Room.Capture(room => room.Environment);

    /// <summary>
    /// Gets what the local user is permitted to do in this room, captured in one consistent value.
    /// </summary>
    /// <remarks>
    /// The value holds ownership, controller level, whether rights are known, the effective
    /// rights flag, spectator status, room mute state, whether the user may mute others and the
    /// moderation levels. It is the same type <see cref="RoomManager.Authority"/> returns and
    /// <see cref="OnRoomAuthorityChanged"/> passes, read under the room lock.
    /// </remarks>
    public RoomAuthorityState RoomAuthority =>
        Room.Capture(room => room.Authority);

    /// <summary>
    /// Gets a detached copy of the room detail flags, or <see langword="null"/> when the room
    /// result has not arrived.
    /// </summary>
    /// <remarks>
    /// Every read returns a new copy taken under the room lock. Unlike
    /// <see cref="RoomManager.Details"/>, which is the instance the room tracker keeps, the copy is
    /// the same type but stays as it was read, and changing it changes nothing in the room.
    /// </remarks>
    public RoomResultDetails? RoomDetailsSnapshot =>
        Room.Capture(room => room.Details is { } details
            ? RoomObjectSnapshot.Copy(details)
            : null);

    /// <summary>
    /// Registers a handler that runs when the room's detail flags arrive or change.
    /// </summary>
    /// <remarks>
    /// This happens on entering a room and whenever the server sends the room result again.
    /// </remarks>
    /// <param name="handler">The handler to call with the details.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomDetailsUpdated(Action<RoomResultDetails> handler)
        => Subscribe(
            handler,
            value => Room.DetailsUpdated += value,
            value => Room.DetailsUpdated -= value);

    /// <summary>Registers a handler that runs when the room's door tile is set or moved.</summary>
    /// <param name="handler">The handler to call with the door tile position and direction.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomEntryTileUpdated(Action<RoomEntryTile> handler)
        => Subscribe(
            handler,
            value => Room.EntryTileUpdated += value,
            value => Room.EntryTileUpdated -= value);

    /// <summary>
    /// Registers a handler that runs for each room property the server sets or changes.
    /// </summary>
    /// <remarks>
    /// This covers floor, wallpaper, landscape and anything else the hotel keys into the property map.
    /// </remarks>
    /// <param name="handler">The handler to call with the property key and its new value.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomPropertyUpdated(Action<FlatProperty> handler)
        => Subscribe(
            handler,
            value => Room.PropertyUpdated += value,
            value => Room.PropertyUpdated -= value);

    /// <summary>Registers a handler that runs when the wall hiding or the wall and floor thickness settings change.</summary>
    /// <param name="handler">The handler to call with the new visualization settings.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomVisualizationUpdated(Action<RoomVisualizationSettings> handler)
        => Subscribe(
            handler,
            value => Room.VisualizationSettingsUpdated += value,
            value => Room.VisualizationSettingsUpdated -= value);

    /// <summary>Registers a handler that runs when the room's chat rules arrive or change.</summary>
    /// <param name="handler">The handler to call with the new chat settings.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomChatSettingsUpdated(Action<RoomChatSettings> handler)
        => Subscribe(
            handler,
            value => Room.ChatSettingsUpdated += value,
            value => Room.ChatSettingsUpdated -= value);

    /// <summary>
    /// Registers a handler that runs whenever a member of <see cref="RoomAuthority"/> changes in the
    /// current room: the local user's ownership, controller level, spectator status or whether the
    /// user may mute others, the room's mute state or its moderation levels.
    /// </summary>
    /// <remarks>
    /// Mute and moderation changes arrive with the room result, which the server also sends again
    /// while the user stays in the room. The room result applied as the room is entered and the reset
    /// on leaving do not raise the event, so read <see cref="RoomAuthority"/> once the room is entered
    /// to get the starting value.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the whole new authority state rather than just the field that moved.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomAuthorityChanged(Action<RoomAuthorityState> handler)
        => Subscribe(
            handler,
            value => Room.AuthorityChanged += value,
            value => Room.AuthorityChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the local user's controller level changes, including the
    /// first time it becomes known.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the previous level and then the new one; either may be
    /// <see langword="null"/> for "unknown". The scale is 0 not a controller, 1 rights, 2 group
    /// member, 3 group admin, 4 owner, 5 moderator.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomRightsLevelChanged(Action<int?, int?> handler)
        => Subscribe(
            handler,
            value => Room.RightsLevelChanged += value,
            value => Room.RightsLevelChanged -= value);

    /// <summary>Registers a handler that runs when the local user's spectator status changes.</summary>
    /// <param name="handler">
    /// The handler to call with the previous value and then the new one; either may be
    /// <see langword="null"/> for "unknown".
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomSpectatingChanged(Action<bool?, bool?> handler)
        => Subscribe(
            handler,
            value => Room.SpectatingChanged += value,
            value => Room.SpectatingChanged -= value);
}
