using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record TryVerificationCodeResult(int ResultCode, int MillisecondsToAllowProcessReset)
    : IParserComposer<TryVerificationCodeResult>
{
    public static TryVerificationCodeResult Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(ResultCode);
        p.WriteInt(MillisecondsToAllowProcessReset);
    }
}
