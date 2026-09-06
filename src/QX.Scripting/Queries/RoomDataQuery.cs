using Qx;
using Qx.Model;

namespace Qx.Scripting;

public sealed class RoomDataQuery : QueryCollection<RoomData>
{
    public RoomDataQuery(IEnumerable<RoomData> rooms) : base(rooms)
    {
    }

    public RoomDataQuery Where(Func<RoomData, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public RoomDataQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public RoomDataQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(room => values.Contains(room.Id));
    }

    public RoomDataQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public RoomDataQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(room => values.Contains(room.Name));
    }

    public RoomDataQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(room => room.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public RoomDataQuery OwnedBy(Id owner_id) =>
        Where(room => room.OwnerId == owner_id);

    public RoomDataQuery OwnedBy(string owner_name)
    {
        ArgumentNullException.ThrowIfNull(owner_name);
        return Where(room => string.Equals(
            room.OwnerName,
            owner_name,
            StringComparison.OrdinalIgnoreCase));
    }

    public RoomDataQuery InCategory(params int[] category_ids) =>
        InCategory((IEnumerable<int>)category_ids);

    public RoomDataQuery InCategory(IEnumerable<int> category_ids)
    {
        HashSet<int> values = QueryValues.Set(category_ids);
        return Where(room => values.Contains(room.Category));
    }

    public RoomDataQuery WithDoorMode(params int[] door_modes) =>
        WithDoorMode((IEnumerable<int>)door_modes);

    public RoomDataQuery WithDoorMode(IEnumerable<int> door_modes)
    {
        HashSet<int> values = QueryValues.Set(door_modes);
        return Where(room => values.Contains((int)room.DoorMode));
    }

    public RoomDataQuery WithDoorMode(params RoomDoorMode[] door_modes) =>
        WithDoorMode((IEnumerable<RoomDoorMode>)door_modes);

    public RoomDataQuery WithDoorMode(IEnumerable<RoomDoorMode> door_modes)
    {
        HashSet<RoomDoorMode> values = QueryValues.Set(door_modes);
        return Where(room => values.Contains(room.DoorMode));
    }

    public RoomDataQuery WithTradeMode(params int[] trade_modes) =>
        WithTradeMode((IEnumerable<int>)trade_modes);

    public RoomDataQuery WithTradeMode(IEnumerable<int> trade_modes)
    {
        HashSet<int> values = QueryValues.Set(trade_modes);
        return Where(room => values.Contains((int)room.TradeMode));
    }

    public RoomDataQuery WithTradeMode(params RoomTradeMode[] trade_modes) =>
        WithTradeMode((IEnumerable<RoomTradeMode>)trade_modes);

    public RoomDataQuery WithTradeMode(IEnumerable<RoomTradeMode> trade_modes)
    {
        HashSet<RoomTradeMode> values = QueryValues.Set(trade_modes);
        return Where(room => values.Contains(room.TradeMode));
    }

    public RoomDataQuery TaggedAny(params string[] tags) =>
        TaggedAny((IEnumerable<string>)tags);

    public RoomDataQuery TaggedAny(IEnumerable<string> tags)
    {
        HashSet<string> values = QueryValues.Strings(tags);
        return Where(room => room.Tags.Any(values.Contains));
    }

    public RoomDataQuery TaggedAll(params string[] tags) =>
        TaggedAll((IEnumerable<string>)tags);

    public RoomDataQuery TaggedAll(IEnumerable<string> tags)
    {
        HashSet<string> values = QueryValues.Strings(tags);
        return Where(room => values.All(tag => room.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    public RoomDataQuery Grouped(bool value = true) =>
        Where(room => room.HasGroup == value);

    public RoomDataQuery InGroup(Id group_id) =>
        Where(room => room.HasGroup && room.GroupId == group_id);

    public RoomDataQuery InGroup(string group_name)
    {
        ArgumentNullException.ThrowIfNull(group_name);
        return Where(room => room.HasGroup && string.Equals(
            room.GroupName,
            group_name,
            StringComparison.OrdinalIgnoreCase));
    }

    public RoomDataQuery EventRooms(bool value = true) =>
        Where(room => room.HasEvent == value);

    public RoomDataQuery AllowsPets(bool value = true) =>
        Where(room => room.AllowPets == value);

    public RoomDataQuery OccupancyBetween(int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum occupancy cannot be below minimum occupancy.");
        return Where(room => room.UserCount >= minimum && room.UserCount <= maximum);
    }

    public RoomDataQuery OccupancyRatioAtLeast(double ratio)
    {
        ValidateRatio(ratio);
        return Where(room => room.MaxUserCount > 0 &&
            (double)room.UserCount / room.MaxUserCount >= ratio);
    }

    public RoomDataQuery HasSpace(bool value = true) =>
        Where(room => (room.MaxUserCount > room.UserCount) == value);

    public RoomDataQuery Full(bool value = true) =>
        Where(room => (room.MaxUserCount > 0 && room.UserCount >= room.MaxUserCount) == value);

    public RoomDataQuery ScoreAtLeast(int minimum) =>
        Where(room => room.Score >= minimum);

    public RoomDataQuery RankingAtMost(int maximum) =>
        Where(room => room.Ranking <= maximum);

    public RoomDataQuery OrderByOccupancy(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(room => room.UserCount)
            : Items.OrderBy(room => room.UserCount));

    public RoomDataQuery OrderByScore(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(room => room.Score)
            : Items.OrderBy(room => room.Score));

    public RoomDataQuery OrderByRanking() =>
        Next(Items.OrderBy(room => room.Ranking));

    private static void ValidateRatio(double ratio)
    {
        if (!double.IsFinite(ratio) || ratio < 0 || ratio > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Ratio must be finite and between zero and one.");
    }

    private static RoomDataQuery Next(IEnumerable<RoomData> rooms) => new(rooms);
}
