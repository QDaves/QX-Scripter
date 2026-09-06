using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NftStorePurchase(short Result) : IParserComposer<NftStorePurchase>
{
    public static NftStorePurchase Parse(in PacketReader p) => new(p.ReadShort());

    public void Compose(in PacketWriter p) => p.WriteShort(Result);
}
