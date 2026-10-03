using Qx.Model;
using Qx.Protocol;
using Qx.Game.Application;

namespace Qx.Scripting;

/// <content>
/// Small one-shot interactions with users and room items: respecting, unbanning, walking through a
/// one-way gate, and editing or removing wall items and sticky notes.
/// <para>
/// Every method here is fire-and-forget. It composes one outgoing message and returns; nothing is
/// awaited and no result is reported.
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
    /// Gives a respect to a user.
    /// </summary>
    /// <remarks>
    /// The hotel limits how many respects a user can give per day and silently ignores the rest.
    /// </remarks>
    /// <param name="userId">The target user's account id, not their room index.</param>
    public void RespectUser(Id userId) =>
        _application.Invoke<RoomUserRespectRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPeopleRespect,
            new RoomUserRespectRequest(userId),
            Ct);

    /// <summary>Gives a respect to a user in the room.</summary>
    /// <param name="user">The target user; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    public void RespectUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        RespectUser(user.Id);
    }

    /// <summary>Lifts a user's ban from a room.</summary>
    /// <param name="userId">The banned user's account id.</param>
    /// <param name="roomId">The room to unban them from, or the current room when omitted.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room id was given and the local user is not in a ready room.
    /// </exception>
    public void UnbanUser(Id userId, Id? roomId = null)
    {
        RoomModerationStateView state = _application.Invoke<
            RoomModerationStateRequest,
            RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationState,
                new RoomModerationStateRequest(),
                Ct);
        Id target_room_id = roomId ?? CurrentRoomIdForUnban(state);
        bool current_room = state.RoomReady && state.RoomId == target_room_id;
        _application.Invoke<RoomModerationUnbanRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationUnban,
            new RoomModerationUnbanRequest(
                userId,
                target_room_id,
                state.SessionGeneration,
                current_room ? state.RoomGeneration : null,
                current_room && state.Loaded ? state.BanList.SnapshotRevision : null),
            Ct);
    }

    /// <summary>Lifts a user's ban from a room.</summary>
    /// <param name="user">The banned user; only its id is used.</param>
    /// <param name="roomId">The room to unban them from, or the current room when omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room id was given and the local user is not in a room.
    /// </exception>
    public void UnbanUser(User user, Id? roomId = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        UnbanUser(user.Id, roomId);
    }

    /// <summary>
    /// Clicks a piece of furni the way the game client does when it is clicked in the room.
    /// </summary>
    /// <remarks>
    /// The furni is not used; the server answers with whatever a click means for it, such as
    /// walking up to a teleporter. The call is fire and forget.
    /// </remarks>
    /// <param name="item">The furni to click, floor or wall.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void ClickFurni(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is WallItem)
            ClickWallItem(item.Id);
        else
            ClickFloorItem(item.Id);
    }

    /// <summary>Clicks a floor item by id, as <see cref="ClickFurni(Furni)"/> does.</summary>
    /// <param name="itemId">The floor item id.</param>
    public void ClickFloorItem(Id itemId) =>
        _application.Invoke<RoomFloorItemClickRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemFloorClick,
            new RoomFloorItemClickRequest(itemId),
            Ct);

    /// <summary>Clicks a wall item by id, as <see cref="ClickFurni(Furni)"/> does.</summary>
    /// <param name="itemId">The wall item id.</param>
    public void ClickWallItem(Id itemId) =>
        _application.Invoke<RoomWallItemClickRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemWallClick,
            new RoomWallItemClickRequest(itemId),
            Ct);

    /// <summary>
    /// Steps through a one-way gate.
    /// </summary>
    /// <remarks>
    /// Walking onto the tile is not enough. The client sends this separate message, and the
    /// server then moves the avatar through.
    /// </remarks>
    /// <param name="itemId">The floor item id of the gate.</param>
    public void EnterOneWayDoor(Id itemId) =>
        _application.Invoke<RoomOneWayDoorEnterRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemOneWayDoorEnter,
            new RoomOneWayDoorEnterRequest(itemId),
            Ct);

    /// <summary>Steps through a one-way gate.</summary>
    /// <param name="item">The gate; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void EnterOneWayDoor(FloorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnterOneWayDoor(item.Id);
    }

    /// <summary>Rewrites a sticky note's color and text.</summary>
    /// <param name="itemId">The wall item id of the sticky note.</param>
    /// <param name="color">The note's background color.</param>
    /// <param name="text">The note's text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="color"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetStickyData(Id itemId, string color, string text) =>
    _application.Invoke<RoomStickySetRequest, RoomItemDispatchResult>(
        ApplicationMemberIds.RoomItemStickySet,
        new RoomStickySetRequest(itemId, color, text),
        Ct);

    /// <summary>Rewrites a sticky note's color and text.</summary>
    /// <param name="item">The wall item holding the note; only its id is used.</param>
    /// <param name="color">The note's background color.</param>
    /// <param name="text">The note's text.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    public void SetStickyData(WallItem item, string color, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        SetStickyData(item.Id, color, text);
    }

    /// <summary>
    /// Writes a sticky note back after editing it, taking the id, color and text from the note
    /// itself.
    /// </summary>
    /// <param name="sticky">The note to save.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sticky"/> is <see langword="null"/>.</exception>
    public void SetStickyData(Sticky sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        SetStickyData(sticky.Id, sticky.Color, sticky.Text);
    }

    /// <summary>
    /// Deletes a wall item from the room outright.
    /// </summary>
    /// <remarks>
    /// This destroys the item rather than returning it to the inventory, which is what the client
    /// does for sticky notes and photos. <see cref="PickupFurni(WallItem, bool)"/> returns an item
    /// to the inventory instead.
    /// </remarks>
    /// <param name="itemId">The wall item id.</param>
    public void DeleteWallItem(Id itemId) =>
        _application.Invoke<RoomWallItemRemoveRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemWallRemove,
            new RoomWallItemRemoveRequest(itemId),
            Ct);

    /// <summary>Deletes a wall item from the room outright.</summary>
    /// <param name="item">The wall item; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void DeleteWallItem(WallItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        DeleteWallItem(item.Id);
    }

    /// <summary>Deletes a sticky note from the room outright.</summary>
    /// <param name="sticky">The note; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sticky"/> is <see langword="null"/>.</exception>
    public void DeleteWallItem(Sticky sticky)
    {
        ArgumentNullException.ThrowIfNull(sticky);
        DeleteWallItem(sticky.Id);
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
