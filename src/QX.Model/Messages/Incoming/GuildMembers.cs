using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum GuildMemberType
{
    Owner = 0,
    Administrator = 1,
    Member = 2,
    Pending = 3,
    Blocked = 4
}

public enum GuildMemberSearchType
{
    All = 0,
    Administrators = 1,
    Pending = 2,
    Blocked = 3
}

public sealed record GuildMember(
    GuildMemberType Type,
    Id Id,
    string Name,
    string Figure,
    string MemberSince) : IParserComposer<GuildMember>
{
    internal const int FlashMinimumSize = sizeof(int) * 2 + sizeof(ushort) * 3;
    internal const int UnityMinimumSize = sizeof(int) + sizeof(long) + sizeof(ushort) * 3;

    public bool IsOwner => Type is GuildMemberType.Owner;
    public bool IsAdministrator => Type is GuildMemberType.Administrator;
    public bool IsMember => Type is
        GuildMemberType.Owner or
        GuildMemberType.Administrator or
        GuildMemberType.Member;
    public bool IsPending => Type is GuildMemberType.Pending;
    public bool IsBlocked => Type is GuildMemberType.Blocked;

    public static GuildMember Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GuildMember ParseFlash(in PacketReader p) =>
        new(
            (GuildMemberType)p.ReadInt(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());

    private static GuildMember ParseUnity(in PacketReader p) =>
        new(
            (GuildMemberType)p.ReadInt(),
            p.ReadLong(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GuildMember value, in PacketWriter p)
    {
        Validate(value, true, in p);
        p.WriteInt((int)value.Type);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(value.MemberSince);
    }

    private static void ComposeUnity(GuildMember value, in PacketWriter p)
    {
        Validate(value, false, in p);
        p.WriteInt((int)value.Type);
        p.WriteLong(value.Id);
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(value.MemberSince);
    }

    internal static void Validate(GuildMember value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash)
            _ = PeopleWire.RequireFlashId(value.Id, nameof(Id));
        PeopleWire.RequireString(value.Name, nameof(Name), in p);
        PeopleWire.RequireString(value.Figure, nameof(Figure), in p);
        PeopleWire.RequireString(value.MemberSince, nameof(MemberSince), in p);
    }
}

public sealed record GuildMembers : IParserComposer<GuildMembers>
{
    private IReadOnlyList<GuildMember> _entries =
        Array.AsReadOnly(Array.Empty<GuildMember>());

    public GuildMembers(
        Id GroupId,
        string GroupName,
        Id BaseRoomId,
        string BadgeCode,
        int TotalEntries,
        IReadOnlyList<GuildMember> Entries,
        bool IsAllowedToManage,
        int PageSize,
        int PageIndex,
        GuildMemberSearchType? SearchType,
        string UserNameFilter)
    {
        this.GroupId = GroupId;
        this.GroupName = GroupName;
        this.BaseRoomId = BaseRoomId;
        this.BadgeCode = BadgeCode;
        this.TotalEntries = TotalEntries;
        this.Entries = Entries;
        this.IsAllowedToManage = IsAllowedToManage;
        this.PageSize = PageSize;
        this.PageIndex = PageIndex;
        this.SearchType = SearchType;
        this.UserNameFilter = UserNameFilter;
    }

    public Id GroupId { get; init; }
    public string GroupName { get; init; }
    public Id BaseRoomId { get; init; }
    public string BadgeCode { get; init; }
    public int TotalEntries { get; init; }

    public IReadOnlyList<GuildMember> Entries
    {
        get => _entries;
        init => _entries = PeopleWire.FreezeReferences(value, nameof(Entries));
    }

    public bool IsAllowedToManage { get; init; }
    public int PageSize { get; init; }
    public int PageIndex { get; init; }
    public GuildMemberSearchType? SearchType { get; init; }
    public string UserNameFilter { get; init; }

    public void Deconstruct(
        out Id GroupId,
        out string GroupName,
        out Id BaseRoomId,
        out string BadgeCode,
        out int TotalEntries,
        out IReadOnlyList<GuildMember> Entries,
        out bool IsAllowedToManage,
        out int PageSize,
        out int PageIndex,
        out GuildMemberSearchType? SearchType,
        out string UserNameFilter)
    {
        GroupId = this.GroupId;
        GroupName = this.GroupName;
        BaseRoomId = this.BaseRoomId;
        BadgeCode = this.BadgeCode;
        TotalEntries = this.TotalEntries;
        Entries = this.Entries;
        IsAllowedToManage = this.IsAllowedToManage;
        PageSize = this.PageSize;
        PageIndex = this.PageIndex;
        SearchType = this.SearchType;
        UserNameFilter = this.UserNameFilter;
    }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Max(
            1L,
            ((long)Math.Max(0, TotalEntries) + PageSize - 1) / PageSize);

    public bool HasPreviousPage => PageIndex > 0;
    public bool HasNextPage => PageIndex + 1 < TotalPages;

    public static GuildMembers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GuildMembers ParseFlash(in PacketReader p)
    {
        Id group_id = p.ReadInt();
        string group_name = p.ReadString();
        Id base_room_id = p.ReadInt();
        string badge_code = p.ReadString();
        int total_entries = p.ReadInt();
        int count = PeopleWire.ReadFlashCount(in p, GuildMember.FlashMinimumSize, nameof(Entries));
        GuildMember[] entries = ReadEntries(in p, count);
        var value = new GuildMembers(
            group_id,
            group_name,
            base_room_id,
            badge_code,
            total_entries,
            entries,
            p.ReadBool(),
            p.ReadInt(),
            p.ReadInt(),
            (GuildMemberSearchType)p.ReadInt(),
            p.ReadString());
        PeopleWire.RequireEmpty(in p, nameof(GuildMembers));
        return value;
    }

    private static GuildMembers ParseUnity(in PacketReader p)
    {
        Id group_id = p.ReadLong();
        string group_name = p.ReadString();
        Id base_room_id = p.ReadLong();
        string badge_code = p.ReadString();
        int total_entries = p.ReadInt();
        int count = PeopleWire.ReadUnityCount(in p, GuildMember.UnityMinimumSize, nameof(Entries));
        GuildMember[] entries = ReadEntries(in p, count);
        var value = new GuildMembers(
            group_id,
            group_name,
            base_room_id,
            badge_code,
            total_entries,
            entries,
            p.ReadBool(),
            p.ReadInt(),
            p.ReadInt(),
            null,
            p.ReadString());
        PeopleWire.RequireEmpty(in p, nameof(GuildMembers));
        return value;
    }

    private static GuildMember[] ReadEntries(in PacketReader p, int count)
    {
        var entries = new GuildMember[count];
        for (int index = 0; index < entries.Length; index++)
            entries[index] = p.Parse<GuildMember>();
        return entries;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GuildMembers value, in PacketWriter p)
    {
        GuildMembers prepared = Prepare(value, true, in p);
        p.WriteInt(PeopleWire.RequireFlashId(prepared.GroupId, nameof(GroupId)));
        p.WriteString(prepared.GroupName);
        p.WriteInt(PeopleWire.RequireFlashId(prepared.BaseRoomId, nameof(BaseRoomId)));
        p.WriteString(prepared.BadgeCode);
        p.WriteInt(prepared.TotalEntries);
        p.WriteInt(prepared.Entries.Count);
        foreach (GuildMember entry in prepared.Entries)
            p.Compose(entry);
        p.WriteBool(prepared.IsAllowedToManage);
        p.WriteInt(prepared.PageSize);
        p.WriteInt(prepared.PageIndex);
        p.WriteInt((int)prepared.SearchType!.Value);
        p.WriteString(prepared.UserNameFilter);
    }

    private static void ComposeUnity(GuildMembers value, in PacketWriter p)
    {
        GuildMembers prepared = Prepare(value, false, in p);
        p.WriteLong(prepared.GroupId);
        p.WriteString(prepared.GroupName);
        p.WriteLong(prepared.BaseRoomId);
        p.WriteString(prepared.BadgeCode);
        p.WriteInt(prepared.TotalEntries);
        PeopleWire.WriteUnityCount(prepared.Entries.Count, in p);
        foreach (GuildMember entry in prepared.Entries)
            p.Compose(entry);
        p.WriteBool(prepared.IsAllowedToManage);
        p.WriteInt(prepared.PageSize);
        p.WriteInt(prepared.PageIndex);
        p.WriteString(prepared.UserNameFilter);
    }

    private static GuildMembers Prepare(GuildMembers value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        GuildMember[] entries = PeopleWire.SnapshotReferences(value.Entries, nameof(Entries));
        var prepared = new GuildMembers(
            value.GroupId,
            value.GroupName,
            value.BaseRoomId,
            value.BadgeCode,
            value.TotalEntries,
            entries,
            value.IsAllowedToManage,
            value.PageSize,
            value.PageIndex,
            value.SearchType,
            value.UserNameFilter);

        PeopleWire.RequireString(prepared.GroupName, nameof(GroupName), in p);
        PeopleWire.RequireString(prepared.BadgeCode, nameof(BadgeCode), in p);
        PeopleWire.RequireString(prepared.UserNameFilter, nameof(UserNameFilter), in p);
        if (flash)
        {
            _ = PeopleWire.RequireFlashId(prepared.GroupId, nameof(GroupId));
            _ = PeopleWire.RequireFlashId(prepared.BaseRoomId, nameof(BaseRoomId));
            if (prepared.SearchType is not GuildMemberSearchType search_type ||
                !Enum.IsDefined(search_type))
            {
                throw new InvalidDataException("Flash GuildMembers requires a valid search type.");
            }
        }
        else
        {
            if (prepared.SearchType is not null)
                throw new InvalidDataException("Unity GuildMembers cannot contain a search type.");
            PeopleWire.RequireUnityCount(prepared.Entries.Count, nameof(Entries));
        }
        foreach (GuildMember entry in prepared.Entries)
            GuildMember.Validate(entry, flash, in p);
        return prepared;
    }
}
