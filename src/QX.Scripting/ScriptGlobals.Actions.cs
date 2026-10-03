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
    /// <summary>Blows a kiss.</summary>
    /// <remarks>Equivalent to <c>Expression(2)</c>.</remarks>
    public void Kiss() => Expression(2);

    /// <summary>Laughs.</summary>
    /// <remarks>Equivalent to <c>Expression(3)</c>.</remarks>
    public void Laugh() => Expression(3);

    /// <summary>Jumps.</summary>
    /// <remarks>Equivalent to <c>Expression(6)</c>.</remarks>
    public void Jump() => Expression(6);

    /// <summary>Gives a thumbs up.</summary>
    /// <remarks>Equivalent to <c>Expression(7)</c>.</remarks>
    public void ThumbsUp() => Expression(7);

    /// <summary>
    /// Puts the avatar to sleep immediately instead of waiting for the idle timer.
    /// </summary>
    /// <remarks>Equivalent to <c>Expression(5)</c>.</remarks>
    public void Idle() => Expression(5);

    /// <summary>
    /// Wakes the avatar from the idle state.
    /// </summary>
    /// <remarks>Equivalent to <c>Expression(0)</c>.</remarks>
    public void Unidle() => Expression(0);

    /// <summary>
    /// Changes the local user's motto.
    /// </summary>
    /// <remarks>
    /// The server truncates or rejects it silently when it is too long or fails the filter;
    /// watch <see cref="OnProfileUpdated"/> for the accepted value.
    /// </remarks>
    /// <param name="motto">The new motto. An empty string clears it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="motto"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="motto"/> is longer than 65535 bytes in UTF-8.
    /// </exception>
    public void SetMotto(string motto) =>
        _application.Invoke<ProfileMottoSetRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileMottoSet,
            new ProfileMottoSetRequest(motto),
            Ct);

    /// <summary>
    /// Sends a friend request to the named user.
    /// </summary>
    /// <remarks>
    /// Nothing is reported when the name does not exist, the user blocks requests, or either
    /// friend list is full.
    /// </remarks>
    /// <param name="name">The exact user name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    public void AddFriend(string name) =>
        _application.Invoke<FriendRequestSendRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestSend,
            new FriendRequestSendRequest(name),
            Ct);

    /// <summary>Sends a friend request to a user in the room.</summary>
    /// <param name="user">The target user; only its name is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the user's name is empty or whitespace.</exception>
    public void AddFriend(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        AddFriend(user.Name);
    }

    /// <summary>
    /// Asks the server to move the local user into the room a friend is currently in.
    /// </summary>
    /// <remarks>
    /// Nothing happens when the friend is offline or their room does not allow entry.
    /// </remarks>
    /// <param name="userId">The friend's user id.</param>
    public void FollowFriend(Id userId) =>
        _application.Invoke<FriendFollowRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendFollow,
            new FriendFollowRequest(userId),
            Ct);

    /// <summary>
    /// Requests to join a group.
    /// </summary>
    /// <remarks>
    /// Depending on the group this either joins immediately or creates a pending membership
    /// request.
    /// </remarks>
    /// <param name="groupId">The group id.</param>
    public void JoinGroup(Id groupId) =>
        _application.Invoke<GroupJoinRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipJoin,
            new GroupJoinRequest(groupId),
            Ct);

    /// <summary>
    /// Sends a private message to a friend through the messenger.
    /// </summary>
    /// <remarks>
    /// Messages from friends arrive through <see cref="OnPrivateMessage(Action{NewConsoleMessage})"/>, and a
    /// message that cannot be delivered is reported through <see cref="OnPrivateMessageFailed"/>.
    /// </remarks>
    /// <param name="userId">The friend's user id.</param>
    /// <param name="message">The message text.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is <see langword="null"/>, empty or whitespace.</exception>
    public void SendPrivateMessage(Id userId, string message) =>
        _application.Invoke<FriendMessageSendRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendMessageSend,
            new FriendMessageSendRequest(userId, message),
            Ct);

    /// <summary>Sends a private message to a friend through the messenger.</summary>
    /// <param name="friend">The recipient; only its id is used.</param>
    /// <param name="message">The message text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="friend"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is <see langword="null"/>, empty or whitespace.</exception>
    public void SendPrivateMessage(Friend friend, string message)
    {
        ArgumentNullException.ThrowIfNull(friend);
        SendPrivateMessage(friend.Id, message);
    }

    /// <summary>
    /// Changes the local user's look.
    /// </summary>
    /// <param name="gender">
    /// The gender: <c>"M"</c>, <c>"F"</c> or <c>"U"</c>, or <c>"male"</c>, <c>"female"</c> or
    /// <c>"unisex"</c>, in any case.
    /// </param>
    /// <param name="figure">The figure string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gender"/> or <paramref name="figure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="gender"/> is not a known gender, or <paramref name="figure"/> is empty or
    /// whitespace.
    /// </exception>
    public void UpdateFigure(string gender, string figure) =>
    _application.Invoke<ProfileFigureSetRequest, ProfileDispatchResult>(
        ApplicationMemberIds.ProfileFigureSet,
        new ProfileFigureSetRequest(gender, figure),
        Ct);

    /// <summary>Changes the local user's look.</summary>
    /// <remarks>
    /// The gender is sent as the single letter code the wire uses, as
    /// <see cref="UpdateFigure(string, string)"/> takes it.
    /// </remarks>
    /// <param name="gender">The avatar's gender.</param>
    /// <param name="figure">The figure string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="figure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="figure"/> is empty or whitespace.</exception>
    public void UpdateFigure(Gender gender, string figure) =>
        UpdateFigure(gender.ToClientString(), figure);

    /// <summary>
    /// Shows the typing indicator above the local avatar.
    /// </summary>
    /// <remarks>
    /// The indicator does not clear on its own; pair it with <see cref="CancelTyping"/>.
    /// </remarks>
    public void StartTyping() =>
        _application.Invoke<RoomAvatarTypingRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarTyping,
            new RoomAvatarTypingRequest(true),
            Ct);

    /// <summary>Hides the typing indicator above the local avatar.</summary>
    public void CancelTyping() =>
        _application.Invoke<RoomAvatarTypingRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarTyping,
            new RoomAvatarTypingRequest(false),
            Ct);

    /// <summary>
    /// Accepts a pending friend request.
    /// </summary>
    /// <param name="userId">The requester's user id, as carried by <see cref="OnFriendRequest"/>.</param>
    public void AcceptFriendRequest(Id userId) => AcceptFriendRequests([userId]);

    /// <summary>Accepts several pending friend requests in one message.</summary>
    /// <remarks>Duplicate ids are collapsed.</remarks>
    /// <param name="userIds">The requesters' user ids.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userIds"/> is empty.</exception>
    public void AcceptFriendRequests(IEnumerable<Id> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        _application.Invoke<FriendRequestIdsRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestAccept,
            new FriendRequestIdsRequest(userIds.ToArray()),
            Ct);
    }

    /// <summary>Declines one pending friend request.</summary>
    /// <param name="userId">The requester's user id.</param>
    public void DeclineFriendRequest(Id userId) => DeclineFriendRequests([userId]);

    /// <summary>
    /// Declines several pending friend requests in one message.
    /// </summary>
    /// <remarks>Duplicate ids are collapsed.</remarks>
    /// <param name="userIds">The requesters' user ids.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userIds"/> is empty.</exception>
    public void DeclineFriendRequests(IEnumerable<Id> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        Id[] ids = userIds.Distinct().ToArray();
        _application.Invoke<FriendRequestDeclineRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestDecline,
            new FriendRequestDeclineRequest(ids),
            Ct);
    }

    /// <summary>
    /// Declines every pending friend request at once.
    /// </summary>
    /// <remarks>
    /// It uses the protocol's "decline all" flag rather than an id list, so no request has to be
    /// known in advance.
    /// </remarks>
    public void DeclineAllFriendRequests() =>
        _application.Invoke<FriendRequestsDeclineAllRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRequestsDeclineAll,
            new FriendRequestsDeclineAllRequest(),
            Ct);

    /// <summary>
    /// Kicks a user out of the current room.
    /// </summary>
    /// <remarks>
    /// It requires room rights or staff permissions; the server ignores it otherwise.
    /// </remarks>
    /// <param name="userId">The target user's account id, not their room index.</param>
    public void Kick(Id userId) =>
        _application.Invoke<RoomModerationTargetRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationKick,
            new RoomModerationTargetRequest(userId),
            Ct);

    /// <summary>
    /// Mutes a user in the current room for a number of minutes.
    /// </summary>
    /// <remarks>
    /// It requires rights, and the room's "who can mute" setting must allow it.
    /// </remarks>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="minutes">The mute duration in minutes, from 0 to 1440.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minutes"/> is below 0 or above 1440.</exception>
    public void Mute(Id userId, int minutes) =>
        _application.Invoke<RoomModerationMuteRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationMute,
            new RoomModerationMuteRequest(userId, minutes),
            Ct);

    /// <summary>
    /// Bans a user from the current room.
    /// </summary>
    /// <remarks>
    /// It requires ownership or rights, subject to the room's "who can ban" setting.
    /// </remarks>
    /// <param name="userId">The target user's account id.</param>
    /// <param name="length">How long the ban lasts: an hour, a day or permanently.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is not a defined value.</exception>
    public void Ban(Id userId, BanLength length = BanLength.Hour) =>
        _application.Invoke<RoomModerationBanRequest, RoomModerationDispatchResult>(
            ApplicationMemberIds.RoomModerationBan,
            new RoomModerationBanRequest(userId, length),
            Ct);

    /// <summary>
    /// Grants room rights to a user who is in the current room.
    /// </summary>
    /// <remarks>The local user must own the room.</remarks>
    /// <param name="userId">The target user's account id.</param>
    public void GiveRights(Id userId) =>
        _application.Invoke<RoomRightsGrantRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPeopleRightsGrant,
            new RoomRightsGrantRequest(userId),
            Ct);

    /// <summary>
    /// Revokes a user's room rights.
    /// </summary>
    /// <remarks>
    /// The local user must own the room. The raw <c>RemoveRights</c> message is sent with a list
    /// holding the one id.
    /// </remarks>
    /// <param name="userId">The target user's account id.</param>
    public void RemoveRights(Id userId) => SendIds(Msg.Out.RemoveRights, userId);

    /// <summary>
    /// Answers a doorbell for a locked room by letting the waiting user in or turning them away.
    /// </summary>
    /// <param name="name">The waiting user's name, as reported by the doorbell event.</param>
    /// <param name="allow"><see langword="true"/> to let them in; <see langword="false"/> to refuse.</param>
    public void LetIn(string name, bool allow = true) =>
        _application.Invoke<RoomDoorbellAnswerRequest, RoomControlDispatchResult>(
            ApplicationMemberIds.RoomDoorbellAnswer,
            new RoomDoorbellAnswerRequest(name, allow),
            Ct);

    /// <summary>Removes a user from the friend list.</summary>
    /// <param name="userId">The friend's user id.</param>
    public void RemoveFriend(Id userId) =>
        _application.Invoke<FriendsRemoveRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendsRemove,
            new FriendsRemoveRequest([userId]),
            Ct);

    /// <summary>Removes a friend from the friend list.</summary>
    /// <param name="friend">The friend to remove; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="friend"/> is <see langword="null"/>.</exception>
    public void RemoveFriend(Friend friend)
    {
        ArgumentNullException.ThrowIfNull(friend);
        RemoveFriend(friend.Id);
    }

    /// <summary>
    /// Gives a pet in the current room a respect.
    /// </summary>
    /// <remarks>
    /// The daily respect allowance is enforced by the server and its exhaustion is not reported
    /// here.
    /// </remarks>
    /// <param name="petId">The pet's id.</param>
    public void RespectPet(Id petId) =>
        _application.Invoke<RoomPetRespectRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPetRespect,
            new RoomPetRespectRequest(petId),
            Ct);

    /// <summary>Gives a pet in the current room a respect, which is what raises its happiness.</summary>
    /// <param name="pet">The pet; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pet"/> is <see langword="null"/>.</exception>
    public void RespectPet(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        RespectPet(pet.Id);
    }

    /// <summary>
    /// Adds a user to the ignore list by account id.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    public void Ignore(Id userId)
    {
        _application.Invoke<ProfileUserRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileIgnoreAddById,
            new ProfileUserRequest(userId),
            Ct);
    }

    /// <summary>
    /// Adds a user to the ignore list by name.
    /// </summary>
    /// <remarks>
    /// The name is resolved to an account id from the users in the current room first, then from
    /// the friend list.
    /// </remarks>
    /// <param name="name">The target user's name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the name is neither in the current room nor on the friend list, so it cannot be resolved
    /// to an id.
    /// </exception>
    public void Ignore(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Ignore(ResolveUserId(name));
    }

    /// <summary>Adds a user in the room to the ignore list.</summary>
    /// <param name="user">The user; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    public void Ignore(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        Ignore(user.Id);
    }

    /// <summary>
    /// Removes a user from the ignore list by account id.
    /// </summary>
    /// <param name="userId">The target user's account id.</param>
    public void Unignore(Id userId)
    {
        string identity = ((long)userId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _application.Invoke<ProfileIgnoreRemoveRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileIgnoreRemove,
            new ProfileIgnoreRemoveRequest(ProfileIdentityKind.Id, identity),
            Ct);
    }

    /// <summary>
    /// Removes a user from the ignore list by name.
    /// </summary>
    /// <remarks>
    /// The name is resolved to an account id from the users in the current room first, then from
    /// the friend list.
    /// </remarks>
    /// <param name="name">The target user's name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the name is neither in the current room nor on the friend list, so it cannot be resolved
    /// to an id.
    /// </exception>
    public void Unignore(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Unignore(ResolveUserId(name));
    }

    /// <summary>Removes a user in the room from the ignore list.</summary>
    /// <param name="user">The user; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    public void Unignore(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        Unignore(user.Id);
    }

    /// <summary>
    /// Mounts or dismounts a rideable pet in the current room.
    /// </summary>
    /// <param name="petId">The pet's id.</param>
    /// <param name="mount"><see langword="true"/> to get on; <see langword="false"/> to get off.</param>
    public void MountPet(Id petId, bool mount = true) =>
        _application.Invoke<RoomPetMountRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPetMountSet,
            new RoomPetMountRequest(petId, mount),
            Ct);

    /// <summary>Dismounts a pet.</summary>
    /// <remarks>Equivalent to <c>MountPet(petId, false)</c>.</remarks>
    /// <param name="petId">The pet's id.</param>
    public void DismountPet(Id petId) => MountPet(petId, false);

    /// <summary>Mounts or dismounts a rideable pet in the current room.</summary>
    /// <param name="pet">The pet; only its id is used.</param>
    /// <param name="mount"><see langword="true"/> to get on; <see langword="false"/> to get off.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pet"/> is <see langword="null"/>.</exception>
    public void MountPet(Pet pet, bool mount = true)
    {
        ArgumentNullException.ThrowIfNull(pet);
        MountPet(pet.Id, mount);
    }

    /// <summary>Dismounts a pet in the current room.</summary>
    /// <remarks>Equivalent to <c>MountPet(pet, false)</c>.</remarks>
    /// <param name="pet">The pet; only its id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pet"/> is <see langword="null"/>.</exception>
    public void DismountPet(Pet pet) => MountPet(pet, false);

    /// <summary>
    /// Removes a member from a group.
    /// </summary>
    /// <remarks>It requires an administrator rank in that group.</remarks>
    /// <param name="groupId">The group id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <param name="blockRejoin">
    /// <see langword="true"/> to also bar the member from applying again; otherwise,
    /// <see langword="false"/>.
    /// </param>
    public void KickGroupMember(Id groupId, Id userId, bool blockRejoin = false) =>
        _application.Invoke<GroupMemberKickRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipKick,
            new GroupMemberKickRequest(groupId, userId, blockRejoin),
            Ct);

    /// <summary>
    /// Leaves a group.
    /// </summary>
    /// <remarks>
    /// There is no dedicated message for leaving: the client kicks the local user out of the
    /// group, so the call forwards to <see cref="KickGroupMember"/> with the own account id.
    /// </remarks>
    /// <param name="groupId">The id of the group to leave.</param>
    /// <exception cref="InvalidOperationException">Thrown when the user's data has not been received yet.</exception>
    public void LeaveGroup(Id groupId) =>
        KickGroupMember(
            groupId,
            Profile.Identity?.Id ?? throw new InvalidOperationException("The user's data has not been loaded."));

    /// <summary>
    /// Approves a pending membership request.
    /// </summary>
    /// <remarks>It requires an administrator rank in that group.</remarks>
    /// <param name="groupId">The group id.</param>
    /// <param name="userId">The applicant's user id.</param>
    public void ApproveGroupMember(Id groupId, Id userId) =>
        _application.Invoke<GroupMemberRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipApprove,
            new GroupMemberRequest(groupId, userId),
            Ct);

    /// <summary>
    /// Rejects a pending membership request.
    /// </summary>
    /// <remarks>It requires an administrator rank in that group.</remarks>
    /// <param name="groupId">The group id.</param>
    /// <param name="userId">The applicant's user id.</param>
    public void RejectGroupMember(Id groupId, Id userId) =>
        _application.Invoke<GroupMemberRequest, GroupMembershipDispatchResult>(
            ApplicationMemberIds.GroupMembershipReject,
            new GroupMemberRequest(groupId, userId),
            Ct);

    /// <summary>
    /// Makes a group the favorite one, so its badge is shown next to the avatar.
    /// </summary>
    /// <remarks>The account must be a member.</remarks>
    /// <param name="groupId">The group id.</param>
    public void SetFavouriteGroup(Id groupId) =>
        _application.Invoke<ProfileFavoriteGroupRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileFavoriteGroupSelect,
            new ProfileFavoriteGroupRequest(groupId),
            Ct);

    /// <summary>Clears the favorite group, hiding its badge again.</summary>
    /// <param name="groupId">The group id currently marked as favorite.</param>
    public void UnsetFavouriteGroup(Id groupId) =>
        _application.Invoke<ProfileFavoriteGroupRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileFavoriteGroupDeselect,
            new ProfileFavoriteGroupRequest(groupId),
            Ct);

    /// <summary>
    /// Places a floor item from the inventory into the room.
    /// </summary>
    /// <remarks>
    /// The item is checked against the loaded furni inventory before anything is sent. The
    /// server still requires room rights and refuses a blocked tile silently.
    /// </remarks>
    /// <param name="itemId">The inventory item id, not a room item id.</param>
    /// <param name="x">The target tile's X coordinate.</param>
    /// <param name="y">The target tile's Y coordinate.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="itemId"/> is 0, <paramref name="x"/> or <paramref name="y"/> is negative,
    /// or <paramref name="direction"/> is outside 0 to 7.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the furni inventory is not loaded and current, or it holds no floor item
    /// with <paramref name="itemId"/>.
    /// </exception>
    public void PlaceFloorItem(Id itemId, int x, int y, int direction = 0) =>
        _application.Invoke<RoomPlacementFloorPlaceRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementFloorPlace,
            new RoomPlacementFloorPlaceRequest(
                itemId,
                new RoomPlacementFloorPosition(x, y, direction)),
            Ct);

    /// <summary>Places a floor item from the inventory at a tile, as <see cref="PlaceFloorItem(Id, int, int, int)"/> does.</summary>
    /// <param name="itemId">The inventory item id, not a room item id.</param>
    /// <param name="location">The target tile.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="itemId"/> is 0, a coordinate is negative, or <paramref name="direction"/> is
    /// outside 0 to 7.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the furni inventory is not loaded and current, or it holds no floor item
    /// with <paramref name="itemId"/>.
    /// </exception>
    public void PlaceFloorItem(Id itemId, Point location, int direction = 0) =>
        PlaceFloorItem(itemId, location.X, location.Y, direction);

    /// <summary>Places a floor item from the inventory at a tile.</summary>
    /// <param name="item">The inventory item to place; it must be a floor item.</param>
    /// <param name="location">The target tile.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a coordinate is negative or <paramref name="direction"/> is outside 0 to 7.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inventory item is not a floor item, no room is ready, or the furni inventory is not
    /// loaded and current or does not hold the item.
    /// </exception>
    public void PlaceFloorItem(InventoryItem item, Point location, int direction = 0)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsFloorItem)
            throw new InvalidOperationException("The inventory item is not a floor item.");
        PlaceFloorItem(item.ItemId, location, direction);
    }

    /// <summary>
    /// Places a wall item from the inventory onto a wall.
    /// </summary>
    /// <remarks>
    /// The item is checked against the loaded furni inventory before anything is sent. The
    /// server still requires room rights.
    /// </remarks>
    /// <param name="itemId">The inventory item id.</param>
    /// <param name="wallLocation">
    /// The wall position in the client's notation, <c>":w=x,y l=x,y direction"</c>, for example
    /// <c>":w=2,3 l=5,20 l"</c>.
    /// </param>
    /// <exception cref="FormatException">Thrown when <paramref name="wallLocation"/> is not a valid wall position.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the furni inventory is not loaded and current, or it holds no wall item
    /// with <paramref name="itemId"/>.
    /// </exception>
    public void PlaceWallItem(Id itemId, string wallLocation) =>
        PlaceWallItem(itemId, WallLocation.ParseString(wallLocation));

    /// <summary>Places a wall item from the inventory at a wall position.</summary>
    /// <param name="itemId">The inventory item id.</param>
    /// <param name="location">The wall position.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="location"/> names neither the left nor the right wall.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the furni inventory is not loaded and current, or it holds no wall item
    /// with <paramref name="itemId"/>.
    /// </exception>
    public void PlaceWallItem(Id itemId, WallLocation location) =>
        _application.Invoke<RoomPlacementWallPlaceRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementWallPlace,
            new RoomPlacementWallPlaceRequest(itemId, PlacementWallPosition(location)),
            Ct);

    /// <summary>Places a wall item from the inventory at a wall position.</summary>
    /// <param name="item">The inventory item to place; it must be a wall item.</param>
    /// <param name="location">The wall position.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="location"/> names neither the left nor the right wall.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inventory item is not a wall item, no room is ready, or the furni inventory is not
    /// loaded and current or does not hold the item.
    /// </exception>
    public void PlaceWallItem(InventoryItem item, WallLocation location)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsWallItem)
            throw new InvalidOperationException("The inventory item is not a wall item.");
        PlaceWallItem(item.ItemId, location);
    }

    /// <summary>
    /// Moves a wall item that is already hanging in the room to a new wall position.
    /// </summary>
    /// <remarks>It requires room rights.</remarks>
    /// <param name="itemId">The item's room id.</param>
    /// <param name="wallLocation">The new wall position, in the <c>":w=x,y l=x,y direction"</c> notation.</param>
    /// <exception cref="FormatException">Thrown when <paramref name="wallLocation"/> is not a valid wall position.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the wall item is not in it.</exception>
    public void MoveWallItem(Id itemId, string wallLocation) =>
        MoveWallItem(itemId, WallLocation.ParseString(wallLocation));

    /// <summary>Moves a wall item that is already hanging in the room to a new wall position.</summary>
    /// <remarks>It requires room rights.</remarks>
    /// <param name="itemId">The item's room id.</param>
    /// <param name="location">The new wall position.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="location"/> names neither the left nor the right wall.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the wall item is not in it.</exception>
    public void MoveWallItem(Id itemId, WallLocation location) =>
        _application.Invoke<RoomPlacementWallMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementWallMove,
            new RoomPlacementWallMoveRequest(itemId, PlacementWallPosition(location)),
            Ct);

    /// <summary>
    /// Places a sticky note (post-it) from the inventory onto a wall, with no text.
    /// </summary>
    /// <remarks>
    /// A wall position written as text converts to <see cref="WallLocation"/>, in the
    /// <c>":w=x,y l=x,y direction"</c> notation.
    /// </remarks>
    /// <param name="itemId">The inventory item id of the sticky pad.</param>
    /// <param name="location">The wall position.</param>
    public void PlaceSticky(Id itemId, WallLocation location) =>
        _application.Invoke<RoomPostItPlaceRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemPostItPlace,
            new RoomPostItPlaceRequest(itemId, location.ToString()),
            Ct);

    /// <summary>Places a blank sticky note from the inventory onto a wall.</summary>
    /// <param name="item">The inventory item; it must be in the sticky note category (5).</param>
    /// <param name="location">The wall position.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the inventory item is not a sticky note.</exception>
    public void PlaceSticky(InventoryItem item, WallLocation location)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Category != 5)
            throw new InvalidOperationException("The inventory item is not a sticky note.");
        PlaceSticky(item.ItemId, location);
    }

    /// <summary>
    /// Places a sticky note on a wall together with its color and initial text.
    /// </summary>
    /// <remarks>
    /// It sends the Flash <c>AddSpamWallPostIt</c> message, which the client uses for a sticky pole.
    /// </remarks>
    /// <param name="itemId">The inventory item id of the sticky pad.</param>
    /// <param name="location">The wall position.</param>
    /// <param name="color">The note color as a hexadecimal string, for example <c>"FFFF33"</c>.</param>
    /// <param name="text">The note text.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="color"/> or <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    public void PlaceStickyWithPole(Id itemId, WallLocation location, string color, string text) =>
        _application.Invoke<RoomPostItAddRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemPostItAdd,
            new RoomPostItAddRequest(itemId, location.ToString(), color, text),
            Ct);

    /// <summary>Places a sticky note from the inventory together with its color and initial text.</summary>
    /// <param name="item">The inventory item; only its item id is used.</param>
    /// <param name="location">The wall position.</param>
    /// <param name="color">The note color as a hexadecimal string, for example <c>"FFFF33"</c>.</param>
    /// <param name="text">The note text.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="item"/>, <paramref name="color"/> or <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    public void PlaceStickyWithPole(InventoryItem item, WallLocation location, string color, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        PlaceStickyWithPole(item.ItemId, location, color, text);
    }

    /// <summary>Moves a wall item to a new wall position.</summary>
    /// <remarks>
    /// Unlike <see cref="MoveWallItem(Id, string)"/>, the item's current location is sent along
    /// and checked, so the move fails if the item has been moved in the meantime.
    /// </remarks>
    /// <param name="item">The placed wall item.</param>
    /// <param name="wallLocation">The new wall position, in the <c>":w=x,y l=x,y direction"</c> notation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="wallLocation"/> is not a valid wall position.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the wall item is not in it, or it is no longer at the location
    /// <paramref name="item"/> holds.
    /// </exception>
    public void MoveWallItem(WallItem item, string wallLocation) =>
        MoveWallItem(item, WallLocation.ParseString(wallLocation));

    /// <summary>Moves a wall item to a new wall position.</summary>
    /// <remarks>
    /// Unlike <see cref="MoveWallItem(Id, WallLocation)"/>, the item's current location is sent along
    /// and checked, so the move fails if the item has been moved in the meantime.
    /// </remarks>
    /// <param name="item">The placed wall item.</param>
    /// <param name="location">The new wall position.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="location"/> names neither the left nor the right wall.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the wall item is not in it, or it is no longer at the location
    /// <paramref name="item"/> holds.
    /// </exception>
    public void MoveWallItem(WallItem item, WallLocation location)
    {
        ArgumentNullException.ThrowIfNull(item);
        _application.Invoke<RoomPlacementWallMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementWallMove,
            new RoomPlacementWallMoveRequest(
                item.Id,
                PlacementWallPosition(location),
                PlacementWallPosition(item.Location)),
            Ct);
    }

    /// <summary>
    /// Rotates a placed floor item without moving it.
    /// </summary>
    /// <remarks>
    /// It sends the item's current tile with the new rotation, along with its current position
    /// as the expected source.
    /// </remarks>
    /// <param name="item">The placed item; its current X and Y are reused.</param>
    /// <param name="direction">The new rotation, in eighths of a turn (0 to 7).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is outside 0 to 7.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no room is ready, the item is not in it, or it is no longer at the position
    /// <paramref name="item"/> holds.
    /// </exception>
    public void RotateFloorItem(FloorItem item, int direction) =>
        _application.Invoke<RoomPlacementFloorMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementFloorMove,
            new RoomPlacementFloorMoveRequest(
                item.Id,
                new RoomPlacementFloorPosition(item.X, item.Y, direction),
                new RoomPlacementFloorPosition(item.X, item.Y, item.Direction)),
            Ct);

    /// <summary>
    /// Picks a floor item up into the inventory.
    /// </summary>
    /// <remarks>It requires room rights or ownership of the item.</remarks>
    /// <param name="item">The floor item to pick up.</param>
    /// <param name="confirmed">
    /// <see langword="true"/> to acknowledge the hotel's remove confirmation prompt; otherwise,
    /// <see langword="false"/>. The flag is part of the Flash pickup message.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the item is not in it.</exception>
    public void PickupFurni(FloorItem item, bool confirmed = false) =>
    SendPickup(2, item.Id, confirmed);

    /// <summary>
    /// Picks a wall item up into the inventory.
    /// </summary>
    /// <remarks>It requires room rights or ownership of the item.</remarks>
    /// <param name="item">The wall item to pick up.</param>
    /// <param name="confirmed">
    /// <see langword="true"/> to acknowledge the hotel's remove confirmation prompt; otherwise,
    /// <see langword="false"/>. The flag is part of the Flash pickup message.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the item is not in it.</exception>
    public void PickupFurni(WallItem item, bool confirmed = false) =>
        SendPickup(1, item.Id, confirmed);

    /// <summary>
    /// Picks a room item up into the inventory, picking the floor or wall message from the item's
    /// runtime type.
    /// </summary>
    /// <remarks>It requires room rights or ownership of the item.</remarks>
    /// <param name="item">The item to pick up.</param>
    /// <param name="confirmed">
    /// <see langword="true"/> to acknowledge the hotel's remove confirmation prompt; otherwise,
    /// <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the item is neither a floor nor a wall item.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the item is not in it.</exception>
    public void PickupFurni(Furni item, bool confirmed = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is FloorItem floor_item)
            PickupFurni(floor_item, confirmed);
        else if (item is WallItem wall_item)
            PickupFurni(wall_item, confirmed);
        else
            throw new ArgumentException("Unsupported furniture type.", nameof(item));
    }

    /// <summary>Picks a floor item up into the inventory, by id.</summary>
    /// <param name="itemId">The floor item's room id.</param>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the item is not in it.</exception>
    public void PickupFloorItem(Id itemId) => SendPickup(2, itemId, false);

    /// <summary>Picks a wall item up into the inventory, by id.</summary>
    /// <param name="itemId">The wall item's room id.</param>
    /// <exception cref="InvalidOperationException">Thrown when no room is ready, or the item is not in it.</exception>
    public void PickupWallItem(Id itemId) => SendPickup(1, itemId, false);

    private void SendPickup(int category, Id itemId, bool confirmed) =>
        _application.Invoke<RoomPlacementPickupRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementPickup,
            new RoomPlacementPickupRequest(
                itemId,
                category == 2 ? RoomPlacementItemKind.Floor : RoomPlacementItemKind.Wall,
                confirmed),
            Ct);

    private static RoomPlacementWallPosition PlacementWallPosition(WallLocation value) =>
        new(
            value.Wall.X,
            value.Wall.Y,
            value.Offset.X,
            value.Offset.Y,
            value.Orientation.ToString());

    /// <summary>
    /// Buys a marketplace offer.
    /// </summary>
    /// <remarks>
    /// The purchase is refused silently when the offer has already been taken or the account
    /// cannot afford it.
    /// </remarks>
    /// <param name="offerId">The marketplace offer id from a search result.</param>
    public void BuyMarketplaceOffer(Id offerId) =>
        _application.Invoke<MarketplaceBuySendRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceOfferBuySend,
            new MarketplaceBuySendRequest(offerId),
            Ct);

    /// <summary>
    /// Withdraws one of the local user's own marketplace offers, returning the item to the
    /// inventory.
    /// </summary>
    /// <param name="offerId">The offer id from <see cref="GetMyMarketplaceOffers"/>.</param>
    public void CancelMarketplaceOffer(Id offerId) =>
        _application.Invoke<MarketplaceCancelSendRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceOfferCancelSend,
            new MarketplaceCancelSendRequest(offerId),
            Ct);

    /// <summary>
    /// Collects the credits earned from sold marketplace offers into the wallet.
    /// </summary>
    /// <remarks>
    /// It sends the request and returns; the new balance arrives as a currency update.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session.</exception>
    public void RedeemMarketplaceCredits() =>
        _application.Invoke<MarketplaceCommandRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceCreditsRedeem,
            new MarketplaceCommandRequest(),
            Ct);

    /// <summary>
    /// Activates an avatar effect that is owned but not yet started, which begins consuming its
    /// duration.
    /// </summary>
    /// <remarks>
    /// Use <see cref="EnableEffect"/> to wear an effect that is already activated.
    /// </remarks>
    /// <param name="effectId">The effect id; <see cref="EffectName"/> resolves it to a name.</param>
    public void ActivateEffect(int effectId) =>
        _application.Invoke<InventoryAvatarEffectRequest, InventoryDispatchResult>(
            ApplicationMemberIds.InventoryAvatarEffectActivate,
            new InventoryAvatarEffectRequest(effectId),
            Ct);

    /// <summary>
    /// Wears one of the currently activated avatar effects.
    /// </summary>
    /// <param name="effectId">The effect id, or -1 to wear none.</param>
    public void EnableEffect(int effectId) =>
        _application.Invoke<RoomAvatarEffectRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarEffect,
            new RoomAvatarEffectRequest(effectId),
            Ct);

    /// <summary>Takes off the current avatar effect.</summary>
    /// <remarks>Equivalent to <c>EnableEffect(-1)</c>.</remarks>
    public void DisableEffect() => EnableEffect(-1);

    /// <summary>
    /// Stores a look in a wardrobe slot, overwriting whatever was in it.
    /// </summary>
    /// <param name="slot">The wardrobe slot number, as used by <see cref="GetWardrobe"/>.</param>
    /// <param name="figure">The figure string to store.</param>
    /// <param name="gender">
    /// The gender: <c>"M"</c>, <c>"F"</c> or <c>"U"</c>, or <c>"male"</c>, <c>"female"</c> or
    /// <c>"unisex"</c>, in any case.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="figure"/> or <paramref name="gender"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="slot"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="gender"/> is not a known gender, or <paramref name="figure"/> is empty or
    /// whitespace.
    /// </exception>
    public void SaveOutfit(int slot, string figure, string gender) =>
        _application.Invoke<ProfileOutfitSaveRequest, ProfileDispatchResult>(
            ApplicationMemberIds.ProfileWardrobeOutfitSave,
            new ProfileOutfitSaveRequest(slot, figure, gender),
            Ct);

    /// <summary>
    /// Throws a dice furni, making it roll to a new value.
    /// </summary>
    /// <remarks>
    /// The result arrives later as an item data change; register a handler with
    /// <see cref="OnFloorItemDataChanged"/> to read it.
    /// </remarks>
    /// <param name="itemId">The dice's room item id.</param>
    public void ThrowDice(Id itemId) =>
        _application.Invoke<RoomDiceRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemDiceThrow,
            new RoomDiceRequest(itemId),
            Ct);

    /// <summary>
    /// Clears a dice furni back to its blank face.
    /// </summary>
    /// <param name="itemId">The dice's room item id.</param>
    public void DiceOff(Id itemId) =>
        _application.Invoke<RoomDiceRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemDiceClear,
            new RoomDiceRequest(itemId),
            Ct);

    /// <summary>
    /// Creates a new room owned by the local user.
    /// </summary>
    /// <remarks>
    /// Nothing is returned; the new room shows up in the navigator's own rooms view, which
    /// <see cref="GetMyRooms"/> reads.
    /// </remarks>
    /// <param name="name">The room name.</param>
    /// <param name="description">The room description.</param>
    /// <param name="model">The floor plan model name, for example <c>"model_a"</c>.</param>
    /// <param name="category">The navigator category id the room is filed under.</param>
    /// <param name="maxVisitors">The visitor cap; the server clamps it to the values it allows.</param>
    /// <param name="tradeMode">The trading policy: 0 disabled, 1 rights holders only, 2 everyone.</param>
    public void CreateRoom(string name, string description, string model, int category, int maxVisitors, int tradeMode = 0) =>
        _application.Invoke<NavigatorRoomCreateInput, NavigatorRoomOperationResult>(
            ApplicationMemberIds.NavigatorRoomCreate,
            new NavigatorRoomCreateInput(name, description, model, category, maxVisitors, tradeMode),
            Ct);

    /// <summary>
    /// Permanently deletes a room owned by the local user, together with everything placed in
    /// it.
    /// </summary>
    /// <remarks>There is no confirmation step.</remarks>
    /// <param name="roomId">The room id.</param>
    public void DeleteRoom(Id roomId) =>
        _application.Invoke<NavigatorRoomDeleteInput, NavigatorRoomOperationResult>(
            ApplicationMemberIds.NavigatorRoomDelete,
            new NavigatorRoomDeleteInput(roomId),
            Ct);

    /// <summary>
    /// Sets the account's home room.
    /// </summary>
    /// <param name="roomId">The room id, or 0 to clear the home room.</param>
    public void SetHomeRoom(Id roomId) =>
        _application.Invoke<NavigatorHomeRoomSetInput, NavigatorRoomOperationResult>(
            ApplicationMemberIds.NavigatorHomeRoomSet,
            new NavigatorHomeRoomSetInput(roomId),
            Ct);

    /// <summary>
    /// Adds a room to or removes it from the staff picks.
    /// </summary>
    /// <remarks>
    /// It requires staff permissions; the server ignores it for ordinary accounts.
    /// </remarks>
    /// <param name="roomId">The room id.</param>
    /// <param name="pick"><see langword="true"/> to pick; <see langword="false"/> to unpick.</param>
    public void ToggleStaffPick(Id roomId, bool pick = true) =>
        _application.Invoke<RoomStaffPickRequest, RoomControlDispatchResult>(
            ApplicationMemberIds.RoomStaffPickSet,
            new RoomStaffPickRequest(roomId, pick),
            Ct);

    /// <summary>
    /// Rates the current room.
    /// </summary>
    /// <remarks>
    /// Each user may rate a given room once per visit; the server ignores further ratings.
    /// </remarks>
    /// <param name="rating">The signed rating value. The current client uses 1 for a positive rating.</param>
    public void RateRoom(int rating) =>
        _application.Invoke<RoomRatingRequest, RoomControlDispatchResult>(
            ApplicationMemberIds.RoomRatingSubmit,
            new RoomRatingRequest(rating),
            Ct);

    /// <summary>
    /// Removes a pet from the current room and returns it to the owner's inventory.
    /// </summary>
    /// <remarks>It requires ownership of the pet or room rights.</remarks>
    /// <param name="petId">The pet's id.</param>
    public void RemovePet(Id petId) =>
        _application.Invoke<RoomPetRemoveRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomPetRemove,
            new RoomPetRemoveRequest(petId),
            Ct);

    /// <summary>
    /// Removes a bot from the current room and returns it to the inventory.
    /// </summary>
    /// <remarks>It requires ownership of the bot or room rights.</remarks>
    /// <param name="botId">The bot's id.</param>
    public void RemoveBot(Id botId) =>
        _application.Invoke<RoomBotRemoveRequest, RoomPeopleDispatchResult>(
            ApplicationMemberIds.RoomBotRemove,
            new RoomBotRemoveRequest(botId),
            Ct);

    private Id ResolveUserId(string name)
    {
        User? user = GetUser(name);
        if (user is not null)
            return user.Id;
        Friend? friend = FindFriend(name);
        if (friend is not null)
            return friend.Id;
        throw new InvalidOperationException($"Cannot resolve user '{name}' to an identifier for this client layout.");
    }

    private void SendIds(string name, params Id[] ids)
    {

        using Packet packet = NewPacket(MessageDirection.Out, name);
        PacketWriter writer = packet.Writer();
        writer.WriteLength((Length)ids.Length);
        foreach (Id id in ids)
            writer.WriteId(id);
        _interceptor.Send(packet);
    }

    /// <summary>
    /// Shows a chat bubble above the local avatar on the local screen only.
    /// </summary>
    /// <remarks>
    /// The bubble is a whisper injected into the game client. Nothing is sent to the server and
    /// nobody else sees it. While the local avatar is not in a room the client shows nothing.
    /// </remarks>
    /// <param name="message">The bubble text.</param>
    /// <param name="bubble">The chat bubble style id; 30 is the neutral gray bubble.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    public void ShowBubble(string message, int bubble = 30) => ShowBubble(SelfAvatar?.Index ?? -1, message, bubble);

    /// <summary>
    /// Shows a chat bubble above an avatar in the room on the local screen only.
    /// </summary>
    /// <remarks>
    /// The bubble is a whisper injected into the game client. Nothing is sent to the server and
    /// nobody else sees it.
    /// </remarks>
    /// <param name="avatar">The user, pet or bot the bubble appears above.</param>
    /// <param name="message">The bubble text.</param>
    /// <param name="bubble">The chat bubble style id; 30 is the neutral gray bubble.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="avatar"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    public void ShowBubble(Avatar avatar, string message, int bubble = 30)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        ShowBubble(avatar.Index, message, bubble);
    }

    private void ShowBubble(int index, string message, int bubble)
    {
        ArgumentNullException.ThrowIfNull(message);
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
                null,
                null));
    }
}
