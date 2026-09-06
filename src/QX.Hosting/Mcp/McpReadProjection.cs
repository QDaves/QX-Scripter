using System.Text.Json.Serialization;
using Qx.Game.Snapshots;

namespace Qx.Hosting;

internal static class McpReadProjection
{
    public static QueryEnvelope<InventorySnapshot> InventoryDetails(
        QueryEnvelope<InventorySnapshot> source,
        int limit)
    {
        if (source.Data is not { } data)
            return source;

        int count = Math.Min(data.Items.Count, limit);
        bool truncated = data.Truncated || count < data.Items.Count;
        InventorySnapshot projected = data with
        {
            Returned = count,
            MaxItems = limit,
            Truncated = truncated,
            Items = data.Items.Take(count).ToArray()
        };
        return source with
        {
            Metadata = source.Metadata with { Truncated = source.Metadata.Truncated || truncated },
            Data = projected
        };
    }

    public static QueryEnvelope<McpInventorySnapshot> Inventory(
        QueryEnvelope<InventorySnapshot> source,
        int limit)
    {
        if (source.Data is not { } data)
            return Convert(source, default(McpInventorySnapshot));

        InventoryItemSnapshot[] items = data.Items.Take(limit).ToArray();
        McpInventoryGroup[] groups = items
            .GroupBy(item => (item.Type, item.Kind))
            .Select(group =>
            {
                FurniDefinitionSnapshot? definition = group
                    .Select(item => item.Definition)
                    .FirstOrDefault(value => value is not null);
                return new McpInventoryGroup(
                    group.Key.Type,
                    group.Key.Kind,
                    definition?.Identifier,
                    definition?.Name,
                    group.Count(),
                    group.Select(item => item.ItemId).ToArray(),
                    group.Select(item => item.Id).Distinct().ToArray());
            })
            .OrderBy(group => group.Type, StringComparer.Ordinal)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Kind)
            .ToArray();
        bool truncated = data.Truncated || items.Length < data.Items.Count;
        var projected = new McpInventorySnapshot(
            data.DefinitionsLoaded,
            data.IsLoading,
            data.IsStale,
            data.Generation,
            data.ExpectedFragments,
            data.ReceivedFragments,
            data.Total,
            items.Length,
            limit,
            truncated,
            groups);
        return Convert(
            source,
            projected,
            source.Metadata with { Truncated = source.Metadata.Truncated || truncated });
    }

    public static QueryEnvelope<McpFriendDetailsSnapshot> FriendDetails(
        QueryEnvelope<FriendCollectionSnapshot> source,
        int limit)
    {
        if (source.Data is not { } data)
            return Convert(source, default(McpFriendDetailsSnapshot));

        FriendSnapshot[] friends = data.Friends.Take(limit).ToArray();
        bool truncated = source.Metadata.Truncated || friends.Length < data.Friends.Count;
        var projected = new McpFriendDetailsSnapshot(
            data.Total,
            data.Online,
            friends.Length,
            limit,
            truncated,
            data.UserLimit,
            data.NormalLimit,
            data.ExtendedLimit,
            data.Categories,
            friends);
        return Convert(
            source,
            projected,
            source.Metadata with { Truncated = truncated });
    }

    public static QueryEnvelope<McpFriendCollectionSnapshot> Friends(
        QueryEnvelope<FriendCollectionSnapshot> source,
        int limit)
    {
        if (source.Data is not { } data)
            return Convert(source, default(McpFriendCollectionSnapshot));

        McpFriend[] friends = data.Friends
            .Take(limit)
            .Select(friend => new McpFriend(
                friend.Id,
                friend.Name,
                friend.IsOnline,
                friend.CanFollow,
                friend.CategoryId,
                EmptyToNull(friend.Relation),
                friend.LastOnline))
            .ToArray();
        bool truncated = source.Metadata.Truncated || friends.Length < data.Friends.Count;
        var projected = new McpFriendCollectionSnapshot(
            data.Total,
            data.Online,
            friends.Length,
            limit,
            truncated,
            data.Categories,
            friends);
        return Convert(
            source,
            projected,
            source.Metadata with { Truncated = truncated });
    }

    public static QueryEnvelope<FurniCollectionSnapshot> FurniDetails(
        QueryEnvelope<FurniCollectionSnapshot> source,
        int limit)
    {
        if (source.Data is not { } data)
            return source;

        FloorItemSnapshot[] floor_items = data.FloorItems.Take(limit).ToArray();
        WallItemSnapshot[] wall_items = data.WallItems.Take(limit).ToArray();
        bool floor_truncated = data.FloorItemsTruncated || floor_items.Length < data.FloorItems.Count;
        bool wall_truncated = data.WallItemsTruncated || wall_items.Length < data.WallItems.Count;
        FurniCollectionSnapshot projected = data with
        {
            ReturnedFloorItemCount = floor_items.Length,
            ReturnedWallItemCount = wall_items.Length,
            MaxItemsPerType = limit,
            FloorItemsTruncated = floor_truncated,
            WallItemsTruncated = wall_truncated,
            FloorItems = floor_items,
            WallItems = wall_items
        };
        return source with
        {
            Metadata = source.Metadata with
            {
                Truncated = source.Metadata.Truncated || floor_truncated || wall_truncated
            },
            Data = projected
        };
    }

    public static QueryEnvelope<McpFurniSnapshot> Furni(
        QueryEnvelope<FurniCollectionSnapshot> source,
        int limit)
    {
        if (source.Data is not { } data)
            return Convert(source, default(McpFurniSnapshot));

        FloorItemSnapshot[] floor_items = data.FloorItems.Take(limit).ToArray();
        WallItemSnapshot[] wall_items = data.WallItems.Take(limit).ToArray();
        McpFurniDefinition[] definitions = floor_items
            .Select(item => item.Definition)
            .Concat(wall_items.Select(item => item.Definition))
            .OfType<FurniDefinitionSnapshot>()
            .DistinctBy(definition => (definition.Type, definition.Kind))
            .Select(definition => new McpFurniDefinition(
                definition.Type,
                definition.Kind,
                definition.Identifier,
                definition.Name,
                definition.Width,
                definition.Length,
                definition.Category,
                definition.Line))
            .OrderBy(definition => definition.Type, StringComparer.Ordinal)
            .ThenBy(definition => definition.Kind)
            .ToArray();
        McpFloorItem[] compact_floor_items = floor_items
            .Select(item => new McpFloorItem(
                item.Id,
                item.IsRemoved,
                item.Kind,
                item.Identifier,
                item.OwnerId,
                item.OwnerName,
                item.Position,
                item.Area,
                item.Direction,
                item.Height,
                item.Data.Type,
                EmptyToNull(item.Data.Value),
                item.State,
                item.SecondsToExpiration,
                item.IsHidden))
            .ToArray();
        McpWallItem[] compact_wall_items = wall_items
            .Select(item => new McpWallItem(
                item.Id,
                item.IsRemoved,
                item.Kind,
                item.Identifier,
                item.OwnerId,
                item.OwnerName,
                item.Location,
                EmptyToNull(item.Data),
                item.State,
                item.SecondsToExpiration,
                item.IsHidden))
            .ToArray();
        bool floor_truncated = data.FloorItemsTruncated || floor_items.Length < data.FloorItems.Count;
        bool wall_truncated = data.WallItemsTruncated || wall_items.Length < data.WallItems.Count;
        var projected = new McpFurniSnapshot(
            data.RoomId,
            data.Generation,
            data.DefinitionsLoaded,
            data.FloorItemCount,
            data.WallItemCount,
            floor_items.Length,
            wall_items.Length,
            limit,
            floor_truncated,
            wall_truncated,
            definitions,
            compact_floor_items,
            compact_wall_items);
        return Convert(
            source,
            projected,
            source.Metadata with
            {
                Truncated = source.Metadata.Truncated || floor_truncated || wall_truncated
            });
    }

    public static QueryEnvelope<HeightmapSnapshot?> HeightmapDetails(
        QueryEnvelope<HeightmapSnapshot?> source,
        int limit)
    {
        if (source.Data is not { } data)
            return source;

        HeightmapTileSnapshot[] tiles = data.Tiles.Take(limit).ToArray();
        bool truncated = data.Truncated || tiles.Length < data.Tiles.Count;
        HeightmapSnapshot projected = data with
        {
            ReturnedTileCount = tiles.Length,
            MaxTiles = limit,
            Truncated = truncated,
            Tiles = tiles
        };
        return source with
        {
            Metadata = source.Metadata with { Truncated = source.Metadata.Truncated || truncated },
            Data = projected
        };
    }

    public static QueryEnvelope<McpHeightmapSnapshot> Heightmap(
        QueryEnvelope<HeightmapSnapshot?> source)
    {
        McpHeightmapSnapshot? projected = source.Data is not { } data
            ? null
            : new McpHeightmapSnapshot(
                data.RoomId,
                data.Generation,
                data.Width,
                data.Length,
                data.TileCount,
                data.FloorTileCount,
                data.WalkableTileCount,
                data.BlockedTileCount,
                data.NonFloorTileCount,
                data.Truncated);
        return Convert<HeightmapSnapshot?, McpHeightmapSnapshot>(
            source,
            projected,
            source.Metadata with { Truncated = false });
    }

    private static QueryEnvelope<TTarget> Convert<TSource, TTarget>(
        QueryEnvelope<TSource> source,
        TTarget? data,
        QueryMetadataSnapshot? metadata = null) =>
        new(
            source.Query,
            metadata ?? source.Metadata,
            data,
            source.Error);

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}

