using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the local account profile state.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileState"/>. No message is sent to the hotel.
/// </remarks>
public sealed record ProfileStateRequest;

/// <summary>
/// Represents a request to load the local account profile from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileRefresh"/>. The profile is requested even when it is
/// already loaded, the request is sent at most twice within the timeout, and the refresh completes
/// once the matching response is stored.
/// </remarks>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record ProfileRefreshRequest(int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents the stored account data of the local user.
/// </summary>
/// <param name="Id">The id of the local user.</param>
/// <param name="Name">The user's name.</param>
/// <param name="Figure">The user's figure string.</param>
/// <param name="Gender">The user's gender.</param>
/// <param name="Motto">The user's motto.</param>
/// <param name="RealName">The user's real name, empty unless the hotel discloses it.</param>
/// <param name="DirectMail">The direct mail flag the hotel sends for the account.</param>
/// <param name="RespectTotal">The total number of respects the user has received.</param>
/// <param name="RespectLeft">The number of respects the user can still give to users today.</param>
/// <param name="PetRespectLeft">The number of respects the user can still give to pets today.</param>
/// <param name="StreamPublishingAllowed">Whether the account may publish to streams.</param>
/// <param name="LastAccessDate">The date of the user's last access as the hotel formats it.</param>
/// <param name="IsNameChangeable">Whether the user may change their name.</param>
/// <param name="IsSafetyLocked">Whether the account is safety locked.</param>
/// <param name="IsTradeLocked">Whether the account is trade locked.</param>
/// <param name="NameColor">The color of the user's name as the hotel sends it.</param>
/// <param name="RespectReplenishesLeft">The number of respect replenishes the user has left.</param>
/// <param name="MaxRespectPerDay">The maximum number of respects the user can give per day.</param>
/// <param name="TrailingFields">
/// The number of the four optional trailing fields the hotel sent, from 0 to 4, counted in the order
/// <paramref name="IsTradeLocked"/>, <paramref name="NameColor"/>, <paramref name="RespectReplenishesLeft"/>
/// and <paramref name="MaxRespectPerDay"/>.
/// </param>
public sealed record ProfileIdentitySnapshot(
    Id Id,
    string Name,
    string Figure,
    Gender Gender,
    string Motto,
    string RealName,
    bool DirectMail,
    int RespectTotal,
    int RespectLeft,
    int PetRespectLeft,
    bool StreamPublishingAllowed,
    string LastAccessDate,
    bool IsNameChangeable,
    bool IsSafetyLocked,
    bool IsTradeLocked,
    string NameColor,
    int RespectReplenishesLeft,
    int MaxRespectPerDay,
    int TrailingFields);

/// <summary>
/// Represents a summary of the local account profile state.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.ProfileState"/> and <see cref="ApplicationMemberIds.ProfileRefresh"/>,
/// and carried by <see cref="ProfileChanged"/>.
/// </remarks>
/// <param name="Generation">The state generation of the hotel session the profile state belongs to.</param>
/// <param name="Revision">The profile state revision, increased by every stored profile change and every reset.</param>
/// <param name="Connected">Whether the profile state belongs to the active hotel session.</param>
/// <param name="Identity">The local account data, or <see langword="null"/> when the profile was not loaded for the session.</param>
/// <param name="BlockListLoaded">Whether the block list was received for the session.</param>
/// <param name="BlockedUserCount">The number of blocked users.</param>
/// <param name="IgnoreListLoaded">Whether the ignore list was received for the session.</param>
/// <param name="IgnoredUserCount">The number of ignored users.</param>
/// <param name="FigureSetsLoaded">Whether the owned figure sets were received for the session.</param>
/// <param name="FigureSetCount">The number of owned figure sets.</param>
/// <param name="BoundFurnitureNameCount">The number of bound furniture names received with the figure sets.</param>
/// <param name="SanctionsLoaded">Whether the account sanction status was received for the session.</param>
/// <param name="SanctionsKind">
/// The kind of the stored sanction status, or <see langword="null"/> when <paramref name="SanctionsLoaded"/> is
/// <see langword="false"/>.
/// </param>
public sealed record ProfileStateView(
    long Generation,
    long Revision,
    bool Connected,
    ProfileIdentitySnapshot? Identity,
    bool BlockListLoaded,
    int BlockedUserCount,
    bool IgnoreListLoaded,
    int IgnoredUserCount,
    bool FigureSetsLoaded,
    int FigureSetCount,
    int BoundFurnitureNameCount,
    bool SanctionsLoaded,
    AccountSanctionStatusKind? SanctionsKind);

