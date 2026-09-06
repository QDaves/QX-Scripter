using Qx.Messages;

namespace Qx.Model;

public readonly record struct AreaHideData(
    Id FurniId, bool On, int RootX, int RootY, int Width, int Length, bool Invert)
    : IParserComposer<AreaHideData>
{
    public static AreaHideData Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AreaHideData ParseFlash(in PacketReader p) => new(
        p.ReadId(), p.ReadBool(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadBool());

    private static AreaHideData ParseUnity(in PacketReader p) => new(
        p.ReadId(), p.ReadBool(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AreaHideData value, in PacketWriter p)
    {
        p.WriteId(value.FurniId);
        p.WriteBool(value.On);
        p.WriteInt(value.RootX);
        p.WriteInt(value.RootY);
        p.WriteInt(value.Width);
        p.WriteInt(value.Length);
        p.WriteBool(value.Invert);
    }

    private static void ComposeUnity(AreaHideData value, in PacketWriter p)
    {
        p.WriteId(value.FurniId);
        p.WriteBool(value.On);
        p.WriteInt(value.RootX);
        p.WriteInt(value.RootY);
        p.WriteInt(value.Width);
        p.WriteInt(value.Length);
        p.WriteBool(value.Invert);
    }
}
