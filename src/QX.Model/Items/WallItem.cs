using Qx;
using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a furni hanging on a room wall.</summary>
public sealed class WallItem : Furni, IParserComposer<WallItem>
{
    /// <summary>Gets the item type, which is always <see cref="ItemType.Wall"/>.</summary>
    public override ItemType Type => ItemType.Wall;

    /// <summary>Gets or sets where on the wall the item hangs.</summary>
    public WallLocation Location { get; set; } = WallLocation.Zero;
    /// <summary>Gets or sets the item's data string.</summary>
    public string Data { get; set; } = "";

    /// <summary>Gets the x coordinate of the wall tile the item hangs on.</summary>
    public int WX => Location.Wall.X;
    /// <summary>Gets the y coordinate of the wall tile the item hangs on.</summary>
    public int WY => Location.Wall.Y;
    /// <summary>Gets the x offset of the item on its wall tile.</summary>
    public int LX => Location.Offset.X;
    /// <summary>Gets the y offset of the item on its wall tile.</summary>
    public int LY => Location.Offset.Y;
    /// <summary>Gets whether the item hangs on a left or a right wall.</summary>
    public WallOrientation Orientation => Location.Orientation;

    /// <summary>Gets <see cref="Data"/> as an integer, or -1 when it is not one.</summary>
    public override int State => int.TryParse(Data, out int state) ? state : -1;

    /// <summary>Initializes a new instance of the <see cref="WallItem"/> class.</summary>
    public WallItem() { }

    /// <summary>Reads a wall item from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when the item identifier is not a valid integer.</exception>
    /// <exception cref="FormatException">Thrown when the wall location string is not valid.</exception>
    public static WallItem Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItem ParseFlash(in PacketReader p) => ParseItem(
        in p,
        RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)));

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

    /// <summary>Writes the wall item to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItem value, in PacketWriter p)
    {
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
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

    /// <summary>Returns the item's identifier and kind.</summary>
    /// <returns>A string in the form <c>WallItem#Id/Kind</c>.</returns>
    public override string ToString() => $"{nameof(WallItem)}#{Id}/{Kind}";
}
