using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Specifies the role of a user in a group.</summary>
public enum GuildMemberType
{
    /// <summary>The owner of the group.</summary>
    Owner = 0,
    /// <summary>An administrator of the group.</summary>
    Administrator = 1,
    /// <summary>A regular member of the group.</summary>
    Member = 2,
    /// <summary>A user whose request to join is pending.</summary>
    Pending = 3,
    /// <summary>A user blocked from the group.</summary>
    Blocked = 4
}

/// <summary>Specifies the category of members a group member list shows.</summary>
public enum GuildMemberSearchType
{
    /// <summary>All members.</summary>
    All = 0,
    /// <summary>The administrators.</summary>
    Administrators = 1,
    /// <summary>The users whose requests to join are pending.</summary>
    Pending = 2,
    /// <summary>The blocked users.</summary>
    Blocked = 3
}

/// <summary>Represents a user in a group member list.</summary>
/// <param name="Type">The role of the user in the group.</param>
/// <param name="Id">The identifier of the user.</param>
/// <param name="Name">The name of the user.</param>
/// <param name="Figure">The figure string of the user.</param>
/// <param name="MemberSince">The date the user joined the group, as the text sent by the hotel.</param>
public sealed record GuildMember(
    GuildMemberType Type,
    Id Id,
    string Name,
    string Figure,
    string MemberSince) : IParserComposer<GuildMember>
{
    internal const int FlashMinimumSize = sizeof(int) * 2 + sizeof(ushort) * 3;

    /// <summary>Gets whether the user owns the group.</summary>
    public bool IsOwner => Type is GuildMemberType.Owner;
    /// <summary>Gets whether the user is an administrator of the group.</summary>
    public bool IsAdministrator => Type is GuildMemberType.Administrator;
    /// <summary>Gets whether the user is the owner, an administrator or a regular member.</summary>
    public bool IsMember => Type is
        GuildMemberType.Owner or
        GuildMemberType.Administrator or
        GuildMemberType.Member;
    /// <summary>Gets whether the user's request to join is pending.</summary>
    public bool IsPending => Type is GuildMemberType.Pending;
    /// <summary>Gets whether the user is blocked from the group.</summary>
    public bool IsBlocked => Type is GuildMemberType.Blocked;

    /// <summary>Parses a group member from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GuildMember Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GuildMember ParseFlash(in PacketReader p) =>
        new(
            (GuildMemberType)p.ReadInt(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());

    /// <summary>Composes the group member into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GuildMember value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt((int)value.Type);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(value.MemberSince);
    }

    internal static void Validate(GuildMember value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = PeopleWire.RequireFlashId(value.Id, nameof(Id));
        PeopleWire.RequireString(value.Name, nameof(Name), in p);
        PeopleWire.RequireString(value.Figure, nameof(Figure), in p);
        PeopleWire.RequireString(value.MemberSince, nameof(MemberSince), in p);
    }
}

/// <summary>Represents the <c>GuildMembers</c> message, received with one page of a group's members.</summary>
public sealed record GuildMembers : IParserComposer<GuildMembers>
{
    private IReadOnlyList<GuildMember> _entries =
        Array.AsReadOnly(Array.Empty<GuildMember>());

    /// <summary>Initializes a new instance of the <see cref="GuildMembers"/> record.</summary>
    /// <param name="groupId">The identifier of the group.</param>
    /// <param name="groupName">The name of the group.</param>
    /// <param name="baseRoomId">The identifier of the group's home room.</param>
    /// <param name="badgeCode">The group's badge code.</param>
    /// <param name="totalEntries">The number of members matching the filter and category.</param>
    /// <param name="entries">The members in the page, copied into a read only list.</param>
    /// <param name="isAllowedToManage">Whether the local user may manage the group's members.</param>
    /// <param name="pageSize">The number of members per page chosen by the hotel.</param>
    /// <param name="pageIndex">The zero based index of the page.</param>
    /// <param name="searchType">The member category echoed by the hotel.</param>
    /// <param name="userNameFilter">The user name filter echoed by the hotel.</param>
    public GuildMembers(
        Id groupId,
        string groupName,
        Id baseRoomId,
        string badgeCode,
        int totalEntries,
        IReadOnlyList<GuildMember> entries,
        bool isAllowedToManage,
        int pageSize,
        int pageIndex,
        GuildMemberSearchType searchType,
        string userNameFilter)
    {
        GroupId = groupId;
        GroupName = groupName;
        BaseRoomId = baseRoomId;
        BadgeCode = badgeCode;
        TotalEntries = totalEntries;
        Entries = entries;
        IsAllowedToManage = isAllowedToManage;
        PageSize = pageSize;
        PageIndex = pageIndex;
        SearchType = searchType;
        UserNameFilter = userNameFilter;
    }

