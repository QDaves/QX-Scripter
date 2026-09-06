using Qx;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public sealed class GuildMemberQuery : QueryCollection<GuildMember>
{
    public GuildMemberQuery(IEnumerable<GuildMember> members) : base(members)
    {
    }

    public GuildMemberQuery Where(Func<GuildMember, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public GuildMemberQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public GuildMemberQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(member => values.Contains(member.Id));
    }

    public GuildMemberQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public GuildMemberQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(member => values.Contains(member.Name));
    }

    public GuildMemberQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(member => member.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public GuildMemberQuery OfType(params GuildMemberType[] types) =>
        OfType((IEnumerable<GuildMemberType>)types);

    public GuildMemberQuery OfType(IEnumerable<GuildMemberType> types)
    {
        HashSet<GuildMemberType> values = QueryValues.Set(types);
        return Where(member => values.Contains(member.Type));
    }

    public GuildMemberQuery Owners() =>
        OfType(GuildMemberType.Owner);

    public GuildMemberQuery Administrators() =>
        OfType(GuildMemberType.Administrator);

    public GuildMemberQuery Members() =>
        Where(member => member.IsMember);

    public GuildMemberQuery Pending() =>
        OfType(GuildMemberType.Pending);

    public GuildMemberQuery Blocked() =>
        OfType(GuildMemberType.Blocked);

    public GuildMemberQuery FigureContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(member => member.Figure.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public GuildMemberQuery MemberSinceContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(member => member.MemberSince.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public GuildMemberQuery OrderByName() =>
        Next(Items.OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase));

    public GuildMemberQuery OrderByType() =>
        Next(Items.OrderBy(member => member.Type).ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase));

    private static GuildMemberQuery Next(IEnumerable<GuildMember> members) => new(members);
}

public static class GuildMemberQueryExtensions
{
    public static GuildMemberQuery Query(this IEnumerable<GuildMember> members) =>
        new(members);
}
