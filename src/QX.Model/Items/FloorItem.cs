using Qx.Messages;

namespace Qx.Model;

public sealed class FloorItem : Furni, IParserComposer<FloorItem>
{
    public override ItemType Type => ItemType.Floor;

    public Tile Location { get; set; }
    public int Direction { get; set; }
    public float Height { get; set; }
    public long Extra { get; set; }
    public ItemData Data { get; set; } = new EmptyItemData();

    public int X => Location.X;
    public int Y => Location.Y;
    public float Z => Location.Z;

    public int SizeX { get; set; } = 1;
    public int SizeZ { get; set; } = 1;

    public Area Area => AreaFor(SizeX, SizeZ);

    public override int State => Data.State;

    public FloorItem() { }

    public static FloorItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItem ParseFlash(in PacketReader p) => ParseItem(in p);

    private static FloorItem ParseUnity(in PacketReader p) => ParseItem(in p);

    private static FloorItem ParseItem(in PacketReader p)
    {
        Id id = p.ReadId();
        int kind = p.ReadInt();
        int x = p.ReadInt();
        int y = p.ReadInt();
        int direction = p.ReadInt();
        float z = p.ReadFloat();
        var item = new FloorItem
        {
            Id = id,
            Kind = kind,
            Direction = direction,
            Location = new Tile(x, y, z),
            Height = p.ReadFloat(),
            Extra = p.ReadId(),
            Data = p.Parse<ItemData>(),
            SecondsToExpiration = p.ReadInt(),
            Usage = (FurniUsage)p.ReadInt(),
            OwnerId = p.ReadId()
        };

        if (item.Kind < 0)
            item.Identifier = p.ReadString();

        return item;
    }

    public Area AreaFor(int width, int length)
    {
        int direction = ((Direction % 4) + 4) % 4;
        return direction == 2
            ? new Area(Location, length, width)
            : new Area(Location, width, length);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItem value, in PacketWriter p) => value.ComposeItem(in p);

    private static void ComposeUnity(FloorItem value, in PacketWriter p) => value.ComposeItem(in p);

    private void ComposeItem(in PacketWriter p)
    {
        p.WriteId(Id);
        p.WriteInt(Kind);
        p.WriteInt(Location.X);
        p.WriteInt(Location.Y);
        p.WriteInt(Direction);
        p.WriteFloat(Location.Z);
        p.WriteFloat(Height);
        p.WriteId(Extra);
        p.Compose(Data);
        p.WriteInt(SecondsToExpiration);
        p.WriteInt((int)Usage);
        p.WriteId(OwnerId);

        if (Kind < 0)
            p.WriteString(Identifier ?? "");
    }

    public override string ToString() => $"{nameof(FloorItem)}#{Id}/{Kind}";
}
