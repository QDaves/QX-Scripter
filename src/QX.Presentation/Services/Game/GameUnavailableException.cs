namespace Qx.Presentation.Services.Game;

public sealed class GameUnavailableException(string memberId, string reason, Exception inner)
    : Exception(reason, inner)
{
    public string MemberId { get; } = memberId;
}
