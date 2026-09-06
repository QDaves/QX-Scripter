using System.Text.Json;
using System.Text.Json.Serialization;
using Qx.Model;

namespace Qx.Game;

public sealed record FurniInfo(
    ItemType Type,
    int Kind,
    string Identifier,
    string Name,
    int Width,
    int Length,
    string Category,
    string Line)
{
    public string ClassName { get; init; } = Identifier;
    public int Revision { get; init; }
    public int DefaultDirection { get; init; }
    public IReadOnlyList<string> PartColors { get; init; } = [];
    public string Description { get; init; } = "";
    public string AdUrl { get; init; } = "";
    public int OfferId { get; init; }
    public bool BuyOut { get; init; }
    public int RentOfferId { get; init; }
    public bool RentBuyOut { get; init; }
    public bool IsBuildersClub { get; init; }
    public int BuildersClubOfferId { get; init; }
    public bool ExcludedDynamic { get; init; }
    public string CustomParams { get; init; } = "";
    public FurniCategory SpecialType { get; init; }
    public bool CanStandOn { get; init; }
    public bool CanSitOn { get; init; }
    public bool CanLayOn { get; init; }
    public bool CanPutStuffOn { get; init; }
    public double Height { get; init; }
    public string Environment { get; init; } = "";
    public bool IsRare { get; init; }
    public bool Tradeable { get; init; }
    public bool Recyclable { get; init; }
    public bool HasIndexedColor { get; init; }
    public int ColorIndex { get; init; }

    public bool IsWalkable => CanStandOn || CanSitOn || CanLayOn;
    public bool IsUnwalkable => !IsWalkable;
}

public sealed partial class FurniData
{
    private readonly Dictionary<int, FurniInfo> _floor = [];
    private readonly Dictionary<int, FurniInfo> _wall = [];
    private readonly Dictionary<string, FurniInfo> _byIdentifier = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<FurniInfo> FloorItems => _floor.Values;
    public IReadOnlyCollection<FurniInfo> WallItems => _wall.Values;

    public FurniInfo? GetInfo(ItemType type, int kind) => type switch
    {
        ItemType.Floor => _floor.GetValueOrDefault(kind),
        ItemType.Wall => _wall.GetValueOrDefault(kind),
        _ => null
    };

    public FurniInfo? GetInfo(string identifier) => _byIdentifier.GetValueOrDefault(identifier);

    public FurniInfo? GetInfo(Furni item) =>
        item.Identifier is { Length: > 0 } id && _byIdentifier.TryGetValue(id, out FurniInfo? byId)
            ? byId
            : GetInfo(item.Type, item.Kind);

    public static FurniData LoadJson(string json)
    {
        var data = new FurniData();
        FurniJson root = JsonSerializer.Deserialize(
            json,
            FurniJsonContext.Default.FurniJson)
            ?? throw new JsonException("Furniture data is empty.");
        if (root.RoomItemTypes is null && root.WallItemTypes is null)
            throw new JsonException("Furniture data contains no item collections.");

        foreach (FurniTypeJson entry in root.RoomItemTypes?.FurniType ?? [])
            data.Add(ItemType.Floor, entry);
        foreach (FurniTypeJson entry in root.WallItemTypes?.FurniType ?? [])
            data.Add(ItemType.Wall, entry);

        return data;
    }

