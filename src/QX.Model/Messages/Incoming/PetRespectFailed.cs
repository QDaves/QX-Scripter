using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PetRespectFailed(int RequiredDays, int AvatarAgeInDays) : IParserComposer<PetRespectFailed>
{
    public static PetRespectFailed Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(RequiredDays);
        p.WriteInt(AvatarAgeInDays);
    }
}
