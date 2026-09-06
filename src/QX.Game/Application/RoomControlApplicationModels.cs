using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomDoorbellAnswerRequest(string UserName, bool Allow = true);

public sealed record RoomHandItemDropRequest;

public sealed record RoomHandItemPassRequest(Id UserId);

public sealed record RoomRatingRequest(int Rating);

public sealed record RoomStaffPickRequest(Id RoomId, bool Pick = true);

public sealed record RoomControlDispatchResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
