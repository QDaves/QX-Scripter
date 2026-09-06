using Qx.Messages;

namespace Qx.Model.Forums;

public sealed record ForumThread(
    Id ThreadId,
    Id AuthorId,
    string AuthorName,
    string Header,
    bool IsSticky,
    bool IsLocked,
    int CreationSecondsAgo,
    int MessageCount,
    int UnreadMessageCount,
    Id LastMessageId,
    Id LastMessageAuthorId,
    string LastMessageAuthorName,
    int LastMessageSecondsAgo,
    byte State,
    Id AdminId,
    string AdminName,
    int AdminOperationSecondsAgo) : IParserComposer<ForumThread>
{
    private string author_name = AuthorName ?? throw new ArgumentNullException(nameof(AuthorName));
    private string header = Header ?? throw new ArgumentNullException(nameof(Header));
    private string last_message_author_name = LastMessageAuthorName ??
        throw new ArgumentNullException(nameof(LastMessageAuthorName));
    private string admin_name = AdminName ?? throw new ArgumentNullException(nameof(AdminName));

    public string AuthorName
    {
        get => author_name;
        init => author_name = value ?? throw new ArgumentNullException(nameof(AuthorName));
    }

    public string Header
    {
        get => header;
        init => header = value ?? throw new ArgumentNullException(nameof(Header));
    }

    public string LastMessageAuthorName
    {
        get => last_message_author_name;
        init => last_message_author_name = value ??
            throw new ArgumentNullException(nameof(LastMessageAuthorName));
    }

    public string AdminName
    {
        get => admin_name;
        init => admin_name = value ?? throw new ArgumentNullException(nameof(AdminName));
    }

    public int LastReadMessageIndex => MessageCount - UnreadMessageCount - 1;
    public bool IsHidden => State is 10 or 20;
    public bool IsHiddenByStaff => State == 20;

    public static ForumThread Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumThread value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget),
            ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumThread));
        return value;
    }

    internal static ForumThread ParseFlashWire(
        in PacketReader p,
        int trailing_bytes,
        ref ForumStringBudget budget)
    {
        ForumProtocol.RequireRemaining(
            in p,
            ForumProtocol.ThreadMinimumBytes,
            trailing_bytes,
            nameof(ForumThread));
        Id thread_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 47),
            "thread");
        Id author_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 43),
            "thread author");
        string author_name = budget.Read(
            in p,
            nameof(AuthorName),
            checked(trailing_bytes + 41));
        string header = budget.Read(
            in p,
            nameof(Header),
            checked(trailing_bytes + 39));
        bool is_sticky = p.ReadBool();
        bool is_locked = p.ReadBool();
        int creation_seconds_ago = p.ReadInt();
        int message_count = p.ReadInt();
        int unread_message_count = p.ReadInt();
        Id last_message_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 21),
            "last message");
        Id last_message_author_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 17),
            "last message author");
        string last_message_author_name = budget.Read(
            in p,
            nameof(LastMessageAuthorName),
            checked(trailing_bytes + 15));
        int last_message_seconds_ago = p.ReadInt();
        byte state = p.ReadByte();
        Id admin_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 6),
            "thread administrator");
        string admin_name = budget.Read(
            in p,
            nameof(AdminName),
            checked(trailing_bytes + sizeof(int)));
        int admin_operation_seconds_ago = p.ReadInt();
        return new ForumThread(
            thread_id,
            author_id,
            author_name,
            header,
            is_sticky,
            is_locked,
            creation_seconds_ago,
            message_count,
            unread_message_count,
            last_message_id,
            last_message_author_id,
            last_message_author_name,
            last_message_seconds_ago,
            state,
            admin_id,
            admin_name,
            admin_operation_seconds_ago);
    }

    private static ForumThread ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumThread>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    internal static void PrepareFlash(
        ForumThread value,
        in PacketWriter p,
        ref ForumStringBudget budget)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumProtocol.RequireFlashId(value.ThreadId, "thread");
        ForumProtocol.RequireFlashId(value.AuthorId, "thread author");
        budget.Require(value.AuthorName, nameof(AuthorName), in p);
        budget.Require(value.Header, nameof(Header), in p);
        ForumProtocol.RequireFlashId(value.LastMessageId, "last message");
        ForumProtocol.RequireFlashId(value.LastMessageAuthorId, "last message author");
        budget.Require(value.LastMessageAuthorName, nameof(LastMessageAuthorName), in p);
        ForumProtocol.RequireFlashId(value.AdminId, "thread administrator");
        budget.Require(value.AdminName, nameof(AdminName), in p);
    }

    internal static void ComposeFlashWire(ForumThread value, in PacketWriter p)
    {
        ForumProtocol.WriteFlashId(in p, value.ThreadId);
        ForumProtocol.WriteFlashId(in p, value.AuthorId);
        p.WriteString(value.AuthorName);
        p.WriteString(value.Header);
        p.WriteBool(value.IsSticky);
        p.WriteBool(value.IsLocked);
        p.WriteInt(value.CreationSecondsAgo);
        p.WriteInt(value.MessageCount);
        p.WriteInt(value.UnreadMessageCount);
        ForumProtocol.WriteFlashId(in p, value.LastMessageId);
        ForumProtocol.WriteFlashId(in p, value.LastMessageAuthorId);
        p.WriteString(value.LastMessageAuthorName);
        p.WriteInt(value.LastMessageSecondsAgo);
        p.WriteByte(value.State);
        ForumProtocol.WriteFlashId(in p, value.AdminId);
        p.WriteString(value.AdminName);
        p.WriteInt(value.AdminOperationSecondsAgo);
    }

    private static void ComposeFlash(ForumThread value, in PacketWriter p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        PrepareFlash(value, in p, ref budget);
        ComposeFlashWire(value, in p);
    }

    private static void ComposeUnity(ForumThread value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}
