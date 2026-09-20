using Qx.Messages;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record GetForumStats(Id GroupId) : IParserComposer<GetForumStats>
{
    public static GetForumStats Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetForumStats ParseFlash(in PacketReader p) =>
        new(ForumRequestProtocol.ReadFlashGroupId(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetForumStats value, in PacketWriter p) =>
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
}

public sealed record GetThreads(
    Id GroupId,
    int StartIndex,
    int MaxCount) : IParserComposer<GetThreads>
{
    public static GetThreads Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetThreads ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetThreads value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetMessages ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetMessages value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        ForumRequestProtocol.WriteIntId(in p, value.ThreadId, "thread");
    }
}

public sealed record GetForumsList(
    ForumListCode ListCode,
    int StartIndex,
    int MaxCount) : IParserComposer<GetForumsList>
{
    public static GetForumsList Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetForumsList ParseFlash(in PacketReader p) =>
        new(
            (ForumListCode)p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetForumsList value, in PacketWriter p)
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static UpdateForumSettings ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateForumSettings value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
        p.WriteInt(value.ReadLevel);
        p.WriteInt(value.PostMessageLevel);
        p.WriteInt(value.PostThreadLevel);
        p.WriteInt(value.ModerateLevel);
    }
}

public sealed record GetUnreadForumsCount : IParserComposer<GetUnreadForumsCount>
{
    public static GetUnreadForumsCount Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetUnreadForumsCount ParseFlash(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetUnreadForumsCount value, in PacketWriter p) { }
}

public sealed record ModerateThread(
    Id GroupId,
    Id ThreadId,
    int State) : IParserComposer<ModerateThread>
{
    public static ModerateThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static ModerateThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ModerateThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static ModerateMessage ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ModerateMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

public readonly record struct ForumReadMarker(
    Id GroupId,
    Id LastReadMessageId,
    bool MarkAsRead) : IParserComposer<ForumReadMarker>
{
    public bool MarkEntireForumRead => MarkAsRead;

    public static ForumReadMarker Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    internal static ForumReadMarker ParseWire(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ForumReadMarker ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    internal static void Prepare(ForumReadMarker value, ClientType client)
    {
        ForumProtocol.RequireFlashId(value.GroupId, "forum group");
        ForumProtocol.RequireFlashId(value.LastReadMessageId, "last-read message");
    }

    internal static void ComposeWire(ForumReadMarker value, in PacketWriter p) =>
        FlashWire.Compose(value, in p, ComposeFlash);

    private static void ComposeFlash(ForumReadMarker value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static UpdateForumReadMarker ParseFlash(in PacketReader p)
    {
        int count = ForumRequestProtocol.ReadFlashMarkerCount(in p);
        var markers = new ForumReadMarker[count];
        for (int index = 0; index < count; index++)
            markers[index] = ForumReadMarker.ParseWire(in p);
        return new UpdateForumReadMarker(markers);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

public sealed record GetForumThreads(
    Id GroupId,
    int StartIndex,
    int Amount) : IParserComposer<GetForumThreads>
{
    public static GetForumThreads Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetForumThreads ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetForumThreads value, in PacketWriter p)
    {
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetForumThreadMessages ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetForumThreadMessages value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static GetForumThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static PostForumMessage ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadString(),
            p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PostForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.PrepareStrings(in p, value.Subject, value.MessageText);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static ModerateForumThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ModerateForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static ModerateForumMessage ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ModerateForumMessage value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId, value.MessageId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static UpdateForumThread ParseFlash(in PacketReader p) =>
        new(
            ForumRequestProtocol.ReadFlashGroupId(in p),
            ForumRequestProtocol.ReadIntId(in p),
            p.ReadBool(),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateForumThread value, in PacketWriter p)
    {
        ForumRequestProtocol.PrepareIds(p.Client, value.GroupId, value.ThreadId);
        ForumRequestProtocol.WriteFlashGroupId(in p, value.GroupId);
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
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static UpdateForumReadMarkers ParseFlash(in PacketReader p)
    {
        int count = ForumRequestProtocol.ReadFlashMarkerCount(in p);
        var markers = new ForumReadMarker[count];
        for (int index = 0; index < count; index++)
            markers[index] = ForumReadMarker.ParseWire(in p);
        return new UpdateForumReadMarkers(markers);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

public sealed record ReportForumThread(
    Id GroupId,
    Id ThreadId,
    int CategoryId,
    string Report) : IParserComposer<ReportForumThread>
{
    public static ReportForumThread Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static ReportForumThread ParseFlash(in PacketReader p) =>
        throw new UnsupportedClientException(p.Client);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ReportForumThread value, in PacketWriter p) =>
        throw new UnsupportedClientException(p.Client);
}

public sealed record ReportForumMessage(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int CategoryId,
    string Report) : IParserComposer<ReportForumMessage>
{
    public static ReportForumMessage Parse(in PacketReader p) =>
        ForumRequestProtocol.ParseRoot(in p, ParseFlash);

    private static ReportForumMessage ParseFlash(in PacketReader p) =>
        throw new UnsupportedClientException(p.Client);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ReportForumMessage value, in PacketWriter p) =>
        throw new UnsupportedClientException(p.Client);
}
