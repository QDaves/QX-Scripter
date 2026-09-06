using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// The query factory bound to the current game state. A fresh instance is created on every
    /// read, and each query it hands out snapshots the state at that moment.
    /// </summary>
    public ScriptQueries Queries => new(Game, Application);

    /// <summary>
    /// A snapshot query over every avatar in the current room - users, bots and pets. Empty
    /// outside a room.
    /// </summary>
    public AvatarQuery QueryAvatars() => Queries.Avatars;

    /// <summary>Wraps an arbitrary avatar sequence in a query, snapshotting it immediately.</summary>
    public AvatarQuery QueryAvatars(IEnumerable<Avatar> avatars) =>
        Queries.From(avatars);

    /// <summary>
    /// A snapshot query over the floor items in the current room, enriched with furni metadata
    /// so filters by name or class identifier work. Metadata-based filters match nothing until
    /// the furni data has downloaded.
    /// </summary>
    public FloorItemQuery QueryFloorItems() => Queries.FloorItems;

    /// <summary>
    /// Wraps an arbitrary floor-item sequence in a query, snapshotting it immediately and
    /// attaching furni metadata.
    /// </summary>
    public FloorItemQuery QueryFloorItems(IEnumerable<FloorItem> items) =>
        Queries.From(items);

    /// <summary>
    /// A snapshot query over the wall items in the current room, enriched with furni metadata.
    /// </summary>
    public WallItemQuery QueryWallItems() => Queries.WallItems;

    /// <summary>
    /// Wraps an arbitrary wall-item sequence in a query, snapshotting it immediately and
    /// attaching furni metadata.
    /// </summary>
    public WallItemQuery QueryWallItems(IEnumerable<WallItem> items) =>
        Queries.From(items);

    /// <summary>
    /// A snapshot query over the furni inventory, enriched with furni metadata. Empty until the
    /// inventory has been loaded; call <see cref="EnsureInventoryLoaded"/> first.
    /// </summary>
    public InventoryItemQuery QueryInventoryItems() => Queries.InventoryItems;

    /// <summary>
    /// Wraps an arbitrary inventory-item sequence in a query, snapshotting it immediately and
    /// attaching furni metadata.
    /// </summary>
    public InventoryItemQuery QueryInventoryItems(IEnumerable<InventoryItem> items) =>
        Queries.From(items);

    /// <summary>
    /// A snapshot query over the friend list. Empty until the friend list has been loaded; call
    /// <see cref="EnsureFriendsLoaded"/> first.
    /// </summary>
    public FriendQuery QueryFriends() => Queries.Friends;

    /// <summary>Wraps an arbitrary friend sequence in a query, snapshotting it immediately.</summary>
    public FriendQuery QueryFriends(IEnumerable<Friend> friends) =>
        Queries.From(friends);

    /// <summary>
    /// A query over the current room's navigator record, as a sequence of either one or zero
    /// rooms. It is empty outside a room and before the room data has arrived.
    /// </summary>
    public RoomDataQuery QueryCurrentRoom() => Queries.CurrentRoom;

    /// <summary>
    /// Wraps a room-data sequence in a query, snapshotting it immediately. Pairs with the
    /// results of <see cref="SearchRooms"/> and the <c>SearchRoomsBy...</c> helpers.
    /// </summary>
    public RoomDataQuery QueryRooms(IEnumerable<RoomData> rooms) =>
        Queries.From(rooms);

    /// <summary>
    /// A snapshot query over the account's achievements. Empty until the achievement list has
    /// been received; call <see cref="GetAchievements"/> first.
    /// </summary>
    public AchievementQuery QueryAchievements() => Queries.Achievements;

    /// <summary>Wraps an arbitrary achievement sequence in a query, snapshotting it immediately.</summary>
    public AchievementQuery QueryAchievements(IEnumerable<Achievement> achievements) =>
        Queries.From(achievements);
}
