using Qx.Messages;

namespace Qx.Model.Wired;

public enum WiredLabelVisibility { Everyone, WiredInspectors }

public sealed record WiredRegionArea(int X, int Y, int Width, int Height);

public sealed record WiredRegion(string Description, int OffsetX, int OffsetY, int MinimumWidth,
    int MinimumHeight, string Color, IReadOnlyList<WiredRegionArea> Areas);

public sealed record WiredFurnitureLabel(int FurnitureId, string Text, int Color = 4278912,
    WiredLabelVisibility Visibility = WiredLabelVisibility.Everyone, bool AlwaysOpen = false,
    WiredRegion? Region = null) : IParserComposer<WiredFurnitureLabel>
{
    public static WiredFurnitureLabel Parse(in PacketReader p)
    {
        int type = p.ReadInt(), id = p.ReadInt();
        string text = p.ReadString();
        if (type == 0)
            return new(id, text, p.ReadInt(), (WiredLabelVisibility)p.ReadInt(), p.ReadBool());
        if (type != 1) throw new InvalidDataException($"Unknown furniture label type {type}.");
        string description = p.ReadString();
        int x = p.ReadInt(), y = p.ReadInt(), width = p.ReadInt(), height = p.ReadInt();
        string color = p.ReadString();
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, 16, nameof(WiredRegion.Areas));
        var areas = new WiredRegionArea[count];
        for (int index = 0; index < count; index++)
            areas[index] = new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt());
        return new(id, text, Region: new(description, x, y, width, height, color, Array.AsReadOnly(areas)));
    }

    public void Compose(in PacketWriter p)
    {
        WiredWire.RequireString(Text, nameof(Text), in p);
        if (Region is { } region)
        {
            WiredWire.RequireString(region.Description, nameof(region.Description), in p);
            WiredWire.RequireString(region.Color, nameof(region.Color), in p);
            ArgumentNullException.ThrowIfNull(region.Areas);
            if (region.Areas.Any(area => area is null)) throw new ArgumentException("A region area is null.", nameof(Region));
        }
        else if (!Enum.IsDefined(Visibility)) throw new ArgumentOutOfRangeException(nameof(Visibility));
        p.WriteInt(Region is null ? 0 : 1);
        p.WriteInt(FurnitureId);
        p.WriteString(Text);
        if (Region is null)
        {
            p.WriteInt(Color);
            p.WriteInt((int)Visibility);
            p.WriteBool(AlwaysOpen);
            return;
        }
        p.WriteString(Region.Description);
        p.WriteInt(Region.OffsetX);
        p.WriteInt(Region.OffsetY);
        p.WriteInt(Region.MinimumWidth);
        p.WriteInt(Region.MinimumHeight);
        p.WriteString(Region.Color);
        p.WriteInt(Region.Areas.Count);
        foreach (var area in Region.Areas)
        {
            p.WriteInt(area.X);
            p.WriteInt(area.Y);
            p.WriteInt(area.Width);
            p.WriteInt(area.Height);
        }
    }
}

public sealed record FurnitureLabelsAndRegions(IReadOnlyList<WiredFurnitureLabel> Entries)
    : IParserComposer<FurnitureLabelsAndRegions>
{
    public static FurnitureLabelsAndRegions Parse(in PacketReader p) => new(read(in p));

    internal static IReadOnlyList<WiredFurnitureLabel> read(in PacketReader p)
    {
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, 10, nameof(Entries));
        var entries = new WiredFurnitureLabel[count];
        for (int index = 0; index < count; index++) entries[index] = p.Parse<WiredFurnitureLabel>();
        return Array.AsReadOnly(entries);
    }

    internal static void write(IReadOnlyList<WiredFurnitureLabel> entries, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Any(entry => entry is null)) throw new ArgumentException("A furniture label is null.", nameof(entries));
        p.WriteInt(entries.Count);
        foreach (var entry in entries) p.Compose(entry);
    }

    public void Compose(in PacketWriter p) => write(Entries, in p);
}

public sealed record FurnitureLabelsAndRegionsUpdate(IReadOnlyList<WiredFurnitureLabel> AddedOrUpdated,
    IReadOnlyList<int> RemovedIds) : IParserComposer<FurnitureLabelsAndRegionsUpdate>
{
    public static FurnitureLabelsAndRegionsUpdate Parse(in PacketReader p)
    {
        var added = FurnitureLabelsAndRegions.read(in p);
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, 4, nameof(RemovedIds));
        var removed = new int[count];
        for (int index = 0; index < count; index++) removed[index] = p.ReadInt();
        return new(added, Array.AsReadOnly(removed));
    }

    public void Compose(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(RemovedIds);
        FurnitureLabelsAndRegions.write(AddedOrUpdated, in p);
        p.WriteInt(RemovedIds.Count);
        foreach (int id in RemovedIds) p.WriteInt(id);
    }
}

public sealed record SetFurnitureLabelData(int FurnitureId, string Text) : IParserComposer<SetFurnitureLabelData>
{
    public static SetFurnitureLabelData Parse(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        WiredWire.RequireString(Text, nameof(Text), in p);
        p.WriteInt(FurnitureId);
        p.WriteString(Text);
    }
}

public sealed record SetFurnitureLabelSettings(int FurnitureId, int Color, WiredLabelVisibility Visibility,
    bool AlwaysOpen) : IParserComposer<SetFurnitureLabelSettings>
{
    public static SetFurnitureLabelSettings Parse(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), (WiredLabelVisibility)p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p)
    {
        if (!Enum.IsDefined(Visibility)) throw new ArgumentOutOfRangeException(nameof(Visibility));
        if (Color is < 0 or > 0xffffff) throw new ArgumentOutOfRangeException(nameof(Color));
        p.WriteInt(FurnitureId);
        p.WriteInt(Color);
        p.WriteInt((int)Visibility);
        p.WriteBool(AlwaysOpen);
    }
}
