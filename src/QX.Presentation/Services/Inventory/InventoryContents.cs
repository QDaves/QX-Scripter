using Qx.Model;

namespace Qx.Presentation.Services.Inventory;

public readonly record struct InventoryItemKind(ItemType? Type, long Value);

public sealed record InventoryEntry(
    InventoryItemKind Key,
    string Name,
    string Identifier,
    string Detail,
    string Group,
    ItemType? Type,
    int Count,
    IReadOnlyList<Id> ItemIds,
    bool Tradeable,
    Id ItemId,
    string? ImageUrl)
{
    public bool CanSell => Tradeable && Type is not null && Identifier.Length > 0;
}

public sealed record InventoryContents(
    bool Connected,
    bool Consistent,
    bool Loaded,
    int Total,
    int Kinds,
    int Pets,
    IReadOnlyList<InventoryEntry> Entries)
{
    public static InventoryContents Disconnected { get; } = new(false, false, false, 0, 0, 0, []);
}
