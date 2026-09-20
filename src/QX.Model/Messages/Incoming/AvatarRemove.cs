using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record AvatarRemove(int Index) : IParserComposer<AvatarRemove>
{
    public static AvatarRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarRemove ParseFlash(in PacketReader p) =>
        new(int.Parse(p.ReadString(), System.Globalization.CultureInfo.InvariantCulture));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarRemove value, in PacketWriter p) =>
        p.WriteString(value.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
