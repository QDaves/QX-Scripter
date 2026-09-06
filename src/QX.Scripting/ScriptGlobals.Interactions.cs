using Qx.Model;
using Qx.Protocol;
using Qx.Game.Application;

namespace Qx.Scripting;

/// <content>
/// Small one-shot interactions with users and room items: respecting, unbanning, walking through a
/// one-way gate, and editing or removing wall items and sticky notes.
/// <para>
/// Every method here is fire-and-forget. It composes one outgoing message and returns; nothing is
/// awaited and no result is reported. Where a message differs by client, the right name and layout
/// are chosen from the active session automatically.
/// </para>
/// <para>
/// Most methods come in two shapes: one taking an id, and one taking the model object it was read
/// from. The model overloads only unwrap the id, so they behave identically apart from rejecting
/// <see langword="null"/>.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Gives a respect to a user. The hotel limits how many respects a user can give per day and
    /// silently ignores the rest.
    /// </summary>
    /// <param name="user_id">The target user's account id, not their room index.</param>
    public void RespectUser(Id user_id) =>
        Application.Invoke<RoomUserRespectRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPeopleRespect,
            new RoomUserRespectRequest(user_id),
            Ct);

    /// <summary>Gives a respect to a user in the room.</summary>
    /// <param name="user">The target user; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is null.</exception>
    public void RespectUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        RespectUser(user.Id);
    }

    /// <summary>
    /// Lifts a user's ban from a room. Requires ownership of that room; the server ignores the
    /// message otherwise.
    /// </summary>
    /// <param name="user_id">The banned user's account id.</param>
    /// <param name="room_id">
    /// The room to unban them from. When omitted, the room the local user is currently in is used.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// No room id was given and the local user is not in a room, so there is nothing to default to.
    /// </exception>
    /// <remarks>
    /// The message name differs by client — <c>RoomUnbanUser</c> on Unity,
    /// <c>UnbanUserFromRoom</c> on Flash — and is selected automatically.
    /// </remarks>
    public void UnbanUser(Id user_id, Id? room_id = null)
    {
        RoomModerationStateView state = Application.Invoke<
            RoomModerationStateRequest,
            RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationState,
                new RoomModerationStateRequest(),
                Ct);
        Id target_room_id = room_id ?? CurrentRoomIdForUnban(state);
        bool current_room = state.RoomReady && state.RoomId == target_room_id;
        Application.Invoke<RoomModerationUnbanRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationUnban,
            new RoomModerationUnbanRequest(
                user_id,
                target_room_id,
                state.SessionGeneration,
                current_room ? state.RoomGeneration : null,
                current_room && state.Loaded ? state.BanList.SnapshotRevision : null),
            Ct);
    }

    /// <summary>Lifts a user's ban from a room.</summary>
    /// <param name="user">The banned user; only its id is used.</param>
    /// <param name="room_id">The room to unban them from, or the current room when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No room id was given and the local user is not in a room.
    /// </exception>
    public void UnbanUser(User user, Id? room_id = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        UnbanUser(user.Id, room_id);
    }

    /// <summary>
    /// Lifts a user's ban from a room. Alternative name for the same call, matching the Unity
    /// message name.
    /// </summary>
    /// <param name="user_id">The banned user's account id.</param>
    /// <param name="room_id">The room to unban them from, or the current room when omitted.</param>
    /// <exception cref="InvalidOperationException">
    /// No room id was given and the local user is not in a room.
    /// </exception>
    public void RoomUnbanUser(Id user_id, Id? room_id = null) =>
        UnbanUser(user_id, room_id);

    /// <summary>Lifts a user's ban from a room. Alternative name for the same call.</summary>
    /// <param name="user">The banned user; only its id is used.</param>
    /// <param name="room_id">The room to unban them from, or the current room when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No room id was given and the local user is not in a room.
    /// </exception>
    public void RoomUnbanUser(User user, Id? room_id = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        UnbanUser(user.Id, room_id);
    }

    /// <summary>
    /// Steps through a one-way gate. Walking onto the tile is not enough — the client sends this
    /// separate message, and the server then moves the avatar through.
    /// </summary>
    /// <param name="item_id">The floor item id of the gate.</param>
    public void EnterOneWayDoor(Id item_id) =>
        Application.Invoke<RoomOneWayDoorEnterRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemOneWayDoorEnter,
            new RoomOneWayDoorEnterRequest(item_id),
            Ct);

    /// <summary>Steps through a one-way gate.</summary>
    /// <param name="item">The gate; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void EnterOneWayDoor(FloorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnterOneWayDoor(item.Id);
    }

    /// <summary>Steps through a one-way gate. Alternative name for the same call.</summary>
    /// <param name="item_id">The floor item id of the gate.</param>
    public void UseGate(Id item_id) =>
        EnterOneWayDoor(item_id);

    /// <summary>Steps through a one-way gate. Alternative name for the same call.</summary>
    /// <param name="item">The gate; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void UseGate(FloorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnterOneWayDoor(item.Id);
    }

    /// <summary>
    /// Rewrites a sticky note's colour and text. The message replaces both values, so pass the
    /// current colour when only the text should change.
    /// </summary>
    /// <param name="item_id">The wall item id of the sticky note.</param>
    /// <param name="color">
    /// The note's background colour as the hotel encodes it, for example <c>FFFF33</c>. It is
    /// encoded independently from the text.
    /// </param>
    /// <param name="text">The note's text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> or <paramref name="text"/> is null.</exception>
    /// <remarks>
    /// The message name differs by client — <c>SetStickyData</c> on Unity, <c>SetItemData</c> on
    /// Flash — and is selected automatically.
    /// </remarks>
    public void SetStickyData(Id item_id, string color, string text) =>
        Application.Invoke<RoomStickySetRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemStickySet,
            new RoomStickySetRequest(item_id, color, text),
            Ct);

    /// <summary>Rewrites a sticky note's colour and text.</summary>
    /// <param name="item">The wall item holding the note; only its id is used.</param>
    /// <param name="color">The note's background colour.</param>
    /// <param name="text">The note's text.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public void SetStickyData(WallItem item, string color, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        SetStickyData(item.Id, color, text);
    }

    /// <summary>
    /// Writes a sticky note back after editing it, taking the id, colour and text from the note
    /// itself.
    /// </summary>
    /// <param name="sticky">The note to save.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sticky"/> is null.</exception>
    public void SetStickyData(Sticky sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        SetStickyData(sticky.Id, sticky.Color, sticky.Text);
    }

    /// <summary>Rewrites a sticky note's colour and text. Alternative name for the same call.</summary>
    /// <param name="item_id">The wall item id of the sticky note.</param>
    /// <param name="color">The note's background colour.</param>
    /// <param name="text">The note's text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> or <paramref name="text"/> is null.</exception>
    public void UpdateSticky(Id item_id, string color, string text) =>
        SetStickyData(item_id, color, text);

    /// <summary>Rewrites a sticky note's colour and text. Alternative name for the same call.</summary>
    /// <param name="item">The wall item holding the note; only its id is used.</param>
    /// <param name="color">The note's background colour.</param>
    /// <param name="text">The note's text.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public void UpdateSticky(WallItem item, string color, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        SetStickyData(item.Id, color, text);
    }

    /// <summary>Writes a sticky note back after editing it. Alternative name for the same call.</summary>
    /// <param name="sticky">The note to save.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sticky"/> is null.</exception>
    public void UpdateSticky(Sticky sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        SetStickyData(sticky.Id, sticky.Color, sticky.Text);
    }

    /// <summary>
    /// Deletes a wall item from the room outright. This destroys the item rather than returning it
    /// to the inventory, which is what the client does for sticky notes and photos.
    /// </summary>
    /// <param name="item_id">The wall item id.</param>
    public void RemoveItem(Id item_id) =>
        Application.Invoke<RoomWallItemRemoveRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemWallRemove,
            new RoomWallItemRemoveRequest(item_id),
            Ct);

    /// <summary>Deletes a wall item from the room outright.</summary>
    /// <param name="item">The wall item; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void RemoveItem(WallItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        RemoveItem(item.Id);
    }

    /// <summary>Deletes a sticky note from the room outright.</summary>
    /// <param name="sticky">The note; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sticky"/> is null.</exception>
    public void RemoveItem(Sticky sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        RemoveItem(sticky.Id);
    }

    /// <summary>Deletes a wall item from the room outright. Alternative name for the same call.</summary>
    /// <param name="item_id">The wall item id.</param>
    public void DeleteWallItem(Id item_id) =>
        RemoveItem(item_id);

    /// <summary>Deletes a wall item from the room outright. Alternative name for the same call.</summary>
    /// <param name="item">The wall item; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void DeleteWallItem(WallItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        RemoveItem(item.Id);
    }

    /// <summary>Deletes a sticky note from the room outright. Alternative name for the same call.</summary>
    /// <param name="item_id">The wall item id of the note.</param>
    public void DeleteSticky(Id item_id) =>
        RemoveItem(item_id);

    /// <summary>Deletes a sticky note from the room outright. Alternative name for the same call.</summary>
    /// <param name="sticky">The note; only its id is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sticky"/> is null.</exception>
    public void DeleteSticky(Sticky sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        RemoveItem(sticky.Id);
    }

    private static Id CurrentRoomIdForUnban(RoomModerationStateView state)
    {
        if (!state.RoomReady || state.RoomId <= 0)
        {
            throw new InvalidOperationException(
                "A room ID is required to unban a user while outside a room.");
        }
        return state.RoomId;
    }
}
