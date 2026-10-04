using Qx.Messages;

namespace Qx.Model.Wired;

/// <summary>The area-hide editor settings decoded from the furniture integer data.</summary>
/// <param name="FurniId">The area-hide furniture ID.</param>
/// <param name="State">The original furniture state; any nonzero value means on.</param>
/// <param name="RootX">The rectangle origin X.</param>
/// <param name="RootY">The rectangle origin Y.</param>
/// <param name="Width">The rectangle width.</param>
/// <param name="Length">The rectangle length.</param>
/// <param name="Invisible">Whether the furniture is invisible.</param>
/// <param name="WallItems">Whether wall items are hidden.</param>
/// <param name="Invert">Whether the selected region is inverted.</param>
public sealed record WiredAreaHideSettings(Id FurniId, int State, int RootX, int RootY,
    int Width, int Length, bool Invisible, bool WallItems, bool Invert)
{
    /// <summary>Gets whether the client disables editing because the effect is on.</summary>
    public bool On => State != 0;

    /// <summary>Reads the eight documented furniture data slots without inventing missing values.</summary>
    /// <param name="furniId">The furniture identity.</param>
    /// <param name="values">The integer furniture data; trailing values are ignored.</param>
    /// <returns>The editor settings.</returns>
    public static WiredAreaHideSettings Read(Id furniId, IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 8) throw new ArgumentException("Area-hide furniture requires eight data values.", nameof(values));
        return new(furniId, values[0], values[1], values[2], values[3], values[4], values[5] == 1, values[6] == 1, values[7] == 1);
    }

    /// <summary>Creates the settings update; switching on or off uses the separate furniture-use command.</summary>
    /// <returns>The complete settings update.</returns>
    public SetAreaHideData ToUpdate() => new(FurniId, RootX, RootY, Width, Length, Invisible, WallItems, Invert);
}

/// <summary>Changes an area-hide rectangle and its three options.</summary>
/// <remarks>Sent as the Flash <c>SetAreaHideData</c> message.</remarks>
/// <param name="FurniId">The furniture identity.</param>
/// <param name="RootX">The rectangle origin X.</param>
/// <param name="RootY">The rectangle origin Y.</param>
/// <param name="Width">The rectangle width.</param>
/// <param name="Length">The rectangle length.</param>
/// <param name="Invisible">Whether the furniture is invisible.</param>
/// <param name="WallItems">Whether wall items are hidden.</param>
/// <param name="Invert">Whether the selected region is inverted.</param>
public sealed record SetAreaHideData(Id FurniId, int RootX, int RootY, int Width, int Length,
    bool Invisible, bool WallItems, bool Invert) : IParserComposer<SetAreaHideData>
{
    /// <summary>Reads the exact Flash editor update layout.</summary>
    /// <param name="p">The packet reader.</param>
    /// <returns>The editor update.</returns>
    public static SetAreaHideData Parse(in PacketReader p) => new(p.ReadId(), p.ReadInt(), p.ReadInt(),
        p.ReadInt(), p.ReadInt(), p.ReadBool(), p.ReadBool(), p.ReadBool());

    /// <summary>Writes the furniture ID, rectangle and option flags in client order.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        int furni_id = WiredWire.FlashId(FurniId);
        p.WriteInt(furni_id);
        p.WriteInt(RootX); p.WriteInt(RootY); p.WriteInt(Width); p.WriteInt(Length);
        p.WriteBool(Invisible); p.WriteBool(WallItems); p.WriteBool(Invert);
    }
}
