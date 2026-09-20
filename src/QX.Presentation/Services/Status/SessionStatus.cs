namespace Qx.Presentation.Services.Status;

public sealed record SessionStatus(
    bool IsGEarthConnected,
    int GEarthPort,
    bool IsGameConnected,
    string ClientName,
    string HotelVersion,
    bool IsInRoom,
    long RoomId,
    string RoomName,
    int UserCount,
    int BotCount,
    int PetCount,
    int FloorItemCount,
    int WallItemCount,
    string? UserName,
    bool IsMcpRunning,
    int McpPort,
    string McpFailure,
    DateTime? McpLastRequestUtc,
    int RunningCount)
{
    public int FurniCount => FloorItemCount + WallItemCount;

    public string ClientTooltip => HotelVersion.Length == 0 ? ClientName : $"{ClientName} build {HotelVersion}";

    public string RoomTooltip => IsInRoom ? $"{RoomName}, id {RoomId}. Click to copy the id." : "Not in a room.";

    public string RunningText => RunningCount == 1 ? "1 running" : $"{RunningCount} running";

    public bool HasMcpFailure => !IsMcpRunning && McpFailure.Length > 0;
}
