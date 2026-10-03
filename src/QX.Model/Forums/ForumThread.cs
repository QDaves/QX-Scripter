using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a thread in a group forum.</summary>
/// <param name="ThreadId">The thread id.</param>
/// <param name="AuthorId">The user id of the thread's author.</param>
/// <param name="AuthorName">The name of the thread's author.</param>
/// <param name="Header">The thread subject.</param>
/// <param name="IsSticky">Whether the thread is pinned to the top of the thread list.</param>
/// <param name="IsLocked">Whether the thread rejects further replies.</param>
/// <param name="CreationSecondsAgo">The number of seconds since the thread was created, at the time it was sent.</param>
/// <param name="MessageCount">The number of messages in the thread.</param>
/// <param name="UnreadMessageCount">The number of messages the viewer has not read.</param>
/// <param name="LastMessageId">The id of the latest message.</param>
/// <param name="LastMessageAuthorId">The user id of the latest message's author.</param>
/// <param name="LastMessageAuthorName">The name of the latest message's author.</param>
/// <param name="LastMessageSecondsAgo">The number of seconds since the latest message was posted, at the time the thread was sent.</param>
/// <param name="State">The moderation state: 0 default, 1 restored, 10 hidden by a forum admin, 20 hidden by staff.</param>
/// <param name="AdminId">The user id of the moderator who last changed the state.</param>
/// <param name="AdminName">The name of the moderator who last changed the state.</param>
/// <param name="AdminOperationSecondsAgo">The number of seconds since the state was last changed, at the time the thread was sent.</param>
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

    /// <summary>Gets the name of the thread's author.</summary>
    public string AuthorName
    {
        get => author_name;
        init => author_name = value ?? throw new ArgumentNullException(nameof(AuthorName));
    }

    /// <summary>Gets the thread subject.</summary>
    public string Header
    {
        get => header;
        init => header = value ?? throw new ArgumentNullException(nameof(Header));
    }

    /// <summary>Gets the name of the latest message's author.</summary>
    public string LastMessageAuthorName
    {
        get => last_message_author_name;
        init => last_message_author_name = value ??
            throw new ArgumentNullException(nameof(LastMessageAuthorName));
    }

    /// <summary>Gets the name of the moderator who last changed the state.</summary>
    public string AdminName
    {
        get => admin_name;
        init => admin_name = value ?? throw new ArgumentNullException(nameof(AdminName));
    }

    /// <summary>Gets the zero based index of the last read message, or -1 when the viewer has read none.</summary>
    public int LastReadMessageIndex => MessageCount - UnreadMessageCount - 1;
    /// <summary>Gets whether the thread is hidden, which is when <see cref="State"/> is 10 or 20.</summary>
    public bool IsHidden => State is 10 or 20;
    /// <summary>Gets whether the thread is hidden by staff, which is when <see cref="State"/> is 20.</summary>
    public bool IsHiddenByStaff => State == 20;

    /// <summary>Parses a forum thread from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumThread Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumThread value = FlashWire.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget));
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

    /// <summary>Composes the forum thread into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}