/// <summary>
/// Represents a request to read a page of the stored blocked or ignored user ids.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileBlocksList"/> and <see cref="ApplicationMemberIds.ProfileIgnoresList"/>.
/// No message is sent to the hotel.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first id to return.</param>
/// <param name="Limit">The maximum number of ids to return, from 1 to 500.</param>
public sealed record ProfileIdPageRequest(
    int Offset = 0,
    int Limit = 200);

/// <summary>
/// Represents a request to load the complete block or ignore list from the hotel and read a page of it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileBlocksRefresh"/> and <see cref="ApplicationMemberIds.ProfileIgnoresRefresh"/>.
/// The request is sent at most twice within the timeout, and the page is read once the matching list is stored.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first id to return.</param>
/// <param name="Limit">The maximum number of ids to return, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record ProfileIdRefreshRequest(
    int Offset = 0,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a page of blocked or ignored user ids.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.ProfileBlocksList"/>, <see cref="ApplicationMemberIds.ProfileBlocksRefresh"/>,
/// <see cref="ApplicationMemberIds.ProfileIgnoresList"/> and <see cref="ApplicationMemberIds.ProfileIgnoresRefresh"/>.
/// Each page is read from the current profile state, so a different <paramref name="Revision"/> between pages
/// means the state changed.
/// </remarks>
/// <param name="Loaded">Whether the list was received for the session.</param>
/// <param name="Generation">The state generation of the hotel session the profile state belongs to.</param>
/// <param name="Revision">The profile state revision the page was read from.</param>
/// <param name="Total">The number of ids in the list.</param>
/// <param name="Offset">The zero-based offset of the first id in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="UserIds">The user ids in the page, without duplicates and in ascending order.</param>
public sealed record ProfileIdPage(
    bool Loaded,
    long Generation,
    long Revision,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<Id> UserIds);

/// <summary>
/// Represents a request to read a page of the stored figure sets and bound furniture names.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileFigureSetsList"/>. The same offset and limit apply to both
/// lists, and no message is sent to the hotel.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first entry to return from each list.</param>
/// <param name="Limit">The maximum number of entries to return from each list, from 1 to 500.</param>
public sealed record ProfileFigureSetsRequest(
    int Offset = 0,
    int Limit = 200);

/// <summary>
/// Represents a page of the owned figure sets and the bound furniture names.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.ProfileFigureSetsList"/>. Each page is read from the current
/// profile state.
/// </remarks>
/// <param name="Loaded">Whether the figure sets were received for the session.</param>
/// <param name="Generation">The state generation of the hotel session the profile state belongs to.</param>
/// <param name="Revision">The profile state revision the page was read from.</param>
/// <param name="Total">The number of owned figure sets.</param>
/// <param name="Offset">The zero-based offset of the first entry in each list.</param>
/// <param name="NextOffset">
/// The offset of the next page of figure sets, or <see langword="null"/> when this is the last page.
/// </param>
/// <param name="FigureSets">The figure sets in the page, in ascending figure set id order.</param>
/// <param name="BoundFurnitureTotal">The number of bound furniture names.</param>
/// <param name="BoundFurnitureNextOffset">
/// The offset of the next page of bound furniture names, or <see langword="null"/> when this is the last page.
/// </param>
/// <param name="BoundFurnitureNames">The bound furniture names in the page, in the order the hotel sent them.</param>
public sealed record ProfileFigureSetsPage(
    bool Loaded,
    long Generation,
    long Revision,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<FigureSetEntry> FigureSets,
    int BoundFurnitureTotal,
    int? BoundFurnitureNextOffset,
    IReadOnlyList<string> BoundFurnitureNames);

