using Qx.Messages;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record GetForumStats(Id GroupId) : IParserComposer<GetForumStats>
{
    public static GetForumStats Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetForumStats ParseFlash(in PacketReader p) =>
        new(ForumRequestProtocol.ReadFlashGroupId(in p));

    private static GetForumStats ParseUnity(in PacketReader p) =>
        new(ForumRequestProtocol.ReadUnityGroupId(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetForumStats value, in PacketWriter p) =>
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);

    private static void ComposeUnity(GetForumStats value, in PacketWriter p) =>
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
}

public sealed record GetThreads(
    Id GroupId,
    int StartIndex,
    int MaxCount) : IParserComposer<GetThreads>
{
    public static GetThreads Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetThreads ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            p.ReadInt(),
            p.ReadInt());

    private static GetThreads ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetThreads value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.MaxCount);
    }

    private static void ComposeUnity(GetThreads value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.MaxCount);
    }
}

public sealed record GetMessages(
    Id GroupId,
    Id ThreadId,
    int StartIndex,
    int MaxCount) : IParserComposer<GetMessages>
{
    public static GetMessages Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetMessages ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadInt());

    private static GetMessages ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetMessages value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.MaxCount);
    }

    private static void ComposeUnity(GetMessages value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.MaxCount);
    }
}

public sealed record GetThread(
    Id GroupId,
    Id ThreadId) : IParserComposer<GetThread>
{
    public static GetThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p));

    private static GetThread ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
    }

    private static void ComposeUnity(GetThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
    }
}

public sealed record GetForumsList(
    ForumListCode ListCode,
    int StartIndex,
    int MaxCount) : IParserComposer<GetForumsList>
{
    public static GetForumsList Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetForumsList ParseFlash(in PacketReader p) =>
        new(
            (ForumListCode)p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    private static GetForumsList ParseUnity(in PacketReader p) =>
        new(
            (ForumListCode)p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetForumsList value, in PacketWriter p)
    {
        p.WriteInt((int)value.ListCode);
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.MaxCount);
    }

    private static void ComposeUnity(GetForumsList value, in PacketWriter p)
    {
        p.WriteInt((int)value.ListCode);
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.MaxCount);
    }
}

public sealed record UpdateForumSettings(
    Id GroupId,
    int ReadLevel,
    int PostMessageLevel,
    int PostThreadLevel,
    int ModerateLevel) : IParserComposer<UpdateForumSettings>
{
    public static UpdateForumSettings Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static UpdateForumSettings ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    private static UpdateForumSettings ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateForumSettings value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        p.WriteInt(value.ReadLevel);
        p.WriteInt(value.PostMessageLevel);
        p.WriteInt(value.PostThreadLevel);
        p.WriteInt(value.ModerateLevel);
    }

    private static void ComposeUnity(UpdateForumSettings value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        p.WriteInt(value.ReadLevel);
        p.WriteInt(value.PostMessageLevel);
        p.WriteInt(value.PostThreadLevel);
        p.WriteInt(value.ModerateLevel);
    }
}

public sealed record GetUnreadForumsCount : IParserComposer<GetUnreadForumsCount>
{
    public static GetUnreadForumsCount Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetUnreadForumsCount ParseFlash(in PacketReader p) => new();

    private static GetUnreadForumsCount ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetUnreadForumsCount value, in PacketWriter p) { }

    private static void ComposeUnity(GetUnreadForumsCount value, in PacketWriter p) { }
}

public sealed record ModerateThread(
    Id GroupId,
    Id ThreadId,
    int State) : IParserComposer<ModerateThread>
{
    public static ModerateThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static ModerateThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    private static ModerateThread ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ModerateThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.State);
    }

    private static void ComposeUnity(ModerateThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.State);
    }
}

public sealed record ModerateMessage(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int State) : IParserComposer<ModerateMessage>
{
    public static ModerateMessage Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static ModerateMessage ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    private static ModerateMessage ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ModerateMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        ForumRequestProtocol.WriteIntId(in p, value.MessageId, "message");
        p.WriteInt(value.State);
    }

    private static void ComposeUnity(ModerateMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        ForumRequestProtocol.WriteIntId(in p, value.MessageId, "message");
        p.WriteInt(value.State);
    }
}

