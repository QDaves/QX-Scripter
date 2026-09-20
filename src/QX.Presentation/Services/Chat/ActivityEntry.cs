namespace Qx.Presentation.Services.Chat;

public enum ActivityKind
{
    Entered,
    Arrived,
    Left,
    Trade
}

public sealed record ActivityEntry(
    long Sequence,
    DateTimeOffset At,
    ActivityKind Kind,
    string Text,
    long RoomGeneration,
    long RoomId);
