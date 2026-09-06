using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record AvatarRemove(int Index) : IParserComposer<AvatarRemove>
{
    public static AvatarRemove Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarRemove ParseFlash(in PacketReader p) =>
        new(int.Parse(p.ReadString(), System.Globalization.CultureInfo.InvariantCulture));

    private static AvatarRemove ParseUnity(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarRemove value, in PacketWriter p) =>
        p.WriteString(value.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void ComposeUnity(AvatarRemove value, in PacketWriter p) =>
        p.WriteInt(value.Index);
}
