using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Represents a forum post message, either the client's request to post or the server's notice of the posted message.
/// </summary>
/// <remarks>
/// The packet direction selects the form. The outgoing request carries <see cref="Subject"/> and
/// <see cref="MessageText"/>, and a thread id of 0 starts a new thread. The incoming notice carries
/// the posted <see cref="Message"/>.
/// </remarks>
public sealed record PostMessage : IParserComposer<PostMessage>
{
    /// <summary>Gets the id of the group that owns the forum.</summary>
    public Id GroupId { get; init; }
    /// <summary>Gets the thread id, 0 in a request that starts a new thread.</summary>
    public Id ThreadId { get; init; }
    /// <summary>Gets the subject of a new thread, empty in an incoming notice.</summary>
    public string Subject { get; init; }
    /// <summary>Gets the text to post, empty for incoming notices.</summary>
    public string MessageText { get; init; }
    /// <summary>Gets the posted message, or <see langword="null"/> for an outgoing request.</summary>
    public ForumPost? Message { get; init; }
    /// <summary>Gets whether the value is an outgoing request, which is when <see cref="Message"/> is <see langword="null"/>.</summary>
    public bool IsRequest => Message is null;

    /// <summary>Initializes a new outgoing post request.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The thread to reply to, or 0 to start a new thread.</param>
    /// <param name="subject">The subject of a new thread.</param>
    /// <param name="messageText">The text to post.</param>
    public PostMessage(
        Id groupId,
        Id threadId,
        string subject,
        string messageText)
    {
        GroupId = groupId;
        ThreadId = threadId;
        Subject = subject;
        MessageText = messageText;
    }

    /// <summary>Initializes a new incoming notice of a posted message.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The thread the message was posted in.</param>
    /// <param name="message">The posted message.</param>
    public PostMessage(
        Id groupId,
        Id threadId,
        ForumPost message)
    {
        GroupId = groupId;
        ThreadId = threadId;
        Subject = "";
        MessageText = "";
        Message = message;
    }

    /// <summary>Parses the message from a packet, in the form its direction selects.</summary>
    /// <param name="p">The packet reader.</param>
    public static PostMessage Parse(in PacketReader p) =>
        ParseRoot(in p);

    private static PostMessage ParseRoot(in PacketReader p)
    {
        PostMessage value = FlashWire.Parse(in p, ParseFlash);
        ForumProtocol.RequireEmpty(in p, nameof(PostMessage));
        return value;
    }

    private static PostMessage ParseFlash(in PacketReader p)
    {
        return p.Header.Direction switch
        {
            MessageDirection.In => ParseIncoming(in p),
            MessageDirection.Out => new PostMessage(
                ForumRequestProtocol.ReadFlashGroupId(in p),
                ForumRequestProtocol.ReadIntId(in p),
                ReadRequestString(in p, nameof(Subject), 2),
                ReadRequestString(in p, nameof(MessageText), 0)),
            _ => throw new InvalidDataException($"Unsupported forum message direction {p.Header.Direction}.")
        };
    }

    /// <summary>Composes the message into a packet, in the form the packet direction selects.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when the value does not match the form the packet direction requires.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static PostMessage ParseIncoming(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        Id group_id = ForumProtocol.ReadFlashId(
            in p,
            checked(sizeof(int) + ForumProtocol.PostMinimumBytes),
            "forum group");
        Id thread_id = ForumProtocol.ReadFlashId(
            in p,
            ForumProtocol.PostMinimumBytes,
            "thread");
        return new PostMessage(
            group_id,
            thread_id,
            ForumPost.ParseFlashWire(in p, 0, ref budget));
    }

    private static string ReadRequestString(
        in PacketReader p,
        string name,
        int trailing_bytes)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        return budget.Read(in p, name, trailing_bytes);
    }

    private static void ComposeFlash(PostMessage value, in PacketWriter p)
    {
        switch (p.Header.Direction)
        {
            case MessageDirection.In:
                ForumPost message = value.Message ??
                    throw new InvalidDataException("Incoming PostMessage requires a forum post.");
                ForumProtocol.RequireFlashId(value.GroupId, "forum group");
                ForumProtocol.RequireFlashId(value.ThreadId, "thread");
                ForumStringBudget incoming_budget = ForumProtocol.NewStringBudget();
                ForumPost.PrepareFlash(message, in p, ref incoming_budget);
                ForumProtocol.WriteFlashId(in p, value.GroupId);
                ForumProtocol.WriteFlashId(in p, value.ThreadId);
                ForumPost.ComposeFlashWire(message, in p);
                return;
            case MessageDirection.Out:
                if (value.Message is not null)
                    throw new InvalidDataException("Outgoing PostMessage cannot contain a parsed forum post.");
                ForumProtocol.RequireFlashId(value.GroupId, "forum group");
                ForumProtocol.RequireFlashId(value.ThreadId, "thread");
                ForumStringBudget request_budget = ForumProtocol.NewStringBudget();
                request_budget.Require(value.Subject, nameof(Subject), in p);
                request_budget.Require(value.MessageText, nameof(MessageText), in p);
                ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
                ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
                p.WriteString(value.Subject);
                p.WriteString(value.MessageText);
                return;
            default:
                throw new InvalidDataException($"Unsupported forum message direction {p.Header.Direction}.");
        }
    }
}