/// <summary>
/// Represents a request to read a page of the stored account sanctions.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileSanctionsList"/>. No message is sent to the hotel.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first sanction to return.</param>
/// <param name="Limit">The maximum number of sanctions to return, from 1 to 500.</param>
public sealed record ProfileSanctionsRequest(
    int Offset = 0,
    int Limit = 100);

/// <summary>
/// Represents a request to load the account sanction status from the hotel and read a page of it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileSanctionsRefresh"/>. The request is sent at most twice within
/// the timeout, and the page is read once the matching status is stored.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first sanction to return.</param>
/// <param name="Limit">The maximum number of sanctions to return, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record ProfileSanctionsRefreshRequest(
    int Offset = 0,
    int Limit = 100,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a page of the account sanctions.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.ProfileSanctionsList"/> and <see cref="ApplicationMemberIds.ProfileSanctionsRefresh"/>.
/// Each page is read from the current profile state.
/// </remarks>
/// <param name="Loaded">Whether the sanction status was received for the session.</param>
/// <param name="Generation">The state generation of the hotel session the profile state belongs to.</param>
/// <param name="Revision">The profile state revision the page was read from.</param>
/// <param name="Kind">
/// The kind of the stored sanction status, or <see langword="null"/> when <paramref name="Loaded"/> is
/// <see langword="false"/>.
/// </param>
/// <param name="Total">The number of sanctions in the stored status.</param>
/// <param name="Offset">The zero-based offset of the first sanction in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Sanctions">The sanctions in the page.</param>
public sealed record ProfileSanctionsPage(
    bool Loaded,
    long Generation,
    long Revision,
    AccountSanctionStatusKind? Kind,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<Sanction> Sanctions);

