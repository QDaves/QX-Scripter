using Qx.Messages;
using Qx.Model.Forums;
using ForumThreadData = Qx.Model.Forums.ForumThread;

namespace Qx.Model.Messages.Incoming;

public sealed record ForumData(ForumDetails Data) : IParserComposer<ForumData>
{
    public static ForumData Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumData value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => new(
                ForumDetails.ParseFlashWire(in reader, 0, ref budget)),
            ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumData));
        return value;
    }

    private static ForumData ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumData>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumDetails.PrepareFlash(value.Data, in p, ref budget);
        ForumDetails.ComposeFlashWire(value.Data, in p);
    }

    private static void ComposeUnity(ForumData value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record ForumStats(ForumDetails Data) : IParserComposer<ForumStats>
{
    public static ForumStats Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumStats value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => new(
                ForumDetails.ParseFlashWire(in reader, 0, ref budget)),
            ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumStats));
        return value;
    }

    private static ForumStats ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumStats>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumStats value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumDetails.PrepareFlash(value.Data, in p, ref budget);
        ForumDetails.ComposeFlashWire(value.Data, in p);
    }

    private static void ComposeUnity(ForumStats value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record ForumsList(
    ForumListCode ListCode,
    int TotalAmount,
    int StartIndex,
    IReadOnlyList<ForumSummary> Forums) : IParserComposer<ForumsList>
{
    private IReadOnlyList<ForumSummary> forums =
        ForumProtocol.FreezeReferences(Forums, nameof(Forums));

    public IReadOnlyList<ForumSummary> Forums
    {
        get => forums;
        init => forums = ForumProtocol.FreezeReferences(value, nameof(Forums));
    }

    public int Amount => Forums.Count;

    public static ForumsList Parse(in PacketReader p)
    {
        ForumsList value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumsList));
        return value;
    }

    private static ForumsList ParseFlash(in PacketReader p)
    {
        ForumProtocol.RequireRemaining(in p, 16, 0, nameof(ForumsList));
        ForumListCode list_code = (ForumListCode)p.ReadInt();
        int total_amount = p.ReadInt();
        int start_index = p.ReadInt();
        int amount = ForumProtocol.ReadFlashCount(
            in p,
            ForumProtocol.SummaryMinimumBytes,
            0,
            "forum");
        var forums = new ForumSummary[amount];
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < amount; index++)
        {
            int trailing = checked((amount - index - 1) * ForumProtocol.SummaryMinimumBytes);
            forums[index] = ForumSummary.ParseFlashWire(in p, trailing, ref budget);
        }
        return new ForumsList(list_code, total_amount, start_index, forums);
    }

    private static ForumsList ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumsList>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumsList value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = ForumProtocol.RequireCount(value.Forums.Count, nameof(Forums));
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < count; index++)
            ForumSummary.PrepareFlash(value.Forums[index], in p, ref budget);
        p.WriteInt((int)value.ListCode);
        p.WriteInt(value.TotalAmount);
        p.WriteInt(value.StartIndex);
        p.WriteInt(count);
        for (int index = 0; index < count; index++)
            ForumSummary.ComposeFlashWire(value.Forums[index], in p);
    }

    private static void ComposeUnity(ForumsList value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record ForumThreads(
    Id GroupId,
    int StartIndex,
    IReadOnlyList<ForumThreadData> Threads) : IParserComposer<ForumThreads>
{
    private IReadOnlyList<ForumThreadData> threads =
        ForumProtocol.FreezeReferences(Threads, nameof(Threads));

    public IReadOnlyList<ForumThreadData> Threads
    {
        get => threads;
        init => threads = ForumProtocol.FreezeReferences(value, nameof(Threads));
    }

    public int Amount => Threads.Count;

    public static ForumThreads Parse(in PacketReader p)
    {
        ForumThreads value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumThreads));
        return value;
    }

    private static ForumThreads ParseFlash(in PacketReader p)
    {
        ForumProtocol.RequireRemaining(in p, 12, 0, nameof(ForumThreads));
        Id group_id = ForumProtocol.ReadFlashId(in p, 8, "forum group");
        int start_index = p.ReadInt();
        int amount = ForumProtocol.ReadFlashCount(
            in p,
            ForumProtocol.ThreadMinimumBytes,
            0,
            "forum thread");
        var threads = new ForumThreadData[amount];
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < amount; index++)
        {
            int trailing = checked((amount - index - 1) * ForumProtocol.ThreadMinimumBytes);
            threads[index] = ForumThreadData.ParseFlashWire(in p, trailing, ref budget);
        }
        return new ForumThreads(group_id, start_index, threads);
    }

    private static ForumThreads ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumThreads>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumThreads value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        int count = ForumProtocol.RequireCount(value.Threads.Count, nameof(Threads));
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < count; index++)
            ForumThreadData.PrepareFlash(value.Threads[index], in p, ref budget);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(count);
        for (int index = 0; index < count; index++)
            ForumThreadData.ComposeFlashWire(value.Threads[index], in p);
    }

    private static void ComposeUnity(ForumThreads value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record ThreadMessages(
    Id GroupId,
    Id ThreadId,
    int StartIndex,
    IReadOnlyList<ForumPost> Messages) : IParserComposer<ThreadMessages>
{
    private IReadOnlyList<ForumPost> messages =
        ForumProtocol.FreezeReferences(Messages, nameof(Messages));

    public IReadOnlyList<ForumPost> Messages
    {
        get => messages;
        init => messages = ForumProtocol.FreezeReferences(value, nameof(Messages));
    }

    public int Amount => Messages.Count;

    public static ThreadMessages Parse(in PacketReader p)
    {
        ThreadMessages value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ThreadMessages));
        return value;
    }

    private static ThreadMessages ParseFlash(in PacketReader p) =>
        ParseMessages<ThreadMessages>(
            in p,
            static (group_id, thread_id, start_index, messages) =>
                new ThreadMessages(group_id, thread_id, start_index, messages));

    private static ThreadMessages ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ThreadMessages>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ThreadMessages value, in PacketWriter p) =>
        ComposeMessages(value.GroupId, value.ThreadId, value.StartIndex, value.Messages, in p);

    private static void ComposeUnity(ThreadMessages value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);

    internal static T ParseMessages<T>(
        in PacketReader p,
        Func<Id, Id, int, IReadOnlyList<ForumPost>, T> create)
    {
        ForumProtocol.RequireRemaining(in p, 16, 0, typeof(T).Name);
        Id group_id = ForumProtocol.ReadFlashId(in p, 12, "forum group");
        Id thread_id = ForumProtocol.ReadFlashId(in p, 8, "thread");
        int start_index = p.ReadInt();
        int amount = ForumProtocol.ReadFlashCount(
            in p,
            ForumProtocol.PostMinimumBytes,
            0,
            "forum message");
        var messages = new ForumPost[amount];
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < amount; index++)
        {
            int trailing = checked((amount - index - 1) * ForumProtocol.PostMinimumBytes);
            messages[index] = ForumPost.ParseFlashWire(in p, trailing, ref budget);
        }
        return create(group_id, thread_id, start_index, messages);
    }

    internal static void ComposeMessages(
        Id group_id,
        Id thread_id,
        int start_index,
        IReadOnlyList<ForumPost> messages,
        in PacketWriter p)
    {
        ForumProtocol.RequireFlashId(group_id, "forum group");
        ForumProtocol.RequireFlashId(thread_id, "thread");
        int count = ForumProtocol.RequireCount(messages.Count, nameof(Messages));
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < count; index++)
            ForumPost.PrepareFlash(messages[index], in p, ref budget);
        ForumProtocol.WriteFlashId(in p, group_id);
        ForumProtocol.WriteFlashId(in p, thread_id);
        p.WriteInt(start_index);
        p.WriteInt(count);
        for (int index = 0; index < count; index++)
            ForumPost.ComposeFlashWire(messages[index], in p);
    }
}

