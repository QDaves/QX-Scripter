using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record JoinGroupRequest(Id GroupId)
    : IParserComposer<JoinGroupRequest>
{
    public static JoinGroupRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static JoinGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static JoinGroupRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(JoinGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));

    private static void ComposeUnity(JoinGroupRequest value, in PacketWriter p) =>
        p.WriteLong(value.GroupId);
}

public sealed record KickGroupMemberRequest(
    Id GroupId,
    Id UserId,
    bool BlockRejoin) : IParserComposer<KickGroupMemberRequest>
{
    public static KickGroupMemberRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static KickGroupMemberRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadBool());

    private static KickGroupMemberRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadLong(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(KickGroupMemberRequest value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        int user_id = checked((int)value.UserId);
        p.WriteInt(group_id);
        p.WriteInt(user_id);
        p.WriteBool(value.BlockRejoin);
    }

    private static void ComposeUnity(KickGroupMemberRequest value, in PacketWriter p)
    {
        p.WriteLong(value.GroupId);
        p.WriteLong(value.UserId);
        p.WriteBool(value.BlockRejoin);
    }
}

public sealed record ApproveGroupMemberRequest(Id GroupId, Id UserId)
    : IParserComposer<ApproveGroupMemberRequest>
{
    public static ApproveGroupMemberRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ApproveGroupMemberRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static ApproveGroupMemberRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ApproveGroupMemberRequest value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        int user_id = checked((int)value.UserId);
        p.WriteInt(group_id);
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(ApproveGroupMemberRequest value, in PacketWriter p)
    {
        p.WriteLong(value.GroupId);
        p.WriteLong(value.UserId);
    }
}

public sealed record RejectGroupMemberRequest(Id GroupId, Id UserId)
    : IParserComposer<RejectGroupMemberRequest>
{
    public static RejectGroupMemberRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RejectGroupMemberRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static RejectGroupMemberRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RejectGroupMemberRequest value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        int user_id = checked((int)value.UserId);
        p.WriteInt(group_id);
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(RejectGroupMemberRequest value, in PacketWriter p)
    {
        p.WriteLong(value.GroupId);
        p.WriteLong(value.UserId);
    }
}

public sealed record GetGuildMembersRequest(
    Id GroupId,
    int PageIndex,
    string UserNameFilter,
    GuildMemberSearchType SearchType) : IParserComposer<GetGuildMembersRequest>
{
    public static GetGuildMembersRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static GetGuildMembersRequest ParseUnity(in PacketReader p)
    {
        var value = new GetGuildMembersRequest(
            p.ReadLong(),
            p.ReadInt(),
            p.ReadString(),
            GuildMemberSearchType.All);
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetGuildMembersRequest value, in PacketWriter p)
    {
        Validate(value, in p);
        int group_id = checked((int)value.GroupId);
        p.WriteInt(group_id);
        p.WriteInt(value.PageIndex);
        p.WriteString(value.UserNameFilter);
        p.WriteInt((int)value.SearchType);
    }

    private static void ComposeUnity(GetGuildMembersRequest value, in PacketWriter p)
    {
        Validate(value, in p);
        if (value.SearchType is not GuildMemberSearchType.All)
        {
            throw new NotSupportedException(
                "The Unity GetGuildMembers request does not contain a search type.");
        }

        p.WriteLong(value.GroupId);
        p.WriteInt(value.PageIndex);
        p.WriteString(value.UserNameFilter);
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GroupDetailsRequest ParseFlash(in PacketReader p)
    {
        var value = new GroupDetailsRequest(p.ReadInt(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

    private static GroupDetailsRequest ParseUnity(in PacketReader p)
    {
        var value = new GroupDetailsRequest(p.ReadLong(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GroupDetailsRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int group_id = checked((int)(long)value.GroupId);
        p.WriteInt(group_id);
        p.WriteBool(value.OpenInClient);
    }

    private static void ComposeUnity(GroupDetailsRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteLong(value.GroupId);
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GuildMembershipsRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new GuildMembershipsRequest();
    }

    private static GuildMembershipsRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new GuildMembershipsRequest();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GuildMembershipsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static void ComposeUnity(GuildMembershipsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(GuildMembershipsRequest)} contains {p.Available} unexpected bytes.");
    }
}
