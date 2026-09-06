using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RewardTrackClaimResult(string TrackId, string RewardId, int ResultCode)
    : IParserComposer<RewardTrackClaimResult>
{
    public static RewardTrackClaimResult Parse(in PacketReader p) => new(p.ReadString(), p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(TrackId);
        p.WriteString(RewardId);
        p.WriteInt(ResultCode);
    }
}
