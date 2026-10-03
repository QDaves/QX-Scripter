using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    private ScriptQueries Queries => new(Game, _application);

    /// <summary>
    /// Creates a snapshot query over every avatar in the current room: users, bots and pets.
    /// </summary>
    /// <remarks>The avatars are copied, and the query is empty outside a room.</remarks>
    /// <returns>A new query over copies of the avatars.</returns>
    public AvatarQuery QueryAvatars() => Queries.Avatars;

    /// <summary>Wraps an avatar sequence in a query, copying the avatars immediately.</summary>
    /// <param name="avatars">The avatars to query.</param>
    /// <returns>A new query over copies of the avatars.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="avatars"/> is <see langword="null"/>.</exception>
    public AvatarQuery QueryAvatars(IEnumerable<Avatar> avatars) =>
        Queries.From(avatars);

    /// <summary>
    /// Creates a snapshot query over the floor items in the current room, with furni metadata
    /// attached so filters by name or class identifier work.
    /// </summary>
    /// <remarks>Metadata-based filters match nothing until the furni data has downloaded.</remarks>
    /// <returns>A new query over copies of the floor items.</returns>
    public FloorItemQuery QueryFloorItems() => Queries.FloorItems;

    /// <summary>
    /// Wraps a floor item sequence in a query, copying the items immediately and attaching furni
    /// metadata.
    /// </summary>
    /// <param name="items">The floor items to query.</param>
    /// <returns>A new query over copies of the items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    public FloorItemQuery QueryFloorItems(IEnumerable<FloorItem> items) =>
        Queries.From(items);

    /// <summary>
    /// Creates a snapshot query over the wall items in the current room, with furni metadata
    /// attached.
    /// </summary>
    /// <returns>A new query over copies of the wall items.</returns>
    public WallItemQuery QueryWallItems() => Queries.WallItems;

    /// <summary>
    /// Wraps a wall item sequence in a query, copying the items immediately and attaching furni
    /// metadata.
    /// </summary>
    /// <param name="items">The wall items to query.</param>
    /// <returns>A new query over copies of the items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    public WallItemQuery QueryWallItems(IEnumerable<WallItem> items) =>
        Queries.From(items);

    /// <summary>
    /// Creates a snapshot query over the furni inventory, with furni metadata attached.
    /// </summary>
    /// <remarks>
    /// Empty until the inventory has been loaded; call <see cref="EnsureInventoryLoaded"/> first.
    /// </remarks>
    /// <returns>A new query over the inventory items.</returns>
    public InventoryItemQuery QueryInventoryItems() => Queries.InventoryItems;

    /// <summary>
    /// Wraps an inventory item sequence in a query, capturing it immediately and attaching furni
    /// metadata.
    /// </summary>
    /// <param name="items">The inventory items to query.</param>
    /// <returns>A new query over the items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    public InventoryItemQuery QueryInventoryItems(IEnumerable<InventoryItem> items) =>
        Queries.From(items);

    /// <summary>
    /// Creates a snapshot query over the friend list.
    /// </summary>
    /// <remarks>
    /// Empty until the friend list has been loaded; call <see cref="EnsureFriendsLoaded"/> first.
    /// </remarks>
    /// <returns>A new query over the friends.</returns>
    public FriendQuery QueryFriends() => Queries.Friends;

    /// <summary>Wraps a friend sequence in a query, capturing it immediately.</summary>
    /// <param name="friends">The friends to query.</param>
    /// <returns>A new query over the friends.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="friends"/> is <see langword="null"/>.</exception>
    public FriendQuery QueryFriends(IEnumerable<Friend> friends) =>
        Queries.From(friends);

    /// <summary>
    /// Creates a query over the current room's navigator record, as a sequence of one or zero
    /// rooms.
    /// </summary>
    /// <remarks>It is empty outside a room and before the room data has arrived.</remarks>
    /// <returns>A new query over the current room data.</returns>
    public RoomDataQuery QueryCurrentRoom() => Queries.CurrentRoom;

    /// <summary>
    /// Wraps a room data sequence in a query, capturing it immediately.
    /// </summary>
    /// <remarks>
    /// Pairs with the rooms of a <see cref="SearchRooms"/> result and the lists returned by the
    /// <c>SearchRoomsBy...</c> helpers.
    /// </remarks>
    /// <param name="rooms">The rooms to query.</param>
    /// <returns>A new query over the rooms.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rooms"/> is <see langword="null"/>.</exception>
    public RoomDataQuery QueryRooms(IEnumerable<RoomData> rooms) =>
        Queries.From(rooms);

    /// <summary>
    /// Creates a snapshot query over the account's achievements.
    /// </summary>
    /// <remarks>
    /// Empty until the achievement list has been received; call <see cref="GetAchievements"/>
    /// first.
    /// </remarks>
    /// <returns>A new query over the achievements.</returns>
    public AchievementQuery QueryAchievements() => Queries.Achievements;

    /// <summary>Wraps an achievement sequence in a query, capturing it immediately.</summary>
    /// <param name="achievements">The achievements to query.</param>
    /// <returns>A new query over the achievements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="achievements"/> is <see langword="null"/>.</exception>
    public AchievementQuery QueryAchievements(IEnumerable<Achievement> achievements) =>
        Queries.From(achievements);
}
