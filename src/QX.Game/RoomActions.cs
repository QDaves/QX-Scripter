using System.Threading.Channels;
using Qx.Game.Application;
using Qx.Interception;
using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game;

/// <summary>Specifies the operation a <see cref="RoomActions"/> run performs.</summary>
public enum FurniOperation
{
    /// <summary>No active run.</summary>
    None,
    /// <summary>A run that picks up the user's own furni.</summary>
    Pickup,
    /// <summary>A run that ejects furni owned by other users.</summary>
    Eject,
    /// <summary>A run that uses each item in turn.</summary>
    Toggle,
    /// <summary>A run that rotates floor items.</summary>
    Rotate,
    /// <summary>A run that moves floor items to clicked tiles.</summary>
    Move,
    /// <summary>A selection of an area by two tile clicks.</summary>
    SelectArea
}

/// <summary>Represents the progress of a <see cref="RoomActions"/> run.</summary>
/// <param name="Operation">The operation the run performs, or <see cref="FurniOperation.None"/> when no run is active.</param>
/// <param name="Done">The number of steps started so far, or the number of clicks received for an area selection.</param>
/// <param name="Total">The total number of steps in the run.</param>
public readonly record struct FurniProgress(FurniOperation Operation, int Done, int Total)
{
    /// <summary>Gets whether a run is active.</summary>
    public bool IsRunning => Operation is not FurniOperation.None;

    /// <summary>Returns a status line for the run, such as a click prompt or the step count.</summary>
    /// <returns>The status line, or an empty string when no run is active.</returns>
    public override string ToString() => Operation switch
    {
        FurniOperation.None => "",
        FurniOperation.SelectArea => Done == 0
            ? "Click the first corner in the room."
            : "Click the opposite corner.",
        FurniOperation.Move => $"Click where item {Done} of {Total} should go.",
        _ => $"{Operation} {Done} of {Total}…"
    };
}

/// <summary>
/// Provides actions on the current room, the user's avatar and room furni, including paced runs over many items.
/// </summary>
/// <remarks>
/// <para>
/// Single item methods send one message and do not wait for a response.
/// </para>
/// <para>
/// A run (<see cref="ToggleAsync"/>, <see cref="RotateAsync"/>, <see cref="MoveAsync"/>,
/// <see cref="PickupAsync"/>, <see cref="EjectAsync"/> and <see cref="SelectAreaAsync"/>) acts on
/// one item at a time with a delay between steps, and reports through <see cref="Progressed"/>.
/// Only one run can be active at a time. Items are processed from the back of the room forwards,
/// and the top of a stack before the items below it.
/// </para>
/// <para>
/// Hiding furni is written to the local client only. The hotel is not told and the mirrored room
/// state keeps the item, so hidden items are tracked through <see cref="Furni.IsHidden"/>.
/// </para>
/// <para>
/// An active run is canceled and <see cref="Progress"/> is cleared when the hotel connection closes.
/// </para>
/// </remarks>
public sealed class RoomActions : GameStateManager
{
    private readonly record struct FurniRunStep(
        Furni Item,
        bool IsPostIt,
        TimeSpan Interval);

    private readonly record struct RoomRunScope(
        Session Session,
        long RoomGeneration,
        FurniData? FurniData);

    private readonly SemaphoreSlim _running = new(1, 1);
    private CancellationTokenSource? _cancel;
    private IRoomPlacementOperations? _placement_operations;
    private int _disposed;

    /// <summary>Gets or sets the room manager that runs read the room and its furni from.</summary>
    public RoomManager? Room { get; set; }

    /// <summary>Gets or sets the function that returns the local user's id.</summary>
    /// <remarks>
    /// <see cref="PickupAsync"/> only picks up items owned by this id, and <see cref="EjectAsync"/>
    /// only ejects items owned by someone else.
    /// </remarks>
    public Func<Id?>? OwnUserId { get; set; }

    /// <summary>Occurs when a run starts, starts a step or finishes, with the new <see cref="FurniProgress"/>.</summary>
    /// <remarks>
    /// Raised on the thread that runs the step. A finished or canceled run reports
    /// <see cref="FurniOperation.None"/>.
    /// </remarks>
    public event Action<FurniProgress>? Progressed;

    /// <summary>Occurs when a furni item is hidden or shown again, with the item.</summary>
    public event Action<Furni>? VisibilityChanged;

    /// <summary>Gets the progress of the active run.</summary>
    /// <remarks>
    /// The operation is <see cref="FurniOperation.None"/> when no run is active.
    /// </remarks>
    public FurniProgress Progress { get; private set; }

    /// <summary>Gets whether a run is active.</summary>
    public bool IsBusy => Progress.IsRunning;

    /// <summary>
    /// Gets or sets the delay between steps of <see cref="ToggleAsync"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to 250 milliseconds. The delay between two steps is the larger interval of the two
    /// steps. The value is read when the run starts.
    /// </remarks>
    public TimeSpan ToggleInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets or sets the delay used for post-it notes in <see cref="ToggleAsync"/>.</summary>
    /// <remarks>
    /// Defaults to 750 milliseconds. The value is read when the run starts.
    /// </remarks>
    public TimeSpan TogglePostItInterval { get; set; } = TimeSpan.FromMilliseconds(750);

