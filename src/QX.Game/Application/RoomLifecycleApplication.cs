using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class RoomLifecycleApplication : IApplicationFeature
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomLifecycleApplication(
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
            new ApplicationCallBinding<RoomEnterRequest, RoomLifecycleDispatchResult>(
                EnterDescriptor(),
                Enter),
            new ApplicationCallBinding<RoomLeaveRequest, RoomLifecycleDispatchResult>(
                LeaveDescriptor(),
                Leave)
        ]);
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);

    private ValueTask<RoomLifecycleDispatchResult> Enter(
        RoomEnterRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Password);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        long generation = game.Room.Capture(state => state.Generation);
        game.RoomActions.Enter(
            request.RoomId,
            request.Password,
            request.EntryPoint,
            session,
            cancellation_token);
        return ValueTask.FromResult(new RoomLifecycleDispatchResult(
            session.Client,
            request.RoomId,
            generation,
            true,
            false,
            time_provider.GetUtcNow()));
    }

    private ValueTask<RoomLifecycleDispatchResult> Leave(
        RoomLeaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        var room = game.Room.Capture(state =>
        {
            Id? room_id = state.RoomId == 0 ? null : (Id)state.RoomId;
            return (RoomId: room_id, state.Generation);
        });
        game.RoomActions.Leave(session, room.Generation, cancellation_token);
        return ValueTask.FromResult(new RoomLifecycleDispatchResult(
            session.Client,
            room.RoomId,
            room.Generation,
            true,
            false,
            time_provider.GetUtcNow()));
    }

    private static ApplicationDescriptor EnterDescriptor() => new(
        ApplicationMemberIds.RoomEnter,
        "Enter room",
        "Requests entry into a room through the active hotel session.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomEnterRequest),
        typeof(RoomLifecycleDispatchResult),
        [
            new("room_id", typeof(Id), true, null, "Target room identifier."),
            new("password", typeof(string), false, "", "Room password."),
            new("entry_point", typeof(long), false, -1L, "Navigator entry point identifier.")
        ],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new ApplicationMessageRequirement(
                MessageKeys.Room.Access.OpenRequest,
                Direction.Out,
                ApplicationMessageRole.Send)
        ],
        tool_hints: new(false, true, false, true));

    private static ApplicationDescriptor LeaveDescriptor() => new(
        ApplicationMemberIds.RoomLeave,
        "Leave room",
        "Requests exit from the current room.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomLeaveRequest),
        typeof(RoomLifecycleDispatchResult),
        [],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new ApplicationMessageRequirement(
                MessageKeys.Room.Lifecycle.Quit,
                Direction.Out,
                ApplicationMessageRole.Send)
        ],
        tool_hints: new(false, true, false, true));
}
