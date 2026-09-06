using Qx.Messages;

namespace Qx.Model.Forums;

public enum ForumListCode
{
    Active = 0,
    Popular = 1,
    MyForums = 2
}

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

    public string Name
    {
        get => name;
        init => name = value ?? throw new ArgumentNullException(nameof(Name));
    }

    public string Description
    {
        get => description;
        init => description = value ?? throw new ArgumentNullException(nameof(Description));
    }

    public string Icon
    {
        get => icon;
        init => icon = value ?? throw new ArgumentNullException(nameof(Icon));
    }

    public string LastMessageAuthorName
    {
        get => last_message_author_name;
        init => last_message_author_name = value ??
            throw new ArgumentNullException(nameof(LastMessageAuthorName));
    }

    public int LastReadMessageId => TotalMessages - UnreadMessages;
    public bool HasUnreadMessages => UnreadMessages > 0;

    public static ForumSummary Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumSummary value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget),
            ParseUnity);
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

    private static ForumSummary ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumSummary>(p.Client);

    public void Compose(in PacketWriter p)
    {
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);
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

    private static void ComposeUnity(ForumSummary value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}
