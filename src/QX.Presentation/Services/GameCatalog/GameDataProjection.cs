using Qx.Game;
using Qx.Model;
using Qx.Presentation.Services.FurniLookup;
using Qx.Presentation.Services.Images;

namespace Qx.Presentation.Services.GameCatalog;

public sealed record FurniSnapshot(
    ItemType Type,
    int Kind,
    string Name,
    string Identifier,
    string Line,
    string Category,
    string Description,
    string? IconUrl);

public sealed record KeyValueEntry(string Key, string Value);

public static class GameDataProjection
{
    public static IReadOnlyList<FurniSnapshot> Furni(FurniData? furni, ExternalTexts? texts)
    {
        if (furni is null)
            return [];
        Func<string, string?> lookup = Lookup(texts);
        List<FurniSnapshot> rows = new(furni.FloorItems.Count + furni.WallItems.Count);
        foreach (FurniInfo info in furni.FloorItems)
            rows.Add(Row(info, ItemType.Floor, lookup));
        foreach (FurniInfo info in furni.WallItems)
            rows.Add(Row(info, ItemType.Wall, lookup));
        return rows;
    }

    public static IReadOnlyList<KeyValueEntry> Texts(ExternalTexts? texts) =>
        texts is null
            ? []
            : [.. texts.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new KeyValueEntry(entry.Key, entry.Value))];

    public static IReadOnlyList<KeyValueEntry> Variables(ExternalVariables? variables) =>
        variables is null
            ? []
            : [.. variables.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new KeyValueEntry(entry.Key, entry.Value))];

    public static IReadOnlyList<KeyValueEntry> Products(ProductData? products) =>
        products is null
            ? []
            : [.. products.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new KeyValueEntry(entry.Key, Describe(entry.Value)))];

    public static string Failure(string? status) =>
        status is not null && status.StartsWith("game data load failed", StringComparison.OrdinalIgnoreCase) ? status : "";

    public static string Describe(ProductInfo product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return product.Description.Length > 0 ? $"{product.Name} — {product.Description}" : product.Name;
    }

    static Func<string, string?> Lookup(ExternalTexts? texts) =>
        key => texts is not null && texts.TryGet(key, out string value) && value.Length > 0 ? value : null;

    static FurniSnapshot Row(FurniInfo info, ItemType type, Func<string, string?> texts) =>
        new(
            type,
            info.Kind,
            FurniText.Named(info.Identifier, info.Name, texts),
            info.Identifier,
            FurniText.Display(info.Line),
            FurniText.Display(info.Category),
            FurniText.DescriptionOf(info.Description, texts),
            HabboUrls.FurniIcon(info.Revision, info.Identifier));
}
