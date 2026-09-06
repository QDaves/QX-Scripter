using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomAvatarWalkRequest(int X, int Y);

public sealed record RoomAvatarLookRequest(int X, int Y);

public sealed record RoomAvatarDanceRequest(int Style);

public sealed record RoomAvatarExpressionRequest(int Expression);

public sealed record RoomAvatarPostureRequest(int Posture);

public sealed record RoomAvatarSignRequest(int Sign);

public sealed record RoomAvatarEffectRequest(int Effect);

public sealed record RoomAvatarTypingRequest(bool Active);

public sealed record RoomAvatarDispatchResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
