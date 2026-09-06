using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomUserRespectRequest(Id UserId);

public sealed record RoomRightsGrantRequest(Id UserId);

public sealed record RoomPetRespectRequest(Id PetId);

public sealed record RoomPetMountRequest(Id PetId, bool Mount = true);

public sealed record RoomPetRemoveRequest(Id PetId);

public sealed record RoomBotRemoveRequest(Id BotId);

public sealed record RoomPeopleDispatchResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