internal sealed record McpInventorySnapshot(
    bool DefinitionsLoaded,
    bool IsLoading,
    bool IsStale,
    long Generation,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total,
    int Returned,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<McpInventoryGroup> Groups);

internal sealed record McpInventoryGroup(
    string Type,
    int Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Identifier,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Name,
    int Count,
    IReadOnlyList<Id> ItemIds,
    IReadOnlyList<Id> ObjectIds);

internal sealed record McpFriendDetailsSnapshot(
    int Total,
    int Online,
    int Returned,
    int MaxItems,
    bool Truncated,
    int UserLimit,
    int NormalLimit,
    int ExtendedLimit,
    IReadOnlyList<FriendCategorySnapshot> Categories,
    IReadOnlyList<FriendSnapshot> Friends);

internal sealed record McpFriendCollectionSnapshot(
    int Total,
    int Online,
    int Returned,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<FriendCategorySnapshot> Categories,
    IReadOnlyList<McpFriend> Friends);

internal sealed record McpFriend(
    Id Id,
    string Name,
    bool IsOnline,
    bool CanFollow,
    int CategoryId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Relation,
    [property: JsonConverter(typeof(ExactInt64JsonConverter))]
    long LastOnline);

internal sealed record McpFurniSnapshot(
    Id? RoomId,
    long Generation,
    bool DefinitionsLoaded,
    int FloorItemCount,
    int WallItemCount,
    int ReturnedFloorItemCount,
    int ReturnedWallItemCount,
    int MaxItemsPerType,
    bool FloorItemsTruncated,
    bool WallItemsTruncated,
    IReadOnlyList<McpFurniDefinition> Definitions,
    IReadOnlyList<McpFloorItem> FloorItems,
    IReadOnlyList<McpWallItem> WallItems);

internal sealed record McpFurniDefinition(
    string Type,
    int Kind,
    string Identifier,
    string Name,
    int Width,
    int Length,
    string Category,
    string Line);

internal sealed record McpFloorItem(
    Id Id,
    bool IsRemoved,
    int Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Identifier,
    Id OwnerId,
    string OwnerName,
    PositionSnapshot Position,
    AreaSnapshot Area,
    int Direction,
    float Height,
    string DataType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DataValue,
    int State,
    int SecondsToExpiration,
    bool IsHidden);

internal sealed record McpWallItem(
    Id Id,
    bool IsRemoved,
    int Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Identifier,
    Id OwnerId,
    string OwnerName,
    WallLocationSnapshot Location,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Data,
    int State,
    int SecondsToExpiration,
    bool IsHidden);

internal sealed record McpHeightmapSnapshot(
    Id? RoomId,
    long Generation,
    int Width,
    int Length,
    int TileCount,
    int FloorTileCount,
    int WalkableTileCount,
    int BlockedTileCount,
    int NonFloorTileCount,
    bool TileDetailsTruncated);
