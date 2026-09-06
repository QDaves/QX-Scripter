using Qx;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Plays an avatar expression. Alias of <see cref="Expression"/>.
    /// </summary>
    /// <param name="type">
    /// 0 clears the expression, 1 wave, 2 blow a kiss, 3 laugh, 4 cry, 5 go idle, 6 jump,
    /// 7 thumbs up.
    /// </param>
    public void Action(int type) => Expression(type);

    /// <summary>Blows a kiss. Equivalent to <c>Expression(2)</c>.</summary>
    public void Kiss() => Expression(2);

    /// <summary>Laughs. Equivalent to <c>Expression(3)</c>.</summary>
    public void Laugh() => Expression(3);

    /// <summary>Jumps. Equivalent to <c>Expression(6)</c>.</summary>
    public void Jump() => Expression(6);

    /// <summary>Gives a thumbs up. Equivalent to <c>Expression(7)</c>.</summary>
    public void ThumbsUp() => Expression(7);

    /// <summary>
    /// Puts the avatar to sleep immediately instead of waiting for the idle timer. Equivalent
    /// to <c>Expression(5)</c>.
    /// </summary>
    public void Idle() => Expression(5);

    /// <summary>
    /// Wakes the avatar from the idle state. Equivalent to <c>Expression(0)</c>.
    /// </summary>
    public void Unidle() => Expression(0);

    /// <summary>Walks to the given tile. Alias of <see cref="Walk(int,int)"/>.</summary>
    public void Move(int x, int y) => Walk(x, y);

    /// <summary>
    /// Changes the local user's motto. The server truncates or rejects it silently when it is
    /// too long or fails the filter; watch <see cref="OnProfileUpdated"/> for the accepted
    /// value.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="motto"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="motto"/> exceeds the protocol string limit.
    /// </exception>
    public void SetMotto(string motto) =>
        Application.Invoke<ProfileMottoSetRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileMottoSet,
            new ProfileMottoSetRequest(motto),
            Ct);

    /// <summary>
    /// Sends a friend request to the named user. Nothing is reported when the name does not
    /// exist, the user blocks requests, or either friend list is full.
    /// </summary>
    /// <param name="name">The exact user name.</param>
    public void AddFriend(string name) =>
        Application.Invoke<FriendRequestSendRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestSend,
            new FriendRequestSendRequest(name),
            Ct);

    /// <summary>
    /// Asks the server to move the local user into the room a friend is currently in. Does
    /// nothing when the friend is offline or their room does not allow entry.
    /// </summary>
    /// <param name="userId">The friend's user id.</param>
    public void FollowFriend(Id userId) =>
        Application.Invoke<FriendFollowRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendFollow,
            new FriendFollowRequest(userId),
            Ct);

    /// <summary>
    /// Requests to join a group. Depending on the group this either joins immediately or
    /// creates a pending membership request.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    public void JoinGroup(Id groupId) =>
        Application.Invoke<GroupJoinRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipJoin,
            new GroupJoinRequest(groupId),
            Ct);

    /// <summary>
    /// Sends a console (offline) message to a friend. The recipient must be on the friend list;
    /// the server drops the message otherwise.
    /// </summary>
    /// <param name="userId">The recipient's user id.</param>
    /// <param name="message">The message text.</param>
    /// <remarks>
    /// Flash carries a trailing sequence number. Unity builds support either two or three fields;
    /// the active verified schema selects the layout automatically. The number is the sender's own
    /// per-conversation counter, which the hotel quotes back on a delivery failure.
    /// </remarks>
    public void SendMessage(Id userId, string message) =>
        Application.Invoke<FriendMessageSendRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendMessageSend,
            new FriendMessageSendRequest(userId, message),
            Ct);

    /// <summary>
    /// Changes the local user's look. The server validates the figure against what the account
    /// actually owns and silently keeps the old look if it does not check out.
    /// </summary>
    /// <param name="gender">
    /// <c>"M"</c> or <c>"F"</c>. The Unity codec requires exactly one character.
    /// </param>
    /// <param name="figure">The figure string, for example <c>"hd-180-1.ch-255-66"</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="gender"/> or <paramref name="figure"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The session is Unity and <paramref name="gender"/> is not exactly one character, or either
    /// value exceeds the protocol string limit.
    /// </exception>
    public void UpdateFigure(string gender, string figure) =>
        Application.Invoke<ProfileFigureSetRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileFigureSet,
            new ProfileFigureSetRequest(gender, figure),
            Ct);

    /// <summary>
    /// Shows the typing indicator above the local avatar. It does not clear on its own; pair it
    /// with <see cref="CancelTyping"/>.
    /// </summary>
    public void StartTyping() =>
        Application.Invoke<RoomAvatarTypingRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarTyping,
            new RoomAvatarTypingRequest(true),
            Ct);

    /// <summary>Hides the typing indicator above the local avatar.</summary>
    public void CancelTyping() =>
        Application.Invoke<RoomAvatarTypingRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarTyping,
            new RoomAvatarTypingRequest(false),
            Ct);

    /// <summary>
    /// Accepts a pending friend request.
    /// </summary>
    /// <param name="userId">The requester's user id, as carried by <see cref="OnFriendRequest"/>.</param>
    public void AcceptFriendRequest(Id userId) =>
        Application.Invoke<FriendRequestIdsRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestAccept,
            new FriendRequestIdsRequest([userId]),
            Ct);

    /// <summary>Declines one pending friend request.</summary>
    /// <param name="userId">The requester's user id.</param>
    public void DeclineFriendRequest(Id userId) => DeclineFriendRequests([userId]);

    /// <summary>
    /// Declines several pending friend requests in one message. Duplicate ids are collapsed.
    /// </summary>
    /// <param name="userIds">The requesters' user ids.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userIds"/> is <see langword="null"/>.</exception>
    public void DeclineFriendRequests(IEnumerable<Id> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        Id[] ids = userIds.Distinct().ToArray();
        Application.Invoke<FriendRequestDeclineRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestDecline,
            new FriendRequestDeclineRequest(ids),
            Ct);
    }

    /// <summary>
    /// Declines every pending friend request at once, using the protocol's "decline all" flag
    /// rather than an id list, so no request has to be known in advance.
    /// </summary>
    public void DeclineAllFriendRequests() =>
        Application.Invoke<FriendRequestsDeclineAllRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestsDeclineAll,
            new FriendRequestsDeclineAllRequest(),
            Ct);

    /// <summary>
    /// Kicks a user out of the current room. Requires room rights or staff permissions; ignored
    /// otherwise.
    /// </summary>
    /// <param name="userId">The target user's account id, not their room index.</param>
    public void Kick(Id userId) =>
        Application.Invoke<RoomModerationTargetRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationKick,
            new RoomModerationTargetRequest(userId),
            Ct);

    /// <summary>
    /// Mutes a user in the current room for a number of minutes. Requires rights, and the
    /// room's "who can mute" setting must allow it.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="minutes">The mute duration in minutes.</param>
    public void Mute(Id userId, int minutes) =>
        Application.Invoke<RoomModerationMuteRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationMute,
            new RoomModerationMuteRequest(userId, minutes),
            Ct);

    /// <summary>
    /// Bans a user from the current room. Requires ownership or rights, subject to the room's
    /// "who can ban" setting.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="duration">The ban duration understood by the room moderation protocol.</param>
    public void Ban(Id userId, string duration = "Room_Session")
    {
        BanLength length = duration switch
        {
            "Room_Session" or "RWUAM_BAN_USER_HOUR" => BanLength.Hour,
            "RWUAM_BAN_USER_DAY" => BanLength.Day,
            "RWUAM_BAN_USER_PERM" => BanLength.Permanent,
            _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, "Unknown room-ban duration.")
        };
        Application.Invoke<RoomModerationBanRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationBan,
            new RoomModerationBanRequest(userId, length),
            Ct);
    }

    /// <summary>
    /// Grants room rights to a user who is in the current room. The local user must own the
    /// room.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    public void GiveRights(Id userId) =>
        Application.Invoke<RoomRightsGrantRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPeopleRightsGrant,
            new RoomRightsGrantRequest(userId),
            Ct);

    /// <summary>
    /// Revokes a user's room rights. The local user must own the room.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    public void RemoveRights(Id userId) => SendIds(Msg.Out.RemoveRights, userId);

    /// <summary>
    /// Answers a doorbell for a locked room: lets the waiting user in or turns them away.
    /// </summary>
    /// <param name="name">The waiting user's name, as reported by the doorbell event.</param>
    /// <param name="allow"><see langword="true"/> to let them in, <see langword="false"/> to refuse.</param>
    public void LetIn(string name, bool allow = true) =>
        Application.Invoke<RoomDoorbellAnswerRequest, RoomControlDispatchResult>(
            ApplicationMemberIds.RoomDoorbellAnswer,
            new RoomDoorbellAnswerRequest(name, allow),
            Ct);

    /// <summary>Removes a user from the friend list.</summary>
    /// <param name="userId">The friend's user id.</param>
    public void RemoveFriend(Id userId) =>
        Application.Invoke<FriendsRemoveRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendsRemove,
            new FriendsRemoveRequest([userId]),
            Ct);

    /// <summary>
    /// Gives a pet in the current room a respect. The daily respect allowance is enforced by the
    /// server and its exhaustion is not reported here.
    /// </summary>
    /// <param name="petId">The pet's id.</param>
    public void RespectPet(Id petId) =>
        Application.Invoke<RoomPetRespectRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPetRespect,
            new RoomPetRespectRequest(petId),
            Ct);

    /// <summary>
    /// Adds a user to the ignore list, so their chat is hidden locally by the client.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    /// <exception cref="InvalidOperationException">
    /// The session's Unity build identifies ignores by name only and the id could not be
    /// resolved to a name from the room or the friend list. Use <see cref="Ignore(string)"/>
    /// in that case.
    /// </exception>
    /// <remarks>
    /// Some Unity builds carry a name-based ignore message and others an id-based one; the
    /// available layout is detected from the message catalog.
    /// </remarks>
    public void Ignore(Id userId)
    {
        if (Application.Describe(ApplicationMemberIds.ProfileIgnoreAddById).Availability.Available)
        {
            Application.Invoke<ProfileUserRequest, ProfileDispatchResult>(
                ApplicationMemberIds.ProfileIgnoreAddById,
                new ProfileUserRequest(userId),
                Ct);
            return;
        }

        if (Application.Describe(ApplicationMemberIds.ProfileIgnoreAddByName).Availability.Available)
        {
            Application.Invoke<ProfileUserNameRequest, ProfileDispatchResult>(
                ApplicationMemberIds.ProfileIgnoreAddByName,
                new ProfileUserNameRequest(ResolveUserName(userId)),
                Ct);
            return;
        }

        Application.Invoke<ProfileUserRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileIgnoreAddById,
            new ProfileUserRequest(userId),
            Ct);
    }

    /// <summary>
    /// Adds a user to the ignore list by name.
    /// </summary>
    /// <param name="name">The target user's name.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// The session needs a numeric id and the name is neither in the current room nor on the
    /// friend list, so it cannot be resolved.
    /// </exception>
    public void Ignore(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Application.Describe(ApplicationMemberIds.ProfileIgnoreAddByName).Availability.Available)
        {
            Application.Invoke<ProfileUserNameRequest, ProfileDispatchResult>(
                ApplicationMemberIds.ProfileIgnoreAddByName,
                new ProfileUserNameRequest(name),
                Ct);
            return;
        }

        if (Application.Describe(ApplicationMemberIds.ProfileIgnoreAddById).Availability.Available)
        {
            Application.Invoke<ProfileUserRequest, ProfileDispatchResult>(
                ApplicationMemberIds.ProfileIgnoreAddById,
                new ProfileUserRequest(ResolveUserId(name)),
                Ct);
            return;
        }

        Application.Invoke<ProfileUserNameRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileIgnoreAddByName,
            new ProfileUserNameRequest(name),
            Ct);
    }

    /// <summary>
    /// Removes a user from the ignore list.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    /// <exception cref="InvalidOperationException">
    /// The session's Unity build identifies unignores by name only and the id could not be
    /// resolved to a name.
    /// </exception>
    public void Unignore(Id userId)
    {
        ProfileIdentityKind kind = UnignoreIdentityKind(ProfileIdentityKind.Id);
        string identity = kind is ProfileIdentityKind.Name
            ? ResolveUserName(userId)
            : ((long)userId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Application.Invoke<ProfileIgnoreRemoveRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileIgnoreRemove,
            new ProfileIgnoreRemoveRequest(kind, identity),
            Ct);
    }

    /// <summary>
    /// Removes a user from the ignore list by name.
    /// </summary>
    /// <param name="name">The target user's name.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// The session needs a numeric id and the name could not be resolved from the room or the
    /// friend list.
    /// </exception>
    public void Unignore(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ProfileIdentityKind kind = UnignoreIdentityKind(ProfileIdentityKind.Name);
        string identity = kind is ProfileIdentityKind.Id
            ? ((long)ResolveUserId(name)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : name;
        Application.Invoke<ProfileIgnoreRemoveRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileIgnoreRemove,
            new ProfileIgnoreRemoveRequest(kind, identity),
            Ct);
    }

    /// <summary>
    /// Mounts or dismounts a rideable pet in the current room.
    /// </summary>
    /// <param name="petId">The pet's id.</param>
    /// <param name="mount"><see langword="true"/> to get on, <see langword="false"/> to get off.</param>
    public void MountPet(Id petId, bool mount = true) =>
        Application.Invoke<RoomPetMountRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPetMountSet,
            new RoomPetMountRequest(petId, mount),
            Ct);

    /// <summary>Dismounts a pet. Equivalent to <c>MountPet(petId, false)</c>.</summary>
    public void DismountPet(Id petId) => MountPet(petId, false);

    /// <summary>
    /// Removes a member from a group. Requires an administrator rank in that group.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <param name="blockRejoin">Whether the member is also barred from re-applying.</param>
    public void KickGroupMember(Id groupId, Id userId, bool blockRejoin = false) =>
        Application.Invoke<GroupMemberKickRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipKick,
            new GroupMemberKickRequest(groupId, userId, blockRejoin),
            Ct);

    /// <summary>
    /// Approves a pending membership request. Requires an administrator rank in that group.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="userId">The applicant's user id.</param>
    public void ApproveGroupMember(Id groupId, Id userId) =>
        Application.Invoke<GroupMemberRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipApprove,
            new GroupMemberRequest(groupId, userId),
            Ct);

    /// <summary>
    /// Rejects a pending membership request. Requires an administrator rank in that group.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="userId">The applicant's user id.</param>
    public void RejectGroupMember(Id groupId, Id userId) =>
        Application.Invoke<GroupMemberRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipReject,
            new GroupMemberRequest(groupId, userId),
            Ct);

    /// <summary>
    /// Makes a group the favourite one, so its badge is shown next to the avatar. The account
    /// must be a member.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    public void SetFavouriteGroup(Id groupId) =>
        Application.Invoke<ProfileFavoriteGroupRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileFavoriteGroupSelect,
            new ProfileFavoriteGroupRequest(groupId),
            Ct);

    /// <summary>Clears the favourite group, hiding its badge again.</summary>
    /// <param name="groupId">The group id currently marked as favourite.</param>
    public void UnsetFavouriteGroup(Id groupId) =>
        Application.Invoke<ProfileFavoriteGroupRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileFavoriteGroupDeselect,
            new ProfileFavoriteGroupRequest(groupId),
            Ct);

    /// <summary>
    /// Places a floor item from the inventory into the room. Requires room rights; a blocked
    /// tile or a missing item is refused silently.
    /// </summary>
    /// <param name="itemId">The inventory item id, not a room item id.</param>
    /// <param name="x">The target tile's X coordinate.</param>
    /// <param name="y">The target tile's Y coordinate.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    public void PlaceFloorItem(Id itemId, int x, int y, int direction = 0) =>
        Application.Invoke<RoomPlacementFloorPlaceRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementFloorPlace,
            new RoomPlacementFloorPlaceRequest(
                itemId,
                new RoomPlacementFloorPosition(x, y, direction)),
            Ct);

    /// <summary>
    /// Places a wall item from the inventory onto a wall. Requires room rights.
    /// </summary>
    /// <param name="itemId">The inventory item id.</param>
    /// <param name="wallLocation">
    /// The wall position in the client's notation, <c>":w=x,y l=x,y direction"</c>, for example
    /// <c>":w=2,3 l=5,20 l"</c>.
    /// </param>
    public void PlaceWallItem(Id itemId, string wallLocation) =>
        Application.Invoke<RoomPlacementWallPlaceRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementWallPlace,
            new RoomPlacementWallPlaceRequest(itemId, PlacementWallPosition(wallLocation)),
            Ct);

    /// <summary>
    /// Moves a wall item that is already hanging in the room to a new wall position. Requires
    /// room rights.
    /// </summary>
    /// <param name="itemId">The item's room id.</param>
    /// <param name="wallLocation">The new wall position, in the <c>":w=x,y l=x,y direction"</c> notation.</param>
    public void MoveWallItem(Id itemId, string wallLocation) =>
        Application.Invoke<RoomPlacementWallMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementWallMove,
            new RoomPlacementWallMoveRequest(itemId, PlacementWallPosition(wallLocation)),
            Ct);

    /// <summary>
    /// Places a sticky note (post-it) from the inventory onto a wall, with no text.
    /// </summary>
    /// <param name="itemId">The inventory item id of the sticky pad.</param>
    /// <param name="wallLocation">The wall position, in the <c>":w=x,y l=x,y direction"</c> notation.</param>
    public void PlacePostIt(Id itemId, string wallLocation) =>
        Application.Invoke<RoomPostItPlaceRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemPostItPlace,
            new RoomPostItPlaceRequest(itemId, wallLocation),
            Ct);

    /// <summary>
    /// Places a sticky note on a wall together with its colour and initial text.
    /// </summary>
    /// <param name="itemId">The inventory item id of the sticky pad.</param>
    /// <param name="wallLocation">The wall position, in the <c>":w=x,y l=x,y direction"</c> notation.</param>
    /// <param name="color">The note colour as a hexadecimal string, for example <c>"FFFF33"</c>.</param>
    /// <param name="text">The note text.</param>
    public void AddPostIt(Id itemId, string wallLocation, string color, string text) =>
        Application.Invoke<RoomPostItAddRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemPostItAdd,
            new RoomPostItAddRequest(itemId, wallLocation, color, text),
            Ct);

    /// <summary>Moves a wall item to a new wall position. See <see cref="MoveWallItem(Id, string)"/>.</summary>
    public void MoveWallItem(WallItem item, string wallLocation) =>
        Application.Invoke<RoomPlacementWallMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementWallMove,
            new RoomPlacementWallMoveRequest(
                item.Id,
                PlacementWallPosition(wallLocation),
                PlacementWallPosition(item.Location)),
            Ct);

    /// <summary>
    /// Rotates a placed floor item without moving it, by re-sending its current tile with a new
    /// rotation.
    /// </summary>
    /// <param name="item">The placed item; its current X and Y are reused.</param>
    /// <param name="direction">The new rotation, in eighths of a turn (0 to 7).</param>
    public void RotateFloorItem(FloorItem item, int direction) =>
        Application.Invoke<RoomPlacementFloorMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementFloorMove,
            new RoomPlacementFloorMoveRequest(
                item.Id,
                new RoomPlacementFloorPosition(item.X, item.Y, direction),
                new RoomPlacementFloorPosition(item.X, item.Y, item.Direction)),
            Ct);

    /// <summary>
    /// Picks a floor item up into the inventory. Requires room rights or ownership of the item.
    /// </summary>
    /// <param name="item">The floor item to pick up.</param>
    /// <param name="confirmed">
    /// Acknowledges the hotel's remove-confirmation prompt. Flash only: the client sends the same
    /// message a second time with this set once the prompt is accepted. Unity has no such field.
    /// </param>
    public void PickupFurni(FloorItem item, bool confirmed = false) =>
        SendPickup(2, item.Id, confirmed);

    /// <summary>
    /// Picks a wall item up into the inventory. Requires room rights or ownership of the item.
    /// </summary>
    /// <param name="item">The wall item to pick up.</param>
    /// <param name="confirmed">
    /// Acknowledges the hotel's remove-confirmation prompt. Flash only, as for floor items.
    /// </param>
    public void PickupFurni(WallItem item, bool confirmed = false) =>
        SendPickup(1, item.Id, confirmed);

    private void SendPickup(int category, Id itemId, bool confirmed) =>
        Application.Invoke<RoomPlacementPickupRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementPickup,
            new RoomPlacementPickupRequest(
                itemId,
                category == 2 ? RoomPlacementItemKind.Floor : RoomPlacementItemKind.Wall,
                confirmed),
            Ct);

    private static RoomPlacementWallPosition PlacementWallPosition(string value) =>
        PlacementWallPosition(WallLocation.ParseString(value));

    private static RoomPlacementWallPosition PlacementWallPosition(WallLocation value) =>
        new(
            value.Wall.X,
            value.Wall.Y,
            value.Offset.X,
            value.Offset.Y,
            value.Orientation.ToString());

    /// <summary>
    /// Buys a marketplace offer. The purchase is refused silently when the offer has already
    /// been taken or the account cannot afford it.
    /// </summary>
    /// <param name="offerId">The marketplace offer id from a search result.</param>
    public void BuyMarketplaceOffer(Id offerId) =>
        Application.Invoke<MarketplaceBuySendRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceOfferBuySend,
            new MarketplaceBuySendRequest(offerId),
            Ct);

    /// <summary>
    /// Withdraws one of the local user's own marketplace offers, returning the item to the
    /// inventory.
    /// </summary>
    /// <param name="offerId">The offer id from <see cref="GetMyMarketplaceOffers(int)"/>.</param>
    public void CancelMarketplaceOffer(Id offerId) =>
        Application.Invoke<MarketplaceCancelSendRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceOfferCancelSend,
            new MarketplaceCancelSendRequest(offerId),
            Ct);

    /// <summary>
    /// Collects the credits earned from sold marketplace offers into the wallet.
    /// </summary>
    public void RedeemMarketplaceCredits() =>
        CollectMarketplaceEarnings();

    /// <summary>
    /// Activates an avatar effect that is owned but not yet started, which begins consuming its
    /// duration. Use <see cref="EnableEffect"/> to actually wear an already-activated effect.
    /// </summary>
    /// <param name="effectId">The effect id; <see cref="EffectName"/> resolves it to a name.</param>
    public void ActivateEffect(int effectId) =>
        Application.Invoke<InventoryAvatarEffectRequest, InventoryDispatchResult>(
            ApplicationMemberIds.InventoryAvatarEffectActivate,
            new InventoryAvatarEffectRequest(effectId),
            Ct);

    /// <summary>
    /// Wears one of the currently activated avatar effects.
    /// </summary>
    /// <param name="effectId">The effect id, or -1 to wear none.</param>
    public void EnableEffect(int effectId) =>
        Application.Invoke<RoomAvatarEffectRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarEffect,
            new RoomAvatarEffectRequest(effectId),
            Ct);

    /// <summary>Takes off the current avatar effect. Equivalent to <c>EnableEffect(-1)</c>.</summary>
    public void DisableEffect() => EnableEffect(-1);

    /// <summary>
    /// Stores a look in a wardrobe slot, overwriting whatever was in it.
    /// </summary>
    /// <param name="slot">The wardrobe slot number, as used by <see cref="GetWardrobe"/>.</param>
    /// <param name="figure">The figure string to store.</param>
    /// <param name="gender"><c>"M"</c> or <c>"F"</c>.</param>
    public void SaveOutfit(int slot, string figure, string gender) =>
        Application.Invoke<ProfileOutfitSaveRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileWardrobeOutfitSave,
            new ProfileOutfitSaveRequest(slot, figure, gender),
            Ct);

    /// <summary>
    /// Throws a dice furni, making it roll to a new value. The result arrives asynchronously as
    /// an item-data change; subscribe to <see cref="OnFloorItemDataChanged"/> to read it.
    /// </summary>
    /// <param name="itemId">The dice's room item id.</param>
    public void ThrowDice(Id itemId) =>
        Application.Invoke<RoomDiceRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemDiceThrow,
            new RoomDiceRequest(itemId),
            Ct);

    /// <summary>
    /// Clears a dice furni back to its blank face.
    /// </summary>
    /// <param name="itemId">The dice's room item id.</param>
    public void DiceOff(Id itemId) =>
        Application.Invoke<RoomDiceRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemDiceClear,
            new RoomDiceRequest(itemId),
            Ct);

    /// <summary>
    /// Creates a new room owned by the local user. Nothing is returned; the new room shows up
    /// in the navigator's own-rooms view, which <see cref="GetUserRooms"/> reads.
    /// </summary>
    /// <param name="name">The room name.</param>
    /// <param name="description">The room description.</param>
    /// <param name="model">The floor-plan model name, for example <c>"model_a"</c>.</param>
    /// <param name="category">The navigator category id the room is filed under.</param>
    /// <param name="maxVisitors">The visitor cap; the server clamps it to the values it allows.</param>
    /// <param name="tradeMode">Trading policy: 0 disabled, 1 rights holders only, 2 everyone.</param>
    public void CreateRoom(string name, string description, string model, int category, int maxVisitors, int tradeMode = 0) =>
        Application.Invoke<NavigatorRoomCreateInput, NavigatorRoomOperationResult>(
            ApplicationMemberIds.NavigatorRoomCreate,
            new NavigatorRoomCreateInput(name, description, model, category, maxVisitors, tradeMode),
            Ct);

    /// <summary>
    /// Permanently deletes a room owned by the local user, together with everything placed in
    /// it. There is no confirmation step.
    /// </summary>
    /// <param name="roomId">The room id.</param>
    public void DeleteRoom(Id roomId) =>
        Application.Invoke<NavigatorRoomDeleteInput, NavigatorRoomOperationResult>(
            ApplicationMemberIds.NavigatorRoomDelete,
            new NavigatorRoomDeleteInput(roomId),
            Ct);

    /// <summary>
    /// Sets the account's home room. The room must be owned by or favourited for the local user.
    /// </summary>
    /// <param name="roomId">The room id, or 0 to clear the home room.</param>
    public void SetHomeRoom(Id roomId) =>
        Application.Invoke<NavigatorHomeRoomSetInput, NavigatorRoomOperationResult>(
            ApplicationMemberIds.NavigatorHomeRoomSet,
            new NavigatorHomeRoomSetInput(roomId),
            Ct);

    /// <summary>
    /// Adds or removes a room from the staff picks. Requires staff permissions; ignored for
    /// ordinary accounts.
    /// </summary>
    /// <param name="roomId">The room id.</param>
    /// <param name="pick"><see langword="true"/> to pick, <see langword="false"/> to unpick.</param>
    public void ToggleStaffPick(Id roomId, bool pick = true) =>
        Application.Invoke<RoomStaffPickRequest, RoomControlDispatchResult>(
            ApplicationMemberIds.RoomStaffPickSet,
            new RoomStaffPickRequest(roomId, pick),
            Ct);

    /// <summary>
    /// Rates the current room. Each user may rate a given room once per visit; further ratings
    /// are ignored by the server.
    /// </summary>
    /// <param name="rating">The signed rating value. The current client uses 1 for a positive rating.</param>
    public void RateRoom(int rating) =>
        Application.Invoke<RoomRatingRequest, RoomControlDispatchResult>(
            ApplicationMemberIds.RoomRatingSubmit,
            new RoomRatingRequest(rating),
            Ct);

    /// <summary>
    /// Removes a pet from the current room and returns it to the owner's inventory. Requires
    /// ownership of the pet or room rights.
    /// </summary>
    /// <param name="petId">The pet's id.</param>
    public void RemovePet(Id petId) =>
        Application.Invoke<RoomPetRemoveRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPetRemove,
            new RoomPetRemoveRequest(petId),
            Ct);

    /// <summary>
    /// Removes a bot from the current room and returns it to the inventory. Requires ownership
    /// of the bot or room rights.
    /// </summary>
    /// <param name="botId">The bot's id.</param>
    public void RemoveBot(Id botId) =>
        Application.Invoke<RoomBotRemoveRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomBotRemove,
            new RoomBotRemoveRequest(botId),
            Ct);

    /// <summary>
    /// Writes a complete set of room settings back to the server. Every field is overwritten, so
    /// read the current settings first - <see cref="ModifyRoomSettings"/> does that for you.
    /// </summary>
    /// <param name="settings">
    /// The full settings block. The room id it carries selects the room; the local user must own
    /// it.
    /// </param>
    /// <param name="password">
    /// The door password, which is not part of <paramref name="settings"/>. It is only used when
    /// the door mode is the password mode, and an empty string clears an existing password.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The session is Unity and the settings use fields the Unity wire layout has no room for -
    /// tags, wall/floor thickness, hidden walls, chat flood sensitivity, the idle timers, the
    /// door-tile rule or pet muting - or the build uses the legacy Unity layout and trade,
    /// food-consumption or walk-through settings are set.
    /// </exception>
    /// <remarks>
    /// Flash accepts the full field set; the Unity layouts are strict subsets and are detected
    /// from the message catalog.
    /// </remarks>
    public void SaveRoomSettings(RoomSettings settings, string password = "")
    {
        ArgumentNullException.ThrowIfNull(settings);
        Application.Invoke<RoomSettingsSaveRequest, RoomSettingsSaveReceipt>(
            ApplicationMemberIds.RoomSettingsSave,
            new RoomSettingsSaveRequest(ToApplicationRoomSettings(settings), password),
            Ct);
    }

    private ProfileIdentityKind UnignoreIdentityKind(ProfileIdentityKind fallback)
    {
        ApplicationAvailability availability = Application
            .Describe(ApplicationMemberIds.ProfileIgnoreRemove)
            .Availability;
        if (!availability.Available)
            return fallback;
        string? capability = availability.ActiveMessages
            .Select(message => message.WireCapability)
            .FirstOrDefault(value => value is not null);
        return capability switch
        {
            "unityUnignoreNameSchema" => ProfileIdentityKind.Name,
            "unityUnignoreIdSchema" or "flashUnignoreIdSchema" => ProfileIdentityKind.Id,
            _ => ProfileIdentityKind.Id
        };
    }

    private Id ResolveUserId(string name)
    {
        User? user = FindUser(name);
        if (user is not null)
            return user.Id;
        Friend? friend = FindFriend(name);
        if (friend is not null)
            return friend.Id;
        throw new InvalidOperationException($"Cannot resolve user '{name}' to an identifier for this client layout.");
    }

    private string ResolveUserName(Id user_id)
    {
        User? user = Users.FirstOrDefault(candidate => candidate.Id == user_id);
        if (user is not null)
            return user.Name;
        Friend? friend = Friends.FirstOrDefault(candidate => candidate.Id == user_id);
        if (friend is not null)
            return friend.Name;
        throw new InvalidOperationException($"Cannot resolve user '{user_id}' to a name for this client layout. Use the string overload instead.");
    }

    /// <summary>
    /// Buys an offer from the catalog. Nothing is returned; failures such as insufficient
    /// credits or a stale offer are reported by the client's own purchase-error message, not
    /// here.
    /// </summary>
    /// <param name="pageId">The catalog page id, from <see cref="GetCatalogIndex"/>.</param>
    /// <param name="offerId">The offer id on that page, from <see cref="GetCatalogPage"/>.</param>
    /// <param name="extraData">
    /// The purchase parameter the offer expects: a colour index, a pet name and colour, a
    /// badge code, and so on. Empty for offers that take none.
    /// </param>
    /// <param name="amount">How many to buy in one purchase.</param>
    public void PurchaseFromCatalog(int pageId, int offerId, string extraData = "", int amount = 1) =>
        Application.Invoke<CatalogPurchaseSendRequest, CatalogPurchaseDispatchReceipt>(
            ApplicationMemberIds.CatalogPurchaseSend,
            new CatalogPurchaseSendRequest(pageId, offerId, extraData, amount),
            Ct);

    /// <summary>
    /// Buys a catalog offer as a wrapped gift for another user.
    /// </summary>
    /// <param name="pageId">The catalog page id.</param>
    /// <param name="offerId">The offer id on that page.</param>
    /// <param name="extraData">The purchase parameter the offer expects; empty for none.</param>
    /// <param name="receiverName">The recipient's user name.</param>
    /// <param name="giftMessage">The message shown when the gift is opened.</param>
    /// <param name="spriteId">The gift-box sprite id from the gift-wrapping catalog page.</param>
    /// <param name="boxType">The box shape index offered by the gift wrapper.</param>
    /// <param name="ribbonType">The ribbon index offered by the gift wrapper.</param>
    /// <param name="showPurchaserName">Whether the sender's name is revealed to the recipient.</param>
    /// <param name="amount">
    /// How many to buy. Only Unity carries this field; on Flash the value is ignored and the
    /// server buys one.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is less than 1.</exception>
    public void PurchaseFromCatalogAsGift(
        int pageId, int offerId, string extraData, string receiverName, string giftMessage,
        int spriteId, int boxType, int ribbonType, bool showPurchaserName = false, int amount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        Application.Invoke<GiftPurchaseRequest, GiftPurchaseDispatchReceipt>(
            ApplicationMemberIds.GiftsPurchase,
            new GiftPurchaseRequest(
                pageId,
                offerId,
                extraData,
                receiverName,
                giftMessage,
                spriteId,
                boxType,
                ribbonType,
                showPurchaserName,
                amount),
            Ct);
    }

    private void SendIds(string name, params Id[] ids)
    {
        if (CurrentClient is ClientType.Unity)
        {
            SendToServer(name, (object)ids);
            return;
        }

        using Packet packet = NewPacket(Direction.Out, name);
        PacketWriter writer = packet.Writer();
        writer.WriteLength((Length)ids.Length);
        foreach (Id id in ids)
            writer.WriteId(id);
        Ext.Send(packet);
    }

    /// <summary>
    /// Shows a chat bubble on the local screen only, by injecting a whisper into the game
    /// client. Nothing is sent to the server and nobody else sees it.
    /// </summary>
    /// <param name="message">The bubble text.</param>
    /// <param name="bubble">The chat-bubble style id; 30 is the neutral grey bubble.</param>
    public void ShowBubble(string message, int bubble = 30) => ShowBubble(message, Me?.Index ?? -1, bubble);

    /// <summary>
    /// Shows a local-only chat bubble above a specific avatar. Nothing is sent to the server.
    /// </summary>
    /// <param name="message">The bubble text.</param>
    /// <param name="index">
    /// The room index of the avatar the bubble appears above; -1 when the own avatar is
    /// unknown, in which case the client shows nothing.
    /// </param>
    /// <param name="bubble">The chat-bubble style id.</param>
    public void ShowBubble(string message, int index, int bubble)
    {
        ArgumentNullException.ThrowIfNull(message);

        bool unity = CurrentClient is ClientType.Unity;
        SendToClient(
            MessageContracts.Room.Chat.Whisper,
            new AvatarChat(
                index,
                message,
                0,
                bubble,
                [],
                0,
                ChatType.Whisper,
                unity ? 0 : null,
                unity ? 0 : null));
    }
}
