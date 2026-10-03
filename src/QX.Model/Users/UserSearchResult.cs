using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents one user found by a user search.</summary>
/// <param name="Id">The user identifier.</param>
/// <param name="Name">The user's name.</param>
/// <param name="Motto">The user's motto.</param>
/// <param name="IsOnline">Whether the user is online.</param>
/// <param name="CanFollow">Whether the local user may follow the user into their room.</param>
/// <param name="LastAccess">The user's last access time as the hotel formats it.</param>
/// <param name="Gender">The user's gender as the hotel numbers it.</param>
/// <param name="Figure">The user's figure string.</param>
/// <param name="RealName">The user's real name, empty unless the hotel discloses it.</param>
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
    /// <summary>Reads a search result from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
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

    /// <summary>Writes the search result to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
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

/// <summary>Represents the results of a user search, split into friends and other users.</summary>
/// <remarks>Received as the Flash <c>HabboSearchResult</c> message.</remarks>
/// <param name="Friends">The matching users who are friends of the local user.</param>
/// <param name="Others">The matching users who are not friends of the local user.</param>
public sealed record UserSearchResults(
    IReadOnlyList<UserSearchResult> Friends,
    IReadOnlyList<UserSearchResult> Others) : IParserComposer<UserSearchResults>
{
    /// <summary>Finds a result by name, ignoring case and checking friends first.</summary>
    /// <param name="name">The name to look for.</param>
    /// <returns>The first matching result, or <see langword="null"/> when none matches.</returns>
    public UserSearchResult? Find(string name) =>
        Friends.Concat(Others).FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads search results from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
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

    /// <summary>Writes the search results to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
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