public sealed record ForumThreadMessages(
    Id GroupId,
    Id ThreadId,
    int StartIndex,
    IReadOnlyList<ForumPost> Messages) : IParserComposer<ForumThreadMessages>
{
    private IReadOnlyList<ForumPost> messages =
        ForumProtocol.FreezeReferences(Messages, nameof(Messages));

    public IReadOnlyList<ForumPost> Messages
    {
        get => messages;
        init => messages = ForumProtocol.FreezeReferences(value, nameof(Messages));
    }

    public int Amount => Messages.Count;

    public static ForumThreadMessages Parse(in PacketReader p)
    {
        ForumThreadMessages value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumThreadMessages));
        return value;
    }

    private static ForumThreadMessages ParseFlash(in PacketReader p) =>
        ThreadMessages.ParseMessages<ForumThreadMessages>(
            in p,
            static (group_id, thread_id, start_index, messages) =>
                new ForumThreadMessages(group_id, thread_id, start_index, messages));

    private static ForumThreadMessages ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumThreadMessages>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumThreadMessages value, in PacketWriter p) =>
        ThreadMessages.ComposeMessages(
            value.GroupId,
            value.ThreadId,
            value.StartIndex,
            value.Messages,
            in p);

    private static void ComposeUnity(ForumThreadMessages value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record PostThread(
    Id GroupId,
    ForumThreadData Thread) : IParserComposer<PostThread>
{
    public static PostThread Parse(in PacketReader p)
    {
        PostThread value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(PostThread));
        return value;
    }

    private static PostThread ParseFlash(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        Id group_id = ForumProtocol.ReadFlashId(
            in p,
            ForumProtocol.ThreadMinimumBytes,
            "forum group");
        return new(group_id, ForumThreadData.ParseFlashWire(in p, 0, ref budget));
    }

    private static PostThread ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<PostThread>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PostThread value, in PacketWriter p) =>
        ComposeThread(value.GroupId, value.Thread, in p);

    private static void ComposeUnity(PostThread value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);

    internal static void ComposeThread(Id group_id, ForumThreadData thread, in PacketWriter p)
    {
        ForumProtocol.RequireFlashId(group_id, "forum group");
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumThreadData.PrepareFlash(thread, in p, ref budget);
        ForumProtocol.WriteFlashId(in p, group_id);
        ForumThreadData.ComposeFlashWire(thread, in p);
    }

    internal static T ParseThread<T>(
        in PacketReader p,
        Func<Id, ForumThreadData, T> create)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        Id group_id = ForumProtocol.ReadFlashId(
            in p,
            ForumProtocol.ThreadMinimumBytes,
            "forum group");
        return create(group_id, ForumThreadData.ParseFlashWire(in p, 0, ref budget));
    }
}

