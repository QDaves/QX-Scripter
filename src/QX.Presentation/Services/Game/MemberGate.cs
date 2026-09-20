namespace Qx.Presentation.Services.Game;

public sealed record MemberGate(bool Available, string Reason)
{
    public static MemberGate Open { get; } = new(true, "");
}