/// <summary>
/// Represents a request to read a page of the local user's saved wardrobe outfits.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileWardrobeGet"/>. Without a snapshot revision the wardrobe is
/// requested from the hotel, the request is sent at most twice within the timeout, the response is not passed
/// on to the game client and a new snapshot is stored. Up to 16 snapshots are retained, and they become
/// unavailable when the profile state resets.
/// </remarks>
/// <param name="Offset">
/// The zero-based offset of the first outfit to return. Must be 0 when <paramref name="SnapshotRevision"/> is
/// <see langword="null"/>.
/// </param>
/// <param name="Limit">The maximum number of outfits to return, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read without contacting the hotel, or <see langword="null"/> to
/// request a new snapshot.
/// </param>
public sealed record ProfileWardrobeRequest(
    int Offset = 0,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a page of saved wardrobe outfits read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.ProfileWardrobeGet"/>.
/// </remarks>
/// <param name="Generation">The state generation of the hotel session the snapshot was created in.</param>
/// <param name="Revision">The profile state revision when the snapshot was created.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, passed back to read the next page.</param>
/// <param name="State">The wardrobe state value sent by the hotel.</param>
/// <param name="Total">The number of outfits in the snapshot.</param>
/// <param name="Offset">The zero-based offset of the first outfit in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Outfits">The outfits in the page.</param>
public sealed record ProfileWardrobePage(
    long Generation,
    long Revision,
    long SnapshotRevision,
    int State,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<WardrobeOutfit> Outfits);

/// <summary>
/// Represents a request that targets one user by id.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileBlockAdd"/>, <see cref="ApplicationMemberIds.ProfileBlockRemove"/>
/// and <see cref="ApplicationMemberIds.ProfileIgnoreAddById"/>.
/// </remarks>
/// <param name="UserId">The id of the user. Must be positive.</param>
public sealed record ProfileUserRequest(Id UserId);

/// <summary>
/// Specifies how <see cref="ProfileIgnoreRemoveRequest.Identity"/> identifies a user.
/// </summary>
public enum ProfileIdentityKind
{
    /// <summary>The user id, written as a positive decimal integer.</summary>
    Id,
    /// <summary>The user name.</summary>
    Name
}

/// <summary>
/// Represents a request to remove a user from the local account's ignore list.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileIgnoreRemove"/>.
/// </remarks>
/// <param name="Kind">How <paramref name="Identity"/> identifies the user.</param>
/// <param name="Identity">
/// The user id as a positive decimal integer or the user name, as selected by <paramref name="Kind"/>. Must not be
/// empty and must be at most 65535 UTF-8 bytes.
/// </param>
public sealed record ProfileIgnoreRemoveRequest(
    ProfileIdentityKind Kind,
    string Identity);

/// <summary>
/// Represents a request to change the local account's motto.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileMottoSet"/>.
/// </remarks>
/// <param name="Motto">The new motto, which may be empty and must be at most 65535 UTF-8 bytes.</param>
public sealed record ProfileMottoSetRequest(string Motto);

/// <summary>
/// Represents a request to change the local account's figure and gender.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileFigureSet"/>.
/// </remarks>
/// <param name="Gender">
/// The gender, given as <c>M</c>, <c>F</c>, <c>U</c>, <c>male</c>, <c>female</c> or <c>unisex</c> in any case.
/// </param>
/// <param name="Figure">The figure string. Must not be empty.</param>
public sealed record ProfileFigureSetRequest(string Gender, string Figure);

/// <summary>
/// Represents a request to save a figure into a wardrobe slot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileWardrobeOutfitSave"/>.
/// </remarks>
/// <param name="SlotId">The wardrobe slot number. Must not be negative.</param>
/// <param name="Figure">The figure string. Must not be empty.</param>
/// <param name="Gender">
/// The gender, given as <c>M</c>, <c>F</c>, <c>U</c>, <c>male</c>, <c>female</c> or <c>unisex</c> in any case.
/// </param>
public sealed record ProfileOutfitSaveRequest(
    int SlotId,
    string Figure,
    string Gender);

/// <summary>
/// Represents a request to select or deselect the local account's favorite group.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.ProfileFavoriteGroupSelect"/> and
/// <see cref="ApplicationMemberIds.ProfileFavoriteGroupDeselect"/>.
/// </remarks>
/// <param name="GroupId">The id of the group. Must be positive.</param>
public sealed record ProfileFavoriteGroupRequest(Id GroupId);

/// <summary>
/// Represents the result of sending a local account change to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.ProfileBlockAdd"/>, <see cref="ApplicationMemberIds.ProfileBlockRemove"/>,
/// <see cref="ApplicationMemberIds.ProfileIgnoreAddById"/>, <see cref="ApplicationMemberIds.ProfileIgnoreRemove"/>,
/// <see cref="ApplicationMemberIds.ProfileMottoSet"/>, <see cref="ApplicationMemberIds.ProfileFigureSet"/>,
/// <see cref="ApplicationMemberIds.ProfileWardrobeOutfitSave"/>, <see cref="ApplicationMemberIds.ProfileFavoriteGroupSelect"/>
/// and <see cref="ApplicationMemberIds.ProfileFavoriteGroupDeselect"/>. The hotel's response is not awaited. Block and
/// ignore results arrive through <see cref="ProfileBlockUpdated"/> and <see cref="ProfileIgnoreUpdated"/>.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="Generation">The state generation of the hotel session the message was sent in.</param>
/// <param name="Revision">The profile state revision captured before the message was sent.</param>
/// <param name="TargetId">
/// The id of the targeted user or group, or <see langword="null"/> when the change has no id target.
/// </param>
/// <param name="TargetName">
/// The name of the targeted user when an ignore entry is removed by name; otherwise, <see langword="null"/>.
/// </param>
/// <param name="SlotId">The wardrobe slot of a saved outfit; otherwise, <see langword="null"/>.</param>
public sealed record ProfileDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    long Generation,
    long Revision,
    Id? TargetId = null,
    string? TargetName = null,
    int? SlotId = null);

/// <summary>
/// Specifies the kind of change reported by <see cref="ProfileChanged"/>.
/// </summary>
public enum ProfileChangeKind
{
    /// <summary>The local account data was received or updated.</summary>
    Identity,
    /// <summary>The block list was received.</summary>
    BlockList,
    /// <summary>A block or unblock result was received.</summary>
    BlockResult,
    /// <summary>The ignore list was received.</summary>
    IgnoreList,
    /// <summary>An ignore result was received.</summary>
    IgnoreResult,
    /// <summary>The owned figure sets were received, or a figure set was added or removed.</summary>
    FigureSets,
    /// <summary>The account sanction status was received.</summary>
    Sanctions,
    /// <summary>The profile state was cleared because a hotel session connected or the state was reset.</summary>
    Reset
}

