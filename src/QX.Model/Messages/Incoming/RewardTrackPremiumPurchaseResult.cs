using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RewardTrackPremiumPurchaseResult(string TrackId, int ResultCode, int Points)
    : IParserComposer<RewardTrackPremiumPurchaseResult>
{
    public static RewardTrackPremiumPurchaseResult Parse(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(TrackId);
        p.WriteInt(ResultCode);
        p.WriteInt(Points);
    }
}
