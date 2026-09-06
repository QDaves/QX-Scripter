using Qx.Messages;
using Qx.Model.Forums;
using ForumThreadData = Qx.Model.Forums.ForumThread;

namespace Qx.Model.Messages.Incoming;

public sealed record PostMessage : IParserComposer<PostMessage>
{
    public Id GroupId { get; init; }
    public Id ThreadId { get; init; }
    public string Subject { get; init; }
    public string MessageText { get; init; }
    public ForumPost? Message { get; init; }
    public bool IsRequest => Message is null;

    public PostMessage(
        Id group_id,
        Id thread_id,
        string subject,
        string message_text)
    {
        GroupId = group_id;
        ThreadId = thread_id;
        Subject = subject;
        MessageText = message_text;
    }

    public PostMessage(
        Id group_id,
        Id thread_id,
        ForumPost message)
    {
        GroupId = group_id;
        ThreadId = thread_id;
        Subject = "";
        MessageText = "";
        Message = message;
    }

    public static PostMessage Parse(in PacketReader p) =>
        ParseRoot(in p);

    private static PostMessage ParseRoot(in PacketReader p)
    {
        PostMessage value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(PostMessage));
        return value;
    }

    private static PostMessage ParseFlash(in PacketReader p)
    {
        return p.Header.Direction switch
        {
            Direction.In => ParseIncoming(in p),
            Direction.Out => new PostMessage(
                ForumRequestProtocol.ReadFlashGroupId(in p),
                ForumRequestProtocol.ReadIntId(in p),
                ReadRequestString(in p, nameof(Subject), 2),
                ReadRequestString(in p, nameof(MessageText), 0)),
            _ => throw new InvalidDataException($"Unsupported forum message direction {p.Header.Direction}.")
        };
    }

    private static PostMessage ParseUnity(in PacketReader p)
    {
        return p.Header.Direction switch
        {
            Direction.In => ForumProtocol.UnsupportedUnity<PostMessage>(p.Client),
            Direction.Out => new PostMessage(
                ForumRequestProtocol.ReadUnityGroupId(in p),
                ForumRequestProtocol.ReadIntId(in p),
                ReadRequestString(in p, nameof(Subject), 2),
                ReadRequestString(in p, nameof(MessageText), 0)),
            _ => throw new InvalidDataException($"Unsupported forum message direction {p.Header.Direction}.")
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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
            case Direction.In:
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
            case Direction.Out:
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

    private static void ComposeUnity(PostMessage value, in PacketWriter p)
    {
        switch (p.Header.Direction)
        {
            case Direction.In:
                ForumProtocol.UnsupportedUnity(p.Client);
                return;
            case Direction.Out:
                if (value.Message is not null)
                    throw new InvalidDataException("Outgoing PostMessage cannot contain a parsed forum post.");
                ForumProtocol.RequireFlashId(value.ThreadId, "thread");
                ForumStringBudget request_budget = ForumProtocol.NewStringBudget();
                request_budget.Require(value.Subject, nameof(Subject), in p);
                request_budget.Require(value.MessageText, nameof(MessageText), in p);
                ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
                ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
                p.WriteString(value.Subject);
                p.WriteString(value.MessageText);
                return;
            default:
                throw new InvalidDataException($"Unsupported forum message direction {p.Header.Direction}.");
        }
    }
}

public sealed record UpdateThread : IParserComposer<UpdateThread>
{
    public Id GroupId { get; init; }
    public Id ThreadId { get; init; }
    public bool IsSticky { get; init; }
    public bool IsLocked { get; init; }
    public ForumThreadData? Thread { get; init; }
    public bool IsRequest => Thread is null;

    public UpdateThread(
        Id group_id,
        Id thread_id,
        bool is_sticky,
        bool is_locked)
    {
        GroupId = group_id;
        ThreadId = thread_id;
        IsSticky = is_sticky;
        IsLocked = is_locked;
    }

    public UpdateThread(Id group_id, ForumThreadData thread)
    {
        GroupId = group_id;
        ThreadId = thread.ThreadId;
        IsSticky = thread.IsSticky;
        IsLocked = thread.IsLocked;
        Thread = thread;
    }

    public static UpdateThread Parse(in PacketReader p) =>
        ParseRoot(in p);

    private static UpdateThread ParseRoot(in PacketReader p)
    {
        UpdateThread value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(UpdateThread));
        return value;
    }

    private static UpdateThread ParseFlash(in PacketReader p)
    {
        return p.Header.Direction switch
        {
            Direction.In => ParseIncoming(in p),
            Direction.Out => new UpdateThread(
                ForumRequestProtocol.ReadFlashGroupId(in p),
                ForumRequestProtocol.ReadIntId(in p),
                p.ReadBool(),
                p.ReadBool()),
            _ => throw new InvalidDataException($"Unsupported forum thread direction {p.Header.Direction}.")
        };
    }

    private static UpdateThread ParseUnity(in PacketReader p)
    {
        return p.Header.Direction switch
        {
            Direction.In => ForumProtocol.UnsupportedUnity<UpdateThread>(p.Client),
            Direction.Out => new UpdateThread(
                ForumRequestProtocol.ReadUnityGroupId(in p),
                ForumRequestProtocol.ReadIntId(in p),
                p.ReadBool(),
                p.ReadBool()),
            _ => throw new InvalidDataException($"Unsupported forum thread direction {p.Header.Direction}.")
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static UpdateThread ParseIncoming(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        Id group_id = ForumProtocol.ReadFlashId(
            in p,
            ForumProtocol.ThreadMinimumBytes,
            "forum group");
        return new UpdateThread(
            group_id,
            ForumThreadData.ParseFlashWire(in p, 0, ref budget));
    }

    private static void ComposeFlash(UpdateThread value, in PacketWriter p)
    {
        switch (p.Header.Direction)
        {
            case Direction.In:
                ForumThreadData thread = value.Thread ??
                    throw new InvalidDataException("Incoming UpdateThread requires a forum thread.");
                ForumProtocol.RequireFlashId(value.GroupId, "forum group");
                ForumStringBudget budget = ForumProtocol.NewStringBudget();
                ForumThreadData.PrepareFlash(thread, in p, ref budget);
                ForumProtocol.WriteFlashId(in p, value.GroupId);
                ForumThreadData.ComposeFlashWire(thread, in p);
                return;
            case Direction.Out:
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

    private static void ComposeUnity(UpdateThread value, in PacketWriter p)
    {
        switch (p.Header.Direction)
        {
            case Direction.In:
                ForumProtocol.UnsupportedUnity(p.Client);
                return;
            case Direction.Out:
                if (value.Thread is not null)
                    throw new InvalidDataException("Outgoing UpdateThread cannot contain a parsed forum thread.");
                ForumProtocol.RequireFlashId(value.ThreadId, "thread");
                ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
                ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
                p.WriteBool(value.IsSticky);
                p.WriteBool(value.IsLocked);
                return;
            default:
                throw new InvalidDataException($"Unsupported forum thread direction {p.Header.Direction}.");
        }
    }
}
