using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NoobnessLevel(int Level) : IParserComposer<NoobnessLevel>
{
    public static NoobnessLevel Parse(in PacketReader p) =>
        new(p.Client is ClientType.Unity ? p.ReadShort() : p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        if (p.Client is ClientType.Unity)
            p.WriteShort(checked((short)Level));
        else
            p.WriteInt(Level);
    }
}