/// <summary>
/// Represents a forum thread update, either the client's request to change the sticky and locked flags or the server's notice of the updated thread.
/// </summary>
/// <remarks>
/// The packet direction selects the form. The outgoing request carries the flags; the incoming
/// notice carries the whole <see cref="Thread"/>, and the flags are copied from it.
/// </remarks>
public sealed record UpdateThread : IParserComposer<UpdateThread>
{
    /// <summary>Gets the id of the group that owns the forum.</summary>
    public Id GroupId { get; init; }
    /// <summary>Gets the thread id.</summary>
    public Id ThreadId { get; init; }
    /// <summary>Gets whether the thread is pinned to the top of the thread list.</summary>
    public bool IsSticky { get; init; }
    /// <summary>Gets whether the thread rejects further replies.</summary>
    public bool IsLocked { get; init; }
    /// <summary>Gets the updated thread, or <see langword="null"/> for an outgoing request.</summary>
    public ForumThread? Thread { get; init; }
    /// <summary>Gets whether the value is an outgoing request, which is when <see cref="Thread"/> is <see langword="null"/>.</summary>
    public bool IsRequest => Thread is null;

    /// <summary>Initializes a new outgoing request to change a thread's flags.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The thread to change.</param>
    /// <param name="isSticky">Whether the thread is pinned to the top of the thread list.</param>
    /// <param name="isLocked">Whether the thread rejects further replies.</param>
    public UpdateThread(
        Id groupId,
        Id threadId,
        bool isSticky,
        bool isLocked)
    {
        GroupId = groupId;
        ThreadId = threadId;
        IsSticky = isSticky;
        IsLocked = isLocked;
    }

    /// <summary>Initializes a new incoming notice of an updated thread.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="thread">The updated thread, which also supplies the thread id and flags.</param>
    public UpdateThread(Id groupId, ForumThread thread)
    {
        GroupId = groupId;
        ThreadId = thread.ThreadId;
        IsSticky = thread.IsSticky;
        IsLocked = thread.IsLocked;
        Thread = thread;
    }

    /// <summary>Parses the message from a packet, in the form its direction selects.</summary>
    /// <param name="p">The packet reader.</param>
    public static UpdateThread Parse(in PacketReader p) =>
        ParseRoot(in p);

    private static UpdateThread ParseRoot(in PacketReader p)
    {
        UpdateThread value = FlashWire.Parse(in p, ParseFlash);
        ForumProtocol.RequireEmpty(in p, nameof(UpdateThread));
        return value;
    }

    private static UpdateThread ParseFlash(in PacketReader p)
    {
        return p.Header.Direction switch
        {
            MessageDirection.In => ParseIncoming(in p),
            MessageDirection.Out => new UpdateThread(
                ForumRequestProtocol.ReadFlashGroupId(in p),
                ForumRequestProtocol.ReadIntId(in p),
                p.ReadBool(),
                p.ReadBool()),
            _ => throw new InvalidDataException($"Unsupported forum thread direction {p.Header.Direction}.")
        };
    }

    /// <summary>Composes the message into a packet, in the form the packet direction selects.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when the value does not match the form the packet direction requires.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static UpdateThread ParseIncoming(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        Id group_id = ForumProtocol.ReadFlashId(
            in p,
            ForumProtocol.ThreadMinimumBytes,
            "forum group");
        return new UpdateThread(
            group_id,
            ForumThread.ParseFlashWire(in p, 0, ref budget));
    }

    private static void ComposeFlash(UpdateThread value, in PacketWriter p)
    {
        switch (p.Header.Direction)
        {
            case MessageDirection.In:
                ForumThread thread = value.Thread ??
                    throw new InvalidDataException("Incoming UpdateThread requires a forum thread.");
                ForumProtocol.RequireFlashId(value.GroupId, "forum group");
                ForumStringBudget budget = ForumProtocol.NewStringBudget();
                ForumThread.PrepareFlash(thread, in p, ref budget);
                ForumProtocol.WriteFlashId(in p, value.GroupId);
                ForumThread.ComposeFlashWire(thread, in p);
                return;
            case MessageDirection.Out:
                if (value.Thread is not null)
                    throw new InvalidDataException("Outgoing UpdateThread cannot contain a parsed forum thread.");
                ForumProtocol.RequireFlashId(value.GroupId, "forum group");
                ForumProtocol.RequireFlashId(value.ThreadId, "thread");
                ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
                ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
                p.WriteBool(value.IsSticky);
                p.WriteBool(value.IsLocked);
                return;
            default:
                throw new InvalidDataException($"Unsupported forum thread direction {p.Header.Direction}.");
        }
    }
}
