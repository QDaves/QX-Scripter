using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record ClaimProductResult(string ClaimId, int Result) : IParserComposer<ClaimProductResult>
{
    public static ClaimProductResult Parse(in PacketReader p) => new(p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(ClaimId);
        p.WriteInt(Result);
    }
}
