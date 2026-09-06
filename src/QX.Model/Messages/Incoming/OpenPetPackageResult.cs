using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record OpenPetPackageResult(Id ObjectId, int NameValidationStatus, string NameValidationInfo)
    : IParserComposer<OpenPetPackageResult>
{
    public static OpenPetPackageResult Parse(in PacketReader p) => new(p.ReadId(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteId(ObjectId);
        p.WriteInt(NameValidationStatus);
        p.WriteString(NameValidationInfo);
    }
}
