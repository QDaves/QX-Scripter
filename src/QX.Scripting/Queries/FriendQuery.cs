using Qx;
using Qx.Model;

namespace Qx.Scripting;

public sealed class FriendQuery : QueryCollection<Friend>
{
    public FriendQuery(IEnumerable<Friend> friends) : base(friends)
    {
    }

    public FriendQuery Where(Func<Friend, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public FriendQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public FriendQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(friend => values.Contains(friend.Id));
    }

    public FriendQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public FriendQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(friend => values.Contains(friend.Name));
    }

    public FriendQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(friend => friend.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public FriendQuery MottoContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(friend => friend.Motto.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public FriendQuery Online(bool value = true) =>
        Where(friend => friend.IsOnline == value);

    public FriendQuery Followable(bool value = true) =>
        Where(friend => friend.CanFollow == value);

    public FriendQuery AcceptsOfflineMessages(bool value = true) =>
        Where(friend => friend.IsAcceptingOfflineMessages == value);

    public FriendQuery Vip(bool value = true) =>
        Where(friend => friend.IsVipMember == value);

    public FriendQuery PocketHabbo(bool value = true) =>
        Where(friend => friend.IsPocketHabboUser == value);

    public FriendQuery OfGender(params Gender[] genders) =>
        OfGender((IEnumerable<Gender>)genders);

    public FriendQuery OfGender(IEnumerable<Gender> genders)
    {
        HashSet<Gender> values = QueryValues.Set(genders);
        return Where(friend => values.Contains(friend.Gender));
    }

    public FriendQuery InCategory(params int[] category_ids) =>
        InCategory((IEnumerable<int>)category_ids);

    public FriendQuery InCategory(IEnumerable<int> category_ids)
    {
        HashSet<int> values = QueryValues.Set(category_ids);
        return Where(friend => values.Contains(friend.CategoryId));
    }

    public FriendQuery WithRelation(params Relation[] relations) =>
        WithRelation((IEnumerable<Relation>)relations);

    public FriendQuery WithRelation(IEnumerable<Relation> relations)
    {
        HashSet<Relation> values = QueryValues.Set(relations);
        return Where(friend => values.Contains(friend.Relation));
    }

    public FriendQuery SeenAfter(long timestamp) =>
        Where(friend => friend.LastOnline > timestamp);

    public FriendQuery OrderByName() =>
        Next(Items.OrderBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase));

    public FriendQuery OrderByLastOnline(bool descending = true) =>
        Next(descending
            ? Items.OrderByDescending(friend => friend.LastOnline)
            : Items.OrderBy(friend => friend.LastOnline));

    private static FriendQuery Next(IEnumerable<Friend> friends) => new(friends);
}