/// <summary>
/// Represents a change of the local account profile state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.ProfileChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="State">The profile state after the change.</param>
public sealed record ProfileChanged(
    ProfileChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ProfileStateView State);

/// <summary>
/// Represents a block or unblock result received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.ProfileBlockUpdated"/>. The result does not change the stored
/// block list.
/// </remarks>
/// <param name="Generation">The state generation of the hotel session the result was received in.</param>
/// <param name="Revision">The profile state revision stored for the result.</param>
/// <param name="UpdatedAtUtc">The time the result was published.</param>
/// <param name="Result">The result as sent by the hotel.</param>
public sealed record ProfileBlockUpdated(
    long Generation,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    BlockUserUpdate Result);

/// <summary>
/// Represents an ignore result received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.ProfileIgnoreUpdated"/>. The result does not change the stored
/// ignore list.
/// </remarks>
/// <param name="Generation">The state generation of the hotel session the result was received in.</param>
/// <param name="Revision">The profile state revision stored for the result.</param>
/// <param name="UpdatedAtUtc">The time the result was published.</param>
/// <param name="Result">The result as sent by the hotel.</param>
public sealed record ProfileIgnoreUpdated(
    long Generation,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    IgnoreUserResult Result);

/// <summary>
/// Represents a request to join a group.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.GroupMembershipJoin"/>.
/// </remarks>
/// <param name="GroupId">The id of the group. Must be positive.</param>
public sealed record GroupJoinRequest(Id GroupId);

/// <summary>
/// Represents a request to approve or reject a pending group membership.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.GroupMembershipApprove"/> and <see cref="ApplicationMemberIds.GroupMembershipReject"/>.
/// </remarks>
/// <param name="GroupId">The id of the group. Must be positive.</param>
/// <param name="UserId">The id of the user who asked to join. Must be positive.</param>
public sealed record GroupMemberRequest(Id GroupId, Id UserId);

/// <summary>
/// Represents a request to remove a member from a group.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.GroupMembershipKick"/>.
/// </remarks>
/// <param name="GroupId">The id of the group. Must be positive.</param>
/// <param name="UserId">The id of the member. Must be positive.</param>
/// <param name="BlockRejoin">Whether the user is prevented from joining the group again.</param>
public sealed record GroupMemberKickRequest(
    Id GroupId,
    Id UserId,
    bool BlockRejoin = false);

/// <summary>
/// Represents the result of sending a group membership action to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.GroupMembershipJoin"/>, <see cref="ApplicationMemberIds.GroupMembershipKick"/>,
/// <see cref="ApplicationMemberIds.GroupMembershipApprove"/> and <see cref="ApplicationMemberIds.GroupMembershipReject"/>.
/// The hotel's response is not awaited.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="GroupId">The id of the group.</param>
/// <param name="UserId">The id of the targeted user, or <see langword="null"/> for a join.</param>
/// <param name="BlockRejoin">Whether rejoining was blocked for a kick, or <see langword="null"/> for other actions.</param>
public sealed record GroupMembershipDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    Id GroupId,
    Id? UserId = null,
    bool? BlockRejoin = null);

/// <summary>
/// Represents a request to load another user's extended profile from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PeopleProfileGet"/>. The request is sent at most twice within the
/// timeout and does not open the profile window in the game client.
/// </remarks>
/// <param name="UserId">The id of the user. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RemoteProfileGetRequest(
    Id UserId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to load another user's relationship summary from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PeopleRelationshipGet"/>. The request is sent at most twice within
/// the timeout.
/// </remarks>
/// <param name="UserId">The id of the user. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RemoteRelationshipGetRequest(
    Id UserId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to load the badges another user has selected from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PeopleBadgesGet"/>. The request is sent at most twice within the timeout.
/// </remarks>
/// <param name="UserId">The id of the user. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RemoteBadgesGetRequest(
    Id UserId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to open another user's profile window in the game client.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PeopleProfileOpen"/>. The profile request is sent with the open
