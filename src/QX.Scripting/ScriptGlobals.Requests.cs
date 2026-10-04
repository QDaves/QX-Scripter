using Qx.Messages;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Game.Protocol;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Requests another user's extended profile, with figure, motto, creation date, achievement
    /// score, group memberships and friend and relationship flags.
    /// </summary>
    /// <remarks>
    /// The request asks the hotel not to open the profile window. The reply is not blocked, so the
    /// game client also receives it.
    /// </remarks>
    /// <param name="userId">The target user's account id, not their room index.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The profile whose id matches <paramref name="userId"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> is not positive, or <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching profile arrived in time.</exception>
    public async Task<UserProfile> GetProfile(Id userId, int timeoutMs = 10000)
    {
        RemoteProfileResult result = await _application
            .InvokeAsync<RemoteProfileGetRequest, RemoteProfileResult>(
                ApplicationMemberIds.PeopleProfileGet,
                new RemoteProfileGetRequest(userId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        return LegacyRemoteProfile(result.Profile);
    }

    /// <summary>
    /// Requests a group's details, with name, description, badge, home room, member count and the
    /// local user's membership state.
    /// </summary>
    /// <remarks>
    /// The request asks the hotel not to open the group window. The reply is not blocked, so the
    /// game client also receives it.
    /// </remarks>
    /// <param name="groupId">The group id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The details of the group whose id matches <paramref name="groupId"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching group details arrived in time.</exception>
    public async Task<GroupData> GetGroup(Id groupId, int timeoutMs = 10000)
    {
        GroupDetailsResult result = await _application
            .InvokeAsync<GroupDetailsGetRequest, GroupDetailsResult>(
                ApplicationMemberIds.GroupsDetailsGet,
                new GroupDetailsGetRequest(groupId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        return result.Details;
    }

    /// <summary>
    /// Requests a pet's stats, with breed, level, experience, energy, happiness, scratches and owner.
    /// </summary>
    /// <remarks>
    /// The pet must be visible to the server in the current context (in the room or in the
    /// inventory). The request is sent once without a retry, and the reply is not blocked, so the
    /// game client also receives it.
    /// </remarks>
    /// <param name="petId">The pet id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The stats of the pet whose id matches <paramref name="petId"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching pet info arrived in time.</exception>
    public async Task<PetInfo> GetPetInfo(Id petId, int timeoutMs = 10000)
    {
        PetInfoReadResult result = await _application
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
    /// Requests the contents of a sticky note (post-it) placed in the room.
    /// </summary>
    /// <remarks>
    /// The request is sent once without a retry, and the reply is not blocked, so the game client
    /// also receives it.
    /// </remarks>
    /// <param name="itemId">The wall item id of the sticky note.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The sticky note's id, color and text.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching item data arrived in time.</exception>
    public async Task<Sticky> GetSticky(Id itemId, int timeoutMs = 10000)
    {
        StickyReadResult result = await _application
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
    /// Requests the contents of a sticky note (post-it) placed in the room.
    /// </summary>
    /// <remarks>Same as <see cref="GetSticky(Id, int)"/> with the id of <paramref name="item"/>.</remarks>
    /// <param name="item">The wall item holding the note; only its id is used.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The sticky note's id, color and text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching item data arrived in time.</exception>
    public Task<Sticky> GetSticky(WallItem item, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetSticky(item.Id, timeoutMs);
    }

    /// <summary>
    /// Requests the badges a user has equipped in their profile slots.
    /// </summary>
    /// <remarks>
    /// This is the small selected set, not the user's full badge collection. The reply is not
    /// blocked, so the game client also receives it.
    /// </remarks>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The user's id and equipped badges.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching badge list arrived in time.</exception>
    public async Task<UserBadges> GetBadges(Id userId, int timeoutMs = 10000)
    {
        RemoteBadgesResult result = await _application
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
    /// Requests the relationship summary shown on a user's profile.
    /// </summary>
    /// <remarks>
    /// Each entry holds a relationship type, how many friends the user marked with it, and one
    /// randomly picked friend with that relationship. The reply is not blocked, so the game client
    /// also receives it.
    /// </remarks>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The user's id and relationship entries.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching relationship status arrived in time.</exception>
    public async Task<RelationshipStatus> GetRelationship(Id userId, int timeoutMs = 10000)
    {
        RemoteRelationshipResult result = await _application
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
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>
    /// The result, which is matched back to the exact <paramref name="code"/> and
    /// <paramref name="filter"/> that were requested. The reply also reaches the game client.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty or white space.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching search result arrived in time.</exception>
    public async Task<NavigatorSearchResult> SearchRooms(
        string code,
        string filter,
        int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await _application.InvokeAsync<NavigatorViewSearchInput, NavigatorSearchSnapshot>(
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
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms of every result block.</returns>
    public async Task<RoomDataQuery> SearchRoomQuery(
        string code,
        string filter = "",
        int timeoutMs = 10000)
    {
        NavigatorSearchResult result = await SearchRooms(code, filter, timeoutMs);
        return Queries.From(result.Rooms);
    }

    /// <summary>
    /// Searches for user accounts by name and waits for the result.
    /// </summary>
    /// <remarks>
    /// The server returns both exact and partial matches, including offline users, and applies its
    /// own result cap. The reply is not blocked, so the game client also receives it.
    /// </remarks>
    /// <param name="name">The name or name fragment to search for.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>
    /// The first search result that arrives, split into friends and other users. The reply carries
    /// no echo of the query, so a concurrent search elsewhere in the client could in principle
    /// satisfy this call.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no search result arrived in time.</exception>
    public async Task<UserSearchResults> SearchUsers(string name, int timeoutMs = 10000)
    {
        FriendsSearchResult result = await _application.InvokeAsync<FriendsSearchRequest, FriendsSearchResult>(
            ApplicationMemberIds.FriendsSearch,
            new FriendsSearchRequest(name, timeoutMs),
            Ct);
        return new UserSearchResults(result.Friends, result.Others);
    }

    /// <summary>
    /// Requests the marketplace price history and current offer counts for one furni kind.
    /// </summary>
    /// <remarks>The reply is not blocked, so the game client also receives it.</remarks>
    /// <param name="furniCategory">The furni category: floor item, wall item or limited edition.</param>
    /// <param name="kind">The furni type id (the sprite or class id shared by all copies of that furni).</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats whose category and furni type match the request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="furniCategory"/> is not a defined value, or <paramref name="kind"/> is below 1.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(
        MarketplaceFurniCategory furniCategory,
        int kind,
        int timeoutMs = 10000) =>
        GetMarketplaceStats(furniCategory, kind, "", timeoutMs);

    /// <summary>
    /// Requests marketplace stats for one furni kind, narrowed to a specific variant.
    /// </summary>
    /// <remarks>The reply is not blocked, so the game client also receives it.</remarks>
    /// <param name="furniCategory">The furni category: floor item, wall item or limited edition.</param>
    /// <param name="kind">The furni type id.</param>
    /// <param name="extraData">
    /// The variant discriminator, for example the limited edition serial data. Pass an empty
    /// string for the whole kind.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats whose category and furni type match the request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="furniCategory"/> is not a defined value, or <paramref name="kind"/> is below 1.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the client build's marketplace uses the legacy wire layout, which has no field for
    /// <paramref name="extraData"/>, and a non-empty value was supplied.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(
        MarketplaceFurniCategory furniCategory,
        int kind,
        string extraData,
        int timeoutMs = 10000)
    {
        return _application.InvokeAsync<MarketplaceItemStatsRequest, MarketplaceItemStatsSnapshot>(
            ApplicationMemberIds.MarketplaceItemStatsGet,
            new MarketplaceItemStatsRequest(
                furniCategory,
                kind,
                extraData,
                timeoutMs),
            Ct).AsTask();
    }

    /// <summary>
    /// Requests the marketplace price history and current offer counts for one floor or wall furni kind.
    /// </summary>
    /// <remarks>
    /// Same as <see cref="GetMarketplaceStats(MarketplaceFurniCategory, int, int)"/> with the floor or wall category.
    /// Limited editions cannot be expressed here. The reply is not blocked, so the game client also
    /// receives it.
    /// </remarks>
    /// <param name="type">Whether the kind is a floor or a wall item.</param>
    /// <param name="kind">The furni type id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats whose category and furni type match the request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type"/> is neither floor nor wall, or <paramref name="kind"/> is below 1.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(
        ItemType type,
        int kind,
        int timeoutMs = 10000) =>
        GetMarketplaceStats(
            type switch
            {
                ItemType.Floor => MarketplaceFurniCategory.Floor,
                ItemType.Wall => MarketplaceFurniCategory.Wall,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported item type.")
            },
            kind,
            timeoutMs);

    /// <summary>
    /// Requests the marketplace price history and current offer counts for a room item's furni kind.
    /// </summary>
    /// <remarks>
    /// Same as <see cref="GetMarketplaceStats(ItemType, int, int)"/> with the item's type and kind.
    /// </remarks>
    /// <param name="item">The room item; its type and kind are used.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats for that furni kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the item is neither a floor nor a wall item.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(Furni item, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetMarketplaceStats(item.Type, item.Kind, timeoutMs);
    }

    /// <summary>
    /// Requests the marketplace price history and current offer counts for an inventory item's furni kind.
    /// </summary>
    /// <remarks>
    /// Same as <see cref="GetMarketplaceStats(ItemType, int, int)"/> with the item's type and kind.
    /// </remarks>
    /// <param name="item">The inventory item; its type and kind are used.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats for that furni kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the item is neither a floor nor a wall item.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(InventoryItem item, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetMarketplaceStats(item.Type, item.Kind, timeoutMs);
    }

    /// <summary>
    /// Requests the marketplace price history and current offer counts for a furni definition.
    /// </summary>
    /// <remarks>
    /// Same as <see cref="GetMarketplaceStats(ItemType, int, int)"/> with the definition's type and kind.
    /// </remarks>
    /// <param name="item">The furni definition; its type and kind are used.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats for that furni kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the definition is neither a floor nor a wall item.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching stats arrived in time.</exception>
    public Task<MarketplaceItemStatsSnapshot> GetMarketplaceStats(FurniInfo item, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetMarketplaceStats(item.Type, item.Kind, timeoutMs);
    }

    /// <summary>
    /// Requests marketplace stats for a floor furni kind.
    /// </summary>
    /// <remarks>Shorthand for <c>GetMarketplaceStats(MarketplaceFurniCategory.Floor, kind)</c>.</remarks>
    /// <param name="kind">The furni type id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats of the floor furni kind.</returns>
    public Task<MarketplaceItemStatsSnapshot> GetFloorItemStats(int kind, int timeoutMs = 10000) =>
        GetMarketplaceStats(MarketplaceFurniCategory.Floor, kind, timeoutMs);

    /// <summary>
    /// Requests marketplace stats for a wall furni kind.
    /// </summary>
    /// <remarks>Shorthand for <c>GetMarketplaceStats(MarketplaceFurniCategory.Wall, kind)</c>.</remarks>
    /// <param name="kind">The furni type id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The stats of the wall furni kind.</returns>
    public Task<MarketplaceItemStatsSnapshot> GetWallItemStats(int kind, int timeoutMs = 10000) =>
        GetMarketplaceStats(MarketplaceFurniCategory.Wall, kind, timeoutMs);

    /// <summary>
    /// Searches the marketplace for offers currently on sale, grouping duplicate unique items.
    /// </summary>
    /// <remarks>
    /// The first search result that arrives after the request is taken as the answer. The reply is
    /// not blocked, so the game client also receives it.
    /// </remarks>
    /// <param name="name">The free text name filter; empty matches everything.</param>
    /// <param name="minPrice">The minimum price in credits, or -1 for no lower bound.</param>
    /// <param name="maxPrice">The maximum price in credits, or -1 for no upper bound.</param>
    /// <param name="sort">The order of the offers.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The first page of up to 100 offers, with the total offer count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a price is below -1, <paramref name="sort"/> is not a defined value, or <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minPrice"/> is greater than <paramref name="maxPrice"/>.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no offers arrived in time.</exception>
    public Task<MarketplaceOfferPage> SearchMarketplace(
        string name = "",
        int minPrice = -1,
        int maxPrice = -1,
        MarketplaceSortOrder sort = MarketplaceSortOrder.HighestPrice,
        int timeoutMs = 10000) =>
        SearchMarketplace(name, minPrice, maxPrice, sort, true, timeoutMs);

    /// <summary>
    /// Searches the marketplace for offers currently on sale, choosing whether duplicate unique
    /// items are grouped.
    /// </summary>
    /// <remarks>
    /// The first search result that arrives after the request is taken as the answer. The reply is
    /// not blocked, so the game client also receives it.
    /// </remarks>
    /// <param name="name">The free text name filter; empty matches everything.</param>
    /// <param name="minPrice">The minimum price in credits, or -1 for no lower bound.</param>
    /// <param name="maxPrice">The maximum price in credits, or -1 for no upper bound.</param>
    /// <param name="sort">The order of the offers.</param>
    /// <param name="combineUniques"><see langword="true"/> to group duplicate unique items into one offer; otherwise, <see langword="false"/>.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The first page of up to 100 offers, with the total offer count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a price is below -1, <paramref name="sort"/> is not a defined value, or <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minPrice"/> is greater than <paramref name="maxPrice"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="combineUniques"/> is <see langword="false"/> and the Flash marketplace uses
    /// the legacy wire layout, which cannot turn grouping off.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no offers arrived in time.</exception>
    public Task<MarketplaceOfferPage> SearchMarketplace(
    string name,
    int minPrice,
    int maxPrice,
    MarketplaceSortOrder sort,
    bool combineUniques,
    int timeoutMs = 10000)
    {
        return _application.InvokeAsync<MarketplaceSearchRequest, MarketplaceOfferPage>(
            ApplicationMemberIds.MarketplaceSearch,
            new MarketplaceSearchRequest(
                name,
                minPrice,
                maxPrice,
                sort,
                combineUniques,
                TimeoutMilliseconds: timeoutMs),
            Ct).AsTask();
    }

    /// <summary>
    /// Requests the local user's own marketplace offers in one category, together with the credits
    /// waiting to be redeemed.
    /// </summary>
    /// <remarks>The reply is not blocked, so the game client also receives it.</remarks>
    /// <param name="category">The offers to load: open, sold or expired. Open offers by default.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The first page of up to 100 offers, with the total count and the credits waiting.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="category"/> is not a defined value, or <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="category"/> is not <see cref="MarketplaceOwnOffersCategory.Open"/> and the
    /// Flash marketplace uses the legacy wire layout, which only reports open offers.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no offers arrived in time.</exception>
    public Task<MarketplaceOwnOfferPage> GetMyMarketplaceOffers(
        MarketplaceOwnOffersCategory category = MarketplaceOwnOffersCategory.Open,
        int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceOwnOffersRequest, MarketplaceOwnOfferPage>(
            ApplicationMemberIds.MarketplaceOwnOffersGet,
            new MarketplaceOwnOffersRequest(category, TimeoutMilliseconds: timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Requests the local user's full owned badge inventory from the server.
    /// </summary>
    /// <remarks>
    /// The inventory is read in pages of 500 from one consistent snapshot. The reply also updates
    /// the tracked badge state read by <see cref="BadgeInventory"/>.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the server to answer, from 1 to 120000.</param>
    /// <returns>Every owned badge.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the badge snapshot was invalid or incomplete.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no badge list arrived in time.</exception>
    public async Task<BadgeInventory> GetBadgeInventory(int timeoutMs = 10000)
    {
        BadgeRefreshResult refreshed = await _application
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
            page = await _application.InvokeAsync<OwnedBadgePageRequest, OwnedBadgePage>(
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
    /// Requests every achievement the account can earn, with the current level and progress of each.
    /// </summary>
    /// <remarks>
    /// The list is read in pages of 500 from one consistent snapshot. The reply also refreshes the
    /// tracked achievement state read by <see cref="Achievements"/>.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the server to answer, from 1 to 120000.</param>
    /// <returns>Every achievement and the default achievement category.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the achievement snapshot was invalid or incomplete.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no achievement list arrived in time.</exception>
    public async Task<Achievements> GetAchievements(int timeoutMs = 10000)
    {
        AchievementRefreshResult refreshed = await _application
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
            page = await _application.InvokeAsync<AchievementPageRequest, AchievementPage>(
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
            refreshed.FirstPage.SnapshotRevision != refreshed.SnapshotRevision ||
            refreshed.FirstPage.SessionGeneration != refreshed.SessionGeneration ||
            refreshed.FirstPage.StateRevision != refreshed.StateRevision ||
            refreshed.FirstPage.InventoryRevision != refreshed.InventoryRevision ||
            refreshed.FirstPage.BaselineRevision != refreshed.BaselineRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision ||
            page.Connected != refreshed.FirstPage.Connected ||
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
            refreshed.FirstPage.SnapshotRevision != refreshed.SnapshotRevision ||
            refreshed.FirstPage.SessionGeneration != refreshed.SessionGeneration ||
            refreshed.FirstPage.StateRevision != refreshed.StateRevision ||
            refreshed.FirstPage.ListRevision != refreshed.ListRevision ||
            refreshed.FirstPage.BaselineRevision != refreshed.BaselineRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision ||
            page.Connected != refreshed.FirstPage.Connected ||
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
    /// <remarks>
    /// The request is sent once without a retry. The reply is not blocked, so the game client also
    /// receives it.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The memberships, or an empty list when the user is in no group.</returns>
    /// <exception cref="InvalidDataException">Thrown when the membership pages were inconsistent.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no membership list arrived in time.</exception>
    public async Task<IReadOnlyList<GuildMembership>> GetGuildMemberships(int timeoutMs = 10000)
    {
        const int page_limit = 500;
        var memberships = new List<GuildMembership>();
        var membership_ids = new HashSet<Id>();
        int offset = 0;
        int? total_memberships = null;
        long? snapshot_revision = null;
        long? session_generation = null;

        while (true)
        {
            GroupMembershipsPage page = await _application
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
            }
            else if (page.TotalMemberships != total_memberships ||
                     page.SnapshotRevision != snapshot_revision ||
                     page.SessionGeneration != session_generation)
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
    /// Requests the navigator record for a room, with name, owner, description, tags, door mode,
    /// visitor counts, rating and group.
    /// </summary>
    /// <remarks>
    /// It works for any room, not only the current one. The request is sent once without a retry,
    /// and the reply is not blocked, so the game client also receives it.
    /// </remarks>
    /// <param name="roomId">The room id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The data of the room whose id matches <paramref name="roomId"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching room data arrived in time.</exception>
    public async Task<RoomData> GetRoomData(Id roomId, int timeoutMs = 10000)
    {
        RoomDataReadResult result = await _application
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
    /// Requests the list of users who hold rights in a room.
    /// </summary>
    /// <remarks>
    /// The server answers only for rooms the local user owns. The request is sent once without a
    /// retry, and the reply is not blocked, so the game client also receives it.
    /// </remarks>
    /// <param name="roomId">The room id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The rights holders as id and name pairs; the owner is not included.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">
    /// Thrown when no matching rights list arrived in time, which is also what happens when the local user
    /// does not own the room.
    /// </exception>
    public async Task<IReadOnlyList<IdName>> GetRightsFor(Id roomId, int timeoutMs = 10000)
    {
        RoomRightsReadResult result = await _application
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
    /// <remarks>Same as <see cref="GetRightsFor(Id, int)"/> for the current room.</remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The rights holders as id and name pairs; the owner is not included.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the user is not in a room.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching rights list arrived in time.</exception>
    public Task<IReadOnlyList<IdName>> GetRights(int timeoutMs = 10000)
    {
        if (!Room.IsInRoom)
            throw new InvalidOperationException("The user is not in a room.");
        return GetRightsFor(Room.RoomId, timeoutMs);
    }

    /// <summary>
    /// Saves the settings of a room and waits for the server to acknowledge them.
    /// </summary>
    /// <remarks>
    /// Read the current values with <see cref="GetRoomSettings"/>, change them and pass them back.
    /// <see cref="ModifyRoomSettings"/> does both and refuses to overwrite a change made in between.
    /// </remarks>
    /// <param name="settings">The complete room settings to save; its room id selects the room.</param>
    /// <param name="password">The room password, used when the door mode requires one; otherwise empty.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>A task that completes once the server has accepted the settings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="password"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="Qx.Game.Application.RoomSettingsRejectedException">Thrown when the server rejected the settings.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the server did not answer in time.</exception>
    public Task SaveRoomSettings(RoomSettings settings, string password = "", int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _application.InvokeAsync<RoomSettingsSaveRequest, RoomSettingsSaveReceipt>(
            ApplicationMemberIds.RoomSettingsSave,
            new RoomSettingsSaveRequest(ToApplicationRoomSettings(settings), password, timeoutMs),
            Ct).AsTask();
    }

    /// <summary>
    /// Reads a room's settings, applies a change to them and saves the result.
    /// </summary>
    /// <remarks>
    /// The current settings are requested first, then passed to <paramref name="update"/>. The
    /// save carries the revisions of that read, so it fails instead of overwriting when the
    /// settings, the room or the session changed in between. Each of the two steps gets its own
    /// <paramref name="timeoutMs"/>.
    /// </remarks>
    /// <param name="update">
    /// The function that receives the current settings and returns the settings to save. It must
    /// keep the room id.
    /// </param>
    /// <param name="roomId">The room to change, or <see langword="null"/> for the current room.</param>
    /// <param name="timeoutMs">The timeout in milliseconds for each step, from 1 to 120000.</param>
    /// <returns>The settings that were saved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="update"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room id was given and the user is not in a room, <paramref name="update"/> returned
    /// <see langword="null"/> or changed the room id, or the settings could not be loaded.
    /// </exception>
    /// <exception cref="Qx.Game.Application.RoomSettingsRejectedException">Thrown when the server refused to send or save the settings.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the settings or the save confirmation did not arrive in time.</exception>
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
        await _application.InvokeAsync<RoomSettingsSaveRequest, RoomSettingsSaveReceipt>(
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
    /// Requests the saved wardrobe outfits, each with its slot number, figure string and
    /// gender.
    /// </summary>
    /// <remarks>
    /// The wardrobe reply is blocked, so the game client does not see it. The outfits are read in
    /// pages of 500 from one consistent snapshot.
    /// </remarks>
    /// <param name="timeoutMs">The total timeout in milliseconds, from 1 to 120000, covering one automatic retry and every page.</param>
    /// <returns>The wardrobe state and every saved outfit.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the wardrobe changed while it was being read, or the result was incomplete.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no wardrobe arrived in time.</exception>
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

            ProfileWardrobePage page = await _application.InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
                ApplicationMemberIds.ProfileWardrobeGet,
                new ProfileWardrobeRequest(
                    offset,
                    500,
                    remaining,
                    first_page?.SnapshotRevision),
                Ct);
            first_page ??= page;
            if (page.Generation != first_page.Generation ||
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
    /// Requests a room's editable settings, with name, description, door mode, category, capacity,
    /// tags, trade mode, moderation permissions and the wall and floor appearance flags.
    /// </summary>
    /// <remarks>
    /// The server answers only for rooms the local user may edit. For other rooms it sends a
    /// settings error or nothing at all.
    /// </remarks>
    /// <param name="roomId">The room id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The room's current settings.</returns>
    /// <exception cref="Qx.Game.Application.RoomSettingsRejectedException">Thrown when the server answered with a settings error.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">
    /// Thrown when no matching settings arrived in time, which can also be the outcome when the room is not
    /// owned by the local user.
    /// </exception>
    public async Task<RoomSettings> GetRoomSettings(Id roomId, int timeoutMs = 10000) =>
        ToLegacyRoomSettings(await GetRoomSettingsState(roomId, timeoutMs));

    private ValueTask<RoomSettingsStateView> GetRoomSettingsState(Id room_id, int timeout_ms) =>
        _application.InvokeAsync<RoomSettingsGetRequest, RoomSettingsStateView>(
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
    /// Gets the catalog's page tree for one catalog mode, requesting it when no fresh copy is cached.
    /// </summary>
    /// <param name="catalogType">
    /// The catalog mode: <c>"NORMAL"</c> for the credit catalog, <c>"BUILDERS_CLUB"</c> for the
    /// builders club catalog. Case is ignored.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds for the server to answer, from 1 to 120000.</param>
    /// <returns>The catalog index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is outside its allowed range or the catalog mode is not known.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching index arrived in time.</exception>
    /// <remarks>
    /// Answered from the catalog cache when a copy younger than <paramref name="maxAge"/> is held,
    /// and the cache is cleared outright when the hotel announces a republish. Concurrent callers
    /// for the same catalog mode share one request, and a new index clears the cached pages of that
    /// mode.
    /// </remarks>
    /// <param name="maxAge">
    /// How old a cached copy may be, or <see langword="null"/> for
    /// <see cref="Qx.Game.CatalogManager.DefaultMaxAge"/> (5 minutes). <see cref="TimeSpan.Zero"/>
    /// always requests a new copy and <see cref="Timeout.InfiniteTimeSpan"/> accepts any cached copy.
    /// </param>
    public Task<CatalogIndex> GetCatalogIndex(
        string catalogType = "NORMAL",
        int timeoutMs = 10000,
        TimeSpan? maxAge = null) =>
        Game.Catalog.GetIndexAsync(catalogType, maxAge, timeoutMs, Ct);

    /// <summary>
    /// Gets one catalog page with its offers, requesting it when no fresh copy is cached.
    /// </summary>
    /// <param name="pageId">The catalog page id, taken from <see cref="GetCatalogIndex"/>.</param>
    /// <param name="offerId">
    /// An offer to preselect on the page, or -1 for none. It does not restrict the reply.
    /// </param>
    /// <param name="catalogType">The catalog mode; see <see cref="GetCatalogIndex"/>.</param>
    /// <param name="timeoutMs">The timeout in milliseconds for the server to answer, from 1 to 120000.</param>
    /// <returns>The catalog page with its offers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is outside its allowed range or the catalog mode is not known.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching page arrived in time.</exception>
    /// <remarks>
    /// The page supplies the page id and offer id pair needed by <see cref="BuyFromCatalog"/>.
    /// It is cached the same way as <see cref="GetCatalogIndex"/>, and concurrent callers for the
    /// same page share one request.
    /// </remarks>
    /// <param name="maxAge">
    /// How old a cached copy may be, or <see langword="null"/> for
    /// <see cref="Qx.Game.CatalogManager.DefaultMaxAge"/> (5 minutes).
    /// </param>
    public Task<CatalogPage> GetCatalogPage(
        int pageId,
        int offerId = -1,
        string catalogType = "NORMAL",
        int timeoutMs = 10000,
        TimeSpan? maxAge = null) =>
        Game.Catalog.GetPageAsync(pageId, catalogType, maxAge, offerId, timeoutMs, Ct);

    /// <summary>
    /// Searches the navigator for rooms owned by a user and keeps only exact owner name matches.
    /// </summary>
    /// <remarks>
    /// The filtering is needed because the server's <c>owner:</c> filter also returns near matches.
    /// </remarks>
    /// <param name="ownerName">The owner's user name; compared ignoring case.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The rooms whose owner name equals <paramref name="ownerName"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByOwner(string ownerName, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByOwner(ownerName, timeoutMs);
        return rooms.OwnedBy(ownerName).ToArray();
    }

    /// <summary>
    /// Searches the navigator by room name and keeps only rooms whose name actually contains
    /// the search text, ignoring case.
    /// </summary>
    /// <param name="roomName">The room name fragment to look for.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The rooms whose name contains <paramref name="roomName"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByName(string roomName, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByName(roomName, timeoutMs);
        return rooms.NameContains(roomName).ToArray();
    }

    /// <summary>
    /// Searches the navigator by tag and keeps only rooms that really carry that tag, ignoring case.
    /// </summary>
    /// <param name="tag">The tag to look for, without a leading <c>#</c>.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The rooms tagged with <paramref name="tag"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no search result arrived in time.</exception>
    public async Task<IReadOnlyList<RoomData>> SearchRoomsByTag(string tag, int timeoutMs = 10000)
    {
        RoomDataQuery rooms = await FindRoomsByTag(tag, timeoutMs);
        return rooms.TaggedAny(tag).ToArray();
    }

    /// <summary>
    /// Searches the navigator by group and keeps only rooms attached to a group whose name
    /// contains the search text, ignoring case.
    /// </summary>
    /// <param name="groupName">The group name fragment to look for.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>The rooms whose group name contains <paramref name="groupName"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no search result arrived in time.</exception>
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
    /// <param name="name">The exact user name; matched ignoring case.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>
    /// The first friend or other user whose name equals <paramref name="name"/>, or
    /// <see langword="null"/> when no result carries that exact name.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no search result arrived in time.</exception>
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
        TotalBadgesRank = profile.TotalBadgesRank
    };
}
