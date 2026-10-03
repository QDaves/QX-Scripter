using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the server's reply with the details of a group forum.</summary>
/// <param name="Data">The forum details, including the viewer's permissions.</param>
public sealed record ForumData(ForumDetails Data) : IParserComposer<ForumData>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumData Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumData value = FlashWire.Parse<ForumData>(
            in p,
            (in PacketReader reader) => new(
                ForumDetails.ParseFlashWire(in reader, 0, ref budget)));
        ForumProtocol.RequireEmpty(in p, nameof(ForumData));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ForumData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumDetails.PrepareFlash(value.Data, in p, ref budget);
        ForumDetails.ComposeFlashWire(value.Data, in p);
    }
}

/// <summary>Represents a page of the forum directory.</summary>
/// <param name="ListCode">The directory list the page belongs to.</param>
/// <param name="TotalAmount">The total number of forums in the list.</param>
/// <param name="StartIndex">The zero based index of the first forum on the page.</param>
/// <param name="Forums">The forums on the page. The list is copied and may hold at most 65535 entries.</param>
public sealed record ForumsList(
    ForumListCode ListCode,
    int TotalAmount,
    int StartIndex,
    IReadOnlyList<ForumSummary> Forums) : IParserComposer<ForumsList>
{
    private IReadOnlyList<ForumSummary> forums =
        ForumProtocol.FreezeReferences(Forums, nameof(Forums));

    /// <summary>Gets the forums on the page, as a read only copy.</summary>
    public IReadOnlyList<ForumSummary> Forums
    {
        get => forums;
        init => forums = ForumProtocol.FreezeReferences(value, nameof(Forums));
    }

    /// <summary>Gets the number of forums on the page.</summary>
    public int Amount => Forums.Count;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumsList Parse(in PacketReader p)
    {
        ForumsList value = FlashWire.Parse(in p, ParseFlash);
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Represents a page of threads in a group forum.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="StartIndex">The zero based index of the first thread on the page.</param>
/// <param name="Threads">The threads on the page. The list is copied and may hold at most 65535 entries.</param>
public sealed record ForumThreads(
    Id GroupId,
    int StartIndex,
    IReadOnlyList<ForumThread> Threads) : IParserComposer<ForumThreads>
{
    private IReadOnlyList<ForumThread> threads =
        ForumProtocol.FreezeReferences(Threads, nameof(Threads));

    /// <summary>Gets the threads on the page, as a read only copy.</summary>
    public IReadOnlyList<ForumThread> Threads
    {
        get => threads;
        init => threads = ForumProtocol.FreezeReferences(value, nameof(Threads));
    }

    /// <summary>Gets the number of threads on the page.</summary>
    public int Amount => Threads.Count;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumThreads Parse(in PacketReader p)
    {
        ForumThreads value = FlashWire.Parse(in p, ParseFlash);
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
        var threads = new ForumThread[amount];
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < amount; index++)
        {
            int trailing = checked((amount - index - 1) * ForumProtocol.ThreadMinimumBytes);
            threads[index] = ForumThread.ParseFlashWire(in p, trailing, ref budget);
        }
        return new ForumThreads(group_id, start_index, threads);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ForumThreads value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        int count = ForumProtocol.RequireCount(value.Threads.Count, nameof(Threads));
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < count; index++)
            ForumThread.PrepareFlash(value.Threads[index], in p, ref budget);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(count);
        for (int index = 0; index < count; index++)
            ForumThread.ComposeFlashWire(value.Threads[index], in p);
    }
}

/// <summary>Represents a page of messages in a group forum thread.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="ThreadId">The thread the messages belong to.</param>
/// <param name="StartIndex">The zero based index of the first message on the page.</param>
/// <param name="Messages">The messages on the page. The list is copied and may hold at most 65535 entries.</param>
public sealed record ThreadMessages(
    Id GroupId,
    Id ThreadId,
    int StartIndex,
    IReadOnlyList<ForumPost> Messages) : IParserComposer<ThreadMessages>
{
    private IReadOnlyList<ForumPost> messages =
        ForumProtocol.FreezeReferences(Messages, nameof(Messages));

    /// <summary>Gets the messages on the page, as a read only copy.</summary>
    public IReadOnlyList<ForumPost> Messages
    {
        get => messages;
        init => messages = ForumProtocol.FreezeReferences(value, nameof(Messages));
    }

    /// <summary>Gets the number of messages on the page.</summary>
    public int Amount => Messages.Count;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ThreadMessages Parse(in PacketReader p)
    {
        ThreadMessages value = FlashWire.Parse(in p, ParseFlash);
        ForumProtocol.RequireEmpty(in p, nameof(ThreadMessages));
        return value;
    }

    private static ThreadMessages ParseFlash(in PacketReader p)
    {
        ForumProtocol.RequireRemaining(in p, 16, 0, nameof(ThreadMessages));
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
        return new ThreadMessages(group_id, thread_id, start_index, messages);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ThreadMessages value, in PacketWriter p)
    {
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        ForumProtocol.RequireFlashId(value.ThreadId, "thread");
        int count = ForumProtocol.RequireCount(value.Messages.Count, nameof(Messages));
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < count; index++)
            ForumPost.PrepareFlash(value.Messages[index], in p, ref budget);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        ForumProtocol.WriteFlashId(in p, value.ThreadId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(count);
        for (int index = 0; index < count; index++)
            ForumPost.ComposeFlashWire(value.Messages[index], in p);
    }
}

/// <summary>Represents the server's notice that a thread was created in a group forum.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="Thread">The created thread.</param>
public sealed record PostThread(
    Id GroupId,
    ForumThread Thread) : IParserComposer<PostThread>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PostThread Parse(in PacketReader p)
    {
        PostThread value = FlashWire.Parse(in p, ParseFlash);
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
        return new(group_id, ForumThread.ParseFlashWire(in p, 0, ref budget));
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PostThread value, in PacketWriter p)
    {
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumThread.PrepareFlash(value.Thread, in p, ref budget);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        ForumThread.ComposeFlashWire(value.Thread, in p);
    }
}

/// <summary>Represents the server's notice that a forum message changed, such as after moderation.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="ThreadId">The thread the message belongs to.</param>
/// <param name="Message">The updated message.</param>
public sealed record UpdateMessage(
    Id GroupId,
    Id ThreadId,
    ForumPost Message) : IParserComposer<UpdateMessage>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UpdateMessage Parse(in PacketReader p)
    {
        UpdateMessage value = FlashWire.Parse(in p, ParseFlash);
        ForumProtocol.RequireEmpty(in p, nameof(UpdateMessage));
        return value;
    }

    private static UpdateMessage ParseFlash(in PacketReader p)
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
        return new(group_id, thread_id, ForumPost.ParseFlashWire(in p, 0, ref budget));
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateMessage value, in PacketWriter p)
    {
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        ForumProtocol.RequireFlashId(value.ThreadId, "thread");
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumPost.PrepareFlash(value.Message, in p, ref budget);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        ForumProtocol.WriteFlashId(in p, value.ThreadId);
        ForumPost.ComposeFlashWire(value.Message, in p);
    }
}
