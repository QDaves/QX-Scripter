using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a furni standing on the room floor.</summary>
public sealed class FloorItem : Furni, IParserComposer<FloorItem>
{
    /// <summary>Gets the item type, which is always <see cref="ItemType.Floor"/>.</summary>
    public override ItemType Type => ItemType.Floor;

    /// <summary>Gets or sets the tile the item's anchor sits on, including its stack height.</summary>
    public Tile Location { get; set; }
    /// <summary>Gets or sets the direction the item faces, from 0 (north) to 7, clockwise.</summary>
    public int Direction { get; set; }
    /// <summary>Gets or sets the item's own height in tile units, as sent with the item.</summary>
    public float Height { get; set; }
    /// <summary>Gets or sets the extra value the hotel sends with the item.</summary>
    public long Extra { get; set; }
    /// <summary>Gets or sets the item's payload, which holds its state and any game data.</summary>
    public ItemData Data { get; set; } = new EmptyItemData();

    /// <summary>Gets the x coordinate of <see cref="Location"/>.</summary>
    public int X => Location.X;
    /// <summary>Gets the y coordinate of <see cref="Location"/>.</summary>
    public int Y => Location.Y;
    /// <summary>Gets the stack height of <see cref="Location"/> in tile units.</summary>
    public float Z => Location.Z;
    /// <summary>Gets the x and y coordinates of <see cref="Location"/>.</summary>
    public Point XY => Location.XY;

    /// <summary>Gets or sets the item's width along x in tiles before rotation.</summary>
    /// <remarks>Filled from the furni definitions when they are loaded; defaults to 1.</remarks>
    public int SizeX { get; set; } = 1;
    /// <summary>Gets or sets the item's length along y in tiles before rotation.</summary>
    /// <remarks>Filled from the furni definitions when they are loaded; defaults to 1.</remarks>
    public int SizeZ { get; set; } = 1;

    /// <summary>Gets the tiles the item covers, rotated for its direction.</summary>
    /// <remarks>The result of <see cref="AreaFor"/> with <see cref="SizeX"/> and <see cref="SizeZ"/>.</remarks>
    public Area Area => AreaFor(SizeX, SizeZ);

    /// <summary>Gets the tile directly in front of the item, in the direction it faces.</summary>
    public Point Front => Location.XY.Step(Direction);

    /// <summary>Gets the item's state, taken from <see cref="ItemData.State"/> of <see cref="Data"/>.</summary>
    public override int State => Data.State;

    /// <summary>Initializes a new instance of the <see cref="FloorItem"/> class.</summary>
    public FloorItem() { }

    /// <summary>Reads a floor item from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static FloorItem Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FloorItem ParseFlash(in PacketReader p) => ParseItem(in p);

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

    /// <summary>Gets the tiles an item of the given size covers at this item's location and direction.</summary>
    /// <remarks>
    /// Width and length are swapped when the direction, taken modulo 4, is 2, so an item facing
    /// east or west is rotated.
    /// </remarks>
    /// <param name="width">The width along x in tiles before rotation.</param>
    /// <param name="length">The length along y in tiles before rotation.</param>
    /// <returns>The covered area, anchored at <see cref="Location"/>.</returns>
    public Area AreaFor(int width, int length)
    {
        int direction = ((Direction % 4) + 4) % 4;
        return direction == 2
            ? new Area(Location, length, width)
            : new Area(Location, width, length);
    }

    /// <summary>Writes the floor item to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FloorItem value, in PacketWriter p) => value.ComposeItem(in p);

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

    /// <summary>Returns the item's identifier and kind.</summary>
    /// <returns>A string in the form <c>FloorItem#Id/Kind</c>.</returns>
    public override string ToString() => $"{nameof(FloorItem)}#{Id}/{Kind}";
}
