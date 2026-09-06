using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GotMysteryBoxPrize(string ContentType, int ClassId) : IParserComposer<GotMysteryBoxPrize>
{
    public static GotMysteryBoxPrize Parse(in PacketReader p) => new(p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(ContentType);
        p.WriteInt(ClassId);
    }
}
