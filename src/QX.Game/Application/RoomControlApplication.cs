using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal interface IRoomControlOperations
{
    RoomControlDispatchResult AnswerDoorbell(
        RoomDoorbellAnswerRequest request,
        CancellationToken cancellation_token = default);

    RoomControlDispatchResult DropHandItem(
        RoomHandItemDropRequest request,
        CancellationToken cancellation_token = default);

    RoomControlDispatchResult PassHandItem(
        RoomHandItemPassRequest request,
        CancellationToken cancellation_token = default);
}

internal sealed class RoomControlApplication : IApplicationFeature, IRoomControlOperations
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomControlApplication(
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
            Call<RoomDoorbellAnswerRequest>(DoorbellDescriptor(), AnswerDoorbell),
            Call<RoomHandItemDropRequest>(HandItemDropDescriptor(), DropHandItem),
            Call<RoomHandItemPassRequest>(HandItemPassDescriptor(), PassHandItem),
            Call<RoomRatingRequest>(RatingDescriptor(), SubmitRating),
            Call<RoomStaffPickRequest>(StaffPickDescriptor(), SetStaffPick)
        ]);
        try
        {
            game.BindRoomControlOperations(this);
        }
        catch
        {
            Volatile.Write(ref disposed, 1);
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomControlDispatchResult AnswerDoorbell(
        RoomDoorbellAnswerRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.UserName);
        return DispatchRoom(
            (session, generation, cancellation) => game.RoomActions.AnswerDoorbell(
                request.UserName,
                request.Allow,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomControlDispatchResult DropHandItem(
        RoomHandItemDropRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchRoom(
            (session, generation, cancellation) => game.RoomActions.DropHandItem(
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomControlDispatchResult PassHandItem(
        RoomHandItemPassRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchRoom(
            (session, generation, cancellation) => game.RoomActions.PassHandItem(
                request.UserId,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        game.UnbindRoomControlOperations(this);
    }

    private RoomControlDispatchResult SubmitRating(
        RoomRatingRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchRoom(
            (session, generation, cancellation) => game.RoomActions.Rate(
                request.Rating,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    private RoomControlDispatchResult SetStaffPick(
        RoomStaffPickRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchSession(
            request.RoomId,
            (session, cancellation) => game.RoomActions.SetStaffPick(
                request.RoomId,
                request.Pick,
                session,
                cancellation),
            cancellation_token);
    }

    private RoomControlDispatchResult DispatchRoom(
        Action<Session, long, CancellationToken> send,
        CancellationToken cancellation_token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        var room = CaptureRoom();
        send(session, room.Generation, cancellation_token);
        return Result(session.Client, room.RoomId, room.Generation);
    }

    private RoomControlDispatchResult DispatchSession(
        Id room_id,
        Action<Session, CancellationToken> send,
        CancellationToken cancellation_token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        long generation = game.Room.Capture(state => state.Generation);
        send(session, cancellation_token);
        return Result(session.Client, room_id, generation);
    }

    private (Id? RoomId, long Generation) CaptureRoom() => game.Room.Capture(state =>
    {
        Id? room_id = state.RoomId == 0 ? null : (Id)state.RoomId;
        return (room_id, state.Generation);
    });

    private RoomControlDispatchResult Result(ClientType client, Id? room_id, long generation) =>
        new(client, room_id, generation, true, false, time_provider.GetUtcNow());

    private static ApplicationCallBinding<TRequest, RoomControlDispatchResult> Call<TRequest>(
        ApplicationDescriptor descriptor,
        Func<TRequest, CancellationToken, RoomControlDispatchResult> invocation) => new(
            descriptor,
            (request, cancellation_token) =>
            {
                ArgumentNullException.ThrowIfNull(request);
                return ValueTask.FromResult(invocation(request, cancellation_token));
            });

    private static ApplicationDescriptor DoorbellDescriptor() => Descriptor<RoomDoorbellAnswerRequest>(
        ApplicationMemberIds.RoomDoorbellAnswer,
        "Answer doorbell",
        "Allows or rejects a user waiting at the current room door.",
        [
            new("user_name", typeof(string), true, null, "Waiting user name."),
            new("allow", typeof(bool), false, true, "Whether entry is allowed.")
        ],
        MessageKeys.Room.Access.DoorbellAnswer);

    private static ApplicationDescriptor HandItemDropDescriptor() => Descriptor<RoomHandItemDropRequest>(
        ApplicationMemberIds.RoomHandItemDrop,
        "Drop hand item",
        "Drops the local avatar hand item in the current room.",
        [],
        MessageKeys.Room.HandItem.Drop);

    private static ApplicationDescriptor HandItemPassDescriptor() => Descriptor<RoomHandItemPassRequest>(
        ApplicationMemberIds.RoomHandItemPass,
        "Pass hand item",
        "Passes the local avatar hand item to a room user.",
        [new("user_id", typeof(Id), true, null, "Target room user identifier.")],
        MessageKeys.Room.HandItem.Pass);

    private static ApplicationDescriptor RatingDescriptor() => Descriptor<RoomRatingRequest>(
        ApplicationMemberIds.RoomRatingSubmit,
        "Rate room",
        "Submits a rating for the current room.",
        [new("rating", typeof(int), true, null, "Signed room rating value.")],
        MessageKeys.Room.RatingRequest);

    private static ApplicationDescriptor StaffPickDescriptor() => Descriptor<RoomStaffPickRequest>(
        ApplicationMemberIds.RoomStaffPickSet,
        "Set staff pick",
        "Adds or removes a room from the staff picks.",
        [
            new("room_id", typeof(Id), true, null, "Target room identifier."),
            new("pick", typeof(bool), false, true, "Whether the room is picked.")
        ],
        MessageKeys.Room.StaffPickUpdateRequest);

    private static ApplicationDescriptor Descriptor<TRequest>(
        string id,
        string title,
        string description,
        ApplicationParameterDescriptor[] parameters,
        MessageKey message) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(RoomControlDispatchResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages:
            [
                new ApplicationMessageRequirement(
                    message,
                    Direction.Out,
                    ApplicationMessageRole.Send)
            ],
            tool_hints: new(false, true, false, true));
}