public sealed record CallForHelpFromForumThread(
    Id GroupId,
    Id ThreadId,
    int CategoryId,
    string Report,
    string FirstContext,
    string SecondContext) : IParserComposer<CallForHelpFromForumThread>
{
    public static CallForHelpFromForumThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static CallForHelpFromForumThread ParseFlash(in PacketReader p)
    {
        return new CallForHelpFromForumThread(
            ForumProtocol.ReadFlashId(in p),
            ForumProtocol.ReadFlashId(in p),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());
    }

    private static CallForHelpFromForumThread ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<CallForHelpFromForumThread>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CallForHelpFromForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.PrepareStrings(in p, value.Report, value.FirstContext, value.SecondContext);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        ForumProtocol.WriteFlashId(in p, value.ThreadId);
        p.WriteInt(value.CategoryId);
        p.WriteString(value.Report);
        p.WriteString(value.FirstContext);
        p.WriteString(value.SecondContext);
    }

    private static void ComposeUnity(CallForHelpFromForumThread value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record CallForHelpFromForumMessage(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int CategoryId,
    string Report,
    string FirstContext,
    string SecondContext) : IParserComposer<CallForHelpFromForumMessage>
{
    public static CallForHelpFromForumMessage Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static CallForHelpFromForumMessage ParseFlash(in PacketReader p)
    {
        return new CallForHelpFromForumMessage(
            ForumProtocol.ReadFlashId(in p),
            ForumProtocol.ReadFlashId(in p),
            ForumProtocol.ReadFlashId(in p),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());
    }

    private static CallForHelpFromForumMessage ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<CallForHelpFromForumMessage>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CallForHelpFromForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.PrepareStrings(in p, value.Report, value.FirstContext, value.SecondContext);
        ForumProtocol.WriteFlashId(in p, value.GroupId);
        ForumProtocol.WriteFlashId(in p, value.ThreadId);
        ForumProtocol.WriteFlashId(in p, value.MessageId);
        p.WriteInt(value.CategoryId);
        p.WriteString(value.Report);
        p.WriteString(value.FirstContext);
        p.WriteString(value.SecondContext);
    }

    private static void ComposeUnity(CallForHelpFromForumMessage value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public readonly record struct ForumReadMarker(
    Id GroupId,
    Id LastReadMessageId,
    bool MarkAsRead) : IParserComposer<ForumReadMarker>
{
    public bool MarkEntireForumRead => MarkAsRead;

    public static ForumReadMarker Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    internal static ForumReadMarker ParseWire(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ForumReadMarker ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadBool());

    private static ForumReadMarker ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    internal static void Prepare(ForumReadMarker value, ClientType client)
    {
        if (client is ClientType.Flash)
            ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        else if (client is not ClientType.Unity)
            throw new UnsupportedClientException(client);
        ForumProtocol.RequireFlashId(value.LastReadMessageId, "last-read message");
    }

    internal static void ComposeWire(ForumReadMarker value, in PacketWriter p) =>
        ModernWireClients.Compose(value, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ForumReadMarker value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.LastReadMessageId, "last-read message");
        p.WriteBool(value.MarkAsRead);
    }

    private static void ComposeUnity(ForumReadMarker value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.LastReadMessageId, "last-read message");
        p.WriteBool(value.MarkAsRead);
    }
}