    /// <summary>Gets or sets the delay between steps of <see cref="PickupAsync"/> and <see cref="EjectAsync"/>.</summary>
    /// <remarks>
    /// Defaults to 150 milliseconds. The value is read when the run starts.
    /// </remarks>
    public TimeSpan PickupInterval { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Gets or sets the delay used for post-it notes in <see cref="PickupAsync"/> and <see cref="EjectAsync"/>.</summary>
    /// <remarks>
    /// Defaults to 750 milliseconds. The value is read when the run starts.
    /// </remarks>
    public TimeSpan PickupPostItInterval { get; set; } = TimeSpan.FromMilliseconds(750);

    /// <summary>Gets or sets the delay between steps of <see cref="RotateAsync"/>.</summary>
    /// <remarks>
    /// Defaults to 150 milliseconds. <see cref="MoveAsync"/> is paced by clicks and does not use it.
    /// </remarks>
    public TimeSpan MoveInterval { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <inheritdoc/>
    protected override void OnAttach()
    {
    }

    internal void BindPlacementOperations(IRoomPlacementOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _placement_operations, operations, null) is not null)
            throw new InvalidOperationException("Room placement operations are already bound.");
        if (Volatile.Read(ref _disposed) != 0)
        {
            Interlocked.CompareExchange(ref _placement_operations, null, operations);
            throw new ObjectDisposedException(nameof(RoomActions));
        }
    }

    internal void UnbindPlacementOperations(IRoomPlacementOperations operations) =>
        Interlocked.CompareExchange(ref _placement_operations, null, operations);