/// window flag set, and the response is not awaited.
/// </remarks>
/// <param name="UserId">The id of the user. Must be positive and fit in a 32-bit integer.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RemoteProfileOpenRequest(
    Id UserId,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents another user's extended profile.
/// </summary>
/// <param name="Id">The id of the user.</param>
/// <param name="Name">The user's name.</param>
/// <param name="Figure">The user's figure string.</param>
/// <param name="Motto">The user's motto.</param>
/// <param name="Created">The account creation date as the hotel formats it.</param>
/// <param name="AchievementScore">The user's achievement score.</param>
/// <param name="FriendCount">The number of friends the user has.</param>
/// <param name="IsFriend">Whether the user is a friend of the local user.</param>
/// <param name="IsFriendRequestSent">Whether the local user has sent the user a friend request.</param>
/// <param name="OnlineStatus">The online status value, where 0 means offline.</param>
/// <param name="Groups">The groups listed on the profile.</param>
/// <param name="LastAccessSeconds">The number of seconds since the user was last online.</param>
/// <param name="OpenProfileWindow">Whether the response asked the client to open the profile window.</param>
/// <param name="IsHidden">Whether the profile is hidden.</param>
/// <param name="Level">The user's level as sent by the hotel.</param>
/// <param name="SubscriptionLevel">The user's subscription level as sent by the hotel.</param>
/// <param name="StarGems">The user's star gem count as sent by the hotel.</param>
/// <param name="AllowFriendRequests">Whether the user accepts friend requests.</param>
/// <param name="HasFriendRequestsPending">Whether the hotel reports pending friend requests for the user.</param>
/// <param name="TotalBadges">The total number of badges the user owns.</param>
/// <param name="AchievementLevel">The user's achievement level as sent by the hotel.</param>
/// <param name="BadgeRarities">The badge rarity tiers and their counts on the profile.</param>
/// <param name="TotalBadgesRank">The user's rank by total badges as sent by the hotel.</param>
public sealed record RemoteProfileView(
    Id Id,
    string Name,
    string Figure,
    string Motto,
    string Created,
    int AchievementScore,
    int FriendCount,
    bool IsFriend,
    bool IsFriendRequestSent,
    int OnlineStatus,
    IReadOnlyList<ProfileGroup> Groups,
    int LastAccessSeconds,
    bool OpenProfileWindow,
    bool IsHidden,
    int Level,
    int SubscriptionLevel,
    int StarGems,
    bool AllowFriendRequests,
    bool HasFriendRequestsPending,
    int TotalBadges,
    int AchievementLevel,
    IReadOnlyList<BadgeRarity> BadgeRarities,
    int TotalBadgesRank);

/// <summary>
/// Represents another user's extended profile received from the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PeopleProfileGet"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the profile was received in.</param>
/// <param name="ReceivedAtUtc">The time the profile was received.</param>
/// <param name="Profile">The received profile.</param>
public sealed record RemoteProfileResult(
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    RemoteProfileView Profile);

/// <summary>
/// Represents another user's relationship summary received from the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PeopleRelationshipGet"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the summary was received in.</param>
/// <param name="ReceivedAtUtc">The time the summary was received.</param>
/// <param name="UserId">The id of the user the summary belongs to.</param>
/// <param name="Entries">The relationship entries, at most 500.</param>
public sealed record RemoteRelationshipResult(
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    Id UserId,
    IReadOnlyList<RelationshipEntry> Entries);

/// <summary>
/// Represents the badges another user has selected, received from the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PeopleBadgesGet"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the badges were received in.</param>
/// <param name="ReceivedAtUtc">The time the badges were received.</param>
/// <param name="UserId">The id of the user the badges belong to.</param>
/// <param name="Badges">The selected badges, at most 500.</param>
public sealed record RemoteBadgesResult(
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    Id UserId,
    IReadOnlyList<SelectedBadge> Badges);

/// <summary>
/// Represents the result of asking the game client to open another user's profile.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PeopleProfileOpen"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the request was sent in.</param>
/// <param name="DispatchedAtUtc">The time the request was sent.</param>
/// <param name="UserId">The id of the user whose profile was requested.</param>
public sealed record RemoteProfileOpenReceipt(
    long SessionGeneration,
    DateTimeOffset DispatchedAtUtc,
    Id UserId);