public sealed record UpdateForumReadMarker(
    IReadOnlyList<ForumReadMarker> Markers) : IParserComposer<UpdateForumReadMarker>
{
    private IReadOnlyList<ForumReadMarker> markers =
        ForumProtocol.FreezeValues(Markers, nameof(Markers));

    public IReadOnlyList<ForumReadMarker> Markers
    {
        get => markers;
        init => markers = ForumProtocol.FreezeValues(value, nameof(Markers));
    }

    public static UpdateForumReadMarker Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static UpdateForumReadMarker ParseFlash(in PacketReader p)
    {
        int count = ForumRequestProtocol.ReadFlashMarkerCount(in p);
        var markers = new ForumReadMarker[count];
        for (int index = 0; index < count; index++)
            markers[index] = ForumReadMarker.ParseWire(in p);
        return new UpdateForumReadMarker(markers);
    }

    private static UpdateForumReadMarker ParseUnity(in PacketReader p)
    {
        int count = ForumRequestProtocol.ReadUnityMarkerCount(in p);
        if (count > p.Available / 13)
            throw new InvalidDataException("Forum read marker count exceeds the remaining payload capacity.");
        var markers = new ForumReadMarker[count];
        for (int index = 0; index < count; index++)
            markers[index] = ForumReadMarker.ParseWire(in p);
        return new UpdateForumReadMarker(markers);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateForumReadMarker value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = ForumProtocol.RequireCount(value.Markers.Count, nameof(Markers));
        for (int index = 0; index < count; index++)
            ForumReadMarker.Prepare(value.Markers[index], p.Client);
        ForumRequestProtocol.WriteFlashMarkerCount(in p, count);
        for (int index = 0; index < count; index++)
            ForumReadMarker.ComposeWire(value.Markers[index], in p);
    }

    private static void ComposeUnity(UpdateForumReadMarker value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = ForumProtocol.RequireCount(value.Markers.Count, nameof(Markers));
        for (int index = 0; index < count; index++)
            ForumReadMarker.Prepare(value.Markers[index], p.Client);
        ForumRequestProtocol.WriteUnityMarkerCount(in p, count);
        for (int index = 0; index < count; index++)
            ForumReadMarker.ComposeWire(value.Markers[index], in p);
    }
}

public sealed record GetForumThreads(
    Id GroupId,
    int StartIndex,
    int Amount) : IParserComposer<GetForumThreads>
{
    public static GetForumThreads Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetForumThreads ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            p.ReadInt(),
            p.ReadInt());

    private static GetForumThreads ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetForumThreads value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.Amount);
    }

    private static void ComposeUnity(GetForumThreads value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.Amount);
    }
}

public sealed record GetForumThreadMessages(
    Id GroupId,
    Id ThreadId,
    int StartIndex,
    int Amount) : IParserComposer<GetForumThreadMessages>
{
    public static GetForumThreadMessages Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetForumThreadMessages ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadInt());

    private static GetForumThreadMessages ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetForumThreadMessages value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.Amount);
    }

    private static void ComposeUnity(GetForumThreadMessages value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.StartIndex);
        p.WriteInt(value.Amount);
    }
}

public sealed record GetForumThread(
    Id GroupId,
    Id ThreadId) : IParserComposer<GetForumThread>
{
    public static GetForumThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static GetForumThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p));

    private static GetForumThread ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
    }

    private static void ComposeUnity(GetForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
    }
}

public sealed record PostForumMessage(
    Id GroupId,
    Id ThreadId,
    string Subject,
    string MessageText) : IParserComposer<PostForumMessage>
{
    public static PostForumMessage Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static PostForumMessage ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadString(),
            p.ReadString());

    private static PostForumMessage ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadString(),
            p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PostForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.PrepareStrings(in p, value.Subject, value.MessageText);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteString(value.Subject);
        p.WriteString(value.MessageText);
    }

    private static void ComposeUnity(PostForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.PrepareStrings(in p, value.Subject, value.MessageText);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteString(value.Subject);
        p.WriteString(value.MessageText);
    }
}

public sealed record ModerateForumThread(
    Id GroupId,
    Id ThreadId,
    int State) : IParserComposer<ModerateForumThread>
{
    public static ModerateForumThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static ModerateForumThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    private static ModerateForumThread ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ModerateForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.State);
    }

    private static void ComposeUnity(ModerateForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.State);
    }
}

public sealed record ModerateForumMessage(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int State) : IParserComposer<ModerateForumMessage>
{
    public static ModerateForumMessage Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static ModerateForumMessage ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    private static ModerateForumMessage ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ModerateForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        ForumRequestProtocol.WriteIntId(in p, value.MessageId, "message");
        p.WriteInt(value.State);
    }

    private static void ComposeUnity(ModerateForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        ForumRequestProtocol.WriteIntId(in p, value.MessageId, "message");
        p.WriteInt(value.State);
    }
}