public sealed record PostForumThreadOk(
    Id GroupId,
    ForumThreadData Thread) : IParserComposer<PostForumThreadOk>
{
    public static PostForumThreadOk Parse(in PacketReader p)
    {
        PostForumThreadOk value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(PostForumThreadOk));
        return value;
    }

    private static PostForumThreadOk ParseFlash(in PacketReader p) =>
        PostThread.ParseThread<PostForumThreadOk>(in p, static (group_id, thread) => new(group_id, thread));

    private static PostForumThreadOk ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<PostForumThreadOk>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PostForumThreadOk value, in PacketWriter p) =>
        PostThread.ComposeThread(value.GroupId, value.Thread, in p);

    private static void ComposeUnity(PostForumThreadOk value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record PostForumMessageOk(
    Id GroupId,
    Id ThreadId,
    ForumPost Message) : IParserComposer<PostForumMessageOk>
{
    public static PostForumMessageOk Parse(in PacketReader p)
    {
        PostForumMessageOk value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(PostForumMessageOk));
        return value;
    }

    private static PostForumMessageOk ParseFlash(in PacketReader p) =>
        ParseMessage<PostForumMessageOk>(in p, static (group_id, thread_id, message) => new(group_id, thread_id, message));

    private static PostForumMessageOk ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<PostForumMessageOk>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PostForumMessageOk value, in PacketWriter p) =>
        ComposeMessage(value.GroupId, value.ThreadId, value.Message, in p);

    private static void ComposeUnity(PostForumMessageOk value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);

    internal static T ParseMessage<T>(
        in PacketReader p,
        Func<Id, Id, ForumPost, T> create)
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
        return create(group_id, thread_id, ForumPost.ParseFlashWire(in p, 0, ref budget));
    }

    internal static void ComposeMessage(
        Id group_id,
        Id thread_id,
        ForumPost message,
        in PacketWriter p)
    {
        ForumProtocol.RequireFlashId(group_id, "forum group");
        ForumProtocol.RequireFlashId(thread_id, "thread");
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumPost.PrepareFlash(message, in p, ref budget);
        ForumProtocol.WriteFlashId(in p, group_id);
        ForumProtocol.WriteFlashId(in p, thread_id);
        ForumPost.ComposeFlashWire(message, in p);
    }
}

public sealed record ForumThread(
    Id GroupId,
    ForumThreadData Thread) : IParserComposer<ForumThread>
{
    public static ForumThread Parse(in PacketReader p)
    {
        ForumThread value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumThread));
        return value;
    }

    private static ForumThread ParseFlash(in PacketReader p) =>
        PostThread.ParseThread<ForumThread>(in p, static (group_id, thread) => new(group_id, thread));

    private static ForumThread ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumThread>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumThread value, in PacketWriter p) =>
        PostThread.ComposeThread(value.GroupId, value.Thread, in p);

    private static void ComposeUnity(ForumThread value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record UpdateMessage(
    Id GroupId,
    Id ThreadId,
    ForumPost Message) : IParserComposer<UpdateMessage>
{
    public static UpdateMessage Parse(in PacketReader p)
    {
        UpdateMessage value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(UpdateMessage));
        return value;
    }

    private static UpdateMessage ParseFlash(in PacketReader p) =>
        PostForumMessageOk.ParseMessage<UpdateMessage>(in p, static (group_id, thread_id, message) => new(group_id, thread_id, message));

    private static UpdateMessage ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<UpdateMessage>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateMessage value, in PacketWriter p) =>
        PostForumMessageOk.ComposeMessage(value.GroupId, value.ThreadId, value.Message, in p);

    private static void ComposeUnity(UpdateMessage value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record ForumMessage(
    Id GroupId,
    Id ThreadId,
    ForumPost Message) : IParserComposer<ForumMessage>
{
    public static ForumMessage Parse(in PacketReader p)
    {
        ForumMessage value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumMessage));
        return value;
    }

    private static ForumMessage ParseFlash(in PacketReader p) =>
        PostForumMessageOk.ParseMessage<ForumMessage>(in p, static (group_id, thread_id, message) => new(group_id, thread_id, message));

    private static ForumMessage ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumMessage>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumMessage value, in PacketWriter p) =>
        PostForumMessageOk.ComposeMessage(value.GroupId, value.ThreadId, value.Message, in p);

    private static void ComposeUnity(ForumMessage value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}
