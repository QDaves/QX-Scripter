namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void release_trade(long user_id, long action_id)
    {
        var reservations = users[user_id].Trade?.Reservations ?? [];
        foreach (var reservation in reservations)
        {
            if (!furniture.TryGetValue(reservation.ChestId, out var source) || source.Chest?.Items is not { } items ||
                reservation.InventoryIds.Any(id => items.Count(item => item.InventoryId == id && item.LockState == 2) != 1))
            {
                fail("transaction.reservation_missing", "A pending reservation no longer matches its captured chest entries.", action_id);
                throw new SimulationFailure();
            }
        }
        foreach (var reservation in reservations)
        {
            var source = furniture[reservation.ChestId];
            var ids = reservation.InventoryIds.ToHashSet();
            furniture[source.Id] = source with { Chest = source.Chest! with
            {
                Items = source.Chest!.Items!.Select(item => ids.Contains(item.InventoryId) ? item with { LockState = 0 } : item).ToArray(),
                PreviewItems = null
            } };
        }
        users[user_id] = users[user_id] with { Trade = new(false) };
    }
}
