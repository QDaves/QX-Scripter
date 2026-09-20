namespace Qx.Presentation.Services.Room;

public static class RoomRotation
{
    public static int[] Options(
        IReadOnlyList<IReadOnlyList<int>> supported,
        IReadOnlyList<int> facing)
    {
        ArgumentNullException.ThrowIfNull(supported);
        ArgumentNullException.ThrowIfNull(facing);
        if (supported.Count == 0 || supported.Any(choice => choice.Count == 0))
            return [];
        HashSet<int> common = [.. supported[0]];
        for (int index = 1; index < supported.Count; index++)
            common.IntersectWith(supported[index]);
        return [.. common.Where(direction => facing.Any(current => current != direction)).Order()];
    }
}
