using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal interface IRoomAvatarOperations
{
    RoomAvatarDispatchResult Walk(
        RoomAvatarWalkRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Look(
        RoomAvatarLookRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Dance(
        RoomAvatarDanceRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Expression(
        RoomAvatarExpressionRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Posture(
        RoomAvatarPostureRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Sign(
        RoomAvatarSignRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Effect(
        RoomAvatarEffectRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Typing(
        RoomAvatarTypingRequest request,
        CancellationToken cancellation_token = default);
}

internal sealed class RoomAvatarApplication : IApplicationFeature, IRoomAvatarOperations
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomAvatarApplication(
        IInterceptor interceptor,
        GameState game,
        TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(time_provider);
        connection = interceptor;
        this.game = game;
        this.time_provider = time_provider;
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            Call<RoomAvatarWalkRequest>(WalkDescriptor(), Walk),
            Call<RoomAvatarLookRequest>(LookDescriptor(), Look),
            Call<RoomAvatarDanceRequest>(DanceDescriptor(), Dance),
            Call<RoomAvatarExpressionRequest>(ExpressionDescriptor(), Expression),
            Call<RoomAvatarPostureRequest>(PostureDescriptor(), Posture),
            Call<RoomAvatarSignRequest>(SignDescriptor(), Sign),
            Call<RoomAvatarEffectRequest>(EffectDescriptor(), Effect),
            Call<RoomAvatarTypingRequest>(TypingDescriptor(), Typing)
        ]);
        try
        {
            game.BindRoomAvatarOperations(this);
        }
        catch
        {
            Volatile.Write(ref disposed, 1);
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomAvatarDispatchResult Walk(
        RoomAvatarWalkRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Walk(
                request.X,
                request.Y,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Look(
        RoomAvatarLookRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.LookTo(
                request.X,
                request.Y,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Dance(
        RoomAvatarDanceRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Dance(
                request.Style,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Expression(
        RoomAvatarExpressionRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Expression(
                request.Expression,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Posture(
        RoomAvatarPostureRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SetPosture(
                request.Posture,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Sign(
        RoomAvatarSignRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Sign(
                request.Sign,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Effect(
        RoomAvatarEffectRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SelectEffect(
                request.Effect,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Typing(
        RoomAvatarTypingRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SetTyping(
                request.Active,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        game.UnbindRoomAvatarOperations(this);
    }

    private RoomAvatarDispatchResult Dispatch(
        Action<Session, long, CancellationToken> send,
        CancellationToken cancellation_token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        var room = game.Room.Capture(state =>
        {
            Id? room_id = state.RoomId == 0 ? null : (Id)state.RoomId;
            return (RoomId: room_id, state.Generation);
        });
        send(session, room.Generation, cancellation_token);
        return new RoomAvatarDispatchResult(
            session.Client,
            room.RoomId,
            room.Generation,
            true,
            false,
            time_provider.GetUtcNow());
    }

    private static ApplicationCallBinding<TRequest, RoomAvatarDispatchResult> Call<TRequest>(
        ApplicationDescriptor descriptor,
        Func<TRequest, CancellationToken, RoomAvatarDispatchResult> invocation) => new(
            descriptor,
            (request, cancellation_token) =>
                ValueTask.FromResult(invocation(request, cancellation_token)));

    private static ApplicationDescriptor WalkDescriptor() => Descriptor<RoomAvatarWalkRequest>(
        ApplicationMemberIds.RoomAvatarWalk,
        "Walk",
        "Requests movement to a tile in the current room.",
        [
            Parameter(nameof(RoomAvatarWalkRequest.X).ToLowerInvariant()),
            Parameter(nameof(RoomAvatarWalkRequest.Y).ToLowerInvariant())
        ],
        [MessageKeys.Room.Movement.Walk]);

    private static ApplicationDescriptor LookDescriptor() => Descriptor<RoomAvatarLookRequest>(
        ApplicationMemberIds.RoomAvatarLook,
        "Look toward tile",
        "Turns the local avatar toward a tile in the current room.",
        [
            Parameter(nameof(RoomAvatarLookRequest.X).ToLowerInvariant()),
            Parameter(nameof(RoomAvatarLookRequest.Y).ToLowerInvariant())
        ],
        [MessageKeys.Room.Movement.LookTo]);

    private static ApplicationDescriptor DanceDescriptor() => Descriptor<RoomAvatarDanceRequest>(
        ApplicationMemberIds.RoomAvatarDance,
        "Avatar dance",
        "Sets the local avatar dance style.",
        [Parameter(nameof(RoomAvatarDanceRequest.Style).ToLowerInvariant())],
        [MessageKeys.Room.Occupants.Action.DanceRequest]);

    private static ApplicationDescriptor ExpressionDescriptor() =>
        Descriptor<RoomAvatarExpressionRequest>(
            ApplicationMemberIds.RoomAvatarExpression,
            "Avatar expression",
            "Plays an expression on the local avatar.",
            [Parameter(nameof(RoomAvatarExpressionRequest.Expression).ToLowerInvariant())],
            [MessageKeys.Room.Occupants.Action.ExpressionRequest]);

    private static ApplicationDescriptor PostureDescriptor() =>
        Descriptor<RoomAvatarPostureRequest>(
            ApplicationMemberIds.RoomAvatarPosture,
            "Avatar posture",
            "Sets the local avatar posture.",
            [Parameter(nameof(RoomAvatarPostureRequest.Posture).ToLowerInvariant())],
            [MessageKeys.Room.Occupants.Action.PostureRequest]);

    private static ApplicationDescriptor SignDescriptor() => Descriptor<RoomAvatarSignRequest>(
        ApplicationMemberIds.RoomAvatarSign,
        "Avatar sign",
        "Raises a sign above the local avatar.",
        [Parameter(nameof(RoomAvatarSignRequest.Sign).ToLowerInvariant())],
        [MessageKeys.Room.Occupants.Action.SignRequest]);

    private static ApplicationDescriptor EffectDescriptor() => Descriptor<RoomAvatarEffectRequest>(
        ApplicationMemberIds.RoomAvatarEffect,
        "Avatar effect",
        "Selects an effect for the local avatar.",
        [Parameter(nameof(RoomAvatarEffectRequest.Effect).ToLowerInvariant())],
        [MessageKeys.Room.Occupants.Action.EffectSelectionRequest]);

    private static ApplicationDescriptor TypingDescriptor() => Descriptor<RoomAvatarTypingRequest>(
        ApplicationMemberIds.RoomAvatarTyping,
        "Typing indicator",
        "Shows or hides the local avatar typing indicator.",
        [new("active", typeof(bool), true, null, "Whether the indicator is visible.")],
        [MessageKeys.Room.Typing.Start, MessageKeys.Room.Typing.Cancel]);

    private static ApplicationDescriptor Descriptor<TRequest>(
        string id,
        string title,
        string description,
        ApplicationParameterDescriptor[] parameters,
        MessageKey[] messages) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(RoomAvatarDispatchResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages: messages
                .Select(message => new ApplicationMessageRequirement(
                    message,
                    Direction.Out,
                    ApplicationMessageRole.Send))
                .ToArray(),
            tool_hints: new(false, true, false, true));

    private static ApplicationParameterDescriptor Parameter(string name) =>
        new(name, typeof(int), true, null, "Integer value sent to the active client dialect.");
}
