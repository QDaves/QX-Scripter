using Qx.Game;
using Qx.Game.Protocol;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// The users who hold rights in the current room, as id/name pairs. Empty outside a room,
    /// and also empty until the server has sent the rights list - check
    /// <see cref="RoomManager.ControllersAreLoaded"/> to tell the two apart. The room owner is
    /// not listed.
    /// </summary>
    public IReadOnlyList<IdName> Controllers => Room.Controllers;

    /// <summary>
    /// Every achievement the server has reported for the local account, with its current level
    /// and progress. Snapshot per read. Empty until the achievement list has been received;
    /// call <see cref="GetAchievements"/> to force it, or check
    /// <see cref="IsAchievementsLoaded"/>.
    /// </summary>
    public IReadOnlyCollection<Achievement> Achievements => Game.Achievements.All;

    /// <summary>
    /// Subscribes to individual achievement progress updates. Fires once per achievement the
    /// server pushes, not for the bulk list that <see cref="GetAchievements"/> retrieves.
    /// </summary>
    /// <param name="handler">Receives the achievement in its new state.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAchievement(Action<Achievement> handler)
    {
        handler = Guarded(handler);
        Game.Achievements.Updated += handler;
        return Track(new Unsubscriber(() => Game.Achievements.Updated -= handler));
    }

    /// <summary>
    /// Subscribes to changes to the local user's own account data - figure, motto or name - as
    /// well as the initial login payload.
    /// </summary>
    /// <param name="handler">Receives the new <see cref="UserData"/>, which replaces the old one.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnProfileUpdated(Action<UserData> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<ProfileChanged>(
            ApplicationMemberIds.ProfileChanged,
            Guarded<ProfileChanged>(change =>
            {
                if (change.Kind is ProfileChangeKind.Identity &&
                    LegacyProfile(change.State.Identity) is { } profile)
                {
                    handler(profile);
                }
            })));
    }

    /// <summary>
    /// Whether the given user owns the current room or appears in its rights list.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when outside a room, and also while the room data and rights list
    /// have not arrived yet, so this is not a reliable negative right after entering a room.
    /// </returns>
    public bool HasRights(Id userId) =>
        Room.Data?.OwnerId == userId ||
        Room.Controllers.Any(controller => controller.Id == userId);

    /// <summary>Whether the given user owns the current room or holds rights in it.</summary>
    public bool HasRights(User user) => HasRights(user.Id);

    /// <summary>
    /// Subscribes to the point where the room session has opened. Room data is available at
    /// this point, but the avatar and furni lists may still be arriving; use
    /// <see cref="OnRoomReady"/> when the room contents must be complete.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnEnteredRoom(Action handler)
    {
        handler = Guarded(handler);
        Room.Entered += handler;
        return Track(new Unsubscriber(() => Room.Entered -= handler));
    }

    /// <summary>
    /// Subscribes to the start of a room entry, before any room contents have arrived.
    /// </summary>
    /// <param name="handler">Receives the id of the room being entered.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnEnteringRoom(Action<Id> handler)
    {
        handler = Guarded(handler);
        Room.Entering += handler;
        return Track(new Unsubscriber(() => Room.Entering -= handler));
    }

    /// <summary>
    /// Subscribes to the room session becoming fully ready: room data, avatars, furni, floor
    /// plan and heightmap have all been received. This is the right hook for scripts that read
    /// room contents on entry.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnRoomReady(Action handler)
    {
        handler = Guarded(handler);
        Room.Ready += handler;
        return Track(new Unsubscriber(() => Room.Ready -= handler));
    }

    /// <summary>
    /// Subscribes to the moment a room exit begins, while the room contents are still tracked.
    /// Read anything needed from the room here rather than in <see cref="OnLeftRoom"/>.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnLeavingRoom(Action handler)
    {
        handler = Guarded(handler);
        Room.Leaving += handler;
        return Track(new Unsubscriber(() => Room.Leaving -= handler));
    }

    /// <summary>
    /// Subscribes to the room session having ended. By this point the avatar and furni
    /// collections are already cleared.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnLeftRoom(Action handler)
    {
        handler = Guarded(handler);
        Room.Left += handler;
        return Track(new Unsubscriber(() => Room.Left -= handler));
    }

    /// <summary>
    /// Subscribes to room exits with the reason attached.
    /// </summary>
    /// <param name="handler">
    /// Receives the exit state: which room was left, whether it had been fully entered, the
    /// native exit reason and the kick that caused it, if any.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnRoomExited(Action<RoomExitState> handler)
    {
        handler = Guarded(handler);
        Room.Exited += handler;
        return Track(new Unsubscriber(() => Room.Exited -= handler));
    }

    /// <summary>
    /// Subscribes to updates of the current room's navigator record: name, description, tags,
    /// door mode, rating and group. Fires on entry and whenever the server re-sends it, for
    /// example after the settings are saved.
    /// </summary>
    /// <param name="handler">Receives the new room data.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnRoomDataUpdated(Action<RoomData> handler)
    {
        handler = Guarded(handler);
        Room.RoomDataUpdated += handler;
        return Track(new Unsubscriber(() => Room.RoomDataUpdated -= handler));
    }

    /// <summary>
    /// Subscribes to the bulk floor-item list having been received. After this fires
    /// <see cref="FloorItems"/> is complete for the current room.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemsLoaded(Action handler)
    {
        handler = Guarded(handler);
        Room.FloorItemsLoaded += handler;
        return Track(new Unsubscriber(() => Room.FloorItemsLoaded -= handler));
    }

    /// <summary>
    /// Subscribes to the bulk wall-item list having been received. After this fires
    /// <see cref="WallItems"/> is complete for the current room.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWallItemsLoaded(Action handler)
    {
        handler = Guarded(handler);
        Room.WallItemsLoaded += handler;
        return Track(new Unsubscriber(() => Room.WallItemsLoaded -= handler));
    }

    /// <summary>
    /// Subscribes to avatars appearing in the room, in batches as the server sends them. The
    /// initial room population arrives as one large batch.
    /// </summary>
    /// <param name="handler">Receives the avatars added by this packet, users, bots and pets alike.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarsAdded(Action<IReadOnlyList<Avatar>> handler)
    {
        handler = Guarded(handler);
        Room.AvatarsAdded += handler;
        return Track(new Unsubscriber(() => Room.AvatarsAdded -= handler));
    }

    /// <summary>
    /// Subscribes to avatars appearing in the room, one callback per avatar. Convenience
    /// wrapper over <see cref="OnAvatarsAdded"/>; the initial room population therefore fires
    /// the handler once for every avatar already present.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarAdded(Action<Avatar> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        void Wrapper(IReadOnlyList<Avatar> avatars)
        {
            foreach (Avatar avatar in avatars)
                handler(avatar);
        }
        Action<IReadOnlyList<Avatar>> guarded = Guarded<IReadOnlyList<Avatar>>(Wrapper);
        Room.AvatarsAdded += guarded;
        return Track(new Unsubscriber(() => Room.AvatarsAdded -= guarded));
    }

    /// <summary>
    /// Subscribes to avatars leaving the room.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar as it last was; it has already been removed from
    /// <see cref="Avatars"/> when the handler runs.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarRemoved(Action<Avatar> handler)
    {
        handler = Guarded(handler);
        Room.AvatarRemoved += handler;
        return Track(new Unsubscriber(() => Room.AvatarRemoved -= handler));
    }

    /// <summary>
    /// Subscribes to any avatar status update - movement, posture, dance, effect, hand item,
    /// idle or typing. The specific <c>OnAvatar...Changed</c> events fire alongside this one and
    /// carry the previous value, which this one does not.
    /// </summary>
    /// <param name="handler">Receives the avatar in its already-updated state.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarUpdated(Action<Avatar> handler)
    {
        handler = Guarded(handler);
        Room.AvatarUpdated += handler;
        return Track(new Unsubscriber(() => Room.AvatarUpdated -= handler));
    }

    /// <summary>
    /// Subscribes to avatars changing tile.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, the tile it was on before, and the tile it is on now, in that order.
    /// The avatar's own properties already hold the new location.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarMoved(Action<Avatar, Tile, Tile> handler)
    {
        handler = Guarded(handler);
        Room.AvatarMoved += handler;
        return Track(new Unsubscriber(() => Room.AvatarMoved -= handler));
    }

    /// <summary>
    /// Subscribes to avatars starting or stopping a dance.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, the previous dance style and the new one, in that order. Style 0
    /// means not dancing.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarDanceChanged(Action<Avatar, int, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarDanceChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarDanceChanged -= handler));
    }

    /// <summary>
    /// Subscribes to avatars gaining, changing or losing an avatar effect.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, the previous effect id and the new one, in that order. Effect 0
    /// means no effect; use <see cref="EffectName"/> to resolve an id to its display name.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarEffectChanged(Action<Avatar, int, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarEffectChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarEffectChanged -= handler));
    }

    /// <summary>
    /// Subscribes to avatars picking up or putting down a hand item (drinks, food, and so on).
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, the previous hand-item id and the new one, in that order. Id 0
    /// means empty-handed; use <see cref="HandItemName"/> to resolve an id.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarHandItemChanged(Action<Avatar, int, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarHandItemChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarHandItemChanged -= handler));
    }

    /// <summary>
    /// Subscribes to avatars falling asleep or waking up.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, the previous idle flag and the new one, in that order.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarIdleChanged(Action<Avatar, bool, bool> handler)
    {
        handler = Guarded(handler);
        Room.AvatarIdleChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarIdleChanged -= handler));
    }

    /// <summary>
    /// Subscribes to the typing indicator above an avatar appearing or disappearing.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, the previous typing flag and the new one, in that order.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarTypingChanged(Action<Avatar, bool, bool> handler)
    {
        handler = Guarded(handler);
        Room.AvatarTypingChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarTypingChanged -= handler));
    }

    /// <summary>
    /// Subscribes to an avatar changing its look or motto while in the room.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar, then the previous figure string, the new figure string, the previous
    /// motto and the new motto, in that order.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAvatarIdentityChanged(Action<Avatar, string, string, string, string> handler)
    {
        handler = Guarded(handler);
        Room.AvatarIdentityChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarIdentityChanged -= handler));
    }

    /// <summary>
    /// Subscribes to avatar actions (expressions) played in the room.
    /// </summary>
    /// <param name="handler">
    /// Receives the avatar and the action id: 1 wave, 2 blow a kiss, 3 laugh, 4 cry, 5 idle,
    /// 6 jump, 7 thumbs up; 0 clears the current action.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnAction(Action<Avatar, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarActioned += handler;
        return Track(new Unsubscriber(() => Room.AvatarActioned -= handler));
    }

    /// <summary>
    /// Subscribes to a floor item appearing in the room, whether it was just placed, dropped
    /// out of a box, or arrived in the initial object list.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemAdded(Action<FloorItem> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemAdded += handler;
        return Track(new Unsubscriber(() => Room.FloorItemAdded -= handler));
    }

    /// <summary>
    /// Subscribes to floor items being removed from the room.
    /// </summary>
    /// <param name="handler">
    /// Receives only the removed item's id. Use <see cref="OnFloorItemRemovedDetailed"/> when
    /// the item's kind or position is needed.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemRemoved(Action<Id> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemRemoved += handler;
        return Track(new Unsubscriber(() => Room.FloorItemRemoved -= handler));
    }

    /// <summary>
    /// Subscribes to floor items being updated in place: a new location, rotation, owner or item
    /// data.
    /// </summary>
    /// <param name="handler">Receives the item in its already-updated state.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemUpdated(Action<FloorItem> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemUpdated += handler;
        return Track(new Unsubscriber(() => Room.FloorItemUpdated -= handler));
    }

    /// <summary>
    /// Subscribes to floor items changing tile, including furni pushed around by wired or by a
    /// roller.
    /// </summary>
    /// <param name="handler">
    /// Receives the item, its previous location and its new location, in that order.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemMoved(Action<FloorItem, Tile, Tile> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemMoved += handler;
        return Track(new Unsubscriber(() => Room.FloorItemMoved -= handler));
    }

    /// <summary>
    /// Subscribes to a floor item's state data changing: the dice value, a gate opening, a
    /// sign's text, and so on. This is the hook for reading furni state changes.
    /// </summary>
    /// <param name="handler">
    /// Receives the item, its previous item data and its new item data, in that order.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemDataChanged(Action<FloorItem, ItemData, ItemData> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemDataChanged += handler;
        return Track(new Unsubscriber(() => Room.FloorItemDataChanged -= handler));
    }

    /// <summary>
    /// Subscribes to floor items being removed, receiving the full item as it last was rather
    /// than just its id.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFloorItemRemovedDetailed(Action<FloorItem> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemRemovedDetailed += handler;
        return Track(new Unsubscriber(() => Room.FloorItemRemovedDetailed -= handler));
    }

    /// <summary>
    /// Subscribes to a wall item appearing in the room, whether newly placed or part of the
    /// initial object list.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWallItemAdded(Action<WallItem> handler)
    {
        handler = Guarded(handler);
        Room.WallItemAdded += handler;
        return Track(new Unsubscriber(() => Room.WallItemAdded -= handler));
    }

    /// <summary>
    /// Subscribes to wall items being removed from the room.
    /// </summary>
    /// <param name="handler">
    /// Receives only the removed item's id. Use <see cref="OnWallItemRemovedDetailed"/> when the
    /// item itself is needed.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWallItemRemoved(Action<Id> handler)
    {
        handler = Guarded(handler);
        Room.WallItemRemoved += handler;
        return Track(new Unsubscriber(() => Room.WallItemRemoved -= handler));
    }

    /// <summary>
    /// Subscribes to wall items being updated in place, for example a sticky note's text or a
    /// change of owner.
    /// </summary>
    /// <param name="handler">Receives the item in its already-updated state.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWallItemUpdated(Action<WallItem> handler)
    {
        handler = Guarded(handler);
        Room.WallItemUpdated += handler;
        return Track(new Unsubscriber(() => Room.WallItemUpdated -= handler));
    }

    /// <summary>
    /// Subscribes to wall items being moved along the wall.
    /// </summary>
    /// <param name="handler">
    /// Receives the item, its previous wall location and its new wall location, in that order.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWallItemMoved(Action<WallItem, WallLocation, WallLocation> handler)
    {
        handler = Guarded(handler);
        Room.WallItemMoved += handler;
        return Track(new Unsubscriber(() => Room.WallItemMoved -= handler));
    }

    /// <summary>
    /// Subscribes to wall items being removed, receiving the full item as it last was rather
    /// than just its id.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWallItemRemovedDetailed(Action<WallItem> handler)
    {
        handler = Guarded(handler);
        Room.WallItemRemovedDetailed += handler;
        return Track(new Unsubscriber(() => Room.WallItemRemovedDetailed -= handler));
    }

    /// <summary>
    /// Subscribes to the furni inventory finishing a full load. The inventory arrives in
    /// fragments; this fires once, after the last one, when
    /// <see cref="InventoryItems"/> is complete.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryLoaded(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change.Kind is InventoryChangeKind.Loaded)
                    handler();
            })));
    }

    /// <summary>
    /// Subscribes to the server declaring the cached furni inventory out of date. Nothing is
    /// re-fetched automatically; call <see cref="EnsureInventoryLoaded"/> to reload it.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryInvalidated(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change.Kind is InventoryChangeKind.Invalidated)
                    handler();
            })));
    }

    /// <summary>
    /// Subscribes to single items appearing in the furni inventory, for example after a
    /// purchase, a completed trade or a pickup. Also fires for items arriving in the bulk load.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryItemAdded(Action<InventoryItem> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Added, Item: { } item })
                    handler(LegacyInventoryItem(item));
            })));
    }

    /// <summary>
    /// Subscribes to an inventory item being replaced by a newer version of itself, for example
    /// when its extra data changes.
    /// </summary>
    /// <param name="handler">Receives the item in its new state.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryItemUpdated(Action<InventoryItem> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Updated, Item: { } item })
                    handler(LegacyInventoryItem(item));
            })));
    }

    /// <summary>
    /// Subscribes to items leaving the furni inventory, for example when placed in a room,
    /// traded away or sold.
    /// </summary>
    /// <param name="handler">Receives the item as it last was.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryItemRemoved(Action<InventoryItem> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Removed, Item: { } item })
                    handler(LegacyInventoryItem(item));
            })));
    }

    /// <summary>
    /// Subscribes to the pet inventory finishing a full load, after which
    /// <see cref="InventoryPets"/> is complete.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnPetInventoryLoaded(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change.Kind is InventoryChangeKind.Loaded)
                    handler();
            })));
    }

    /// <summary>
    /// Subscribes to pets appearing in the pet inventory.
    /// </summary>
    /// <param name="handler">
    /// Receives the pet and a flag that is <see langword="true"/> when the server marked this as
    /// a newly acquired pet rather than one arriving in the bulk load.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryPetAdded(Action<InventoryPet, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Added, Pet: { } pet })
                    handler(LegacyInventoryPet(pet), change.OpenInventory == true);
            })));
    }

    /// <summary>
    /// Subscribes to a pet in the inventory being replaced by a newer version of itself.
    /// </summary>
    /// <param name="handler">Receives the pet in its new state.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryPetUpdated(Action<InventoryPet> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Updated, Pet: { } pet })
                    handler(LegacyInventoryPet(pet));
            })));
    }

    /// <summary>
    /// Subscribes to pets leaving the inventory, for example when placed in a room or given
    /// away.
    /// </summary>
    /// <param name="handler">Receives the pet as it last was.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnInventoryPetRemoved(Action<InventoryPet> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Removed, Pet: { } pet })
                    handler(LegacyInventoryPet(pet));
            })));
    }

    /// <summary>
    /// Subscribes to the server announcing that it is closing the connection.
    /// </summary>
    /// <param name="handler">
    /// Receives the raw disconnect reason code the server sent. The connection is torn down
    /// immediately afterwards, so this handler is the last chance to react.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnDisconnected(Action<int> handler) =>
        OnIn(MessageContracts.Session.DisconnectReason, message => handler(message.Reason));

    /// <summary>
    /// Subscribes to incoming friend requests.
    /// </summary>
    /// <param name="handler">
    /// Receives the request, which carries the requester's user id, name and figure.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFriendRequest(Action<NewFriendRequest> handler) =>
        Track(Application.Subscribe(
            ApplicationMemberIds.FriendRequestReceived,
            Guarded(handler)));

    /// <summary>
    /// Automatically accepts every friend request that arrives from now on. Requests already
    /// pending before the call are not touched.
    /// </summary>
    /// <returns>
    /// A handle that stops the auto-accepting when disposed; also disposed when the script
    /// stops, so this only lasts for the run.
    /// </returns>
    public IDisposable AcceptAllFriendRequests() =>
        OnFriendRequest(request => AcceptFriendRequest(request.RequesterUserId));

    public IDisposable OnTradeChanged(Action<TradeChanged> handler) =>
        Track(Application.Subscribe(
            ApplicationMemberIds.TradeChanged,
            Guarded(handler)));

    public IDisposable OnTradeOpenFailed(Action<TradeOpenFailure> handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.OpenFailed &&
                change.OpenFailure is { } failure)
            {
                handler(failure);
            }
        });

    /// <summary>
    /// Subscribes to a trade window opening. Read <see cref="Trade"/> for the participants and
    /// their permissions.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnTradeOpened(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.Opened)
                handler();
        });

    public IDisposable OnTradeUpdated(Action<TradeEpochSummary> handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.OffersUpdated &&
                change.State.Active is { } active)
            {
                handler(active);
            }
        });

    /// <summary>
    /// Subscribes to a participant accepting or un-accepting the trade.
    /// </summary>
    /// <param name="handler">
    /// Receives the user id of the participant and their new acceptance state:
    /// <see langword="true"/> accepted, <see langword="false"/> withdrawn.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnTradeAccepted(Action<Id, bool> handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.AcceptanceUpdated &&
                change.Acceptance is { } acceptance)
            {
                handler(acceptance.UserId, acceptance.Accepted);
            }
        });

    /// <summary>
    /// Subscribes to the trade entering the final confirmation phase, where both sides must
    /// call <see cref="ConfirmTrade"/>. Offers can no longer be changed after this point.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnTradeConfirmed(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.Confirmation)
                handler();
        });

    /// <summary>
    /// Subscribes to a trade completing successfully, with the items having changed hands. The
    /// inventory is invalidated at the same time and has to be reloaded to see them.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnTradeCompleted(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.Completed)
                handler();
        });

    /// <summary>
    /// Subscribes to the trade window closing, whether it completed, was cancelled by either
    /// side, or collapsed because a participant left the room.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnTradeClosed(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.PreviousEpoch is not null && change.State.Active is null)
                handler();
        });
}
