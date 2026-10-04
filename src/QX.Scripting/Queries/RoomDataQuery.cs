using Qx;
using Qx.Model;

namespace Qx.Scripting;

/// <summary>
/// Represents a query over room data.
/// </summary>
/// <remarks>
/// Every filter and sort returns a new query. Text matching ignores case,
/// <see langword="null"/> entries in text lists are skipped, and a <see langword="null"/>
/// argument throws <see cref="ArgumentNullException"/>.
/// </remarks>
public sealed class RoomDataQuery : QueryCollection<RoomData>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoomDataQuery"/> class over the specified rooms.
    /// </summary>
    /// <param name="rooms">The room data to query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rooms"/> is <see langword="null"/>.</exception>
    public RoomDataQuery(IEnumerable<RoomData> rooms) : base(rooms)
    {
    }

    /// <summary>
    /// Filters the rooms with a predicate.
    /// </summary>
    /// <param name="predicate">The condition a room must meet to be kept.</param>
    /// <returns>A new query with the rooms that match <paramref name="predicate"/>.</returns>
    public RoomDataQuery Where(Func<RoomData, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    /// <summary>
    /// Filters the rooms to those with any of the specified ids.
    /// </summary>
    /// <param name="ids">The room ids to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    /// <summary>
    /// Filters the rooms to those with any of the specified ids.
    /// </summary>
    /// <param name="ids">The room ids to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(room => values.Contains(room.Id));
    }

    /// <summary>
    /// Filters the rooms to those with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The room names to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    /// <summary>
    /// Filters the rooms to those with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The room names to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(room => values.Contains(room.Name));
    }

    /// <summary>
    /// Filters the rooms to those whose name contains the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The text to search for.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(room => room.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the rooms to those owned by the specified user id.
    /// </summary>
    /// <param name="ownerId">The owner's user id.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery OwnedBy(Id ownerId) =>
        Where(room => room.OwnerId == ownerId);

    /// <summary>
    /// Filters the rooms to those whose owner has the specified name, ignoring case.
    /// </summary>
    /// <param name="ownerName">The owner's name.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery OwnedBy(string ownerName)
    {
        ArgumentNullException.ThrowIfNull(ownerName);
        return Where(room => string.Equals(
            room.OwnerName,
            ownerName,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the rooms to those in any of the specified navigator categories.
    /// </summary>
    /// <param name="categoryIds">The category ids to keep, compared with <see cref="RoomData.Category"/>.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery InCategory(params int[] categoryIds) =>
        InCategory((IEnumerable<int>)categoryIds);

    /// <summary>
    /// Filters the rooms to those in any of the specified navigator categories.
    /// </summary>
    /// <param name="categoryIds">The category ids to keep, compared with <see cref="RoomData.Category"/>.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery InCategory(IEnumerable<int> categoryIds)
    {
        HashSet<int> values = QueryValues.Set(categoryIds);
        return Where(room => values.Contains(room.Category));
    }

    /// <summary>
    /// Filters the rooms to those with any of the specified numeric door modes.
    /// </summary>
    /// <param name="doorModes">The numeric values of the <see cref="RoomDoorMode"/> members to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithDoorMode(params int[] doorModes) =>
        WithDoorMode((IEnumerable<int>)doorModes);

    /// <summary>
    /// Filters the rooms to those with any of the specified numeric door modes.
    /// </summary>
    /// <param name="doorModes">The numeric values of the <see cref="RoomDoorMode"/> members to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithDoorMode(IEnumerable<int> doorModes)
    {
        HashSet<int> values = QueryValues.Set(doorModes);
        return Where(room => values.Contains((int)room.DoorMode));
    }

    /// <summary>
    /// Filters the rooms to those with any of the specified door modes.
    /// </summary>
    /// <param name="doorModes">The door modes to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithDoorMode(params RoomDoorMode[] doorModes) =>
        WithDoorMode((IEnumerable<RoomDoorMode>)doorModes);

    /// <summary>
    /// Filters the rooms to those with any of the specified door modes.
    /// </summary>
    /// <param name="doorModes">The door modes to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithDoorMode(IEnumerable<RoomDoorMode> doorModes)
    {
        HashSet<RoomDoorMode> values = QueryValues.Set(doorModes);
        return Where(room => values.Contains(room.DoorMode));
    }

    /// <summary>
    /// Filters the rooms to those with any of the specified numeric trade modes.
    /// </summary>
    /// <param name="tradeModes">The numeric values of the <see cref="RoomTradeMode"/> members to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithTradeMode(params int[] tradeModes) =>
        WithTradeMode((IEnumerable<int>)tradeModes);

    /// <summary>
    /// Filters the rooms to those with any of the specified numeric trade modes.
    /// </summary>
    /// <param name="tradeModes">The numeric values of the <see cref="RoomTradeMode"/> members to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithTradeMode(IEnumerable<int> tradeModes)
    {
        HashSet<int> values = QueryValues.Set(tradeModes);
        return Where(room => values.Contains((int)room.TradeMode));
    }

    /// <summary>
    /// Filters the rooms to those with any of the specified trade modes.
    /// </summary>
    /// <param name="tradeModes">The trade modes to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithTradeMode(params RoomTradeMode[] tradeModes) =>
        WithTradeMode((IEnumerable<RoomTradeMode>)tradeModes);

    /// <summary>
    /// Filters the rooms to those with any of the specified trade modes.
    /// </summary>
    /// <param name="tradeModes">The trade modes to keep.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery WithTradeMode(IEnumerable<RoomTradeMode> tradeModes)
    {
        HashSet<RoomTradeMode> values = QueryValues.Set(tradeModes);
        return Where(room => values.Contains(room.TradeMode));
    }

    /// <summary>
    /// Filters the rooms to those with at least one of the specified tags, ignoring case.
    /// </summary>
    /// <remarks>
    /// With no tags, no room is kept.
    /// </remarks>
    /// <param name="tags">The tags to look for.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery TaggedAny(params string[] tags) =>
        TaggedAny((IEnumerable<string>)tags);

    /// <summary>
    /// Filters the rooms to those with at least one of the specified tags, ignoring case.
    /// </summary>
    /// <remarks>
    /// With no tags, no room is kept.
    /// </remarks>
    /// <param name="tags">The tags to look for.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery TaggedAny(IEnumerable<string> tags)
    {
        HashSet<string> values = QueryValues.Strings(tags);
        return Where(room => room.Tags.Any(values.Contains));
    }

    /// <summary>
    /// Filters the rooms to those with every one of the specified tags, ignoring case.
    /// </summary>
    /// <remarks>
    /// With no tags, every room is kept.
    /// </remarks>
    /// <param name="tags">The tags a room must have.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery TaggedAll(params string[] tags) =>
        TaggedAll((IEnumerable<string>)tags);

    /// <summary>
    /// Filters the rooms to those with every one of the specified tags, ignoring case.
    /// </summary>
    /// <remarks>
    /// With no tags, every room is kept.
    /// </remarks>
    /// <param name="tags">The tags a room must have.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery TaggedAll(IEnumerable<string> tags)
    {
        HashSet<string> values = QueryValues.Strings(tags);
        return Where(room => values.All(tag => room.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Filters the rooms by whether they belong to a group.
    /// </summary>
    /// <param name="value">Whether to keep group rooms instead of rooms without a group.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery Grouped(bool value = true) =>
        Where(room => room.HasGroup == value);

    /// <summary>
    /// Filters the rooms to those that belong to the group with the specified id.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery InGroup(Id groupId) =>
        Where(room => room.HasGroup && room.GroupId == groupId);

    /// <summary>
    /// Filters the rooms to those that belong to the group with the specified name, ignoring case.
    /// </summary>
    /// <param name="groupName">The group name.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery InGroup(string groupName)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        return Where(room => room.HasGroup && string.Equals(
            room.GroupName,
            groupName,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the rooms by whether they host an event.
    /// </summary>
    /// <param name="value">Whether to keep rooms with an event instead of rooms without one.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery EventRooms(bool value = true) =>
        Where(room => room.HasEvent == value);

    /// <summary>
    /// Filters the rooms by whether they allow pets.
    /// </summary>
    /// <param name="value">Whether to keep rooms that allow pets instead of the others.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery AllowsPets(bool value = true) =>
        Where(room => room.AllowPets == value);

    /// <summary>
    /// Filters the rooms to those whose user count is in the specified range.
    /// </summary>
    /// <param name="minimum">The lowest user count to keep, inclusive.</param>
    /// <param name="maximum">The highest user count to keep, inclusive.</param>
    /// <returns>A new query with the matching rooms.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimum"/> is negative, or <paramref name="maximum"/> is less than <paramref name="minimum"/>.</exception>
    public RoomDataQuery OccupancyBetween(int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum occupancy cannot be below minimum occupancy.");
        return Where(room => room.UserCount >= minimum && room.UserCount <= maximum);
    }

    /// <summary>
    /// Filters the rooms to those whose user count divided by the maximum user count is at least the specified ratio.
    /// </summary>
    /// <remarks>
    /// Rooms with a maximum user count of zero are dropped.
    /// </remarks>
    /// <param name="ratio">The lowest ratio to keep, from 0 to 1 inclusive.</param>
    /// <returns>A new query with the matching rooms.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ratio"/> is not finite or is outside the range 0 to 1.</exception>
    public RoomDataQuery OccupancyRatioAtLeast(double ratio)
    {
        ValidateRatio(ratio);
        return Where(room => room.MaxUserCount > 0 &&
            (double)room.UserCount / room.MaxUserCount >= ratio);
    }

    /// <summary>
    /// Filters the rooms by whether their user count is below the maximum user count.
    /// </summary>
    /// <param name="value">Whether to keep rooms with space instead of rooms without space.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery HasSpace(bool value = true) =>
        Where(room => (room.MaxUserCount > room.UserCount) == value);

    /// <summary>
    /// Filters the rooms by whether they are full.
    /// </summary>
    /// <remarks>
    /// A room is full when its maximum user count is above zero and its user count has reached it,
    /// so a room with a maximum of zero never counts as full.
    /// </remarks>
    /// <param name="value">Whether to keep full rooms instead of the others.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery Full(bool value = true) =>
        Where(room => (room.MaxUserCount > 0 && room.UserCount >= room.MaxUserCount) == value);

    /// <summary>
    /// Filters the rooms to those with a score of at least the specified value.
    /// </summary>
    /// <param name="minimum">The lowest score to keep, inclusive.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery ScoreAtLeast(int minimum) =>
        Where(room => room.Score >= minimum);

    /// <summary>
    /// Filters the rooms to those with a ranking of at most the specified value.
    /// </summary>
    /// <param name="maximum">The highest ranking to keep, inclusive.</param>
    /// <returns>A new query with the matching rooms.</returns>
    public RoomDataQuery RankingAtMost(int maximum) =>
        Where(room => room.Ranking <= maximum);

    /// <summary>
    /// Sorts the rooms by user count.
    /// </summary>
    /// <remarks>
    /// Rooms with equal user counts keep their current order.
    /// </remarks>
    /// <param name="descending">Whether to sort from the most users instead of the fewest.</param>
    /// <returns>A new query with the sorted rooms.</returns>
    public RoomDataQuery OrderByOccupancy(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(room => room.UserCount)
            : Items.OrderBy(room => room.UserCount));

    /// <summary>
    /// Sorts the rooms by score.
    /// </summary>
    /// <remarks>
    /// Rooms with equal scores keep their current order.
    /// </remarks>
    /// <param name="descending">Whether to sort from the highest score instead of the lowest.</param>
    /// <returns>A new query with the sorted rooms.</returns>
    public RoomDataQuery OrderByScore(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(room => room.Score)
            : Items.OrderBy(room => room.Score));

    /// <summary>
    /// Sorts the rooms by ranking, lowest first.
    /// </summary>
    /// <remarks>
    /// Rooms with equal rankings keep their current order.
    /// </remarks>
    /// <returns>A new query with the sorted rooms.</returns>
    public RoomDataQuery OrderByRanking() =>
        Next(Items.OrderBy(room => room.Ranking));

    private static void ValidateRatio(double ratio)
    {
        if (!double.IsFinite(ratio) || ratio < 0 || ratio > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Ratio must be finite and between zero and one.");
    }

    private static RoomDataQuery Next(IEnumerable<RoomData> rooms) => new(rooms);
}
