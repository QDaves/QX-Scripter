using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record HasClaimedProductResponse(string ClaimId, bool HasClaimed)
    : IParserComposer<HasClaimedProductResponse>
{
    public static HasClaimedProductResponse Parse(in PacketReader p) => new(p.ReadString(), p.ReadBool());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(ClaimId);
        p.WriteBool(HasClaimed);
    }
}