    private void Add(ItemType type, FurniTypeJson entry)
    {
        string identifier = entry.ClassName ?? "";
        string[] identifierParts = identifier.Split('*', 2);
        string className = identifierParts[0];
        int colorIndex = 0;
        bool hasIndexedColor = identifierParts.Length == 2 &&
            int.TryParse(identifierParts[1], out colorIndex);

        var info = new FurniInfo(
            type,
            entry.Id,
            identifier,
            entry.Name ?? "",
            Math.Max(1, entry.XDim),
            Math.Max(1, entry.YDim),
            entry.Category ?? "",
            entry.FurniLine ?? "")
        {
            ClassName = className,
            Revision = entry.Revision,
            DefaultDirection = entry.DefaultDirection,
            PartColors = entry.PartColors?.Colors?.AsReadOnly() ?? [],
            Description = entry.Description ?? "",
            AdUrl = entry.AdUrl ?? "",
            OfferId = entry.OfferId,
            BuyOut = entry.Buyout,
            RentOfferId = entry.RentOfferId,
            RentBuyOut = entry.RentBuyout,
            IsBuildersClub = entry.BuildersClub,
            BuildersClubOfferId = entry.BuildersClubOfferId,
            ExcludedDynamic = entry.ExcludedDynamic,
            CustomParams = entry.CustomParameters ?? "",
            SpecialType = (FurniCategory)entry.SpecialType,
            CanStandOn = entry.CanStandOn,
            CanSitOn = entry.CanSitOn,
            CanLayOn = entry.CanLayOn,
            CanPutStuffOn = entry.CanPutStuffOn,
            Height = entry.Height,
            Environment = entry.Environment ?? "",
            IsRare = entry.Rare,
            Tradeable = entry.Tradeable,
            Recyclable = entry.Recyclable,
            HasIndexedColor = hasIndexedColor,
            ColorIndex = colorIndex
        };

        (type == ItemType.Floor ? _floor : _wall)[info.Kind] = info;
        if (info.Identifier.Length > 0)
            _byIdentifier[info.Identifier] = info;
        if (info.ClassName.Length > 0)
            _byIdentifier.TryAdd(info.ClassName, info);
    }

    private sealed class FurniJson
    {
        [JsonPropertyName("roomitemtypes")] public FurniTypeList? RoomItemTypes { get; set; }
        [JsonPropertyName("wallitemtypes")] public FurniTypeList? WallItemTypes { get; set; }
    }

    private sealed class FurniTypeList
    {
        [JsonPropertyName("furnitype")] public List<FurniTypeJson>? FurniType { get; set; }
    }

    private sealed class FurniTypeJson
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("classname")] public string? ClassName { get; set; }
        [JsonPropertyName("revision")] public int Revision { get; set; }
        [JsonPropertyName("defaultdir")] public int DefaultDirection { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("xdim")] public int XDim { get; set; }
        [JsonPropertyName("ydim")] public int YDim { get; set; }
        [JsonPropertyName("partcolors")] public PartColorsJson? PartColors { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("adurl")] public string? AdUrl { get; set; }
        [JsonPropertyName("offerid")] public int OfferId { get; set; }
        [JsonPropertyName("buyout")] public bool Buyout { get; set; }
        [JsonPropertyName("rentofferid")] public int RentOfferId { get; set; }
        [JsonPropertyName("rentbuyout")] public bool RentBuyout { get; set; }
        [JsonPropertyName("bc")] public bool BuildersClub { get; set; }
        [JsonPropertyName("bcofferid")] public int BuildersClubOfferId { get; set; }
        [JsonPropertyName("excludeddynamic")] public bool ExcludedDynamic { get; set; }
        [JsonPropertyName("customparams")] public string? CustomParameters { get; set; }
        [JsonPropertyName("specialtype")] public int SpecialType { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("canstandon")] public bool CanStandOn { get; set; }
        [JsonPropertyName("cansiton")] public bool CanSitOn { get; set; }
        [JsonPropertyName("canlayon")] public bool CanLayOn { get; set; }
        [JsonPropertyName("canputstuffon")] public bool CanPutStuffOn { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
        [JsonPropertyName("furniline")] public string? FurniLine { get; set; }
        [JsonPropertyName("environment")] public string? Environment { get; set; }
        [JsonPropertyName("rare")] public bool Rare { get; set; }
        [JsonPropertyName("tradeable")] public bool Tradeable { get; set; }
        [JsonPropertyName("recyclable")] public bool Recyclable { get; set; }
    }

    private sealed class PartColorsJson
    {
        [JsonPropertyName("color")] public List<string>? Colors { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(FurniJson))]
    private sealed partial class FurniJsonContext : JsonSerializerContext;
}
