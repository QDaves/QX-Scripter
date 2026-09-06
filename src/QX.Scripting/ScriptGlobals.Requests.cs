using Qx.Messages;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Game.Protocol;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Requests another user's extended profile: figure, motto, creation date, achievement
    /// score, group memberships and friend/relationship flags.
    /// </summary>
    /// <param name="userId">The target user's account id, not their room index.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <returns>The profile whose id matches <paramref name="userId"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching profile arrived in time.</exception>
    public async Task<UserProfile> GetProfile(Id userId, int timeoutMs = 10000)
    {
        RemoteProfileResult result = await Application
            .InvokeAsync<RemoteProfileGetRequest, RemoteProfileResult>(
                ApplicationMemberIds.PeopleProfileGet,
                new RemoteProfileGetRequest(userId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        return LegacyRemoteProfile(result.Profile);
    }

    /// <summary>
    /// Requests a group's details: name, description, badge, home room, member count and the
    /// local user's membership state.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching group details arrived in time.</exception>
    public async Task<GroupData> GetGroup(Id groupId, int timeoutMs = 10000)
    {
        GroupDetailsResult result = await Application
            .InvokeAsync<GroupDetailsGetRequest, GroupDetailsResult>(
                ApplicationMemberIds.GroupsDetailsGet,
                new GroupDetailsGetRequest(groupId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        return result.Details;
    }

    /// <summary>
    /// Requests a pet's stats: breed, level, experience, energy, happiness, scratches and
    /// owner. The pet must be visible to the server in the current context (in the room or in
    /// the inventory).
    /// </summary>
    /// <param name="petId">The pet id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching pet info arrived in time.</exception>
    public async Task<PetInfo> GetPetInfo(Id petId, int timeoutMs = 10000)
    {
        PetInfoReadResult result = await Application
            .InvokeAsync<PetInfoReadRequest, PetInfoReadResult>(
                ApplicationMemberIds.PetsInfoGet,
                new PetInfoReadRequest(petId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (result.RequestedPetId != petId || result.Pet.Id != petId || result.MessagesDispatched != 1)
            throw new InvalidDataException("The pet-info application returned an inconsistent result.");
        PetInfoView pet = result.Pet;
        return new PetInfo
        {
            Id = pet.Id,
            Name = pet.Name,
            Level = pet.Level,
            MaxLevel = pet.MaxLevel,
            Experience = pet.Experience,
            MaxExperience = pet.MaxExperience,
            Energy = pet.Energy,
            MaxEnergy = pet.MaxEnergy,
            Happiness = pet.Happiness,
            MaxHappiness = pet.MaxHappiness,
            Scratches = pet.Scratches,
            OwnerId = pet.OwnerId,
            Age = pet.Age,
            OwnerName = pet.OwnerName,
            BreedId = pet.BreedId,
            HasFreeSaddle = pet.HasFreeSaddle,
            IsRiding = pet.IsRiding,
            SkillThresholds = pet.SkillThresholds,
            AccessRights = pet.AccessRights,
            CanBreed = pet.CanBreed,
            CanHarvest = pet.CanHarvest,
            CanRevive = pet.CanRevive,
            RarityLevel = pet.RarityLevel,
            MaxWellbeingSeconds = pet.MaxWellbeingSeconds,
            RemainingWellbeingSeconds = pet.RemainingWellbeingSeconds,
            RemainingGrowingSeconds = pet.RemainingGrowingSeconds,
            HasBreedingPermission = pet.HasBreedingPermission
        };
    }

    /// <summary>
    /// Requests the contents of a sticky note (post-it) placed in the room, returning its text
    /// and colour.
    /// </summary>
    /// <param name="itemId">The wall item id of the sticky note.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching item data arrived in time.</exception>
    public async Task<Sticky> GetSticky(Id itemId, int timeoutMs = 10000)
    {
        StickyReadResult result = await Application
            .InvokeAsync<StickyReadRequest, StickyReadResult>(
                ApplicationMemberIds.RoomStickyGet,
                new StickyReadRequest(itemId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (result.ItemId != itemId || result.MessagesDispatched != 1)
            throw new InvalidDataException("The sticky-data application returned an inconsistent result.");
        return new Sticky(result.ItemId, result.Color, result.Text);
    }

    /// <summary>
    /// Requests the badges a user has equipped in their profile slots. This is the small
    /// selected set, not the user's full badge collection.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching badge list arrived in time.</exception>
    public async Task<UserBadges> GetBadges(Id userId, int timeoutMs = 10000)
    {
        RemoteBadgesResult result = await Application
            .InvokeAsync<RemoteBadgesGetRequest, RemoteBadgesResult>(
                ApplicationMemberIds.PeopleBadgesGet,
                new RemoteBadgesGetRequest(userId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        return new UserBadges(
            result.UserId,
            Array.AsReadOnly(result.Badges.ToArray()));
    }

    /// <summary>
    /// Requests the relationship (heart/smile/bobba) tallies a user has set on their profile.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching relationship info arrived in time.</exception>
    /// <remarks>Unity sends an extra trailing field; the correct layout is chosen automatically.</remarks>
    public async Task<RelationshipStatus> GetRelationship(Id userId, int timeoutMs = 10000)
    {
        RemoteRelationshipResult result = await Application
            .InvokeAsync<RemoteRelationshipGetRequest, RemoteRelationshipResult>(
                ApplicationMemberIds.PeopleRelationshipGet,
                new RemoteRelationshipGetRequest(userId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        return new RelationshipStatus(
            result.UserId,
            Array.AsReadOnly(result.Entries.ToArray()));
    }

    /// <summary>
    /// Runs a navigator search and returns the raw result blocks exactly as the navigator
    /// renders them.
    /// </summary>
    /// <param name="code">
    /// The navigator view code, for example <c>"official-root"</c>, <c>"hotel_view"</c>,
    /// <c>"my"</c> for the user's own rooms, <c>"favorites"</c>, or <c>"query"</c> for a
    /// free-text search.
    /// </param>
    /// <param name="filter">
    /// The filter text. With <c>"query"</c> this accepts the navigator's prefixes, such as
    /// <c>owner:name</c>, <c>roomname:text</c>, <c>tag:text</c> and <c>group:name</c>; empty
    /// means no filter.
    /// </param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <returns>
    /// The result, which is matched back to the exact <paramref name="code"/> and
    /// <paramref name="filter"/> that were requested.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching search result arrived in time.</exception>
    public async Task<NavigatorSearchResult> SearchRooms(
        string code,
        string filter,
        int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await Application.InvokeAsync<NavigatorViewSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchView,
                new NavigatorViewSearchInput(code, filter, timeoutMs),
                Ct);
        return ResultFromSnapshot(result);
    }

    /// <summary>
    /// Runs the same navigator search as <see cref="SearchRooms"/> and wraps the flattened room
    /// list in a query object for further filtering, sorting and projection.
    /// </summary>
    /// <param name="code">The navigator view code; see <see cref="SearchRooms"/>.</param>
    /// <param name="filter">The filter text; see <see cref="SearchRooms"/>.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    public async Task<RoomDataQuery> SearchRoomQuery(
        string code,
        string filter = "",
        int timeoutMs = 10000)
    {
        NavigatorSearchResult result = await SearchRooms(code, filter, timeoutMs);
        return Queries.From(result.Rooms);
    }

    /// <summary>
    /// Searches for user accounts by name. The server returns both exact and partial matches,
    /// including offline users, and applies its own result cap.
    /// </summary>
    /// <param name="name">The name or name fragment to search for.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <returns>
    /// The first search result that arrives. The reply carries no echo of the query, so a
    /// concurrent search elsewhere in the client could in principle satisfy this call.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<UserSearchResults> SearchUsers(string name, int timeoutMs = 10000)
    {
        FriendsSearchResult result = await Application.InvokeAsync<FriendsSearchRequest, FriendsSearchResult>(
            ApplicationMemberIds.FriendsSearch,
            new FriendsSearchRequest(name, timeoutMs),
            Ct);
        return new UserSearchResults(result.Friends, result.Others);
    }

    /// <summary>
    /// Requests the marketplace price history and current offer counts for one furni kind.
    /// </summary>
    /// <param name="itemType">The furni category: 1 floor item, 2 wall item, 3 limited edition.</param>
    /// <param name="kind">The furni type id (the sprite/class id shared by all copies of that furni).</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(
        int itemType,
        int kind,
        int timeoutMs = 10000) =>
        GetMarketplaceStats(itemType, kind, "", timeoutMs);

    /// <summary>
    /// Requests marketplace stats for one furni kind, narrowed to a specific variant.
    /// </summary>
    /// <param name="itemType">The furni category: 1 floor item, 2 wall item, 3 limited edition.</param>
    /// <param name="kind">The furni type id.</param>
    /// <param name="extraData">
    /// The variant discriminator, for example the limited-edition serial data. Pass an empty
    /// string for the whole kind.
    /// </param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="NotSupportedException">
    /// The session is a Flash client whose marketplace uses the legacy wire layout, which has
    /// no field for <paramref name="extraData"/>, and a non-empty value was supplied.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(
        int itemType,
        int kind,
        string extraData,
        int timeoutMs = 10000)
    {
        return Application.InvokeAsync<MarketplaceItemStatsRequest, MarketplaceItemStatsSnapshot>(
            ApplicationMemberIds.MarketplaceItemStatsGet,
            new MarketplaceItemStatsRequest(
                (MarketplaceFurniCategory)itemType,
                kind,
                extraData,
                timeoutMs),
            Ct).AsTask();
    }

    /// <summary>
    /// Requests marketplace stats for a floor furni kind. Shorthand for
    /// <c>GetMarketplaceStats(1, kind)</c>.
    /// </summary>
    /// <param name="kind">The furni type id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    public Task<MarketplaceItemStatsSnapshot> GetFloorItemStats(int kind, int timeoutMs = 10000) => GetMarketplaceStats(1, kind, timeoutMs);

    /// <summary>
    /// Requests marketplace stats for a wall furni kind. Shorthand for
    /// <c>GetMarketplaceStats(2, kind)</c>.
    /// </summary>
    /// <param name="kind">The furni type id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    public Task<MarketplaceItemStatsSnapshot> GetWallItemStats(int kind, int timeoutMs = 10000) => GetMarketplaceStats(2, kind, timeoutMs);

    /// <summary>
    /// Searches the marketplace for offers currently on sale, grouping duplicate unique items.
    /// </summary>
    /// <param name="name">Free-text name filter; empty matches everything.</param>
    /// <param name="minPrice">Minimum price in credits, or -1 for no lower bound.</param>
    /// <param name="maxPrice">Maximum price in credits, or -1 for no upper bound.</param>
    /// <param name="sort">
    /// Sort order: 1 highest price, 2 lowest price, 3 most trades, 4 least trades, 5 most
    /// offers, 6 least offers.
    /// </param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No offers arrived in time.</exception>
    public Task<MarketplaceOfferPage> SearchMarketplace(
        string name = "", int minPrice = -1, int maxPrice = -1, int sort = 1, int timeoutMs = 10000) =>
        SearchMarketplace(name, minPrice, maxPrice, sort, true, timeoutMs);

    /// <summary>
    /// Searches the marketplace for offers currently on sale, with explicit control over
    /// unique-offer grouping.
    /// </summary>
    /// <param name="name">Free-text name filter; empty matches everything.</param>
    /// <param name="minPrice">Minimum price in credits, or -1 for no lower bound.</param>
    /// <param name="maxPrice">Maximum price in credits, or -1 for no upper bound.</param>
    /// <param name="sort">
    /// Sort order: 1 highest price, 2 lowest price, 3 most trades, 4 least trades, 5 most
    /// offers, 6 least offers.
    /// </param>
    /// <param name="combineUniques">
    /// Whether duplicate unique (limited edition) offers are collapsed into one row. Only the
    /// modern Flash marketplace layout can express <see langword="false"/>.
    /// </param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="NotSupportedException">
    /// <paramref name="combineUniques"/> is <see langword="false"/> on a Unity session or on a
    /// legacy Flash marketplace layout, neither of which can carry the flag.
    /// </exception>
    /// <exception cref="UnsupportedClientException">The session's client flavour is unknown.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">No offers arrived in time.</exception>
    public Task<MarketplaceOfferPage> SearchMarketplace(
        string name,
        int minPrice,
        int maxPrice,
        int sort,
        bool combineUniques,
        int timeoutMs = 10000)
    {
        return Application.InvokeAsync<MarketplaceSearchRequest, MarketplaceOfferPage>(
            ApplicationMemberIds.MarketplaceSearch,
            new MarketplaceSearchRequest(
                name,
                minPrice,
                maxPrice,
                (MarketplaceSortOrder)sort,
                combineUniques,
                TimeoutMilliseconds: timeoutMs),
            Ct).AsTask();
    }

    /// <summary>
    /// Requests the local user's own marketplace offers that are still open for sale, together
    /// with the credits waiting to be redeemed.
    /// </summary>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No offers arrived in time.</exception>
    public Task<MarketplaceOwnOfferPage> GetMyMarketplaceOffers(int timeoutMs = 10000) =>
        GetMyMarketplaceOffers(1, timeoutMs);

    /// <summary>
    /// Requests one category of the local user's own marketplace offers.
    /// </summary>
    /// <param name="category">1 open offers, 2 sold offers, 3 expired offers.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="NotSupportedException">
    /// A category other than 1 was requested on a Unity session or on a legacy Flash
    /// marketplace layout; neither exposes the sold/expired history.
    /// </exception>
    /// <exception cref="UnsupportedClientException">The session's client flavour is unknown.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">No offers arrived in time.</exception>
    public Task<MarketplaceOwnOfferPage> GetMyMarketplaceOffers(
        int category,
        int timeoutMs)
    {
        return Application.InvokeAsync<MarketplaceOwnOffersRequest, MarketplaceOwnOfferPage>(
            ApplicationMemberIds.MarketplaceOwnOffersGet,
            new MarketplaceOwnOffersRequest(
                (MarketplaceOwnOffersCategory)category,
                TimeoutMilliseconds: timeoutMs),
            Ct).AsTask();
    }

    /// <exception cref="Qx.Game.RequestTimeoutException">No badge list arrived in time.</exception>
    public async Task<BadgeInventory> GetBadgeInventory(int timeoutMs = 10000)
    {
        BadgeRefreshResult refreshed = await Application
            .InvokeAsync<BadgeRefreshRequest, BadgeRefreshResult>(
                ApplicationMemberIds.BadgesRefresh,
                new BadgeRefreshRequest(Limit: 500, TimeoutMilliseconds: timeoutMs),
                Ct)
            .ConfigureAwait(false);
        OwnedBadgePage page = refreshed.FirstPage;
        ValidateOwnedBadgePage(refreshed, page, 0);
        var badges = new List<OwnedBadge>(page.Total);
        AddOwnedBadges(page, badges);
        while (page.NextOffset is int offset)
        {
            page = await Application.InvokeAsync<OwnedBadgePageRequest, OwnedBadgePage>(
                    ApplicationMemberIds.BadgesOwnedList,
                    new OwnedBadgePageRequest(offset, 500, refreshed.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateOwnedBadgePage(refreshed, page, offset);
            AddOwnedBadges(page, badges);
        }
        if (badges.Count != refreshed.FirstPage.Total)
            throw new InvalidOperationException("The badge application returned an incomplete inventory.");
        return new BadgeInventory(1, 0, Array.AsReadOnly(badges.ToArray()));
    }

    /// <summary>
    /// Requests every achievement the account can earn, with the current level and progress of
    /// each. The reply also refreshes the tracked achievement state read by the
    /// <c>Achievements</c> property.
    /// </summary>
    /// <exception cref="Qx.Game.RequestTimeoutException">No achievement list arrived in time.</exception>
    public async Task<Achievements> GetAchievements(int timeoutMs = 10000)
    {
        AchievementRefreshResult refreshed = await Application
            .InvokeAsync<AchievementRefreshRequest, AchievementRefreshResult>(
                ApplicationMemberIds.AchievementsRefresh,
                new AchievementRefreshRequest(Limit: 500, TimeoutMilliseconds: timeoutMs),
                Ct)
            .ConfigureAwait(false);
        AchievementPage page = refreshed.FirstPage;
        ValidateAchievementPage(refreshed, page, 0);
        var achievements = new List<Achievement>(page.Total);
        AddAchievements(page, achievements);
        while (page.NextOffset is int offset)
        {
            page = await Application.InvokeAsync<AchievementPageRequest, AchievementPage>(
                    ApplicationMemberIds.AchievementsList,
                    new AchievementPageRequest(offset, 500, refreshed.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateAchievementPage(refreshed, page, offset);
            AddAchievements(page, achievements);
        }
        if (achievements.Count != refreshed.FirstPage.Total)
            throw new InvalidOperationException("The achievement application returned an incomplete list.");
        return new Achievements(
            Array.AsReadOnly(achievements.ToArray()),
            refreshed.FirstPage.DefaultCategory);
    }

    private static void AddOwnedBadges(OwnedBadgePage page, List<OwnedBadge> badges)
    {
        badges.AddRange(page.Badges.Select(badge => new OwnedBadge(
            badge.Id,
            badge.Code,
            badge.OwnerCount,
            badge.RarityId,
            badge.HasRarityData)));
    }

    private static void ValidateOwnedBadgePage(
        BadgeRefreshResult refreshed,
        OwnedBadgePage page,
        int offset)
    {
        int consumed = checked(offset + page.Badges.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (refreshed.SnapshotRevision <= 0 ||
            !refreshed.FirstPage.Connected ||
            refreshed.FirstPage.Client != refreshed.Client ||
            refreshed.FirstPage.SnapshotRevision != refreshed.SnapshotRevision ||
            refreshed.FirstPage.SessionGeneration != refreshed.SessionGeneration ||
            refreshed.FirstPage.StateRevision != refreshed.StateRevision ||
            refreshed.FirstPage.InventoryRevision != refreshed.InventoryRevision ||
            refreshed.FirstPage.BaselineRevision != refreshed.BaselineRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision ||
            page.Connected != refreshed.FirstPage.Connected ||
            page.Client != refreshed.Client ||
            page.SessionGeneration != refreshed.SessionGeneration ||
            page.StateRevision != refreshed.StateRevision ||
            page.InventoryRevision != refreshed.InventoryRevision ||
            page.BaselineRevision != refreshed.BaselineRevision ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Total != refreshed.FirstPage.Total ||
            page.Inventory != refreshed.FirstPage.Inventory ||
            !page.Inventory.Loaded ||
            page.Inventory.Loading ||
            page.Inventory.Stale ||
            page.Inventory.RecoveryPending ||
            page.Inventory.OwnedCount != page.Total ||
            page.Badges.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Badges.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException("The badge application returned an invalid snapshot page.");
        }
    }

    private static void AddAchievements(AchievementPage page, List<Achievement> achievements)
    {
        achievements.AddRange(page.Achievements.Select(achievement => new Achievement
        {
            Id = achievement.Id,
            Level = achievement.Level,
            BadgeCode = achievement.BadgeCode,
            BaseProgress = achievement.BaseProgress,
            MaxProgress = achievement.MaxProgress,
            LevelRewardPoints = achievement.LevelRewardPoints,
            LevelRewardPointType = achievement.LevelRewardPointType,
            CurrentProgress = achievement.CurrentProgress,
            IsComplete = achievement.IsComplete,
            Category = achievement.Category,
            Subcategory = achievement.Subcategory,
            MaxLevel = achievement.MaxLevel,
            DisplayMethod = achievement.DisplayMethod,
            State = achievement.State
        }));
    }

    private static void ValidateAchievementPage(
        AchievementRefreshResult refreshed,
        AchievementPage page,
        int offset)
    {
        int consumed = checked(offset + page.Achievements.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (refreshed.SnapshotRevision <= 0 ||
            !refreshed.FirstPage.Connected ||
            refreshed.FirstPage.Client != refreshed.Client ||
            refreshed.FirstPage.SnapshotRevision != refreshed.SnapshotRevision ||
            refreshed.FirstPage.SessionGeneration != refreshed.SessionGeneration ||
            refreshed.FirstPage.StateRevision != refreshed.StateRevision ||
            refreshed.FirstPage.ListRevision != refreshed.ListRevision ||
            refreshed.FirstPage.BaselineRevision != refreshed.BaselineRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision ||
            page.Connected != refreshed.FirstPage.Connected ||
            page.Client != refreshed.Client ||
            page.SessionGeneration != refreshed.SessionGeneration ||
            page.StateRevision != refreshed.StateRevision ||
            page.ListRevision != refreshed.ListRevision ||
            page.BaselineRevision != refreshed.BaselineRevision ||
            page.NewCodesRevision != refreshed.FirstPage.NewCodesRevision ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Total != refreshed.FirstPage.Total ||
            page.Completed < 0 ||
            page.Completed > page.Total ||
            page.Completed != refreshed.FirstPage.Completed ||
            page.DefaultCategory != refreshed.FirstPage.DefaultCategory ||
            !page.Loaded ||
            page.Achievements.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Achievements.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException("The achievement application returned an invalid snapshot page.");
        }
    }

    /// <summary>
    /// Requests the groups the local user belongs to, with the membership rank in each.
    /// </summary>
    /// <returns>The memberships, or an empty list when the user is in no group.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No membership list arrived in time.</exception>
    public async Task<IReadOnlyList<GuildMembership>> GetGuildMemberships(int timeoutMs = 10000)
    {
        const int page_limit = 500;
        var memberships = new List<GuildMembership>();
        var membership_ids = new HashSet<Id>();
        int offset = 0;
        int? total_memberships = null;
        long? snapshot_revision = null;
        long? session_generation = null;
        ClientType? client = null;

        while (true)
        {
            GroupMembershipsPage page = await Application
                .InvokeAsync<GroupMembershipsGetRequest, GroupMembershipsPage>(
                    ApplicationMemberIds.GroupsMembershipsGet,
                    new GroupMembershipsGetRequest(
                        offset,
                        page_limit,
                        timeoutMs,
                        snapshot_revision,
                        session_generation),
                    Ct)
                .ConfigureAwait(false);
            if (page.Offset != offset ||
                page.TotalMemberships < 0 ||
                page.SnapshotRevision <= 0 ||
                page.Client is not (ClientType.Flash or ClientType.Unity) ||
                page.Memberships.Count > page_limit ||
                (long)page.Offset + page.Memberships.Count > page.TotalMemberships)
            {
                throw new InvalidDataException("Group membership pagination returned invalid metadata.");
            }

            if (total_memberships is null)
            {
                total_memberships = page.TotalMemberships;
                snapshot_revision = page.SnapshotRevision;
                session_generation = page.SessionGeneration;
                client = page.Client;
            }
            else if (page.TotalMemberships != total_memberships ||
                     page.SnapshotRevision != snapshot_revision ||
                     page.SessionGeneration != session_generation ||
                     page.Client != client)
            {
                throw new InvalidDataException("Group memberships changed while the result was being collected.");
            }

            foreach (GuildMembership membership in page.Memberships)
            {
                if (!membership_ids.Add(membership.Id))
                    throw new InvalidDataException("Group membership pagination returned an overlapping item.");
                memberships.Add(membership);
            }

            if (page.NextOffset is not int next_offset)
                break;
            if (page.Memberships.Count == 0 ||
                next_offset != (long)page.Offset + page.Memberships.Count ||
                next_offset >= page.TotalMemberships)
            {
                throw new InvalidDataException("Group membership pagination returned an invalid continuation.");
            }
            offset = next_offset;
        }

        if (memberships.Count != total_memberships)
            throw new InvalidDataException("Group membership pagination returned an incomplete result.");
        return Array.AsReadOnly(memberships.ToArray());
    }

    /// <summary>
    /// Requests the navigator record for a room: name, owner, description, tags, door mode,
    /// visitor counts, rating and group. Works for any room, not only the current one.
    /// </summary>
    /// <param name="roomId">The room id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching room data arrived in time.</exception>
    public async Task<RoomData> GetRoomData(Id roomId, int timeoutMs = 10000)
    {
        RoomDataReadResult result = await Application
            .InvokeAsync<RoomDataReadRequest, RoomDataReadResult>(
                ApplicationMemberIds.RoomDataGet,
                new RoomDataReadRequest(roomId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (result.RequestedRoomId != roomId || result.Room.Id != roomId || result.MessagesDispatched != 1)
            throw new InvalidDataException("The room-data application returned an inconsistent result.");
        RoomDataView room = result.Room;
        return new RoomData
        {
            Id = room.Id,
            Name = room.Name,
            OwnerId = room.OwnerId,
            OwnerName = room.OwnerName,
            DoorMode = room.DoorMode,
            UserCount = room.UserCount,
            MaxUserCount = room.MaxUserCount,
            Description = room.Description,
            TradeMode = room.TradeMode,
            Score = room.Score,
            Ranking = room.Ranking,
            Category = room.Category,
            Tags = Array.AsReadOnly(room.Tags.ToArray()),
            OfficialRoomPicRef = room.OfficialRoomPicRef,
            HasGroup = room.HasGroup,
            GroupId = room.GroupId,
            GroupName = room.GroupName,
            GroupBadge = room.GroupBadge,
            HasEvent = room.HasEvent,
            EventName = room.EventName,
            EventDescription = room.EventDescription,
            EventMinutesRemaining = room.EventMinutesRemaining,
            ShowOwner = room.ShowOwner,
            AllowPets = room.AllowPets,
            DisplayRoomEntryAd = room.DisplayRoomEntryAd
        };
    }

    /// <summary>
    /// Requests the list of users who hold rights in a room. The server answers only for rooms
    /// the local user owns.
    /// </summary>
    /// <param name="roomId">The room id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds.</param>
    /// <returns>The rights holders as id/name pairs; the owner is not included.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">
    /// No matching rights list arrived in time - which is also what happens when the local user
    /// does not own the room.
    /// </exception>
    public async Task<IReadOnlyList<IdName>> GetRightsFor(Id roomId, int timeoutMs = 10000)
    {
        RoomRightsReadResult result = await Application
            .InvokeAsync<RoomRightsReadRequest, RoomRightsReadResult>(
                ApplicationMemberIds.RoomRightsList,
                new RoomRightsReadRequest(roomId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (result.RoomId != roomId || result.MessagesDispatched != 1)
            throw new InvalidDataException("The room-rights application returned an inconsistent result.");
        return Array.AsReadOnly(result.Users.ToArray());
    }

    /// <summary>
    /// Requests the rights holders of the room the user is currently in.
    /// </summary>
    /// <param name="timeoutMs">Total time budget in milliseconds.</param>
    /// <exception cref="InvalidOperationException">The user is not in a room.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching rights list arrived in time.</exception>
    public Task<IReadOnlyList<IdName>> GetRights(int timeoutMs = 10000)
    {
        if (!Room.IsInRoom)
            throw new InvalidOperationException("The user is not in a room.");
        return GetRightsFor(Room.RoomId, timeoutMs);
    }

    /// <summary>
    /// Reads a room's current settings, applies <paramref name="update"/> to them and saves the
    /// result. This is the safe way to change one setting without clearing the others.
    /// </summary>
    /// <param name="update">
    /// Receives the current settings and returns the modified copy. <see cref="RoomSettings"/>
    /// is a record, so use <c>with</c> expressions. It must not change the room id and must not
    /// return <see langword="null"/>.
    /// </param>
    /// <param name="roomId">The room to modify, or <see langword="null"/> for the current room.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds for reading and saving the settings.</param>
    /// <exception cref="InvalidOperationException">
    /// No room id was given and the user is not in a room, or <paramref name="update"/> returned
    /// <see langword="null"/> or changed the room id.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The session is Unity and the settings contain fields the Unity wire layout cannot carry.
    /// </exception>
    /// <remarks>
    /// The room password is not part of <see cref="RoomSettings"/> and is therefore saved as
    /// empty; do not use this on a password-locked room unless clearing the password is
    /// intended.
    /// </remarks>
    public async Task<RoomSettings> ModifyRoomSettings(
        Func<RoomSettings, RoomSettings> update,
        Id? roomId = null,
        int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(update);
        Id target_room_id = roomId ?? Room.Capture(room => room.IsInRoom
            ? (Id)room.RoomId
            : throw new InvalidOperationException("The user is not in a room."));
        RoomSettingsStateView current_state = await GetRoomSettingsState(target_room_id, timeoutMs);
        RoomSettings current = ToLegacyRoomSettings(current_state);
        RoomSettings changed = update(current) ??
            throw new InvalidOperationException("The room settings update returned null.");
        if (changed.RoomId != target_room_id)
            throw new InvalidOperationException("The room settings update changed the room ID.");
        await Application.InvokeAsync<RoomSettingsSaveRequest, RoomSettingsSaveReceipt>(
            ApplicationMemberIds.RoomSettingsSave,
            new RoomSettingsSaveRequest(
                ToApplicationRoomSettings(changed),
                TimeoutMilliseconds: timeoutMs,
                ExpectedSessionGeneration: current_state.SessionGeneration,
                ExpectedRoomGeneration: current_state.RoomGeneration,
                ExpectedOperationRevision: current_state.OperationRevision,
                ExpectedSnapshotRevision: current_state.SnapshotRevision),
            Ct);
        return changed;
    }

    /// <summary>
    /// Requests the rooms owned by the local user, through the navigator's <c>"my"</c> view.
    /// </summary>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> GetUserRooms(int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRooms(NavigatorQuickSearch.MyRooms, timeoutMs);
        return rooms.ToArray();
    }

    /// <summary>
    /// Requests the saved wardrobe outfits, each with its slot number, figure string and
    /// gender.
    /// </summary>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No wardrobe arrived in time.</exception>
    public async Task<Wardrobe> GetWardrobe(int timeoutMs = 10000)
    {
        long started = Environment.TickCount64;
        var outfits = new List<WardrobeOutfit>();
        ProfileWardrobePage? first_page = null;
        int offset = 0;

        while (true)
        {
            int remaining = first_page is null
                ? timeoutMs
                : timeoutMs - checked((int)Math.Min(int.MaxValue, Environment.TickCount64 - started));
            if (remaining <= 0)
                throw new RequestTimeoutException("profile.wardrobe.get", "wardrobe", timeoutMs);

            ProfileWardrobePage page = await Application.InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
                ApplicationMemberIds.ProfileWardrobeGet,
                new ProfileWardrobeRequest(
                    offset,
                    500,
                    remaining,
                    first_page?.SnapshotRevision),
                Ct);
            first_page ??= page;
            if (page.Client != first_page.Client ||
                page.Generation != first_page.Generation ||
                page.Revision != first_page.Revision ||
                page.SnapshotRevision != first_page.SnapshotRevision ||
                page.State != first_page.State ||
                page.Total != first_page.Total ||
                page.Offset != offset)
            {
                throw new InvalidOperationException("The wardrobe changed while it was being read.");
            }

            outfits.AddRange(page.Outfits);
            if (page.NextOffset is not int next_offset)
                break;
            if (next_offset <= offset)
                throw new InvalidOperationException("The wardrobe returned an invalid continuation offset.");
            offset = next_offset;
        }

        if (outfits.Count != first_page.Total)
            throw new InvalidOperationException("The wardrobe returned an incomplete result.");
        return new Wardrobe(first_page.State, Array.AsReadOnly(outfits.ToArray()));
    }

    /// <summary>
    /// Requests a room's editable settings: name, description, door mode, category, capacity,
    /// tags, trade mode, moderation permissions and the wall/floor appearance flags. The server
    /// answers only for rooms the local user owns.
    /// </summary>
    /// <param name="roomId">The room id.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">
    /// No matching settings arrived in time - also the outcome when the room is not owned by the
    /// local user.
    /// </exception>
    public async Task<RoomSettings> GetRoomSettings(Id roomId, int timeoutMs = 10000) =>
        ToLegacyRoomSettings(await GetRoomSettingsState(roomId, timeoutMs));

    private ValueTask<RoomSettingsStateView> GetRoomSettingsState(Id room_id, int timeout_ms) =>
        Application.InvokeAsync<RoomSettingsGetRequest, RoomSettingsStateView>(
            ApplicationMemberIds.RoomSettingsGet,
            new RoomSettingsGetRequest(room_id, timeout_ms),
            Ct);

    private static RoomSettings ToLegacyRoomSettings(RoomSettingsStateView state)
    {
        if (!state.Loaded || state.Settings is not { } settings || state.Metadata is not { } metadata)
            throw new InvalidOperationException($"Room settings for room {state.RoomId} were not loaded.");

        return new RoomSettings
        {
            RoomId = settings.RoomId,
            Name = settings.Name,
            Description = settings.Description,
            DoorMode = settings.DoorMode,
            CategoryId = settings.CategoryId,
            MaximumVisitors = settings.MaximumVisitors,
            MaximumVisitorsLimit = metadata.MaximumVisitorsLimit,
            MaximumVisitorsLowerLimit = metadata.MaximumVisitorsLowerLimit,
            Tags = settings.Tags,
            TradeMode = settings.TradeMode,
            AllowPets = settings.AllowPets,
            AllowFoodConsume = settings.AllowFoodConsume,
            AllowWalkThrough = settings.AllowWalkThrough,
            HideWalls = settings.HideWalls,
            WallThickness = settings.WallThickness,
            FloorThickness = settings.FloorThickness,
            ChatFloodSensitivity = settings.ChatFloodSensitivity,
            LeaveOnDoorTile = settings.LeaveOnDoorTile,
            IdleSleepEnabled = settings.IdleSleepEnabled,
            IdleSleepTimeoutSeconds = settings.IdleSleepTimeoutSeconds,
            IdleAutokickEnabled = settings.IdleAutokickEnabled,
            IdleAutokickTimeoutSeconds = settings.IdleAutokickTimeoutSeconds,
            MuteAllPets = settings.MuteAllPets,
            HiddenByBc = metadata.HiddenByBuildersClub,
            IsGroupRoom = metadata.IsGroupRoom,
            GroupRightsPolicy = metadata.GroupRightsPolicy,
            RequiresBuildersClub = metadata.RequiresBuildersClub,
            NftGroupIds = settings.NftGroupIds,
            IsHabboXDemoRoom = metadata.IsHabboXDemoRoom,
            WhoCanMute = settings.WhoCanMute,
            WhoCanKick = settings.WhoCanKick,
            WhoCanBan = settings.WhoCanBan
        };
    }

    private static RoomSettingsValues ToApplicationRoomSettings(RoomSettings settings) => new(
        settings.RoomId,
        settings.Name,
        settings.Description,
        settings.DoorMode,
        settings.CategoryId,
        settings.MaximumVisitors,
        settings.Tags,
        settings.TradeMode,
        settings.AllowPets,
        settings.AllowFoodConsume,
        settings.AllowWalkThrough,
        settings.HideWalls,
        settings.WallThickness,
        settings.FloorThickness,
        settings.ChatFloodSensitivity,
        settings.LeaveOnDoorTile,
        settings.IdleSleepEnabled,
        settings.IdleSleepTimeoutSeconds,
        settings.IdleAutokickEnabled,
        settings.IdleAutokickTimeoutSeconds,
        settings.MuteAllPets,
        settings.WhoCanMute,
        settings.WhoCanKick,
        settings.WhoCanBan,
        settings.NftGroupIds);

    /// <summary>
    /// Requests the catalog's top-level page tree for one catalog mode.
    /// </summary>
    /// <param name="catalogType">
    /// The catalog mode: <c>"NORMAL"</c> for the credit catalog, <c>"BUILDERS_CLUB"</c> for the
    /// builders club catalog.
    /// </param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching index arrived in time.</exception>
    /// <remarks>
    /// Answered from the catalog cache when a copy newer than
    /// <see cref="Qx.Game.CatalogManager.DefaultMaxAge"/> is held, and the cache is cleared
    /// outright when the hotel announces a republish. Pass <see cref="TimeSpan.Zero"/> as
    /// <paramref name="maxAge"/> to insist on a fetch.
    /// </remarks>
    /// <param name="maxAge">How old a cached copy may be.</param>
    public Task<CatalogIndex> GetCatalogIndex(
        string catalogType = "NORMAL",
        int timeoutMs = 10000,
        TimeSpan? maxAge = null) =>
        Game.Catalog.GetIndexAsync(catalogType, maxAge, timeoutMs, Ct);

    /// <summary>
    /// Requests one catalog page with its offers, which is what supplies the page id and offer
    /// id pair needed by <see cref="PurchaseFromCatalog"/>.
    /// </summary>
    /// <param name="pageId">The catalog page id, taken from <see cref="GetCatalogIndex"/>.</param>
    /// <param name="offerId">
    /// An offer to pre-select on the page, or -1 for none. It does not restrict the reply.
    /// </param>
    /// <param name="catalogType">The catalog mode; see <see cref="GetCatalogIndex"/>.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching page arrived in time.</exception>
    /// <remarks>Cached the same way as <see cref="GetCatalogIndex"/>.</remarks>
    /// <param name="maxAge">How old a cached copy may be.</param>
    public Task<CatalogPage> GetCatalogPage(
        int pageId,
        int offerId = -1,
        string catalogType = "NORMAL",
        int timeoutMs = 10000,
        TimeSpan? maxAge = null) =>
        Game.Catalog.GetPageAsync(pageId, catalogType, maxAge, offerId, timeoutMs, Ct);

    /// <summary>
    /// Searches the navigator for rooms owned by a user and keeps only exact owner-name
    /// matches, since the server's <c>owner:</c> filter also returns near matches.
    /// </summary>
    /// <param name="ownerName">The owner's user name; compared case-insensitively.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByOwner(string ownerName, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByOwner(ownerName, timeoutMs);
        return rooms.OwnedBy(ownerName).ToArray();
    }

    /// <summary>
    /// Searches the navigator by room name and keeps only rooms whose name actually contains
    /// the search text, case-insensitively.
    /// </summary>
    /// <param name="roomName">The room-name fragment to look for.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByName(string roomName, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByName(roomName, timeoutMs);
        return rooms.NameContains(roomName).ToArray();
    }

    /// <summary>
    /// Searches the navigator by tag and keeps only rooms that really carry that tag,
    /// case-insensitively.
    /// </summary>
    /// <param name="tag">The tag to look for, without a leading <c>#</c>.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByTag(string tag, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByTag(tag, timeoutMs);
        return rooms.TaggedAny(tag).ToArray();
    }

    /// <summary>
    /// Searches the navigator by group and keeps only rooms attached to a group whose name
    /// contains the search text, case-insensitively.
    /// </summary>
    /// <param name="groupName">The group-name fragment to look for.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByGroup(string groupName, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByGroup(groupName, timeoutMs);
        return rooms
            .Where(room => room.HasGroup &&
                room.GroupName.Contains(groupName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Searches for a user account and returns the single exact name match.
    /// </summary>
    /// <param name="name">The exact user name; matched case-insensitively.</param>
    /// <param name="timeoutMs">Total time budget in milliseconds, across one retry.</param>
    /// <returns>
    /// The matching account, or <see langword="null"/> when the search returned results but none
    /// of them carried that exact name.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No search result arrived in time.</exception>
    public async Task<UserSearchResult?> SearchUser(string name, int timeoutMs = 10000)
    {
        UserSearchResults results = await SearchUsers(name, timeoutMs);
        return results.Find(name);
    }

    private static UserProfile LegacyRemoteProfile(RemoteProfileView profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Figure = profile.Figure,
        Motto = profile.Motto,
        Created = profile.Created,
        AchievementScore = profile.AchievementScore,
        FriendCount = profile.FriendCount,
        IsFriend = profile.IsFriend,
        IsFriendRequestSent = profile.IsFriendRequestSent,
        OnlineStatus = profile.OnlineStatus,
        Groups = profile.Groups.ToArray(),
        LastAccessSeconds = profile.LastAccessSeconds,
        OpenProfileWindow = profile.OpenProfileWindow,
        IsHidden = profile.IsHidden,
        Level = profile.Level,
        SubscriptionLevel = profile.SubscriptionLevel,
        StarGems = profile.StarGems,
        AllowFriendRequests = profile.AllowFriendRequests,
        HasFriendRequestsPending = profile.HasFriendRequestsPending,
        TotalBadges = profile.TotalBadges,
        AchievementLevel = profile.AchievementLevel,
        BadgeRarities = profile.BadgeRarities.ToArray(),
        TotalBadgesRank = profile.TotalBadgesRank,
        NameColor = profile.NameColor,
        OldNames = profile.OldNames.ToArray()
    };
}
