using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public Task<GuildMembers> GetGuildMembers(
    Id groupId,
    int pageIndex = 0,
    string userNameFilter = "",
    GuildMemberSearchType searchType = GuildMemberSearchType.All,
    int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentNullException.ThrowIfNull(userNameFilter);
        return GetGuildMembersPage(
            groupId,
            pageIndex,
            userNameFilter,
            searchType,
            timeoutMs,
            null);
    }

    public async Task<GuildMemberQuery> GetAllGuildMembers(
    Id groupId,
    string userNameFilter = "",
    GuildMemberSearchType searchType = GuildMemberSearchType.All,
    int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(userNameFilter);
        GroupMembersPage first = await RequestGuildMembersPage(
            groupId,
            0,
            userNameFilter,
            searchType,
            timeoutMs,
            null)
            .ConfigureAwait(false);
        ValidateGuildMemberPage(
            first,
            groupId,
            0,
            userNameFilter,
            searchType,
            null);
        var members = new List<GuildMember>();
        var memberIds = new HashSet<Id>();
        AddGuildMemberPage(first, members, memberIds);

        for (int pageIndex = 1; pageIndex < TotalPages(first); pageIndex++)
        {
            GroupMembersPage page = await RequestGuildMembersPage(
                groupId,
                pageIndex,
                userNameFilter,
                searchType,
                timeoutMs,
                first.SessionGeneration)
                .ConfigureAwait(false);
            ValidateGuildMemberPage(
                page,
                groupId,
                pageIndex,
                userNameFilter,
                searchType,
                first);
            AddGuildMemberPage(page, members, memberIds);
        }

        if (members.Count != first.TotalEntries)
            throw new InvalidDataException("Guild member pagination returned an incomplete result.");
        return new GuildMemberQuery(members);
    }

    /// <summary>
    /// Starts a filter/sort/projection query over a caller-supplied member sequence. Nothing is
    /// requested — this only wraps members that were already fetched.
    /// </summary>
    /// <param name="members">The members to query.</param>
    /// <returns>A query over the given members.</returns>
    public GuildMemberQuery QueryGuildMembers(IEnumerable<GuildMember> members) =>
        new(members);

    private static void ValidateGuildMemberPage(
        GroupMembersPage page,
        Id groupId,
        int pageIndex,
        string userNameFilter,
        GuildMemberSearchType searchType,
        GroupMembersPage? first)
    {
        if (page.GroupId != groupId ||
            page.PageIndex != pageIndex ||
            !string.Equals(page.UserNameFilter, userNameFilter, StringComparison.Ordinal) ||
            (page.Client is ClientType.Flash && page.SearchType != searchType) ||
            page.Client is not (ClientType.Flash))
        {
            throw new InvalidDataException("Guild member pagination returned an unrelated page.");
        }
        if (page.TotalEntries < 0 ||
            page.PageSize < 0 ||
            page.PageIndex < 0 ||
            page.PageIndex >= TotalPages(page) ||
            (page.TotalEntries > 0 && page.PageSize <= 0) ||
            page.Entries.Count > page.PageSize ||
            page.Entries.Count > page.TotalEntries ||
            (page.TotalEntries > 0 && page.Entries.Count == 0))
        {
            throw new InvalidDataException("Guild member pagination returned invalid page metadata.");
        }
        if (first is not null &&
            (page.Client != first.Client ||
             page.SessionGeneration != first.SessionGeneration ||
             page.TotalEntries != first.TotalEntries ||
             page.PageSize != first.PageSize ||
             TotalPages(page) != TotalPages(first) ||
             page.BaseRoomId != first.BaseRoomId ||
             page.IsAllowedToManage != first.IsAllowedToManage ||
             !string.Equals(page.GroupName, first.GroupName, StringComparison.Ordinal) ||
             !string.Equals(page.BadgeCode, first.BadgeCode, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Guild member pagination changed while the result was being collected.");
        }
    }

    private static void AddGuildMemberPage(
        GroupMembersPage page,
        List<GuildMember> members,
        HashSet<Id> memberIds)
    {
        foreach (GuildMember member in page.Entries)
        {
            if (!memberIds.Add(member.Id))
                throw new InvalidDataException("Guild member pagination returned overlapping member ids.");
            members.Add(member);
        }
    }

    private async Task<GuildMembers> GetGuildMembersPage(
        Id groupId,
        int pageIndex,
        string userNameFilter,
        GuildMemberSearchType searchType,
        int timeoutMs,
        long? expectedSessionGeneration)
    {
        GroupMembersPage page = await RequestGuildMembersPage(
            groupId,
            pageIndex,
            userNameFilter,
            searchType,
            timeoutMs,
            expectedSessionGeneration)
            .ConfigureAwait(false);
        ValidateGuildMemberPage(
            page,
            groupId,
            pageIndex,
            userNameFilter,
            searchType,
            null);
        return LegacyGuildMembers(page);
    }

    private Task<GroupMembersPage> RequestGuildMembersPage(
        Id groupId,
        int pageIndex,
        string userNameFilter,
        GuildMemberSearchType searchType,
        int timeoutMs,
        long? expectedSessionGeneration) => Application
        .InvokeAsync<GroupMembersPageRequest, GroupMembersPage>(
            ApplicationMemberIds.GroupsMembersPage,
            new GroupMembersPageRequest(
                groupId,
                pageIndex,
                userNameFilter,
                searchType,
                timeoutMs,
                expectedSessionGeneration),
            Ct)
        .AsTask();

    private static GuildMembers LegacyGuildMembers(GroupMembersPage page) => new(
        page.GroupId,
        page.GroupName,
        page.BaseRoomId,
        page.BadgeCode,
        page.TotalEntries,
        Array.AsReadOnly(page.Entries.ToArray()),
        page.IsAllowedToManage,
        page.PageSize,
        page.PageIndex,
        page.SearchType,
        page.UserNameFilter);

    private static int TotalPages(GroupMembersPage page) => page.PageSize <= 0
        ? 1
        : (int)Math.Max(
            1L,
            ((long)Math.Max(0, page.TotalEntries) + page.PageSize - 1) / page.PageSize);
}
