namespace Qx.Presentation.Services.Game;

public sealed class GameUnavailableException(string member_id, string reason, Exception inner)
    : Exception(reason, inner)
{
    public string MemberId { get; } = member_id;
}
