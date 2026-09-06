using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record EmeraldBalance(int Balance) : IParserComposer<EmeraldBalance>
{
    public static EmeraldBalance Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(Balance);
}
