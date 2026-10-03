using Qx.Game;
using Qx.Model;

namespace Qx.Scripting;

/// <content>
/// Names kept for one release after their capability got one canonical name. Each member forwards
/// to its replacement, and the compiler names that replacement in the obsolete warning.
/// </content>
public partial class ScriptGlobals
{
    /// <summary>Gets the local user's avatar in the current room.</summary>
    /// <remarks>Replaced by <see cref="SelfAvatar"/>.</remarks>
    [Obsolete("Use SelfAvatar.")]
    public User? Me => SelfAvatar;

    /// <summary>Gets the local user's account data.</summary>
    /// <remarks>Replaced by <see cref="SelfProfile"/>.</remarks>
    [Obsolete("Use SelfProfile.")]
    public UserData? Self => SelfProfile;

    /// <summary>Says a message in the room.</summary>
    /// <remarks>Replaced by <see cref="Talk"/>.</remarks>
    /// <param name="message">The text to say.</param>
    /// <param name="bubble">The chat bubble style id; 0 is the account's default bubble.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or no ready room.</exception>
    [Obsolete("Use Talk.")]
    public void Say(string message, int bubble = 0) => Talk(message, bubble);

    /// <summary>Writes a line to the script output.</summary>
    /// <remarks>Replaced by <see cref="Log(object)"/>.</remarks>
    /// <param name="message">The value to write; <see langword="null"/> writes an empty line.</param>
    [Obsolete("Use Log.")]
    public void Status(object? message) => Log(message);

    /// <summary>Steps through a one-way gate.</summary>
    /// <remarks>Replaced by <see cref="EnterOneWayDoor(Id)"/>.</remarks>
    /// <param name="itemId">The floor item id of the gate.</param>
    [Obsolete("Use EnterOneWayDoor.")]
    public void UseGate(Id itemId) => EnterOneWayDoor(itemId);

    /// <summary>Steps through a one-way gate.</summary>
    /// <remarks>Replaced by <see cref="EnterOneWayDoor(FloorItem)"/>.</remarks>
    /// <param name="item">The gate; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    [Obsolete("Use EnterOneWayDoor.")]
    public void UseGate(FloorItem item) => EnterOneWayDoor(item);

    /// <summary>Gets the hotel's furniture definitions.</summary>
    /// <remarks>
    /// Replaced by <see cref="Qx.Game.GameData.Furni"/> on <see cref="GameData"/>, which is
    /// <see langword="null"/> where this throws.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the furniture data has not been loaded.</exception>
    [Obsolete("Use GameData.Furni.")]
    public FurniData FurniData =>
        GameData.Furni ?? throw new InvalidOperationException("Furniture data has not been loaded.");

    /// <summary>Moves a floor item already in the room to another tile, keeping or changing its rotation.</summary>
    /// <remarks>Replaced by <see cref="MoveFloorItem(FloorItem, Point, int?)"/>.</remarks>
    /// <param name="item">The item to move.</param>
    /// <param name="location">The target tile.</param>
    /// <param name="direction">
    /// The item's new rotation, 0-7, or <see langword="null"/> to keep the rotation
    /// <paramref name="item"/> holds.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    [Obsolete("Use MoveFloorItem.")]
    public void Move(FloorItem item, Point location, int? direction = null) =>
        MoveFloorItem(item, location, direction);

    /// <summary>Sends a private message to a friend through the messenger.</summary>
    /// <remarks>Replaced by <see cref="SendPrivateMessage(Id, string)"/>.</remarks>
    /// <param name="userId">The friend's user id.</param>
    /// <param name="message">The message text.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is <see langword="null"/>, empty or whitespace.</exception>
    [Obsolete("Use SendPrivateMessage.")]
    public void SendMessage(Id userId, string message) => SendPrivateMessage(userId, message);

    /// <summary>Registers a handler that runs when a room starts loading, on the room ready message.</summary>
    /// <remarks>Replaced by <see cref="OnRoomLoading"/>.</remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    [Obsolete("Use OnRoomLoading.")]
    public IDisposable OnRoomReady(Action handler) => OnRoomLoading(handler);

    /// <summary>Registers a handler that runs when the room session has ended, with how it ended.</summary>
    /// <remarks>Replaced by <see cref="OnLeftRoom(Action{RoomExitState})"/>.</remarks>
    /// <param name="handler">The handler to call with the exit state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    [Obsolete("Use OnLeftRoom.")]
    public IDisposable OnRoomExited(Action<RoomExitState> handler) => OnLeftRoom(handler);

    /// <summary>Registers a handler that runs when a floor item is removed from the room.</summary>
    /// <remarks>Replaced by <see cref="OnFloorItemRemoved"/>.</remarks>
    /// <param name="handler">The handler to call with the item as it last was.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    [Obsolete("Use OnFloorItemRemoved.")]
    public IDisposable OnFloorItemRemovedDetailed(Action<FloorItem> handler) => OnFloorItemRemoved(handler);
}
