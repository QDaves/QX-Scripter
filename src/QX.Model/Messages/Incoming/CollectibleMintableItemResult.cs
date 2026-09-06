using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CollectibleMintableItemResult(short MintResult) : IParserComposer<CollectibleMintableItemResult>
{
    public static CollectibleMintableItemResult Parse(in PacketReader p) => new(p.ReadShort());

    public void Compose(in PacketWriter p) => p.WriteShort(MintResult);
}
