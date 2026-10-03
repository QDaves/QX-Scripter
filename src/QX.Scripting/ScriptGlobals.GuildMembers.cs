using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Requests one page of a group's members from the server.
    /// </summary>
    /// <remarks>
    /// The page size is decided by the server. The reply also reaches the game client.
    /// </remarks>
    /// <param name="groupId">The id of the group.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="userNameFilter">The user name filter sent with the request; empty for no filter.</param>
    /// <param name="searchType">The member category to list.</param>
    /// <param name="timeoutMs">The total timeout in milliseconds, across one automatic retry.</param>
    /// <returns>The requested page with the group's name, badge, base room and total member count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userNameFilter"/> is <see langword="null"/>.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching page arrived in time.</exception>
    /// <exception cref="InvalidDataException">Thrown when the reply does not match the request or has invalid page metadata.</exception>
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

    /// <summary>
    /// Requests every page of a group's members and returns them as one query.
    /// </summary>
    /// <remarks>
    /// Pages are requested one after another. The collection fails when the pages overlap, the
    /// member count or page layout changes while collecting, or the hotel session changes.
    /// </remarks>
    /// <param name="groupId">The id of the group.</param>
    /// <param name="userNameFilter">The user name filter sent with every page request; empty for no filter.</param>
    /// <param name="searchType">The member category to list.</param>
    /// <param name="timeoutMs">The total timeout in milliseconds for each page, across one automatic retry.</param>
    /// <returns>A query over all matching members.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userNameFilter"/> is <see langword="null"/>.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when a page did not arrive in time.</exception>
    /// <exception cref="InvalidDataException">Thrown when a reply does not match the request, the pages are inconsistent, or the result is incomplete.</exception>
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
    /// Starts a filter, sort and projection query over a caller-supplied member sequence.
    /// </summary>
    /// <remarks>
    /// Nothing is requested; it only wraps members that were already fetched.
    /// </remarks>
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
            page.SearchType != searchType)
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
            (page.SessionGeneration != first.SessionGeneration ||
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
        long? expectedSessionGeneration) => _application
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
