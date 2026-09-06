using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record SpecialRoomEffect(int EffectId) : IParserComposer<SpecialRoomEffect>
{
    public static SpecialRoomEffect Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(EffectId);
}
