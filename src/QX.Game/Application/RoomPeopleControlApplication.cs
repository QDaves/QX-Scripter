using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class RoomPeopleControlApplication : IApplicationFeature
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomPeopleControlApplication(
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
            Call<RoomUserRespectRequest>(UserRespectDescriptor(), RespectUser),
            Call<RoomRightsGrantRequest>(RightsGrantDescriptor(), GrantRights),
            Call<RoomPetRespectRequest>(PetRespectDescriptor(), RespectPet),
            Call<RoomPetMountRequest>(PetMountDescriptor(), MountPet),
            Call<RoomPetRemoveRequest>(PetRemoveDescriptor(), RemovePet),
            Call<RoomBotRemoveRequest>(BotRemoveDescriptor(), RemoveBot)
        ]);
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);

    private RoomPeopleDispatchResult RespectUser(
        RoomUserRespectRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.People.Respect(
                request.UserId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomPeopleDispatchResult GrantRights(
        RoomRightsGrantRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.People.GiveRights(
                request.UserId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomPeopleDispatchResult RespectPet(
        RoomPetRespectRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.People.RespectPet(
                request.PetId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomPeopleDispatchResult MountPet(
        RoomPetMountRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.People.MountPet(
                request.PetId,
                request.Mount,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomPeopleDispatchResult RemovePet(
        RoomPetRemoveRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.People.RemovePet(
                request.PetId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomPeopleDispatchResult RemoveBot(
        RoomBotRemoveRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.People.RemoveBot(
                request.BotId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomPeopleDispatchResult Dispatch(
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
        return new RoomPeopleDispatchResult(
            session.Client,
            room.RoomId,
            room.Generation,
            true,
            false,
            time_provider.GetUtcNow());
    }

    private static ApplicationCallBinding<TRequest, RoomPeopleDispatchResult> Call<TRequest>(
        ApplicationDescriptor descriptor,
        Func<TRequest, CancellationToken, RoomPeopleDispatchResult> invocation) => new(
            descriptor,
            (request, cancellation_token) =>
            {
                ArgumentNullException.ThrowIfNull(request);
                return ValueTask.FromResult(invocation(request, cancellation_token));
            });

    private static ApplicationDescriptor UserRespectDescriptor() => Descriptor<RoomUserRespectRequest>(
        ApplicationMemberIds.RoomPeopleRespect,
        "Respect room user",
        "Gives a respect to a user in the current room.",
        [new("user_id", typeof(Id), true, null, "Target user identifier.")],
        MessageKeys.Room.Occupants.RespectRequest);

    private static ApplicationDescriptor RightsGrantDescriptor() => Descriptor<RoomRightsGrantRequest>(
        ApplicationMemberIds.RoomPeopleRightsGrant,
        "Grant room rights",
        "Grants room rights to a user in the current room.",
        [new("user_id", typeof(Id), true, null, "Target user identifier.")],
        MessageKeys.Room.Authority.ControllerGrantRequest);

    private static ApplicationDescriptor PetRespectDescriptor() => Descriptor<RoomPetRespectRequest>(
        ApplicationMemberIds.RoomPetRespect,
        "Respect room pet",
        "Gives a respect to a pet in the current room.",
        [new("pet_id", typeof(Id), true, null, "Target pet identifier.")],
        MessageKeys.Room.Occupants.Pet.RespectRequest);

    private static ApplicationDescriptor PetMountDescriptor() => Descriptor<RoomPetMountRequest>(
        ApplicationMemberIds.RoomPetMountSet,
        "Set pet mount",
        "Mounts or dismounts a pet in the current room.",
        [
            new("pet_id", typeof(Id), true, null, "Target pet identifier."),
            new("mount", typeof(bool), false, true, "Whether the pet is mounted.")
        ],
        MessageKeys.Room.Occupants.Pet.MountRequest);

    private static ApplicationDescriptor PetRemoveDescriptor() => Descriptor<RoomPetRemoveRequest>(
        ApplicationMemberIds.RoomPetRemove,
        "Remove room pet",
        "Returns a pet from the current room to its inventory.",
        [new("pet_id", typeof(Id), true, null, "Target pet identifier.")],
        MessageKeys.Room.Occupants.Pet.RemoveRequest);

    private static ApplicationDescriptor BotRemoveDescriptor() => Descriptor<RoomBotRemoveRequest>(
        ApplicationMemberIds.RoomBotRemove,
        "Remove room bot",
        "Returns a bot from the current room to its inventory.",
        [new("bot_id", typeof(Id), true, null, "Target bot identifier.")],
        MessageKeys.Room.Occupants.Bot.RemoveRequest);

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
            typeof(RoomPeopleDispatchResult),
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
