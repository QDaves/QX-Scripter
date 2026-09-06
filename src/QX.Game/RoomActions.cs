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

/// <summary>What a run of <see cref="RoomActions"/> is doing, for the line that reports it.</summary>
public enum FurniOperation
{
    None,
    Pickup,
    Eject,
    Toggle,
    Rotate,
    Move,
    SelectArea
}

/// <summary>How far along a run is.</summary>
public readonly record struct FurniProgress(FurniOperation Operation, int Done, int Total)
{
    public bool IsRunning => Operation is not FurniOperation.None;

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
/// Doing things to the furni in the room, one at a time and at a pace the hotel tolerates.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these could be a line in a script. They are here because a list you are already
/// looking at is the natural place to act on what you selected, and because all of them share the
/// same two problems: a hotel that disconnects you for sending three hundred messages at once, and
/// a run that has to be abandonable halfway. Solving those twice, once for scripts and once for
/// the list, would be solving them twice.
/// </para>
/// <para>
/// Hiding is the odd one. Nothing is sent to the hotel — the removal is written to the client, so
/// the client stops drawing something that is still standing in the room. That is why what is
/// hidden is tracked here and not read back from anywhere: the hotel was never told, and neither
/// was the room state we mirror.
/// </para>
/// <para>
/// All of it works on both clients. Message names are resolved rather than hard-coded, and each
/// typed request writes the client-specific layout before anything reaches the transport.
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

    /// <summary>The room being acted on, handed over by the game state.</summary>
    public RoomManager? Room { get; set; }

    /// <summary>Used to tell your own furni from someone else's, which decides pickup from eject.</summary>
    public Func<Id?>? OwnUserId { get; set; }

    /// <summary>Raised when a run starts, steps or finishes.</summary>
    public event Action<FurniProgress>? Progressed;

    /// <summary>Raised when a piece of furni has been hidden or shown again.</summary>
    public event Action<Furni>? VisibilityChanged;

    public FurniProgress Progress { get; private set; }

    public bool IsBusy => Progress.IsRunning;

    /// <summary>
    /// How long to wait between messages of a run.
    /// </summary>
    /// <remarks>
    /// Toggling is the one the hotel watches most closely, so it is the slowest. These are
    /// deliberately not zero: a run with no gap at all is indistinguishable from a flood.
    /// </remarks>
    public TimeSpan ToggleInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan TogglePostItInterval { get; set; } = TimeSpan.FromMilliseconds(750);

    public TimeSpan PickupInterval { get; set; } = TimeSpan.FromMilliseconds(150);

    public TimeSpan PickupPostItInterval { get; set; } = TimeSpan.FromMilliseconds(750);

    public TimeSpan MoveInterval { get; set; } = TimeSpan.FromMilliseconds(150);

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

    /// <summary>Stops whatever run is going on. Safe to call when there is none.</summary>
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


    public void Enter(Id room_id, string password = "", long entry_point = -1)
    {
        ArgumentNullException.ThrowIfNull(password);
        SendMessage(
            MessageContracts.Room.Access.OpenRequest,
            new OpenFlatConnection(room_id, password, entry_point));
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

    public void AnswerDoorbell(string user_name, bool allow) =>
        SendMessage(
            MessageContracts.Room.Access.DoorbellAnswer,
            new AnswerDoorbellRequest(user_name, allow));

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

    public void SetStaffPick(Id room_id, bool pick) =>
        SendMessage(
            MessageContracts.Room.StaffPickUpdateRequest,
            new ToggleRoomStaffPickRequest(room_id, !pick));

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

    public void Talk(string message, int bubble = 0, int tracking_id = -1) =>
        SendMessage(
            MessageContracts.Room.Chat.TalkSend,
            new TalkRequest(message, bubble, tracking_id));

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

    public void StartTyping() =>
        SendMessage(
            MessageContracts.Room.Typing.Start,
            new StartTypingRequest());

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

    public void PassHandItem(Id user_id) =>
        SendMessage(
            MessageContracts.Room.HandItem.Pass,
            new PassHandItemRequest(user_id));

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

    /// <summary>Takes one piece of furni off the screen without touching the room.</summary>
    public void Hide(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsHidden)
            return;

        item.IsHidden = true;
        Erase(item);
        VisibilityChanged?.Invoke(item);
    }

    /// <summary>Puts a hidden piece of furni back on the screen.</summary>
    public void Show(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsHidden)
            return;

        item.IsHidden = false;
        Draw(item);
        VisibilityChanged?.Invoke(item);
    }

    public void ToggleHidden(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsHidden)
            Show(item);
        else
            Hide(item);
    }

    /// <summary>Shows everything that was hidden, in one pass.</summary>
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

    /// <summary>Presses one piece of furni, as clicking it would.</summary>
    public void Use(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is FloorItem)
            UseFloorItem(item.Id);
        else
            UseWallItem(item.Id);
    }

    public void UseFloorItem(Id item_id, int state = 0) =>
        SendMessage(
            MessageContracts.Room.FloorItemUse,
            new UseFloorItemRequest(item_id, state));

    internal void UseFloorItem(
        Id item_id,
        int state,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.FloorItemUse,
            new UseFloorItemRequest(item_id, state),
            expected_session,
            expected_room_generation,
            cancellation_token);

    public void EnterOneWayDoor(Id item_id) =>
        SendMessage(
            MessageContracts.Room.FloorItem.OneWayDoorEnter,
            new EnterOneWayDoorRequest(item_id));

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

    public void ThrowDice(Id item_id) =>
        SendMessage(
            MessageContracts.Room.FloorItem.ThrowDice,
            new ThrowDiceRequest(item_id));

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

    public void DiceOff(Id item_id) =>
        SendMessage(
            MessageContracts.Room.FloorItem.DiceOff,
            new DiceOffRequest(item_id));

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

    public void UseWallItem(Id item_id, int state = 0) =>
        SendMessage(
            MessageContracts.Room.WallItemUse,
            new UseWallItemRequest(item_id, state));

    internal void UseWallItem(
        Id item_id,
        int state,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItemUse,
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

    public void RemoveWallItem(Id item_id) =>
        SendMessage(
            MessageContracts.Room.WallItemRemove,
            new RemoveWallItemRequest(item_id));

    internal void RemoveWallItem(
        Id item_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendGenerationGuardedMessage(
            MessageContracts.Room.WallItemRemove,
            new RemoveWallItemRequest(item_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    public void SetStickyData(Id item_id, string color, string text)
    {
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(text);
        SendMessage(
            MessageContracts.Room.WallItem.StickyDataSet,
            new SetStickyDataRequest(item_id, color, text));
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

    public void PlacePostIt(Id item_id, string wall_location) =>
        SendMessage(
            MessageContracts.Room.WallItem.PostItPlace,
            new PlacePostItRequest(item_id, wall_location));

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

    public void AddSpamWallPostIt(
        Id item_id,
        string wall_location,
        string color,
        string text) =>
        SendMessage(
            MessageContracts.Room.WallItem.SpamPostItAdd,
            new AddSpamWallPostItRequest(item_id, wall_location, color, text));

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

    /// <summary>Puts one floor item down somewhere else, facing a given way.</summary>
    public void MoveTo(FloorItem item, Point tile, int direction)
    {
        ArgumentNullException.ThrowIfNull(item);
        MoveFloorItem(item.Id, tile.X, tile.Y, direction, item, null, default);
    }

    public void MoveFloorItem(Id item_id, int x, int y, int direction) =>
        MoveFloorItem(item_id, x, y, direction, null, null, default);

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

    /// <summary>
    /// Takes one piece of furni into the inventory.
    /// </summary>
    /// <remarks>
    /// The category leads: two for a floor item, one for a wall item. Flash carries a third field
    /// acknowledging the hotel's are-you-sure prompt, which Unity has no room for.
    /// </remarks>
    public void Pickup(Furni item)
    {
        ArgumentNullException.ThrowIfNull(item);
        int category = item is FloorItem ? 2 : 1;

        Pickup(category, item.Id);
    }

    public void Pickup(int category, Id item_id, bool confirmed = false)
    {
        if (category is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(category));
        Pickup(
            item_id,
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
    /// Moves each selected item to wherever you click next.
    /// </summary>
    /// <remarks>
    /// One click per item, in the order they are listed. The click is swallowed rather than passed
    /// on, so you do not walk to every tile you are placing furni on.
    /// </remarks>
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

    /// <summary>Takes your own furni back into your inventory.</summary>
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
    /// Sends other people's furni back to them.
    /// </summary>
    /// <remarks>
    /// The same message as a pickup. What separates them is whose furni it is: yours comes back to
    /// you, theirs goes back to them, and only the room's owner may do the second.
    /// </remarks>
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
    /// Waits for two clicks in the room and returns the rectangle between them.
    /// </summary>
    /// <remarks>
    /// Both clicks are swallowed, so picking an area does not walk you across the room. Answering
    /// "which of these is over there" by pointing at the floor beats typing coordinates that you
    /// would have to read off the room first.
    /// </remarks>
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
    /// Whether a pickup would be allowed, so a run does not spend a minute being ignored.
    /// </summary>
    /// <remarks>
    /// Your own furni comes back to you wherever it is standing. Someone else's does not — that is
    /// an eject, and the hotel drops a pickup aimed at it without saying so.
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
    /// A click on the floor is a walk request, which is the only thing either client sends that
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

    protected override void Reset()
    {
        Cancel();
        Progress = new FurniProgress(FurniOperation.None, 0, 0);
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Cancel();
        Interlocked.Exchange(ref _placement_operations, null);
        base.Dispose();
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
