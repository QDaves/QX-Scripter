using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void scan_chests(IReadOnlyList<WiredSimulationNode> boxes, Frame frame)
    {
        var scanners = boxes.Where(node => node.Name == "ChestItemTypeScanner").ToArray();
        if (scanners.GroupBy(node => resolve_reference(node.Text("variable"), frame)).Any(group => group.Count() > 1))
            unsupported(scanners[0], "Several chest scanners writing the same context variable require separate ordering conformance.");
        foreach (var scanner in scanners)
        {
            string variable = resolve_reference(scanner.Text("variable"), frame);
            if (!variables.TryGetValue(variable, out var definition) || definition.Domain != WiredSourceDomain.Context ||
                !definition.HasValue || !definition.Writable)
                unsupported(scanner, "Chest scanners require a writable numeric context-variable definition.");
            long mode = scanner.Number("scanning_mode");
            if (mode is not (0 or 1)) unsupported(scanner, "Unknown chest scanning mode.");
            var kinds = fs(scanner, frame).Select(id => furniture[id].Kind).ToHashSet();
            long count = 0;
            foreach (long id in fs(scanner, frame, 1))
            {
                var chest = furniture[id];
                if (chest.Chest is not { StoresCoins: false })
                    unsupported(scanner, "Chest scanners require furniture chests; the hotel rejects credit chests and other furniture.");
                IReadOnlyList<ChestItemType> contents;
                if (mode == 0)
                {
                    require_chest_items(chest, scanner, allow_reserved: true);
                    contents = chest.Chest!.Items!.Select(item => item.Type).ToArray();
                }
                else
                {
                    if (chest.Chest!.PreviewItems is null)
                        unsupported(scanner, "Preview-only chest scanning requires captured preview items; missing preview metadata is not an empty preview.");
                    contents = chest.Chest.PreviewItems!;
                }
                charge(frame, contents.Count, scanner.FurnitureId);
                count += contents.LongCount(item => !item.IsWallItem && kinds.Contains(item.TypeId));
            }
            write(WiredSourceDomain.Context, 0, variable, count, frame, create: true);
            record("chest.scan", scanner.FurnitureId, "Matching stored chest items, including trade reservations.", fs(scanner, frame, 1), after: count);
        }
    }

    private static WiredSimulationChest? copy_chest(WiredSimulationChest? chest) => chest is null ? null : chest with
    {
        Items = chest.Items?.Select(copy_chest_entry).ToArray(), PreviewItems = chest.PreviewItems?.ToArray()
    };

    private static WiredChestStorageSnapshot copy_chest_entry(WiredChestStorageSnapshot item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Data);
        return item with
        {
            Data = item.Data with
            {
                MapEntries = item.Data.MapEntries?.ToDictionary(StringComparer.Ordinal),
                StringValues = item.Data.StringValues?.ToArray(),
                IntValues = item.Data.IntValues?.ToArray(),
                HighScores = item.Data.HighScores?.Select(score => score with { Names = score.Names.ToArray() }).ToArray()
            }
        };
    }

    private long chest_count(WiredSimulationFurniture item, WiredSimulationNode node)
    {
        if (item.Chest is not { } chest)
            unsupported(node, "A selected chest requires captured balance or completed contents metadata.");
        var current = item.Chest!;
        if (current.StoresCoins)
        {
            if (current.Coins is not { } coins) unsupported(node, "The selected credit chest balance is unknown.");
            return current.Coins!.Value;
        }
        require_chest_items(item, node, allow_reserved: true);
        return current.Items!.Count;
    }

    private void require_chest_items(WiredSimulationFurniture item, WiredSimulationNode node, bool allow_reserved = false)
    {
        if (item.Chest is not { StoresCoins: false, ItemsComplete: true, Items: { } items })
            unsupported(node, "The selected furniture chest requires a completed contents snapshot.");
        if (item.Chest!.Items!.Any(entry => entry.LockState != 0 && (!allow_reserved || entry.LockState != 2)))
            unsupported(node, "Locked or reserved chest items require separate counting conformance.");
    }

    private long chest_type_count(WiredSimulationFurniture prototype, IReadOnlyList<WiredSimulationFurniture> chests,
        WiredSimulationNode node)
    {
        long count = 0;
        foreach (var item in chests)
        {
            if (item.Chest is { StoresCoins: true }) continue;
            require_chest_items(item, node, allow_reserved: true);
            count += item.Chest!.Items!.LongCount(entry => !entry.Type.IsWallItem && entry.Type.TypeId == prototype.Kind);
        }
        return count;
    }

}
