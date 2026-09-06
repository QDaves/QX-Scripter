using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NftRewardItemClaimResult(string CollectionId, string WalletAddress, bool Success)
    : IParserComposer<NftRewardItemClaimResult>
{
    public static NftRewardItemClaimResult Parse(in PacketReader p) => new(p.ReadString(), p.ReadString(), p.ReadBool());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(CollectionId);
        p.WriteString(WalletAddress);
        p.WriteBool(Success);
    }
}
