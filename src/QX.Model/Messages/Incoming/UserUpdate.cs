using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record UserUpdate(IReadOnlyList<AvatarStatus> Updates) : IParserComposer<UserUpdate>
{
    public static UserUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserUpdate ParseFlash(in PacketReader p) => new(p.ParseArray<AvatarStatus>());

    private static UserUpdate ParseUnity(in PacketReader p) => new(p.ParseArray<AvatarStatus>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserUpdate value, in PacketWriter p) =>
        p.ComposeArray(value.Updates);

    private static void ComposeUnity(UserUpdate value, in PacketWriter p) =>
        p.ComposeArray(value.Updates);
}
