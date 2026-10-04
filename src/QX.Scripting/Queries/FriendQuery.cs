using Qx;
using Qx.Model;

namespace Qx.Scripting;

/// <summary>
/// Represents a query over friends.
/// </summary>
/// <remarks>
/// Every filter and sort returns a new query. Text matching ignores case,
/// <see langword="null"/> entries in text lists are skipped, and a <see langword="null"/>
/// argument throws <see cref="ArgumentNullException"/>.
/// </remarks>
public sealed class FriendQuery : QueryCollection<Friend>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FriendQuery"/> class over the specified friends.
    /// </summary>
    /// <param name="friends">The friends to query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="friends"/> is <see langword="null"/>.</exception>
    public FriendQuery(IEnumerable<Friend> friends) : base(friends)
    {
    }

    /// <summary>
    /// Filters the friends with a predicate.
    /// </summary>
    /// <param name="predicate">The condition a friend must meet to be kept.</param>
    /// <returns>A new query with the friends that match <paramref name="predicate"/>.</returns>
    public FriendQuery Where(Func<Friend, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    /// <summary>
    /// Filters the friends to those with any of the specified user ids.
    /// </summary>
    /// <param name="ids">The user ids to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    /// <summary>
    /// Filters the friends to those with any of the specified user ids.
    /// </summary>
    /// <param name="ids">The user ids to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(friend => values.Contains(friend.Id));
    }

    /// <summary>
    /// Filters the friends to those with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The names to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    /// <summary>
    /// Filters the friends to those with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The names to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(friend => values.Contains(friend.Name));
    }

    /// <summary>
    /// Filters the friends to those whose name contains the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The text to search for.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(friend => friend.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the friends to those whose motto contains the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The text to search for.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery MottoContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(friend => friend.Motto.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the friends by whether they are online.
    /// </summary>
    /// <param name="value">Whether to keep online friends instead of offline ones.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery Online(bool value = true) =>
        Where(friend => friend.IsOnline == value);

    /// <summary>
    /// Filters the friends by whether they can be followed.
    /// </summary>
    /// <param name="value">Whether to keep friends that can be followed instead of the others.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery Followable(bool value = true) =>
        Where(friend => friend.CanFollow == value);

    /// <summary>
    /// Filters the friends by whether they accept offline messages.
    /// </summary>
    /// <param name="value">Whether to keep friends that accept offline messages instead of the others.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery AcceptsOfflineMessages(bool value = true) =>
        Where(friend => friend.IsAcceptingOfflineMessages == value);

    /// <summary>
    /// Filters the friends by whether they are VIP members.
    /// </summary>
    /// <param name="value">Whether to keep VIP members instead of the others.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery Vip(bool value = true) =>
        Where(friend => friend.IsVipMember == value);

    /// <summary>
    /// Filters the friends by whether they use Pocket Habbo.
    /// </summary>
    /// <param name="value">Whether to keep Pocket Habbo users instead of the others.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery PocketHabbo(bool value = true) =>
        Where(friend => friend.IsPocketHabboUser == value);

    /// <summary>
    /// Filters the friends to those of any of the specified genders.
    /// </summary>
    /// <param name="genders">The genders to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery OfGender(params Gender[] genders) =>
        OfGender((IEnumerable<Gender>)genders);

    /// <summary>
    /// Filters the friends to those of any of the specified genders.
    /// </summary>
    /// <param name="genders">The genders to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery OfGender(IEnumerable<Gender> genders)
    {
        HashSet<Gender> values = QueryValues.Set(genders);
        return Where(friend => values.Contains(friend.Gender));
    }

    /// <summary>
    /// Filters the friends to those in any of the specified friend list categories.
    /// </summary>
    /// <param name="categoryIds">The category ids to keep, compared with <see cref="Friend.CategoryId"/>.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery InCategory(params int[] categoryIds) =>
        InCategory((IEnumerable<int>)categoryIds);

    /// <summary>
    /// Filters the friends to those in any of the specified friend list categories.
    /// </summary>
    /// <param name="categoryIds">The category ids to keep, compared with <see cref="Friend.CategoryId"/>.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery InCategory(IEnumerable<int> categoryIds)
    {
        HashSet<int> values = QueryValues.Set(categoryIds);
        return Where(friend => values.Contains(friend.CategoryId));
    }

    /// <summary>
    /// Filters the friends to those with any of the specified relationship statuses.
    /// </summary>
    /// <param name="relations">The relationship statuses to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery WithRelation(params Relation[] relations) =>
        WithRelation((IEnumerable<Relation>)relations);

    /// <summary>
    /// Filters the friends to those with any of the specified relationship statuses.
    /// </summary>
    /// <param name="relations">The relationship statuses to keep.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery WithRelation(IEnumerable<Relation> relations)
    {
        HashSet<Relation> values = QueryValues.Set(relations);
        return Where(friend => values.Contains(friend.Relation));
    }

    /// <summary>
    /// Filters the friends to those whose <see cref="Friend.LastOnline"/> value is greater than the specified value.
    /// </summary>
    /// <param name="timestamp">The value that <see cref="Friend.LastOnline"/> must exceed.</param>
    /// <returns>A new query with the matching friends.</returns>
    public FriendQuery SeenAfter(long timestamp) =>
        Where(friend => friend.LastOnline > timestamp);

    /// <summary>
    /// Sorts the friends by name, ignoring case.
    /// </summary>
    /// <remarks>
    /// Friends with equal names keep their current order.
    /// </remarks>
    /// <returns>A new query with the sorted friends.</returns>
    public FriendQuery OrderByName() =>
        Next(Items.OrderBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Sorts the friends by their <see cref="Friend.LastOnline"/> value.
    /// </summary>
    /// <remarks>
    /// Friends with equal values keep their current order.
    /// </remarks>
    /// <param name="descending">Whether to sort from the highest value instead of the lowest.</param>
    /// <returns>A new query with the sorted friends.</returns>
    public FriendQuery OrderByLastOnline(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(friend => friend.LastOnline)
            : Items.OrderBy(friend => friend.LastOnline));

    private static FriendQuery Next(IEnumerable<Friend> friends) => new(friends);
}
