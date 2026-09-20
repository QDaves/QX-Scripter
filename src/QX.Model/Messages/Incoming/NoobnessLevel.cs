using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NoobnessLevel(int Level) : IParserComposer<NoobnessLevel>
{
    public static NoobnessLevel Parse(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Level);
    }
}
