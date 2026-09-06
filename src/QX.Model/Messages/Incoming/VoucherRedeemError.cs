using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record VoucherRedeemError(string ErrorCode) : IParserComposer<VoucherRedeemError>
{
    public static VoucherRedeemError Parse(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) => p.WriteString(ErrorCode);
}
