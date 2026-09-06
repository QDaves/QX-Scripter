using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Guild (group) member lists. Available on both the Flash and the Unity client, with one
/// difference: the Unity request has no server-side search-type field.
/// <para>
/// These are live requests, not cached state. Nothing is stored between calls.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Requests one page of a group's member list and waits for it.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="pageIndex">The zero-based page number.</param>
    /// <param name="userNameFilter">
    /// A name fragment to filter by; empty means no filter. It is echoed back by the server and is
    /// part of what identifies the matching reply.
    /// </param>
    /// <param name="searchType">
    /// Which slice to list: <c>All</c> (0), <c>Administrators</c> (1), <c>Pending</c> (2) or
    /// <c>Blocked</c> (3).
    /// </param>
    /// <param name="timeoutMs">
    /// The total time budget in milliseconds, split across one retry — not a per-attempt timeout.
    /// </param>
    /// <returns>
    /// The page, carrying the group's name, badge and home room, the total member count, the page
    /// size and index, and whether the local user may manage the group. It is matched back to the
    /// exact group, page, filter and search type that were requested.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="userNameFilter"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The session is Unity and <paramref name="searchType"/> is anything other than <c>All</c>.
    /// The Unity request carries no search type, so the other values cannot be expressed.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching page arrived in time.</exception>
    /// <exception cref="Qx.Game.RequestDisconnectedException">The connection closed while waiting.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
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
    /// Walks every page of a group's member list and returns all members as one query. Pages are
    /// fetched one after another, so a large group takes several round trips.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="userNameFilter">A name fragment to filter by; empty means no filter.</param>
    /// <param name="searchType">
    /// Which slice to list: <c>All</c>, <c>Administrators</c>, <c>Pending</c> or <c>Blocked</c>.
    /// </param>
    /// <param name="timeoutMs">
    /// The time budget for <em>each</em> page, in milliseconds. This is not a total budget: a group
    /// spanning many pages can take a multiple of it.
    /// </param>
    /// <exception cref="NotSupportedException">
    /// The session is Unity and <paramref name="searchType"/> is not <c>All</c>.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">A page did not arrive in time.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
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
            (page.Client is ClientType.Unity &&
                (searchType is not GuildMemberSearchType.All || page.SearchType is not null)) ||
            page.Client is not (ClientType.Flash or ClientType.Unity))
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
