using Qx.Game;
using Qx.Model;

namespace Qx.Scripting;

internal sealed class FurniMetadataResolver
{
    private readonly FurniData? _data;

    public FurniMetadataResolver(FurniData? data)
    {
        _data = data;
    }

    public FurniInfo? Info(Furni item) =>
        _data?.GetInfo(item);

    public FurniInfo? Info(InventoryItem item) =>
        _data?.GetInfo(item.Type, item.Kind);

    public bool Identifier(Furni item, out string value)
    {
        if (!string.IsNullOrWhiteSpace(item.Identifier))
        {
            value = item.Identifier;
            return true;
        }
        return Metadata(Info(item)?.Identifier, out value);
    }

    public bool Identifier(InventoryItem item, out string value) =>
        Metadata(Info(item)?.Identifier, out value);

    public bool Name(Furni item, out string value) =>
        Name(Info(item), out value);

    public bool Name(InventoryItem item, out string value) =>
        Name(Info(item), out value);

    public bool Category(Furni item, out string value) =>
        Metadata(Info(item)?.Category, out value);

    public bool Category(InventoryItem item, out string value) =>
        Metadata(Info(item)?.Category, out value);

    public bool Line(Furni item, out string value) =>
        Metadata(Info(item)?.Line, out value);

    public bool Line(InventoryItem item, out string value) =>
        Metadata(Info(item)?.Line, out value);

    public Area Area(FloorItem item)
    {
        FurniInfo? info = Info(item);
        int width = PositiveDimension(info?.Width ?? item.SizeX);
        int length = PositiveDimension(info?.Length ?? item.SizeZ);
        return item.AreaFor(width, length);
    }

    private static bool Name(FurniInfo? info, out string value)
    {
        if (Metadata(info?.Name, out value))
            return true;
        return Metadata(info?.Identifier, out value);
    }

    private static bool Metadata(string? candidate, out string value)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            value = candidate;
            return true;
        }
        value = "";
        return false;
    }

    private static int PositiveDimension(int value) =>
        value > 0 ? value : 1;
}