    /// <summary>Cancels the active run, if there is one.</summary>
    public void Cancel()
    {
        try
        {
            Volatile.Read(ref _cancel)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }


    /// <summary>Sends a request to enter a room.</summary>
    /// <param name="roomId">The id of the room.</param>
    /// <param name="password">The room password, or an empty string when the room has none.</param>
    /// <param name="entryPoint">The entry point sent with the request, or -1 for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> is <see langword="null"/>.</exception>
    public void Enter(Id roomId, string password = "", long entryPoint = -1)
    {
        ArgumentNullException.ThrowIfNull(password);
        SendMessage(
            MessageContracts.Room.Access.OpenRequest,
            new OpenFlatConnection(roomId, password, entryPoint));
    }

    internal void Enter(
        Id room_id,
        string password,
        long entry_point,
        Session expected_session,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(password);
        SendSessionGuardedMessage(
            MessageContracts.Room.Access.OpenRequest,
            new OpenFlatConnection(room_id, password, entry_point),
            expected_session,
            cancellation_token);
    }

    /// <summary>Sends a request to leave the current room.</summary>
    public void Leave() =>
        SendMessage(
            MessageContracts.Room.Lifecycle.Quit,
            new QuitRoomRequest());

    internal void Leave(
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Lifecycle.Quit,
            new QuitRoomRequest(),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Answers a user who is ringing the doorbell of the current room.</summary>
    /// <param name="userName">The name of the user at the door.</param>
    /// <param name="allow"><see langword="true"/> to let the user in, <see langword="false"/> to turn them away.</param>
    public void AnswerDoorbell(string userName, bool allow) =>
        SendMessage(
            MessageContracts.Room.Access.DoorbellAnswer,
            new AnswerDoorbellRequest(userName, allow));

    internal void AnswerDoorbell(
        string user_name,
        bool allow,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(user_name);
        SendGenerationGuardedMessage(
            MessageContracts.Room.Access.DoorbellAnswer,
            new AnswerDoorbellRequest(user_name, allow),
            expected_session,
            expected_room_generation,
            cancellation_token);
    }

    /// <summary>Sends a request to walk the user's avatar to a tile.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public void Walk(int x, int y) =>
        SendMessage(
            MessageContracts.Room.Movement.Walk,
            new WalkRequest(x, y));

    internal void Walk(
        int x,
        int y,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Movement.Walk,
            new WalkRequest(x, y),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Turns the user's avatar to face a tile.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public void LookTo(int x, int y) =>
        SendMessage(
            MessageContracts.Room.Movement.LookTo,
            new LookToRequest(x, y));

    internal void LookTo(
        int x,
        int y,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Movement.LookTo,
            new LookToRequest(x, y),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Starts or stops a dance.</summary>
    /// <param name="style">The dance style, or 0 to stop dancing.</param>
    public void Dance(int style) =>
        SendMessage(
            MessageContracts.Room.Occupants.Action.DanceRequest,
            new AvatarDanceRequest(style));

    internal void Dance(
        int style,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Occupants.Action.DanceRequest,
            new AvatarDanceRequest(style),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Performs an avatar expression, such as a wave.</summary>
    /// <param name="expression">The expression id.</param>
    public void Expression(int expression) =>
        SendMessage(
            MessageContracts.Room.Occupants.Action.ExpressionRequest,
            new AvatarExpressionRequest(expression));

    internal void Expression(
        int expression,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Occupants.Action.ExpressionRequest,
            new AvatarExpressionRequest(expression),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Holds up a sign over the user's avatar.</summary>
    /// <param name="sign">The sign id.</param>
    public void Sign(int sign) =>
        SendMessage(
            MessageContracts.Room.Occupants.Action.SignRequest,
            new AvatarSignRequest(sign));

    internal void Sign(
        int sign,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Occupants.Action.SignRequest,
            new AvatarSignRequest(sign),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Selects the avatar effect the user's avatar wears.</summary>
    /// <param name="effect">The effect id.</param>
    public void SelectEffect(int effect) =>
        SendMessage(
            MessageContracts.Room.Occupants.Action.EffectSelectionRequest,
            new AvatarEffectSelectionRequest(effect));

    internal void SelectEffect(
        int effect,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Occupants.Action.EffectSelectionRequest,
            new AvatarEffectSelectionRequest(effect),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Sets the posture of the user's avatar.</summary>
    /// <param name="posture">The posture id.</param>
    public void SetPosture(int posture) =>
        SendMessage(
            MessageContracts.Room.Occupants.Action.PostureRequest,
            new AvatarPostureRequest(posture));

    internal void SetPosture(
        int posture,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Occupants.Action.PostureRequest,
            new AvatarPostureRequest(posture),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Rates the current room.</summary>
    /// <param name="rating">The rating value.</param>
    public void Rate(int rating) =>
        SendMessage(
            MessageContracts.Room.RatingRequest,
            new RateRoomRequest(rating));

    internal void Rate(
        int rating,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.RatingRequest,
            new RateRoomRequest(rating),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Sets whether a room is a staff pick.</summary>
    /// <remarks>
    /// The hotel toggles the staff pick, so the request states the current value as the opposite
    /// of <paramref name="pick"/>.
    /// </remarks>
    /// <param name="roomId">The id of the room.</param>
    /// <param name="pick"><see langword="true"/> to make the room a staff pick, <see langword="false"/> to remove it.</param>
    public void SetStaffPick(Id roomId, bool pick) =>
        SendMessage(
            MessageContracts.Room.StaffPickUpdateRequest,
            new ToggleRoomStaffPickRequest(roomId, !pick));

    internal void SetStaffPick(
        Id room_id,
        bool pick,
        Session expected_session,
        CancellationToken cancellation_token) =>
        SendSessionGuardedMessage(
            MessageContracts.Room.StaffPickUpdateRequest,
            new ToggleRoomStaffPickRequest(room_id, !pick),
            expected_session,
            cancellation_token);

    /// <summary>Sends a chat message to the room.</summary>
    /// <param name="message">The message text.</param>
    /// <param name="bubble">The chat bubble style.</param>
    /// <param name="trackingId">The tracking id sent with the message, or -1 for none.</param>
    public void Talk(string message, int bubble = 0, int trackingId = -1) =>
        SendMessage(
            MessageContracts.Room.Chat.TalkSend,
            new TalkRequest(message, bubble, trackingId));

    internal void Talk(
        string message,
        int bubble,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Chat.TalkSend,
            new TalkRequest(message, bubble, -1),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Shouts a chat message to the room.</summary>
    /// <param name="message">The message text.</param>
    /// <param name="bubble">The chat bubble style.</param>
    public void Shout(string message, int bubble = 0) =>
        SendMessage(
            MessageContracts.Room.Chat.ShoutSend,
            new ShoutRequest(message, bubble));

    internal void Shout(
        string message,
        int bubble,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Chat.ShoutSend,
            new ShoutRequest(message, bubble),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Whispers a chat message to a user in the room.</summary>
    /// <param name="recipient">The name of the user to whisper to.</param>
    /// <param name="message">The message text.</param>
    /// <param name="bubble">The chat bubble style.</param>
    public void Whisper(string recipient, string message, int bubble = 0) =>
        SendMessage(
            MessageContracts.Room.Chat.WhisperSend,
            new WhisperRequest(recipient, message, bubble));

    internal void Whisper(
        string recipient,
        string message,
        int bubble,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
    {
        SendRoomMessage(
            MessageContracts.Room.Chat.WhisperSend,
            new WhisperRequest(recipient, message, bubble),
            expected_session,
            expected_room_generation,
            cancellation_token);
    }

    /// <summary>Shows the typing indicator over the user's avatar.</summary>
    public void StartTyping() =>
        SendMessage(
            MessageContracts.Room.Typing.Start,
            new StartTypingRequest());

    /// <summary>Hides the typing indicator over the user's avatar.</summary>
    public void CancelTyping() =>
        SendMessage(
            MessageContracts.Room.Typing.Cancel,
            new CancelTypingRequest());

    internal void SetTyping(
        bool active,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
    {
        if (active)
        {
            SendGenerationGuardedMessage(
                MessageContracts.Room.Typing.Start,
                new StartTypingRequest(),
                expected_session,
                expected_room_generation,
                cancellation_token);
            return;
        }
        SendGenerationGuardedMessage(
            MessageContracts.Room.Typing.Cancel,
            new CancelTypingRequest(),
            expected_session,
            expected_room_generation,
            cancellation_token);
    }

    /// <summary>Drops the item the user's avatar is holding.</summary>
    public void DropHandItem() =>
        SendMessage(
            MessageContracts.Room.HandItem.Drop,
            new DropHandItemRequest());

    internal void DropHandItem(
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.HandItem.Drop,
            new DropHandItemRequest(),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Gives the item the user's avatar is holding to another user.</summary>
    /// <param name="userId">The id of the user who receives the item.</param>
    public void PassHandItem(Id userId) =>
        SendMessage(
            MessageContracts.Room.HandItem.Pass,
            new PassHandItemRequest(userId));

    internal void PassHandItem(
        Id user_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.HandItem.Pass,
            new PassHandItemRequest(user_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    private void SendRoomMessage<T>(
        MessageContract<T> contract,
        T message,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        RoomManager room = Room
            ?? throw new InvalidOperationException("The room action manager is not attached.");
        void ValidateDispatch() => room.Capture(state =>
        {
            if (!state.IsReady || state.Generation != expected_room_generation)
                throw new InvalidOperationException("The room changed before dispatch.");
            cancellation_token.ThrowIfCancellationRequested();
            return true;
        });
        SendMessage(contract, message, expected_session, cancellation_token, ValidateDispatch);
    }

    private void SendGenerationGuardedMessage<T>(
        MessageContract<T> contract,
        T message,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        RoomManager room = Room
            ?? throw new InvalidOperationException("The room action manager is not attached.");
        void ValidateDispatch() => room.Capture(state =>
        {
            if (state.Generation != expected_room_generation)
                throw new InvalidOperationException("The room changed before dispatch.");
            cancellation_token.ThrowIfCancellationRequested();
            return true;
        });
        SendMessage(contract, message, expected_session, cancellation_token, ValidateDispatch);
    }

    private void SendSessionGuardedMessage<T>(
        MessageContract<T> contract,
        T message,
        Session expected_session,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        void ValidateDispatch() => cancellation_token.ThrowIfCancellationRequested();
        SendMessage(contract, message, expected_session, cancellation_token, ValidateDispatch);
    }

    /// <summary>Removes a furni item from the local client's view without changing the room.</summary>
    /// <remarks>
    /// A removal is written to the client only. The item is marked with <see cref="Furni.IsHidden"/>
    /// and <see cref="VisibilityChanged"/> is raised. Nothing happens when the item is already hidden.
    /// </remarks>
    /// <param name="item">The item to hide.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void Hide(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsHidden)
            return;

        item.IsHidden = true;
        Erase(item);
        VisibilityChanged?.Invoke(item);
    }

    /// <summary>Draws a hidden furni item in the local client again.</summary>
    /// <remarks>
    /// The item is written back to the client only, <see cref="Furni.IsHidden"/> is cleared and
    /// <see cref="VisibilityChanged"/> is raised. Nothing happens when the item is not hidden.
    /// </remarks>
    /// <param name="item">The item to show.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void Show(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsHidden)
            return;

        item.IsHidden = false;
        Draw(item);
        VisibilityChanged?.Invoke(item);
    }

    /// <summary>Hides a visible furni item, or shows a hidden one.</summary>
    /// <param name="item">The item to hide or show.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void ToggleHidden(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsHidden)
            Show(item);
        else
            Hide(item);
    }

    /// <summary>Shows every hidden floor and wall item in the room.</summary>
    /// <returns>The number of items shown, or 0 when <see cref="Room"/> is not set.</returns>
    public int ShowAll()
    {
        if (Room is not { } room)
            return 0;

        int shown = 0;
        foreach (Furni item in All(room).Where(item => item.IsHidden))
        {
            Show(item);
            shown++;
        }
        return shown;
    }

    /// <summary>Uses a floor or wall item with state 0.</summary>
    /// <param name="item">The item to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void Use(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is FloorItem)
            UseFloorItem(item.Id);
        else
            UseWallItem(item.Id);
    }

    /// <summary>Uses a floor item.</summary>
    /// <param name="itemId">The id of the floor item.</param>
    /// <param name="state">The state value sent with the request.</param>
    public void UseFloorItem(Id itemId, int state = 0) =>
        SendMessage(
            MessageContracts.Room.FloorItem.Use,
            new UseFloorItemRequest(itemId, state));

    internal void UseFloorItem(
        Id item_id,
        int state,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.FloorItem.Use,
            new UseFloorItemRequest(item_id, state),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>
    /// Sends a click on a furni item, as the client does when the item is clicked in the room.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Use"/>, the item is not triggered. The server answers with what a click
    /// means for the item, such as walking up to a teleporter.
    /// </remarks>
    /// <param name="item">The item to click.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public void Click(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        SendMessage(
            MessageContracts.Room.Item.Click,
            new ClickRoomItemRequest(item.Id, item is FloorItem ? ItemType.Floor : ItemType.Wall));
    }

    internal void ClickItem(
        Id item_id,
        ItemType type,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.Item.Click,
            new ClickRoomItemRequest(item_id, type),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Sends a request to enter a one-way door.</summary>
    /// <param name="itemId">The id of the one-way door.</param>
    public void EnterOneWayDoor(Id itemId) =>
        SendMessage(
            MessageContracts.Room.FloorItem.OneWayDoorEnter,
            new EnterOneWayDoorRequest(itemId));

    internal void EnterOneWayDoor(
        Id item_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.FloorItem.OneWayDoorEnter,
            new EnterOneWayDoorRequest(item_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Throws a dice.</summary>
    /// <param name="itemId">The id of the dice.</param>
    public void ThrowDice(Id itemId) =>
        SendMessage(
            MessageContracts.Room.FloorItem.ThrowDice,
            new ThrowDiceRequest(itemId));

    internal void ThrowDice(
        Id item_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.FloorItem.ThrowDice,
            new ThrowDiceRequest(item_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Turns a dice off.</summary>
    /// <param name="itemId">The id of the dice.</param>
    public void DiceOff(Id itemId) =>
        SendMessage(
            MessageContracts.Room.FloorItem.DiceOff,
            new DiceOffRequest(itemId));

    internal void DiceOff(
        Id item_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.FloorItem.DiceOff,
            new DiceOffRequest(item_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Uses a wall item.</summary>
    /// <param name="itemId">The id of the wall item.</param>
    /// <param name="state">The state value sent with the request.</param>
    public void UseWallItem(Id itemId, int state = 0) =>
        SendMessage(
            MessageContracts.Room.WallItem.Use,
            new UseWallItemRequest(itemId, state));

    internal void UseWallItem(
        Id item_id,
        int state,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItem.Use,
            new UseWallItemRequest(item_id, state),
            expected_session,
            expected_room_generation,
            cancellation_token);

    private void RequestStickyData(
        Id item_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItem.StickyDataRequest,
            new GetStickyDataRequest(item_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Removes a wall item from the room.</summary>
    /// <remarks>
    /// Post-it notes are removed with this request, which deletes them.
    /// </remarks>
    /// <param name="itemId">The id of the wall item.</param>
    public void RemoveWallItem(Id itemId) =>
        SendMessage(
            MessageContracts.Room.WallItem.Remove,
            new RemoveWallItemRequest(itemId));

    internal void RemoveWallItem(
        Id item_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItem.Remove,
            new RemoveWallItemRequest(item_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Sets the color and text of a post-it note.</summary>
    /// <param name="itemId">The id of the post-it note.</param>
    /// <param name="color">The note color.</param>
    /// <param name="text">The note text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="color"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetStickyData(Id itemId, string color, string text)
    {
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(text);
        SendMessage(
            MessageContracts.Room.WallItem.StickyDataSet,
            new SetStickyDataRequest(itemId, color, text));
    }

    internal void SetStickyData(
        Id item_id,
        string color,
        string text,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(text);
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItem.StickyDataSet,
            new SetStickyDataRequest(item_id, color, text),
            expected_session,
            expected_room_generation,
            cancellation_token);
    }

    /// <summary>Places a post-it note on a wall.</summary>
    /// <param name="itemId">The id of the post-it note item.</param>
    /// <param name="wallLocation">The wall location string of the target position.</param>
    public void PlacePostIt(Id itemId, string wallLocation) =>
        SendMessage(
            MessageContracts.Room.WallItem.PostItPlace,
            new PlacePostItRequest(itemId, wallLocation));

    internal void PlacePostIt(
        Id item_id,
        string wall_location,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(wall_location);
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItem.PostItPlace,
            new PlacePostItRequest(item_id, wall_location),
            expected_session,
            expected_room_generation,
            cancellation_token);
    }

    /// <summary>Places a post-it note on a wall with its color and text in one request.</summary>
    /// <param name="itemId">The id of the post-it note item.</param>
    /// <param name="wallLocation">The wall location string of the target position.</param>
    /// <param name="color">The note color.</param>
    /// <param name="text">The note text.</param>
    public void AddSpamWallPostIt(
        Id itemId,
        string wallLocation,
        string color,
        string text) =>
        SendMessage(
            MessageContracts.Room.WallItem.SpamPostItAdd,
            new AddSpamWallPostItRequest(itemId, wallLocation, color, text));

    internal void AddSpamWallPostIt(
        Id item_id,
        string wall_location,
        string color,
        string text,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(wall_location);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(text);
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItem.SpamPostItAdd,
            new AddSpamWallPostItRequest(item_id, wall_location, color, text),
            expected_session,
            expected_room_generation,
            cancellation_token);
    }

    /// <summary>Moves a floor item to a tile and direction.</summary>
    /// <remarks>
    /// The move is sent through the room placement operations, which check that the item is still
    /// in the active room at the position <paramref name="item"/> holds.
    /// </remarks>
    /// <param name="item">The floor item to move.</param>
    /// <param name="tile">The target tile.</param>
    /// <param name="direction">The target direction.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the placement operations are unavailable, or the item is not in the active room or has moved.
    /// </exception>
    public void MoveTo(FloorItem item, Point tile, int direction)
    {
        ArgumentNullException.ThrowIfNull(item);
        MoveFloorItem(item.Id, tile.X, tile.Y, direction, item, null, default);
    }

    /// <summary>Moves a floor item to a tile and direction.</summary>
    /// <remarks>
    /// The move is sent through the room placement operations, which check that the item is in the
    /// active room.
    /// </remarks>
    /// <param name="itemId">The id of the floor item.</param>
    /// <param name="x">The target tile x coordinate.</param>
    /// <param name="y">The target tile y coordinate.</param>
    /// <param name="direction">The target direction.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the placement operations are unavailable, or the item is not in the active room.
    /// </exception>
    public void MoveFloorItem(Id itemId, int x, int y, int direction) =>
        MoveFloorItem(itemId, x, y, direction, null, null, default);

    private void MoveFloorItem(
        Id item_id,
        int x,
        int y,
        int direction,
        FloorItem? source,
        long? expected_room_generation,
        CancellationToken cancellation_token)
    {
        RoomPlacementFloorPosition? expected_source = source is null
            ? null
            : new RoomPlacementFloorPosition(source.X, source.Y, source.Direction);
        PlacementOperations().MoveFloor(
            new RoomPlacementFloorMoveRequest(
                item_id,
                new RoomPlacementFloorPosition(x, y, direction),
                expected_source,
                ExpectedRoomGeneration: expected_room_generation),
            cancellation_token);
    }

    /// <summary>Picks up a floor or wall item from the room.</summary>
    /// <param name="item">The item to pick up.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the placement operations are unavailable, or the item is not in the active room.
    /// </exception>
    public void Pickup(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        int category = item is FloorItem ? 2 : 1;

        Pickup(category, item.Id);
    }

    /// <summary>Picks up an item from the room by category and id.</summary>
    /// <param name="category">The item category, 1 for a wall item or 2 for a floor item.</param>
    /// <param name="itemId">The id of the item.</param>
    /// <param name="confirmed">The confirmation flag sent with the pickup request.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="category"/> is not 1 or 2.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the placement operations are unavailable, or the item is not in the active room.
    /// </exception>
    public void Pickup(int category, Id itemId, bool confirmed = false)
    {
        if (category is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(category));
        Pickup(
            itemId,
            category == 2 ? RoomPlacementItemKind.Floor : RoomPlacementItemKind.Wall,
            confirmed,
            null,
            default);
    }

    private void Pickup(
        Id item_id,
        RoomPlacementItemKind item_kind,
        bool confirmed,
        long? expected_room_generation,
        CancellationToken cancellation_token) =>
        PlacementOperations().Pickup(
            new RoomPlacementPickupRequest(
                item_id,
                item_kind,
                confirmed,
                ExpectedRoomGeneration: expected_room_generation),
            cancellation_token);


    /// <summary>Uses each item in turn, paced by <see cref="ToggleInterval"/>.</summary>
    /// <remarks>
    /// Floor and wall items are used with state 0. Post-it notes have their note data requested
    /// instead and are paced by <see cref="TogglePostItInterval"/>. Canceling ends the run without
    /// faulting the task.
    /// </remarks>
    /// <param name="items">The items to use.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the run finishes or is canceled.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session, <see cref="Room"/> is not set or the room is not ready. The
    /// returned task faults with this exception when another run is active or the furni data for a
    /// wall item is unavailable.
    /// </exception>
    public Task ToggleAsync(IEnumerable<Furni> items, CancellationToken cancellationToken = default)
    {
        RoomRunScope scope = CaptureReadyRoomScope(cancellationToken);
        TimeSpan interval = ToggleInterval;
        TimeSpan post_it_interval = TogglePostItInterval;
        return RunAsync(
            FurniOperation.Toggle,
            items,
            item => Plan(item, scope.FurniData, interval, post_it_interval),
            (step, token) =>
            {
                Toggle(step, scope, token);
                return Task.CompletedTask;
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>Rotates each floor item in turn to a direction, paced by <see cref="MoveInterval"/>.</summary>
    /// <remarks>
    /// Wall items and floor items that already face the direction are skipped. Canceling ends the
    /// run without faulting the task.
    /// </remarks>
    /// <param name="items">The items to rotate.</param>
    /// <param name="direction">The target direction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the run finishes or is canceled.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Room"/> is not set or the room is not ready. The returned task faults with this
    /// exception when another run is active.
    /// </exception>
    public Task RotateAsync(
        IEnumerable<Furni> items,
        int direction,
        CancellationToken cancellationToken = default)
    {
        long room_generation = CaptureReadyRoomGeneration(cancellationToken);
        return RunAsync(
            FurniOperation.Rotate,
            items,
            MoveInterval,
            (item, token) =>
            {
                var floor = (FloorItem)item;
                MoveFloorItem(
                    floor.Id,
                    floor.X,
                    floor.Y,
                    direction,
                    floor,
                    room_generation,
                    token);
                return Task.CompletedTask;
            },
            item => item is FloorItem floor && floor.Direction != direction,
            cancellationToken);
    }

    /// <summary>
    /// Moves each floor item in turn to the next tile the user clicks in the room.
    /// </summary>
    /// <remarks>
    /// One click is taken per item, in run order. Each click is blocked before it reaches the hotel,
    /// so the avatar does not walk to the clicked tile. Wall items are skipped. Canceling ends the
    /// run without faulting the task.
    /// </remarks>
    /// <param name="items">The items to move.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the run finishes or is canceled.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Room"/> is not set or the room is not ready. The returned task faults with this
    /// exception when another run is active.
    /// </exception>
    public Task MoveAsync(IEnumerable<Furni> items, CancellationToken cancellationToken = default)
    {
        long room_generation = CaptureReadyRoomGeneration(cancellationToken);
        return RunAsync(
            FurniOperation.Move,
            items,
            TimeSpan.Zero,
            async (item, token) =>
            {
                Point tile = await NextClickAsync(token).ConfigureAwait(false);
                var floor = (FloorItem)item;
                MoveFloorItem(
                    floor.Id,
                    tile.X,
                    tile.Y,
                    floor.Direction,
                    floor,
                    room_generation,
                    token);
            },
            item => item is FloorItem,
            cancellationToken);
    }

    /// <summary>Picks up each of the user's own items in turn, paced by <see cref="PickupInterval"/>.</summary>
    /// <remarks>
    /// Items not owned by the id from <see cref="OwnUserId"/> are skipped, and nothing is picked up
    /// when it is not set. Post-it notes cannot be picked up, so they are removed with
    /// <see cref="RemoveWallItem(Id)"/>, which deletes them, and paced by <see cref="PickupPostItInterval"/>.
    /// Canceling ends the run without faulting the task.
    /// </remarks>
    /// <param name="items">The items to pick up.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the run finishes or is canceled.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session, <see cref="Room"/> is not set or the room is not ready. The
    /// returned task faults with this exception when another run is active or the furni data for a
    /// wall item is unavailable.
    /// </exception>
    public Task PickupAsync(IEnumerable<Furni> items, CancellationToken cancellationToken = default)
    {
        RoomRunScope scope = CaptureReadyRoomScope(cancellationToken);
        TimeSpan interval = PickupInterval;
        TimeSpan post_it_interval = PickupPostItInterval;
        return RunAsync(
            FurniOperation.Pickup,
            items,
            item => Plan(item, scope.FurniData, interval, post_it_interval),
            (step, token) =>
            {
                TakeFromRoom(step, scope, token);
                return Task.CompletedTask;
            },
            CanPickup,
            cancellationToken);
    }

    /// <summary>
    /// Ejects each item owned by another user in turn, paced by <see cref="PickupInterval"/>.
    /// </summary>
    /// <remarks>
    /// An eject is the same request as a pickup, sent for an item owned by someone else, which
    /// returns it to its owner. Items are only ejected when the user owns the room and
    /// <see cref="OwnUserId"/> is set, and items owned by that id are skipped. Canceling ends the
    /// run without faulting the task.
    /// </remarks>
    /// <param name="items">The items to eject.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the run finishes or is canceled.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session, <see cref="Room"/> is not set or the room is not ready. The
    /// returned task faults with this exception when another run is active, the furni data for a
    /// wall item is unavailable, or the items include a post-it note, since removing one deletes it.
    /// </exception>
    public Task EjectAsync(IEnumerable<Furni> items, CancellationToken cancellationToken = default)
    {
        RoomRunScope scope = CaptureReadyRoomScope(cancellationToken);
        TimeSpan interval = PickupInterval;
        TimeSpan post_it_interval = PickupPostItInterval;
        return RunAsync(
            FurniOperation.Eject,
            items,
            item => PlanEject(item, scope.FurniData, interval, post_it_interval),
            (step, token) =>
            {
                TakeFromRoom(step, scope, token);
                return Task.CompletedTask;
            },
            CanEject,
            cancellationToken);
    }

    private static FurniRunStep Plan(
        Furni item,
        FurniData? furni_data,
        TimeSpan interval,
        TimeSpan post_it_interval)
    {
        if (item is not WallItem)
            return new FurniRunStep(item, false, interval);

        FurniInfo info = furni_data?.GetInfo(item)
            ?? throw new InvalidOperationException(
                $"Furniture data for wall item {item.Id} is unavailable.");
        bool post_it = info.SpecialType == FurniCategory.PostIt;
        return new FurniRunStep(item, post_it, post_it ? post_it_interval : interval);
    }

    private static FurniRunStep PlanEject(
        Furni item,
        FurniData? furni_data,
        TimeSpan interval,
        TimeSpan post_it_interval)
    {
        FurniRunStep step = Plan(item, furni_data, interval, post_it_interval);
        if (step.IsPostIt)
        {
            throw new InvalidOperationException(
                "Post-it notes cannot be ejected because removing them would delete them.");
        }
        return step;
    }

    private void Toggle(
        FurniRunStep step,
        RoomRunScope scope,
        CancellationToken cancellation_token)
    {
        if (step.Item is FloorItem)
        {
            UseFloorItem(
                step.Item.Id,
                0,
                scope.Session,
                scope.RoomGeneration,
                cancellation_token);
        }
        else if (step.IsPostIt)
        {
            RequestStickyData(
                step.Item.Id,
                scope.Session,
                scope.RoomGeneration,
                cancellation_token);
        }
        else
        {
            UseWallItem(
                step.Item.Id,
                0,
                scope.Session,
                scope.RoomGeneration,
                cancellation_token);
        }
    }

    private void TakeFromRoom(
        FurniRunStep step,
        RoomRunScope scope,
        CancellationToken cancellation_token)
    {
        if (step.IsPostIt)
        {
            RemoveWallItem(
                step.Item.Id,
                scope.Session,
                scope.RoomGeneration,
                cancellation_token);
            return;
        }

        Pickup(
            step.Item.Id,
            step.Item is FloorItem
                ? RoomPlacementItemKind.Floor
                : RoomPlacementItemKind.Wall,
            false,
            scope.RoomGeneration,
            cancellation_token);
    }

    /// <summary>
    /// Waits for two tile clicks in the room and returns the area between them.
    /// </summary>
    /// <remarks>
    /// Both clicks are blocked before they reach the hotel, so the avatar does not walk. The
    /// selection counts as a run and reports <see cref="FurniOperation.SelectArea"/> progress.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with the selected area, or <see langword="null"/> when the selection is canceled.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when another run is active. The returned task faults with this exception.</exception>
    public async Task<Area?> SelectAreaAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource run = BeginRun(cancellationToken);
        try
        {
            Report(FurniOperation.SelectArea, 0, 2);
            Point[] corners = await NextClicksAsync(
                2,
                done =>
                {
                    if (done < 2)
                        Report(FurniOperation.SelectArea, done, 2);
                },
                run.Token).ConfigureAwait(false);
            return new Area(corners[0], corners[1]);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            Finish(run);
        }
    }


    /// <summary>
    /// Gets whether a pickup of the item would be allowed.
    /// </summary>
    /// <remarks>
    /// Only the user's own furni can be picked up. Furni owned by someone else needs an eject, and
    /// the hotel drops a pickup aimed at it without a response.
    /// </remarks>
    private bool CanPickup(Furni item) =>
        OwnUserId?.Invoke() is { } self && item.OwnerId == self;

    private bool CanEject(Furni item) =>
        Room?.IsOwner == true && OwnUserId?.Invoke() is { } self && item.OwnerId != self;

    private static IEnumerable<Furni> All(RoomManager room) =>
        room.FloorItems.Cast<Furni>().Concat(room.WallItems);

    private void Erase(Furni item)
    {
        if (item is FloorItem floor)
            SendToClient(MessageKeys.Room.FloorItem.Removed, new FloorItemRemove(floor.Id, false, 0, 0));
        else
            SendToClient(MessageKeys.Room.WallItem.Removed, new WallItemRemove(item.Id, 0));
    }

    private void Draw(Furni item)
    {
        if (item is FloorItem floor)
            SendToClient(MessageKeys.Room.FloorItem.Added, new FloorItemAdd(floor));
        else if (item is WallItem wall)
            SendToClient(MessageKeys.Room.WallItem.Added, new WallItemAdd(wall));
    }

    /// <summary>
    /// Waits for the next tile click and swallows it.
    /// </summary>
    /// <remarks>
    /// A click on the floor is a walk request, which is the only thing the client sends that
    /// says "this tile". Blocking it is what turns walking into pointing.
    /// </remarks>
    private async Task<Point> NextClickAsync(CancellationToken cancellationToken) =>
        (await NextClicksAsync(1, null, cancellationToken).ConfigureAwait(false))[0];

    private async Task<Point[]> NextClicksAsync(
        int count,
        Action<int>? accepted,
        CancellationToken cancellationToken)
    {
        var clicks = Channel.CreateBounded<Point>(new BoundedChannelOptions(count)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        int remaining = count;

        using IDisposable binding = Interceptor.Intercept(
            MessageContracts.Room.Movement.Walk.Key,
            intercept =>
            {
                if (Volatile.Read(ref remaining) <= 0)
                    return;

                WalkRequest request;
                try
                {
                    PacketReader reader = intercept.Packet.Reader();
                    request = MessageContracts.Room.Movement.Walk.Parse(in reader);
                    if (reader.Available != 0)
                        return;
                }
                catch
                {
                    return;
                }

                if (Interlocked.Decrement(ref remaining) < 0)
                {
                    Interlocked.Increment(ref remaining);
                    return;
                }

                if (!clicks.Writer.TryWrite(new Point(request.X, request.Y)))
                {
                    Interlocked.Increment(ref remaining);
                    return;
                }

                intercept.Block();
            });

        using CancellationTokenRegistration cancelled =
            cancellationToken.Register(() =>
                clicks.Writer.TryComplete(new OperationCanceledException(cancellationToken)));

        var points = new Point[count];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = await clicks.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            accepted?.Invoke(i + 1);
        }
        return points;
    }

    private async Task RunAsync(
        FurniOperation operation,
        IEnumerable<Furni> items,
        Func<Furni, FurniRunStep> plan,
        Func<FurniRunStep, CancellationToken, Task> act,
        Func<Furni, bool>? allowed = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        CancellationTokenSource run = BeginRun(cancellationToken);
        try
        {
            FurniRunStep[] queue =
                [.. Ordered(allowed is null ? items : items.Where(allowed)).Select(plan)];
            Report(operation, 0, queue.Length);

            for (int i = 0; i < queue.Length; i++)
            {
                run.Token.ThrowIfCancellationRequested();
                if (i > 0)
                {
                    TimeSpan delay = queue[i - 1].Interval > queue[i].Interval
                        ? queue[i - 1].Interval
                        : queue[i].Interval;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, run.Token).ConfigureAwait(false);
                }

                Report(operation, i + 1, queue.Length);
                await act(queue[i], run.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Finish(run);
        }
    }

    private Task RunAsync(
        FurniOperation operation,
        IEnumerable<Furni> items,
        TimeSpan interval,
        Func<Furni, CancellationToken, Task> act,
        Func<Furni, bool>? allowed = null,
        CancellationToken cancellationToken = default) => RunAsync(
            operation,
            items,
            item => new FurniRunStep(item, false, interval),
            (step, token) => act(step.Item, token),
            allowed,
            cancellationToken);

    /// <summary>
    /// Puts a run in the order a person would do it by hand.
    /// </summary>
    /// <remarks>
    /// Back of the room forwards, and the top of a stack before what it is standing on. Picking a
    /// stack up from the bottom leaves the hotel refusing every item above it.
    /// </remarks>
    private static Furni[] Ordered(IEnumerable<Furni> items) =>
        [.. items
            .OrderBy(item => item.Type)
            .ThenBy(item => item switch
            {
                FloorItem floor => floor.Y,
                WallItem wall => wall.WX * 16 - wall.WY * 16 + wall.LX,
                _ => 0
            })
            .ThenBy(item => item switch
            {
                FloorItem floor => floor.X,
                WallItem wall => wall.WX * 16 + wall.WY * 16 - wall.LY,
                _ => 0
            })
            .ThenByDescending(item => item is FloorItem floor ? floor.Z : 0f)];

    private void Report(FurniOperation operation, int done, int total)
    {
        Progress = new FurniProgress(operation, done, total);
        Progressed?.Invoke(Progress);
    }

    private CancellationTokenSource BeginRun(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!_running.Wait(0))
            throw new InvalidOperationException("Something is already running.");

        CancellationTokenSource run;
        try
        {
            run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }
        catch
        {
            _running.Release();
            throw;
        }

        Volatile.Write(ref _cancel, run);
        if (Volatile.Read(ref _disposed) != 0)
            run.Cancel();
        return run;
    }

    private void Finish(CancellationTokenSource run)
    {
        Interlocked.CompareExchange(ref _cancel, null, run);
        run.Dispose();
        try
        {
            Report(FurniOperation.None, 0, 0);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <inheritdoc/>
    protected override void Reset()
    {
        Cancel();
        Progress = new FurniProgress(FurniOperation.None, 0, 0);
    }

    /// <inheritdoc/>
    protected internal override void Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Cancel();
        Interlocked.Exchange(ref _placement_operations, null);
        base.Close();
    }

    private IRoomPlacementOperations PlacementOperations()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Volatile.Read(ref _placement_operations)
            ?? throw new InvalidOperationException("Room placement operations are unavailable.");
    }

    private RoomRunScope CaptureReadyRoomScope(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Session session = CurrentSession
            ?? throw new InvalidOperationException("An active hotel session is required.");
        RoomManager room = Room
            ?? throw new InvalidOperationException("The room action manager is not attached.");
        string web_host = GameData.WebHostFor(session.Host);
        RoomRunScope scope = room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!current_room.IsReady)
                throw new InvalidOperationException("A ready hotel room is required for this run.");

            GameDataState? game_data = current_room.GameData?.State;
            FurniData? furni_data = game_data is
            {
                Loaded: true,
                LoadGeneration: > 0,
                Furni: not null
            } && string.Equals(
                    game_data.WebHost,
                    web_host,
                    StringComparison.OrdinalIgnoreCase)
                ? game_data.Furni
                : null;
            return new RoomRunScope(
                session,
                current_room.Generation,
                furni_data);
        });
        if (!ReferenceEquals(CurrentSession, session))
            throw new InvalidOperationException("The hotel session changed before the run started.");
        return scope;
    }

    private long CaptureReadyRoomGeneration(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        RoomManager room = Room
            ?? throw new InvalidOperationException("The room action manager is not attached.");
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!current_room.IsReady)
                throw new InvalidOperationException("A ready hotel room is required for this run.");
            return current_room.Generation;
        });
    }
}