    /// <summary>Gets the identifier of the group.</summary>
    public Id GroupId { get; init; }
    /// <summary>Gets the name of the group.</summary>
    public string GroupName { get; init; }
    /// <summary>Gets the identifier of the group's home room.</summary>
    public Id BaseRoomId { get; init; }
    /// <summary>Gets the group's badge code.</summary>
    public string BadgeCode { get; init; }
    /// <summary>Gets the number of members matching the filter and category.</summary>
    public int TotalEntries { get; init; }

    /// <summary>Gets the members in the page, as a read only copy.</summary>
    public IReadOnlyList<GuildMember> Entries
    {
        get => _entries;
        init => _entries = PeopleWire.FreezeReferences(value, nameof(Entries));
    }

    /// <summary>Gets whether the local user may manage the group's members.</summary>
    public bool IsAllowedToManage { get; init; }
    /// <summary>Gets the number of members per page chosen by the hotel.</summary>
    public int PageSize { get; init; }
    /// <summary>Gets the zero based index of the page.</summary>
    public int PageIndex { get; init; }
    /// <summary>Gets the member category echoed by the hotel.</summary>
    public GuildMemberSearchType SearchType { get; init; }
    /// <summary>Gets the user name filter echoed by the hotel.</summary>
    public string UserNameFilter { get; init; }

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="groupId">The identifier of the group.</param>
    /// <param name="groupName">The name of the group.</param>
    /// <param name="baseRoomId">The identifier of the group's home room.</param>
    /// <param name="badgeCode">The group's badge code.</param>
    /// <param name="totalEntries">The number of members matching the filter and category.</param>
    /// <param name="entries">The members in the page.</param>
    /// <param name="isAllowedToManage">Whether the local user may manage the group's members.</param>
    /// <param name="pageSize">The number of members per page.</param>
    /// <param name="pageIndex">The zero based index of the page.</param>
    /// <param name="searchType">The member category.</param>
    /// <param name="userNameFilter">The user name filter.</param>
    public void Deconstruct(
        out Id groupId,
        out string groupName,
        out Id baseRoomId,
        out string badgeCode,
        out int totalEntries,
        out IReadOnlyList<GuildMember> entries,
        out bool isAllowedToManage,
        out int pageSize,
        out int pageIndex,
        out GuildMemberSearchType searchType,
        out string userNameFilter)
    {
        groupId = GroupId;
        groupName = GroupName;
        baseRoomId = BaseRoomId;
        badgeCode = BadgeCode;
        totalEntries = TotalEntries;
        entries = Entries;
        isAllowedToManage = IsAllowedToManage;
        pageSize = PageSize;
        pageIndex = PageIndex;
        searchType = SearchType;
        userNameFilter = UserNameFilter;
    }

    /// <summary>
    /// Gets the number of pages, at least 1, computed from <see cref="TotalEntries"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Max(
            1L,
            ((long)Math.Max(0, TotalEntries) + PageSize - 1) / PageSize);

    /// <summary>Gets whether a page comes before this one.</summary>
    public bool HasPreviousPage => PageIndex > 0;
    /// <summary>Gets whether a page comes after this one.</summary>
    public bool HasNextPage => PageIndex + 1 < TotalPages;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GuildMembers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    private static GuildMember[] ReadEntries(in PacketReader p, int count)
    {
        var entries = new GuildMember[count];
        for (int index = 0; index < entries.Length; index++)
            entries[index] = p.Parse<GuildMember>();
        return entries;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when <see cref="SearchType"/> is not a defined value.
    /// </exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GuildMembers value, in PacketWriter p)
    {
        GuildMembers prepared = Prepare(value, in p);
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
        p.WriteInt((int)prepared.SearchType);
        p.WriteString(prepared.UserNameFilter);
    }

    private static GuildMembers Prepare(GuildMembers value, in PacketWriter p)
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
        _ = PeopleWire.RequireFlashId(prepared.GroupId, nameof(GroupId));
        _ = PeopleWire.RequireFlashId(prepared.BaseRoomId, nameof(BaseRoomId));
        if (!Enum.IsDefined(prepared.SearchType))
            throw new InvalidDataException("Flash GuildMembers requires a valid search type.");
        foreach (GuildMember entry in prepared.Entries)
            GuildMember.Validate(entry, in p);
        return prepared;
    }
}
