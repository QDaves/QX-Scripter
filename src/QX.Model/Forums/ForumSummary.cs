using Qx.Messages;

namespace Qx.Model;

/// <summary>Specifies which forum directory list to request.</summary>
public enum ForumListCode
{
    /// <summary>The most active forums.</summary>
    Active = 0,
    /// <summary>The most popular forums.</summary>
    Popular = 1,
    /// <summary>The forums of the viewer's groups.</summary>
    MyForums = 2
}

/// <summary>Represents one group forum as listed in the forum directory.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="Name">The forum name.</param>
/// <param name="Description">The forum description.</param>
/// <param name="Icon">The forum icon as sent by the server.</param>
/// <param name="TotalThreads">The number of threads in the forum.</param>
/// <param name="LeaderboardScore">The forum's leaderboard score.</param>
/// <param name="TotalMessages">The number of messages in the forum.</param>
/// <param name="UnreadMessages">The number of messages the viewer has not read.</param>
/// <param name="LastMessageId">The id of the latest message.</param>
/// <param name="LastMessageAuthorId">The user id of the latest message's author.</param>
/// <param name="LastMessageAuthorName">The name of the latest message's author.</param>
/// <param name="LastMessageSecondsAgo">The number of seconds since the latest message was posted, at the time the summary was sent.</param>
public sealed record ForumSummary(
    Id GroupId,
    string Name,
    string Description,
    string Icon,
    int TotalThreads,
    int LeaderboardScore,
    int TotalMessages,
    int UnreadMessages,
    Id LastMessageId,
    Id LastMessageAuthorId,
    string LastMessageAuthorName,
    int LastMessageSecondsAgo) : IParserComposer<ForumSummary>
{
    private string name = Name ?? throw new ArgumentNullException(nameof(Name));
    private string description = Description ?? throw new ArgumentNullException(nameof(Description));
    private string icon = Icon ?? throw new ArgumentNullException(nameof(Icon));
    private string last_message_author_name = LastMessageAuthorName ??
        throw new ArgumentNullException(nameof(LastMessageAuthorName));

    /// <summary>Gets the forum name.</summary>
    public string Name
    {
        get => name;
        init => name = value ?? throw new ArgumentNullException(nameof(Name));
    }

    /// <summary>Gets the forum description.</summary>
    public string Description
    {
        get => description;
        init => description = value ?? throw new ArgumentNullException(nameof(Description));
    }

    /// <summary>Gets the forum icon as sent by the server.</summary>
    public string Icon
    {
        get => icon;
        init => icon = value ?? throw new ArgumentNullException(nameof(Icon));
    }

    /// <summary>Gets the name of the latest message's author.</summary>
    public string LastMessageAuthorName
    {
        get => last_message_author_name;
        init => last_message_author_name = value ??
            throw new ArgumentNullException(nameof(LastMessageAuthorName));
    }

    /// <summary>Gets the last read message id, computed as <see cref="TotalMessages"/> minus <see cref="UnreadMessages"/>.</summary>
    public int LastReadMessageId => TotalMessages - UnreadMessages;
    /// <summary>Gets whether the forum has messages the viewer has not read.</summary>
    public bool HasUnreadMessages => UnreadMessages > 0;

    /// <summary>Parses a forum summary from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumSummary Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumSummary value = FlashWire.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget));
        ForumProtocol.RequireEmpty(in p, nameof(ForumSummary));
        return value;
    }

    internal static ForumSummary ParseFlashWire(
        in PacketReader p,
        int trailing_bytes,
        ref ForumStringBudget budget)
    {
        ForumProtocol.RequireRemaining(
            in p,
            ForumProtocol.SummaryMinimumBytes,
            trailing_bytes,
            nameof(ForumSummary));
        Id group_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 36),
            "forum group");
        string name = budget.Read(in p, nameof(Name), checked(trailing_bytes + 34));
        string description = budget.Read(
            in p,
            nameof(Description),
            checked(trailing_bytes + 32));
        string icon = budget.Read(in p, nameof(Icon), checked(trailing_bytes + 30));
        int total_threads = p.ReadInt();
        int leaderboard_score = p.ReadInt();
        int total_messages = p.ReadInt();
        int unread_messages = p.ReadInt();
        Id last_message_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 10),
            "last message");
        Id last_message_author_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 6),
            "last message author");
        string last_message_author_name = budget.Read(
            in p,
            nameof(LastMessageAuthorName),
            checked(trailing_bytes + sizeof(int)));
        int last_message_seconds_ago = p.ReadInt();
        return new ForumSummary(
            group_id,
            name,
            description,
            icon,
            total_threads,
            leaderboard_score,
            total_messages,
            unread_messages,
            last_message_id,
            last_message_author_id,
            last_message_author_name,
            last_message_seconds_ago);
    }

    /// <summary>Composes the forum summary into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        FlashWire.Compose(this, in p, ComposeFlash);
    }

    internal static void PrepareFlash(
        ForumSummary value,
        in PacketWriter p,
        ref ForumStringBudget budget)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        budget.Require(value.Name, nameof(Name), in p);
        budget.Require(value.Description, nameof(Description), in p);
        budget.Require(value.Icon, nameof(Icon), in p);
        ForumProtocol.RequireFlashId(value.LastMessageId, "last message");
        ForumProtocol.RequireFlashId(value.LastMessageAuthorId, "last message author");
        budget.Require(value.LastMessageAuthorName, nameof(LastMessageAuthorName), in p);
    }

    internal static void ComposeFlashWire(ForumSummary value, in PacketWriter p)
    {
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteString(value.Icon);
        p.WriteInt(value.TotalThreads);
        p.WriteInt(value.LeaderboardScore);
        p.WriteInt(value.TotalMessages);
        p.WriteInt(value.UnreadMessages);
        ForumProtocol.WriteFlashId(in p, value.LastMessageId);
        ForumProtocol.WriteFlashId(in p, value.LastMessageAuthorId);
        p.WriteString(value.LastMessageAuthorName);
        p.WriteInt(value.LastMessageSecondsAgo);
    }

    private static void ComposeFlash(ForumSummary value, in PacketWriter p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        PrepareFlash(value, in p, ref budget);
        ComposeFlashWire(value, in p);
    }
}
