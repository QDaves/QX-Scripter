using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record JoinGroupRequest(Id GroupId)
    : IParserComposer<JoinGroupRequest>
{
    public static JoinGroupRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static JoinGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(JoinGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));
}

public sealed record KickGroupMemberRequest(
    Id GroupId,
    Id UserId,
    bool BlockRejoin) : IParserComposer<KickGroupMemberRequest>
{
    public static KickGroupMemberRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static KickGroupMemberRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(KickGroupMemberRequest value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        int user_id = checked((int)value.UserId);
        p.WriteInt(group_id);
        p.WriteInt(user_id);
        p.WriteBool(value.BlockRejoin);
    }
}

public sealed record ApproveGroupMemberRequest(Id GroupId, Id UserId)
    : IParserComposer<ApproveGroupMemberRequest>
{
    public static ApproveGroupMemberRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ApproveGroupMemberRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ApproveGroupMemberRequest value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        int user_id = checked((int)value.UserId);
        p.WriteInt(group_id);
        p.WriteInt(user_id);
    }
}

public sealed record RejectGroupMemberRequest(Id GroupId, Id UserId)
    : IParserComposer<RejectGroupMemberRequest>
{
    public static RejectGroupMemberRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RejectGroupMemberRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RejectGroupMemberRequest value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        int user_id = checked((int)value.UserId);
        p.WriteInt(group_id);
        p.WriteInt(user_id);
    }
}

public sealed record GetGuildMembersRequest(
    Id GroupId,
    int PageIndex,
    string UserNameFilter,
    GuildMemberSearchType SearchType) : IParserComposer<GetGuildMembersRequest>
{
    public static GetGuildMembersRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetGuildMembersRequest ParseFlash(in PacketReader p)
    {
        var value = new GetGuildMembersRequest(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadString(),
            (GuildMemberSearchType)p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetGuildMembersRequest value, in PacketWriter p)
    {
        Validate(value, in p);
        int group_id = checked((int)value.GroupId);
        p.WriteInt(group_id);
        p.WriteInt(value.PageIndex);
        p.WriteString(value.UserNameFilter);
        p.WriteInt((int)value.SearchType);
    }

    private static void Validate(GetGuildMembersRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(value.PageIndex);
        if (!Enum.IsDefined(value.SearchType))
            throw new ArgumentOutOfRangeException(nameof(SearchType));
        ArgumentNullException.ThrowIfNull(value.UserNameFilter);
        int length = p.Encoding.GetByteCount(value.UserNameFilter);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                nameof(UserNameFilter));
        }
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(GetGuildMembersRequest)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record GroupDetailsRequest(Id GroupId, bool OpenInClient)
    : IParserComposer<GroupDetailsRequest>
{
    public static GroupDetailsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GroupDetailsRequest ParseFlash(in PacketReader p)
    {
        var value = new GroupDetailsRequest(p.ReadInt(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GroupDetailsRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int group_id = checked((int)(long)value.GroupId);
        p.WriteInt(group_id);
        p.WriteBool(value.OpenInClient);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(GroupDetailsRequest)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record GuildMembershipsRequest : IParserComposer<GuildMembershipsRequest>
{
    public static GuildMembershipsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GuildMembershipsRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new GuildMembershipsRequest();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GuildMembershipsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(GuildMembershipsRequest)} contains {p.Available} unexpected bytes.");
    }
}
