using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record LatencyPingResponse(int RequestId) : IParserComposer<LatencyPingResponse>
{
    public static LatencyPingResponse Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(RequestId);
}
