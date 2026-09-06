using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record ProfileStateRequest;

public sealed record ProfileRefreshRequest(int TimeoutMilliseconds = 10000);

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

public sealed record ProfileStateView(
    long Generation,
    long Revision,
    bool Connected,
    ClientType? Client,
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

public sealed record ProfileIdPageRequest(
    int Offset = 0,
    int Limit = 200);

public sealed record ProfileIdRefreshRequest(
    int Offset = 0,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

public sealed record ProfileIdPage(
    bool Loaded,
    long Generation,
    long Revision,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<Id> UserIds);

public sealed record ProfileFigureSetsRequest(
    int Offset = 0,
    int Limit = 200);

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

public sealed record ProfileSanctionsRequest(
    int Offset = 0,
    int Limit = 100);

public sealed record ProfileSanctionsRefreshRequest(
    int Offset = 0,
    int Limit = 100,
    int TimeoutMilliseconds = 10000);

public sealed record ProfileSanctionsPage(
    bool Loaded,
    long Generation,
    long Revision,
    AccountSanctionStatusKind? Kind,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<Sanction> Sanctions,
    CfhSanctionStatus? CallForHelp);

public sealed record ProfileWardrobeRequest(
    int Offset = 0,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? SnapshotRevision = null);

public sealed record ProfileWardrobePage(
    ClientType Client,
    long Generation,
    long Revision,
    long SnapshotRevision,
    int State,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<WardrobeOutfit> Outfits);

public sealed record ProfileUserRequest(Id UserId);

public sealed record ProfileUserNameRequest(string UserName);

public enum ProfileIdentityKind
{
    Id,
    Name
}

public sealed record ProfileIgnoreRemoveRequest(
    ProfileIdentityKind Kind,
    string Identity);

public sealed record ProfileMottoSetRequest(string Motto);

public sealed record ProfileFigureSetRequest(string Gender, string Figure);

public sealed record ProfileOutfitSaveRequest(
    int SlotId,
    string Figure,
    string Gender);

public sealed record ProfileFavoriteGroupRequest(Id GroupId);

public sealed record ProfileDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long Generation,
    long Revision,
    Id? TargetId = null,
    string? TargetName = null,
    int? SlotId = null);

public enum ProfileChangeKind
{
    Identity,
    BlockList,
    BlockResult,
    IgnoreList,
    IgnoreResult,
    FigureSets,
    Sanctions,
    Reset
}

public sealed record ProfileChanged(
    ProfileChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ProfileStateView State);

public sealed record ProfileBlockUpdated(
    long Generation,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    BlockUserUpdate Result);

public sealed record ProfileIgnoreUpdated(
    long Generation,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    IgnoreUserResult Result);

public sealed record GroupJoinRequest(Id GroupId);

public sealed record GroupMemberRequest(Id GroupId, Id UserId);

public sealed record GroupMemberKickRequest(
    Id GroupId,
    Id UserId,
    bool BlockRejoin = false);

public sealed record GroupMembershipDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    Id GroupId,
    Id? UserId = null,
    bool? BlockRejoin = null);

public sealed record RemoteProfileGetRequest(
    Id UserId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record RemoteRelationshipGetRequest(
    Id UserId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record RemoteBadgesGetRequest(
    Id UserId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record RemoteProfileOpenRequest(
    Id UserId,
    long? ExpectedSessionGeneration = null);

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
    int TotalBadgesRank,
    string NameColor,
    IReadOnlyList<ProfileOldName> OldNames);

public sealed record RemoteProfileResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    RemoteProfileView Profile);

public sealed record RemoteRelationshipResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    Id UserId,
    IReadOnlyList<RelationshipEntry> Entries);

public sealed record RemoteBadgesResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    Id UserId,
    IReadOnlyList<SelectedBadge> Badges);

public sealed record RemoteProfileOpenReceipt(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset DispatchedAtUtc,
    Id UserId);

public sealed record GroupDetailsGetRequest(
    Id GroupId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record GroupDetailsResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    GroupData Details);

public sealed record GroupMembersPageRequest(
    Id GroupId,
    int PageIndex = 0,
    string UserNameFilter = "",
    GuildMemberSearchType SearchType = GuildMemberSearchType.All,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record GroupMembersPage(
    ClientType Client,
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
    GuildMemberSearchType? SearchType,
    string UserNameFilter);

public sealed record GroupMembershipsGetRequest(
    int Offset = 0,
    int Limit = 500,
    int TimeoutMilliseconds = 10000,
    long? SnapshotRevision = null,
    long? ExpectedSessionGeneration = null);

public sealed record GroupMembershipsPage(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ReceivedAtUtc,
    long SnapshotRevision,
    int TotalMemberships,
    int Offset,
    int? NextOffset,
    IReadOnlyList<GuildMembership> Memberships);
