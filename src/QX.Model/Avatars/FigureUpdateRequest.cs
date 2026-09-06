using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record FigureUpdateRequest(string Gender, string Figure)
    : IParserComposer<FigureUpdateRequest>
{
    public static FigureUpdateRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FigureUpdateRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    private static FigureUpdateRequest ParseUnity(in PacketReader p)
    {
        string gender = p.ReadString();
        ValidateUnityGender(gender);
        return new FigureUpdateRequest(gender, p.ReadString());
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FigureUpdateRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteString(value.Gender);
        p.WriteString(value.Figure);
    }

    private static void ComposeUnity(FigureUpdateRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        ValidateUnityGender(value.Gender);
        p.WriteString(value.Gender);
        p.WriteString(value.Figure);
    }

    private static void ValidateStrings(FigureUpdateRequest value, in PacketWriter p)
    {
        ValidateString(value.Gender, nameof(Gender), in p);
        ValidateString(value.Figure, nameof(Figure), in p);
    }

    private static void ValidateString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("String exceeds the protocol limit.", name);
    }

    private static void ValidateUnityGender(string gender)
    {
        if (gender.Length != 1)
            throw new ArgumentException("Unity gender must contain exactly one character.", nameof(gender));
    }
}