/// <summary>
/// Represents a request to load a group's details from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.GroupsDetailsGet"/>. The request is sent at most twice within the
/// timeout and does not open the group window in the game client.
/// </remarks>
/// <param name="GroupId">The id of the group. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record GroupDetailsGetRequest(
    Id GroupId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a group's details received from the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.GroupsDetailsGet"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the details were received in.</param>
/// <param name="ReceivedAtUtc">The time the details were received.</param>
/// <param name="Details">The group details.</param>
public sealed record GroupDetailsResult(
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    GroupData Details);

/// <summary>
/// Represents a request to load one page of a group's members from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.GroupsMembersPage"/>. The request is sent at most twice within the
/// timeout, and the page size is chosen by the hotel.
/// </remarks>
/// <param name="GroupId">The id of the group. Must be positive and fit in a 32-bit integer.</param>
/// <param name="PageIndex">The zero-based index of the hotel page to load.</param>
/// <param name="UserNameFilter">
/// The user name filter sent with the request, at most 65535 UTF-8 bytes, or an empty string for no filter.
/// </param>
/// <param name="SearchType">The category of members to list.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record GroupMembersPageRequest(
    Id GroupId,
    int PageIndex = 0,
    string UserNameFilter = "",
    GuildMemberSearchType SearchType = GuildMemberSearchType.All,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents one page of a group's members received from the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.GroupsMembersPage"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the page was received in.</param>
/// <param name="ReceivedAtUtc">The time the page was received.</param>
/// <param name="GroupId">The id of the group.</param>
/// <param name="GroupName">The name of the group.</param>
/// <param name="BaseRoomId">The id of the group's home room.</param>
/// <param name="BadgeCode">The group's badge code.</param>
/// <param name="TotalEntries">The number of members matching the filter and category.</param>
/// <param name="Entries">The members in the page.</param>
/// <param name="IsAllowedToManage">Whether the local user may manage the group's members.</param>
/// <param name="PageSize">The number of members per page chosen by the hotel.</param>
/// <param name="PageIndex">The zero-based index of the page.</param>
/// <param name="SearchType">The member category echoed by the hotel.</param>
/// <param name="UserNameFilter">The user name filter echoed by the hotel.</param>
public sealed record GroupMembersPage(
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    Id GroupId,
    string GroupName,
    Id BaseRoomId,
    string BadgeCode,
    int TotalEntries,
    IReadOnlyList<GuildMember> Entries,
    bool IsAllowedToManage,
    int PageSize,
    int PageIndex,
    GuildMemberSearchType SearchType,
    string UserNameFilter);

/// <summary>
/// Represents a request to read a page of the groups the local user belongs to.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.GroupsMembershipsGet"/>. Without a snapshot revision the memberships
/// are requested from the hotel once and a new snapshot is stored. Up to 16 snapshots are retained, and they
/// become unavailable when the hotel session changes or the profile state resets.
/// </remarks>
/// <param name="Offset">
/// The zero-based offset of the first membership to return. Must be 0 when <paramref name="SnapshotRevision"/> is
/// <see langword="null"/>, and not greater than the snapshot's membership count.
/// </param>
/// <param name="Limit">The maximum number of memberships to return, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read without contacting the hotel, or <see langword="null"/> to
/// request a new snapshot.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record GroupMembershipsGetRequest(
    int Offset = 0,
    int Limit = 500,
    int TimeoutMilliseconds = 10000,
    long? SnapshotRevision = null,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a page of the groups the local user belongs to, read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.GroupsMembershipsGet"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was created in.</param>
/// <param name="ReceivedAtUtc">The time the memberships were received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, passed back to read the next page.</param>
/// <param name="TotalMemberships">The number of memberships in the snapshot.</param>
/// <param name="Offset">The zero-based offset of the first membership in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Memberships">The memberships in the page.</param>
public sealed record GroupMembershipsPage(
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    long SnapshotRevision,
    int TotalMemberships,
    int Offset,
    int? NextOffset,
    IReadOnlyList<GuildMembership> Memberships);
