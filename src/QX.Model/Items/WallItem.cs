using Qx;
using Qx.Messages;

namespace Qx.Model;

public sealed class WallItem : Furni, IParserComposer<WallItem>
{
    public override ItemType Type => ItemType.Wall;

    public WallLocation Location { get; set; } = WallLocation.Zero;
    public string Data { get; set; } = "";

    public int WX => Location.Wall.X;
    public int WY => Location.Wall.Y;
    public int LX => Location.Offset.X;
    public int LY => Location.Offset.Y;
    public WallOrientation Orientation => Location.Orientation;

    public override int State => int.TryParse(Data, out int state) ? state : -1;

    public WallItem() { }

    public static WallItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WallItem ParseFlash(in PacketReader p) => ParseItem(
        in p,
        RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)));

    private static WallItem ParseUnity(in PacketReader p) => ParseItem(in p, p.ReadId());

    private static WallItem ParseItem(in PacketReader p, Id id)
    {
        return new WallItem
        {
            Id = id,
            Kind = p.ReadInt(),
            Location = WallLocation.ParseString(p.ReadString()),
            Data = p.ReadString(),
            SecondsToExpiration = p.ReadInt(),
            Usage = (FurniUsage)p.ReadInt(),
            OwnerId = p.ReadId()
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WallItem value, in PacketWriter p)
    {
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
        value.ComposeItem(in p);
    }

    private static void ComposeUnity(WallItem value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        value.ComposeItem(in p);
    }

    private void ComposeItem(in PacketWriter p)
    {
        p.WriteInt(Kind);
        p.WriteString(Location.ToString());
        p.WriteString(Data);
        p.WriteInt(SecondsToExpiration);
        p.WriteInt((int)Usage);
        p.WriteId(OwnerId);
    }

    public override string ToString() => $"{nameof(WallItem)}#{Id}/{Kind}";
}
