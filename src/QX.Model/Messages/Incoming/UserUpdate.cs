using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record UserUpdate(IReadOnlyList<AvatarStatus> Updates) : IParserComposer<UserUpdate>
{
    public static UserUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UserUpdate ParseFlash(in PacketReader p) => new(p.ParseArray<AvatarStatus>());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UserUpdate value, in PacketWriter p) =>
        p.ComposeArray(value.Updates);
}
