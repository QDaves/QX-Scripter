using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record HabboBroadcast(string MessageText) : IParserComposer<HabboBroadcast>
{
    public static HabboBroadcast Parse(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) => p.WriteString(MessageText);
}
