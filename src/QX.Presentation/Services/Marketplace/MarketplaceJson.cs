using System.Text.Json.Serialization;

namespace Qx.Presentation.Services.Marketplace;

internal sealed record MarketplaceBatchRequest(
    [property: JsonPropertyName("roomItems")] IReadOnlyList<string> RoomItems,
    [property: JsonPropertyName("wallItems")] IReadOnlyList<string> WallItems);

internal sealed record MarketplaceBatchResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("roomItemData")] IReadOnlyList<MarketplaceItemStats>? RoomItemData,
    [property: JsonPropertyName("wallItemData")] IReadOnlyList<MarketplaceItemStats>? WallItemData);

internal sealed record MarketplaceItemStats(
    [property: JsonPropertyName("item")] string? Item,
    [property: JsonPropertyName("currentPrice")] int CurrentPrice,
    [property: JsonPropertyName("averagePrice")] int AveragePrice,
    [property: JsonPropertyName("currentOpenOffers")] int CurrentOpenOffers,
    [property: JsonPropertyName("totalOpenOffers")] int TotalOpenOffers,
    [property: JsonPropertyName("soldItemCount")] int SoldItemCount,
    [property: JsonPropertyName("history")] IReadOnlyList<MarketplaceHistoryPoint>? History);

internal sealed record MarketplaceHistoryPoint(
    [property: JsonPropertyName("dayOffset")] string? DayOffset,
    [property: JsonPropertyName("averagePrice")] string? AveragePrice,
    [property: JsonPropertyName("totalSoldItems")] string? TotalSoldItems,
    [property: JsonPropertyName("totalCreditSum")] string? TotalCreditSum);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MarketplaceBatchRequest))]
[JsonSerializable(typeof(MarketplaceBatchResponse))]
internal sealed partial class MarketplaceJson : JsonSerializerContext;