public sealed record UpdateForumThread(
    Id GroupId,
    Id ThreadId,
    bool IsSticky,
    bool IsLocked) : IParserComposer<UpdateForumThread>
{
    public static UpdateForumThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static UpdateForumThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadBool(),
            p.ReadBool());

    private static UpdateForumThread ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadBool(),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteBool(value.IsSticky);
        p.WriteBool(value.IsLocked);
    }

    private static void ComposeUnity(UpdateForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteBool(value.IsSticky);
        p.WriteBool(value.IsLocked);
    }
}

public sealed record UpdateForumReadMarkers(
    IReadOnlyList<ForumReadMarker> Markers) : IParserComposer<UpdateForumReadMarkers>
{
    private IReadOnlyList<ForumReadMarker> markers =
        ForumProtocol.FreezeValues(Markers, nameof(Markers));

    public IReadOnlyList<ForumReadMarker> Markers
    {
        get => markers;
        init => markers = ForumProtocol.FreezeValues(value, nameof(Markers));
    }

    public static UpdateForumReadMarkers Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static UpdateForumReadMarkers ParseFlash(in PacketReader p)
    {
        int count = ForumRequestProtocol.ReadFlashMarkerCount(in p);
        var markers = new ForumReadMarker[count];
        for (int index = 0; index < count; index++)
            markers[index] = ForumReadMarker.ParseWire(in p);
        return new UpdateForumReadMarkers(markers);
    }

    private static UpdateForumReadMarkers ParseUnity(in PacketReader p)
    {
        int count = ForumRequestProtocol.ReadUnityMarkerCount(in p);
        if (count > p.Available / 13)
            throw new InvalidDataException("Forum read marker count exceeds the remaining payload capacity.");
        var markers = new ForumReadMarker[count];
        for (int index = 0; index < count; index++)
            markers[index] = ForumReadMarker.ParseWire(in p);
        return new UpdateForumReadMarkers(markers);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateForumReadMarkers value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = ForumProtocol.RequireCount(value.Markers.Count, nameof(Markers));
        for (int index = 0; index < count; index++)
            ForumReadMarker.Prepare(value.Markers[index], p.Client);
        ForumRequestProtocol.WriteFlashMarkerCount(in p, count);
        for (int index = 0; index < count; index++)
            ForumReadMarker.ComposeWire(value.Markers[index], in p);
    }

    private static void ComposeUnity(UpdateForumReadMarkers value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = ForumProtocol.RequireCount(value.Markers.Count, nameof(Markers));
        for (int index = 0; index < count; index++)
            ForumReadMarker.Prepare(value.Markers[index], p.Client);
        ForumRequestProtocol.WriteUnityMarkerCount(in p, count);
        for (int index = 0; index < count; index++)
            ForumReadMarker.ComposeWire(value.Markers[index], in p);
    }
}

public sealed record ReportForumThread(
    Id GroupId,
    Id ThreadId,
    int CategoryId,
    string Report) : IParserComposer<ReportForumThread>
{
    public static ReportForumThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static ReportForumThread ParseFlash(in PacketReader p) =>
        throw new UnsupportedClientException(p.Client);

    private static ReportForumThread ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ReportForumThread value, in PacketWriter p) =>
        throw new UnsupportedClientException(p.Client);

    private static void ComposeUnity(ReportForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.PrepareStrings(in p, value.Report);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        p.WriteInt(value.CategoryId);
        p.WriteString(value.Report);
    }
}

public sealed record ReportForumMessage(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int CategoryId,
    string Report) : IParserComposer<ReportForumMessage>
{
    public static ReportForumMessage Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash, ParseUnity);

    private static ReportForumMessage ParseFlash(in PacketReader p) =>
        throw new UnsupportedClientException(p.Client);

    private static ReportForumMessage ParseUnity(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadUnityGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ReportForumMessage value, in PacketWriter p) =>
        throw new UnsupportedClientException(p.Client);

    private static void ComposeUnity(ReportForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.PrepareStrings(in p, value.Report);
        ForumRequestProtocol.WriteUnityGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
        ForumRequestProtocol.WriteIntId(in p, value.MessageId, "message");
        p.WriteInt(value.CategoryId);
        p.WriteString(value.Report);
    }
}
