using Qx.Game;
using Qx.Game.Protocol;
using Qx.Game.Application;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets every achievement the server has reported for the local account, with its current
    /// level and progress.
    /// </summary>
    /// <remarks>
    /// Every read returns a new snapshot. The collection is empty until the achievement list has
    /// been received; call <see cref="GetAchievements"/> to request it, or check
    /// <see cref="IsAchievementsLoaded"/>.
    /// </remarks>
    public IReadOnlyCollection<Achievement> Achievements => Game.Achievements.All;

    /// <summary>
    /// Registers a handler that runs when the server pushes a progress update for a single
    /// achievement.
    /// </summary>
    /// <remarks>
    /// It runs once per pushed achievement, not for the bulk list that
    /// <see cref="GetAchievements"/> retrieves.
    /// </remarks>
    /// <param name="handler">The handler to call with the achievement in its new state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAchievement(Action<Achievement> handler)
    {
        handler = Guarded(handler);
        Game.Achievements.Updated += handler;
        return Track(new Unsubscriber(() => Game.Achievements.Updated -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the local user's account data changes.
    /// </summary>
    /// <remarks>
    /// It runs for the initial user data sent after login and for later changes such as figure,
    /// motto or name.
    /// </remarks>
    /// <param name="handler">The handler to call with the new <see cref="UserData"/>, which replaces the old one.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnProfileUpdated(Action<UserData> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<ProfileChanged>(
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
    /// Gets whether the given user owns the current room or appears in its rights list.
    /// </summary>
    /// <param name="userId">The user's account id.</param>
    /// <returns>
    /// <see langword="true"/> if the user owns the room or holds rights in it; otherwise,
    /// <see langword="false"/>. The result is also <see langword="false"/> outside a room and
    /// while the room data and rights list have not arrived yet, so it is not a reliable negative
    /// right after entering a room.
    /// </returns>
    public bool HasRights(Id userId) =>
        Room.Data?.OwnerId == userId ||
        Room.Controllers.Any(controller => controller.Id == userId);

    /// <summary>Gets whether the given user owns the current room or holds rights in it.</summary>
    /// <param name="user">The user to check; only its id is used.</param>
    /// <returns>
    /// <see langword="true"/> if the user owns the room or holds rights in it; otherwise,
    /// <see langword="false"/>, with the same caveats as <see cref="HasRights(Id)"/>.
    /// </returns>
    public bool HasRights(User user) => HasRights(user.Id);

    /// <summary>
    /// Registers a handler that runs when the server confirms that the local user has entered a
    /// room.
    /// </summary>
    /// <remarks>
    /// It runs once per room, when the room entry info message arrives. The
    /// avatar and furni lists may still be arriving at this point; use
    /// <see cref="OnFloorItemsLoaded"/>, <see cref="OnWallItemsLoaded"/> or the loaded flags on
    /// <see cref="Room"/> when the room contents must be complete.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnEnteredRoom(Action handler)
    {
        handler = Guarded(handler);
        Room.Entered += handler;
        return Track(new Unsubscriber(() => Room.Entered -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a room entry starts, before any room contents have
    /// arrived.
    /// </summary>
    /// <param name="handler">The handler to call with the id of the room being entered.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnEnteringRoom(Action<Id> handler)
    {
        handler = Guarded(handler);
        Room.Entering += handler;
        return Track(new Unsubscriber(() => Room.Entering -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a room starts loading, on the room ready message that
    /// carries the room model and id.
    /// </summary>
    /// <remarks>
    /// The message starts a new room session and sets <see cref="RoomManager.RoomType"/>, but the
    /// room is not ready yet: <see cref="IsRoomReady"/> is normally still <see langword="false"/>
    /// and turns <see langword="true"/> with <see cref="OnEnteredRoom"/>. The avatar and furni
    /// lists, floor plan and heightmap are sent after it and may still be arriving when the
    /// handler runs; use <see cref="WaitRoomReady"/>, <see cref="OnFloorItemsLoaded"/>,
    /// <see cref="OnWallItemsLoaded"/> or the loaded flags on <see cref="Room"/> when the room
    /// contents must be complete.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnRoomLoading(Action handler)
    {
        handler = Guarded(handler);
        Room.Ready += handler;
        return Track(new Unsubscriber(() => Room.Ready -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the local user leaves a room, before the handlers
    /// registered with <see cref="OnLeftRoom(Action{RoomExitState})"/> and
    /// <see cref="OnLeftRoom(Action)"/>.
    /// </summary>
    /// <remarks>
    /// The handler runs once the exit has been applied, so the avatar and furni collections are
    /// already cleared, and on a direct move to another room <see cref="RoomId"/> already holds
    /// the new room. Use <see cref="OnLeftRoom(Action{RoomExitState})"/> to learn which room was
    /// left.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnLeavingRoom(Action handler)
    {
        handler = Guarded(handler);
        Room.Leaving += handler;
        return Track(new Unsubscriber(() => Room.Leaving -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the room session has ended.
    /// </summary>
    /// <remarks>
    /// By this point the avatar and furni collections are already cleared. It runs right after
    /// the handlers registered with <see cref="OnLeftRoom(Action{RoomExitState})"/>, which also
    /// receive which room was left and why.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnLeftRoom(Action handler)
    {
        handler = Guarded(handler);
        Room.Left += handler;
        return Track(new Unsubscriber(() => Room.Left -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the room session has ended, with how it ended.
    /// </summary>
    /// <remarks>
    /// By this point the avatar and furni collections are already cleared, and on a direct move to
    /// another room <see cref="RoomId"/> already holds the new room; the exit state still names
    /// the room that was left. It runs right before the handlers registered with
    /// <see cref="OnLeftRoom(Action)"/>.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the exit state: which room was left, whether it had been fully
    /// entered, the native exit reason and the kick that caused it, if any.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnLeftRoom(Action<RoomExitState> handler)
    {
        handler = Guarded(handler);
        Room.Exited += handler;
        return Track(new Unsubscriber(() => Room.Exited -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the server reports that the local user was kicked from
    /// the room.
    /// </summary>
    /// <remarks>
    /// It runs as soon as the kick arrives, before the room exit. The exit that follows reaches
    /// <see cref="OnLeftRoom(Action{RoomExitState})"/> with the same kick attached.
    /// </remarks>
    /// <param name="handler">The handler to call with the kick.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnKicked(Action<RoomKick> handler)
    {
        handler = Guarded(handler);
        Room.Kicked += handler;
        return Track(new Unsubscriber(() => Room.Kicked -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the current room's navigator record is updated.
    /// </summary>
    /// <remarks>
    /// The record holds the name, description, tags, door mode, rating and group. It arrives on
    /// entry and whenever the server sends it again, for example after the settings are saved.
    /// </remarks>
    /// <param name="handler">The handler to call with the new room data.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnRoomDataUpdated(Action<RoomData> handler)
    {
        handler = Guarded(handler);
        Room.RoomDataUpdated += handler;
        return Track(new Unsubscriber(() => Room.RoomDataUpdated -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a batch of floor items for the current room has been
    /// received.
    /// </summary>
    /// <remarks>
    /// The first batch carries the room's floor items on entry, after which
    /// <see cref="FloorItems"/> is complete. The hotel can send further batches later, for
    /// example for temporary furni, and the handler runs again for each of them.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFloorItemsLoaded(Action handler)
    {
        handler = Guarded(handler);
        Room.FloorItemsLoaded += handler;
        return Track(new Unsubscriber(() => Room.FloorItemsLoaded -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a batch of wall items for the current room has been
    /// received.
    /// </summary>
    /// <remarks>
    /// The first batch carries the room's wall items on entry, after which
    /// <see cref="WallItems"/> is complete. The handler runs again for any later batch.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWallItemsLoaded(Action handler)
    {
        handler = Guarded(handler);
        Room.WallItemsLoaded += handler;
        return Track(new Unsubscriber(() => Room.WallItemsLoaded -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when avatars appear in the room, once per batch the server
    /// sends.
    /// </summary>
    /// <remarks>
    /// The initial room population arrives as one large batch.
    /// </remarks>
    /// <param name="handler">The handler to call with the avatars added by this packet, users, bots and pets alike.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarsAdded(Action<IReadOnlyList<Avatar>> handler)
    {
        handler = Guarded(handler);
        Room.AvatarsAdded += handler;
        return Track(new Unsubscriber(() => Room.AvatarsAdded -= handler));
    }

    /// <summary>
    /// Registers a handler that runs once for every avatar that appears in the room.
    /// </summary>
    /// <remarks>
    /// It wraps <see cref="OnAvatarsAdded"/>, so the initial room population calls the handler
    /// once for every avatar already present.
    /// </remarks>
    /// <param name="handler">The handler to call with each added avatar.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
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
    /// Registers a handler that runs when an avatar leaves the room.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar as it last was. It has already been removed from
    /// <see cref="Avatars"/> when the handler runs.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarRemoved(Action<Avatar> handler)
    {
        handler = Guarded(handler);
        Room.AvatarRemoved += handler;
        return Track(new Unsubscriber(() => Room.AvatarRemoved -= handler));
    }

    /// <summary>
    /// Registers a handler that runs on any avatar status or appearance update.
    /// </summary>
    /// <remarks>
    /// It covers movement, posture, dance, effect, hand item, idle, typing, look, name and group
    /// updates. The specific <c>OnAvatar...Changed</c> handlers run alongside it and receive the
    /// previous value, which this handler does not.
    /// </remarks>
    /// <param name="handler">The handler to call with the avatar in its already updated state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarUpdated(Action<Avatar> handler)
    {
        handler = Guarded(handler);
        Room.AvatarUpdated += handler;
        return Track(new Unsubscriber(() => Room.AvatarUpdated -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar changes tile, by walking, a roller or wired.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, the tile it was on before and the tile it is on now,
    /// in that order. The avatar's own properties already hold the new location.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarMoved(Action<Avatar, Tile, Tile> handler)
    {
        handler = Guarded(handler);
        Room.AvatarMoved += handler;
        return Track(new Unsubscriber(() => Room.AvatarMoved -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar starts, changes or stops a dance.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, the previous dance style and the new one, in that
    /// order. Style 0 means not dancing.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarDanceChanged(Action<Avatar, int, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarDanceChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarDanceChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar gains, changes or loses an avatar effect.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, the previous effect id and the new one, in that
    /// order. Effect 0 means no effect; use <see cref="EffectName"/> to resolve an id to its
    /// display name.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarEffectChanged(Action<Avatar, int, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarEffectChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarEffectChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar picks up or puts down a hand item, such as a
    /// drink or food.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, the previous hand item id and the new one, in that
    /// order. Id 0 means empty handed; use <see cref="HandItemName"/> to resolve an id.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarHandItemChanged(Action<Avatar, int, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarHandItemChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarHandItemChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar falls asleep or wakes up.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, the previous idle flag and the new one, in that order.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarIdleChanged(Action<Avatar, bool, bool> handler)
    {
        handler = Guarded(handler);
        Room.AvatarIdleChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarIdleChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the typing indicator above an avatar appears or
    /// disappears.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, the previous typing flag and the new one, in that
    /// order.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarTypingChanged(Action<Avatar, bool, bool> handler)
    {
        handler = Guarded(handler);
        Room.AvatarTypingChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarTypingChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar in the room changes its look or motto.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, then the previous figure string, the new figure
    /// string, the previous motto and the new motto, in that order. For pets only the figure
    /// changes, so both motto arguments hold the current motto.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarIdentityChanged(Action<Avatar, string, string, string, string> handler)
    {
        handler = Guarded(handler);
        Room.AvatarIdentityChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarIdentityChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar in the room is renamed.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar, its previous name and its new name, in that order.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarNameChanged(Action<Avatar, string, string> handler)
    {
        handler = Guarded(handler);
        Room.AvatarNameChanged += handler;
        return Track(new Unsubscriber(() => Room.AvatarNameChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when an avatar plays an expression in the room, such as a
    /// wave.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the avatar and the expression id: 1 wave, 2 blow a kiss, 3 laugh,
    /// 4 cry, 5 go idle, 6 jump, 7 thumbs up; 0 clears the current expression. These are the ids
    /// <see cref="Expression"/> plays.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnAvatarExpression(Action<Avatar, int> handler)
    {
        handler = Guarded(handler);
        Room.AvatarActioned += handler;
        return Track(new Unsubscriber(() => Room.AvatarActioned -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a single floor item appears in the room, for example
    /// when it is placed.
    /// </summary>
    /// <remarks>
    /// Items that arrive in a batch, such as the room's initial object list, do not trigger it;
    /// use <see cref="OnFloorItemsLoaded"/> for those.
    /// </remarks>
    /// <param name="handler">The handler to call with the new item.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFloorItemAdded(Action<FloorItem> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemAdded += handler;
        return Track(new Unsubscriber(() => Room.FloorItemAdded -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a floor item is removed from the room.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the item as it last was. It has already been removed from
    /// <see cref="FloorItems"/> when the handler runs.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFloorItemRemoved(Action<FloorItem> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemRemovedDetailed += handler;
        return Track(new Unsubscriber(() => Room.FloorItemRemovedDetailed -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a floor item is updated in place, such as a new
    /// location, rotation, owner or item data.
    /// </summary>
    /// <param name="handler">The handler to call with the item in its already updated state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFloorItemUpdated(Action<FloorItem> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemUpdated += handler;
        return Track(new Unsubscriber(() => Room.FloorItemUpdated -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a floor item changes tile, including furni pushed by
    /// wired or by a roller.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the item, its previous location and its new location, in that
    /// order.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFloorItemMoved(Action<FloorItem, Tile, Tile> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemMoved += handler;
        return Track(new Unsubscriber(() => Room.FloorItemMoved -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a floor item's state data changes, such as a dice
    /// value, a gate opening or a sign's text.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the item, its previous item data and its new item data, in that
    /// order.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFloorItemDataChanged(Action<FloorItem, ItemData, ItemData> handler)
    {
        handler = Guarded(handler);
        Room.FloorItemDataChanged += handler;
        return Track(new Unsubscriber(() => Room.FloorItemDataChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a single wall item appears in the room, for example
    /// when it is placed.
    /// </summary>
    /// <remarks>
    /// Items that arrive in a batch, such as the room's initial wall item list, do not trigger
    /// it; use <see cref="OnWallItemsLoaded"/> for those.
    /// </remarks>
    /// <param name="handler">The handler to call with the new item.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWallItemAdded(Action<WallItem> handler)
    {
        handler = Guarded(handler);
        Room.WallItemAdded += handler;
        return Track(new Unsubscriber(() => Room.WallItemAdded -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a wall item is removed from the room.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the item as it last was. It has already been removed from
    /// <see cref="WallItems"/> when the handler runs.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWallItemRemoved(Action<WallItem> handler)
    {
        handler = Guarded(handler);
        Room.WallItemRemovedDetailed += handler;
        return Track(new Unsubscriber(() => Room.WallItemRemovedDetailed -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a wall item is updated in place, for example a sticky
    /// note's text or a change of owner.
    /// </summary>
    /// <param name="handler">The handler to call with the item in its already updated state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWallItemUpdated(Action<WallItem> handler)
    {
        handler = Guarded(handler);
        Room.WallItemUpdated += handler;
        return Track(new Unsubscriber(() => Room.WallItemUpdated -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a wall item is moved along the wall.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the item, its previous wall location and its new wall location,
    /// in that order.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWallItemMoved(Action<WallItem, WallLocation, WallLocation> handler)
    {
        handler = Guarded(handler);
        Room.WallItemMoved += handler;
        return Track(new Unsubscriber(() => Room.WallItemMoved -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when a wall item's data changes.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the item, its previous data string and its new data string, in
    /// that order. The item already holds the new data.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWallItemDataChanged(Action<WallItem, string, string> handler)
    {
        handler = Guarded(handler);
        Room.WallItemDataChanged += handler;
        return Track(new Unsubscriber(() => Room.WallItemDataChanged -= handler));
    }

    /// <summary>
    /// Registers a handler that runs when the furni inventory finishes a full load.
    /// </summary>
    /// <remarks>
    /// The inventory arrives in fragments; the handler runs once, after the last one, when
    /// <see cref="InventoryItems"/> is complete.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryLoaded(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change.Kind is InventoryChangeKind.Loaded)
                    handler();
            })));
    }

    /// <summary>
    /// Registers a handler that runs when the server declares the cached furni inventory out of
    /// date.
    /// </summary>
    /// <remarks>
    /// Nothing is fetched again automatically; call <see cref="EnsureInventoryLoaded"/> to reload
    /// it.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryInvalidated(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change.Kind is InventoryChangeKind.Invalidated)
                    handler();
            })));
    }

    /// <summary>
    /// Registers a handler that runs when a single item appears in the furni inventory, for
    /// example after a purchase or a pickup.
    /// </summary>
    /// <remarks>
    /// Items that arrive with a full inventory load do not trigger it; use
    /// <see cref="OnInventoryLoaded"/> for those.
    /// </remarks>
    /// <param name="handler">The handler to call with the new item.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryItemAdded(Action<InventoryItem> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Added, Item: { } item })
                    handler(LegacyInventoryItem(item));
            })));
    }

    /// <summary>
    /// Registers a handler that runs when a furni inventory item is replaced by a newer version
    /// of itself, for example when its extra data changes.
    /// </summary>
    /// <param name="handler">The handler to call with the item in its new state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryItemUpdated(Action<InventoryItem> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Updated, Item: { } item })
                    handler(LegacyInventoryItem(item));
            })));
    }

    /// <summary>
    /// Registers a handler that runs when an item leaves the furni inventory, for example when it
    /// is placed in a room, traded away or sold.
    /// </summary>
    /// <param name="handler">The handler to call with the item as it last was.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryItemRemoved(Action<InventoryItem> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            Guarded<InventoryFurniChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Removed, Item: { } item })
                    handler(LegacyInventoryItem(item));
            })));
    }

    /// <summary>
    /// Registers a handler that runs when the pet inventory finishes a full load, after which
    /// <see cref="InventoryPets"/> is complete.
    /// </summary>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnPetInventoryLoaded(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change.Kind is InventoryChangeKind.Loaded)
                    handler();
            })));
    }

    /// <summary>
    /// Registers a handler that runs when a single pet is added to the pet inventory.
    /// </summary>
    /// <remarks>
    /// Pets that arrive with a full inventory load do not trigger it; use
    /// <see cref="OnPetInventoryLoaded"/> for those.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the pet and the open inventory flag of the server message, which
    /// is <see langword="true"/> when the server asks the client to open the inventory for it.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryPetAdded(Action<InventoryPet, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Added, Pet: { } pet })
                    handler(LegacyInventoryPet(pet), change.OpenInventory == true);
            })));
    }

    /// <summary>
    /// Registers a handler that runs when a pet in the inventory is replaced by a newer version
    /// of itself.
    /// </summary>
    /// <param name="handler">The handler to call with the pet in its new state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryPetUpdated(Action<InventoryPet> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Updated, Pet: { } pet })
                    handler(LegacyInventoryPet(pet));
            })));
    }

    /// <summary>
    /// Registers a handler that runs when a pet leaves the inventory, for example when it is
    /// placed in a room or given away.
    /// </summary>
    /// <param name="handler">The handler to call with the pet as it last was.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnInventoryPetRemoved(Action<InventoryPet> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<InventoryPetChanged>(
            ApplicationMemberIds.InventoryPetsChanged,
            Guarded<InventoryPetChanged>(change =>
            {
                if (change is { Kind: InventoryChangeKind.Removed, Pet: { } pet })
                    handler(LegacyInventoryPet(pet));
            })));
    }

    /// <summary>
    /// Registers a handler that runs when a hotel session starts.
    /// </summary>
    /// <remarks>
    /// It does not run for a session that is already active when it is registered; check
    /// <see cref="IsConnected"/> for that. The account, room and inventory state of the new
    /// session fills in afterwards, as the hotel sends it.
    /// </remarks>
    /// <param name="handler">The handler to call with the new session.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnSessionStarted(Action<Session> handler) =>
        Subscribe(
            handler,
            value => _interceptor.Connected += value,
            value => _interceptor.Connected -= value);

    /// <summary>
    /// Registers a handler that runs when the hotel session ends, however it ends.
    /// </summary>
    /// <remarks>
    /// It runs when the connection to the hotel closes and when the connection to G-Earth is lost,
    /// whether or not the server sent a disconnect reason. A run started from the command line is
    /// stopped as soon as its session ends, so the handler never runs there.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnSessionEnded(Action handler) =>
        Subscribe(
            handler,
            value => _interceptor.Disconnected += value,
            value => _interceptor.Disconnected -= value);

    /// <summary>
    /// Registers a handler that runs when the server announces that it is closing the
    /// connection.
    /// </summary>
    /// <remarks>
    /// It runs only when the server sends a disconnect reason. Use <see cref="OnSessionEnded"/> to
    /// react to every way a session ends.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the raw disconnect reason code the server sent. The connection
    /// is closed right afterwards, so this handler is the last chance to react.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnDisconnected(Action<int> handler) =>
        OnIn(MessageContracts.Session.DisconnectReason, message => handler(message.Reason));

    /// <summary>
    /// Registers a handler that runs when a friend request arrives.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the request, which carries the requester's user id, name and
    /// figure.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnFriendRequest(Action<NewFriendRequest> handler) =>
        Track(_application.Subscribe(
            ApplicationMemberIds.FriendRequestReceived,
            Guarded(handler)));

    /// <summary>
    /// Accepts every friend request that arrives from now on.
    /// </summary>
    /// <remarks>
    /// Requests already pending before the call are not touched.
    /// </remarks>
    /// <returns>
    /// A handle that stops accepting requests when disposed. It is also disposed when the script
    /// stops, so the effect only lasts for the run.
    /// </returns>
    public IDisposable AcceptAllFriendRequests() =>
        OnFriendRequest(request => AcceptFriendRequest(request.RequesterUserId));

    /// <summary>
    /// Registers a handler that runs on every change to the trade state.
    /// </summary>
    /// <remarks>
    /// The more specific <c>OnTrade...</c> methods filter this stream by
    /// <see cref="TradeChanged.Kind"/>.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the change, which carries its kind, the new trade state, the
    /// previous trade and any acceptance, close or open failure details.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTradeChanged(Action<TradeChanged> handler) =>
        Track(_application.Subscribe(
            ApplicationMemberIds.TradeChanged,
            Guarded(handler)));

    /// <summary>
    /// Registers a handler that runs when the server refuses to open a trade.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the failure, which carries the reason code and the other user's
    /// name.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
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
    /// Registers a handler that runs when a trade window opens.
    /// </summary>
    /// <remarks>
    /// Read <see cref="Trade"/> for the participants and their permissions.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTradeOpened(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.Opened)
                handler();
        });

    /// <summary>
    /// Registers a handler that runs when the furni and credit offers of the open trade change.
    /// </summary>
    /// <param name="handler">The handler to call with a summary of the open trade after the update.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
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
    /// Registers a handler that runs when a participant accepts the trade or withdraws their
    /// acceptance.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the user id of the participant and their new acceptance state:
    /// <see langword="true"/> accepted, <see langword="false"/> withdrawn.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
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
    /// Registers a handler that runs when both sides have accepted and the trade enters the final
    /// confirmation phase.
    /// </summary>
    /// <remarks>
    /// In this phase both sides must call <see cref="ConfirmTrade"/>, and the offers can no
    /// longer be changed. The trade has not completed yet; <see cref="OnTradeCompleted"/> runs
    /// once it has.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTradeAwaitingConfirmation(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.Confirmation)
                handler();
        });

    /// <summary>
    /// Registers a handler that runs when a trade completes and the items have changed hands.
    /// </summary>
    /// <remarks>
    /// The traded items show up in <see cref="InventoryItems"/> only once the furni inventory
    /// has been reloaded.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTradeCompleted(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.Kind is TradeChangeKind.Completed)
                handler();
        });

    /// <summary>
    /// Registers a handler that runs when the open trade ends for any reason.
    /// </summary>
    /// <remarks>
    /// It runs when the trade completed, was canceled by either side, was dropped because the
    /// local user changed room, or was cleared by a session reset.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTradeClosed(Action handler) =>
        OnTradeChanged(change =>
        {
            if (change.PreviousEpoch is not null && change.State.Active is null)
                handler();
        });
}
