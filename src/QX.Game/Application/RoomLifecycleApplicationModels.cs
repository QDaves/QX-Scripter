using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomEnterRequest(
    Id RoomId,
    string Password = "",
    long EntryPoint = -1);

public sealed record RoomLeaveRequest;

public sealed record RoomLifecycleDispatchResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
