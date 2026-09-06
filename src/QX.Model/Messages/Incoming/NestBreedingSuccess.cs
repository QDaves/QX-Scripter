using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NestBreedingSuccess(Id PetId, int RarityCategory) : IParserComposer<NestBreedingSuccess>
{
    public static NestBreedingSuccess Parse(in PacketReader p) => new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteId(PetId);
        p.WriteInt(RarityCategory);
    }
}
