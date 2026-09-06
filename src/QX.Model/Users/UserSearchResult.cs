using Qx.Messages;

namespace Qx.Model;

public sealed record UserSearchResult(
    Id Id,
    string Name,
    string Motto,
    bool IsOnline,
    bool CanFollow,
    string LastAccess,
    int Gender,
    string Figure,
    string RealName) : IParserComposer<UserSearchResult>
{
    public static UserSearchResult Parse(in PacketReader p) =>
        new(
            p.ReadId(),
            p.ReadString(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadBool(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteId(Id);
        p.WriteString(Name);
        p.WriteString(Motto);
        p.WriteBool(IsOnline);
        p.WriteBool(CanFollow);
        p.WriteString(LastAccess);
        p.WriteInt(Gender);
        p.WriteString(Figure);
        p.WriteString(RealName);
    }
}

public sealed record UserSearchResults(
    IReadOnlyList<UserSearchResult> Friends,
    IReadOnlyList<UserSearchResult> Others) : IParserComposer<UserSearchResults>
{
    public UserSearchResult? Find(string name) =>
        Friends.Concat(Others).FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));

    public static UserSearchResults Parse(in PacketReader p)
    {
        int friendCount = p.ReadLength();
        var friends = new UserSearchResult[friendCount];
        for (int i = 0; i < friendCount; i++)
            friends[i] = p.Parse<UserSearchResult>();

        int otherCount = p.ReadLength();
        var others = new UserSearchResult[otherCount];
        for (int i = 0; i < otherCount; i++)
            others[i] = p.Parse<UserSearchResult>();

        return new UserSearchResults(friends, others);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)Friends.Count);
        foreach (UserSearchResult result in Friends)
            p.Compose(result);

        p.WriteLength((Length)Others.Count);
        foreach (UserSearchResult result in Others)
            p.Compose(result);
    }
}
