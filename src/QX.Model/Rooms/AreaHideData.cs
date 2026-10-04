using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a floor region that an area-hide furni hides, as listed in the floor plan.</summary>
/// <remarks>Received as the Flash <c>AreaHide</c> message and embedded in <c>FloorHeightmap</c>.</remarks>
/// <param name="FurniId">The identifier of the furni that defines the region.</param>
/// <param name="On">Whether the hiding is switched on.</param>
/// <param name="RootX">The x coordinate of the region's corner tile.</param>
/// <param name="RootY">The y coordinate of the region's corner tile.</param>
/// <param name="Width">The number of tiles the region spans along x.</param>
/// <param name="Length">The number of tiles the region spans along y.</param>
/// <param name="Invert">Whether the region is inverted.</param>
public readonly record struct AreaHideData(
    Id FurniId, bool On, int RootX, int RootY, int Width, int Length, bool Invert)
    : IParserComposer<AreaHideData>
{
    /// <summary>Reads an area-hide entry from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static AreaHideData Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AreaHideData ParseFlash(in PacketReader p) => new(
        p.ReadId(), p.ReadBool(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadBool());

    /// <summary>Writes the area-hide entry to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}
