using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class ProfileApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = Query<ProfileStateRequest, ProfileStateView>(
        ApplicationMemberIds.ProfileState,
        "Profile state",
        "Reads the active local account snapshot and its loaded-state summary.",
        [],
        [new(ApplicationStateKey.ProfileLoaded, ApplicationStateEffectKind.Reads)]);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.ProfileRefresh,
        "Refresh profile",
        "Loads the active session's local account profile.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(ProfileRefreshRequest),
        typeof(ProfileStateView),
        [TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.ProfileLoaded, ApplicationStateEffectKind.Changes)],
        [
            Send(MessageKeys.Users.ProfileRequest),
            Observe(MessageKeys.Users.ProfileSnapshot)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor BlocksList { get; } = Query<ProfileIdPageRequest, ProfileIdPage>(
        ApplicationMemberIds.ProfileBlocksList,
        "Blocked users",
        "Reads a bounded page from the local account's block-list snapshot.",
        PagingParameters(200),
        [new(ApplicationStateKey.ProfileBlockListLoaded, ApplicationStateEffectKind.Reads)]);

    public static ApplicationDescriptor BlocksRefresh { get; } = RefreshIds(
        ApplicationMemberIds.ProfileBlocksRefresh,
        "Refresh blocked users",
        "Loads the active session's complete block list and returns a bounded page.",
        MessageKeys.Users.Block.ListRequest,
        MessageKeys.Users.Block.ListSnapshot,
        ApplicationStateKey.ProfileBlockListLoaded);

    public static ApplicationDescriptor BlockAdd { get; } = UserOperation(
        ApplicationMemberIds.ProfileBlockAdd,
        "Block user",
        "Adds a user to the local account's block list.",
        MessageKeys.Users.Block.Add,
        ApplicationStateKey.ProfileBlockListLoaded);

    public static ApplicationDescriptor BlockRemove { get; } = UserOperation(
        ApplicationMemberIds.ProfileBlockRemove,
        "Unblock user",
        "Removes a user from the local account's block list.",
        MessageKeys.Users.Block.Remove,
        ApplicationStateKey.ProfileBlockListLoaded);

    public static ApplicationDescriptor IgnoresList { get; } = Query<ProfileIdPageRequest, ProfileIdPage>(
        ApplicationMemberIds.ProfileIgnoresList,
        "Ignored users",
        "Reads a bounded page from the local account's ignore-list snapshot.",
        PagingParameters(200),
        [new(ApplicationStateKey.ProfileIgnoreListLoaded, ApplicationStateEffectKind.Reads)]);

    public static ApplicationDescriptor IgnoresRefresh { get; } = RefreshIds(
        ApplicationMemberIds.ProfileIgnoresRefresh,
        "Refresh ignored users",
        "Loads the active session's complete ignore list and returns a bounded page.",
        MessageKeys.Users.Ignore.ListRequest,
        MessageKeys.Users.Ignore.ListSnapshot,
        ApplicationStateKey.ProfileIgnoreListLoaded);

    public static ApplicationDescriptor IgnoreAddById { get; } = UserOperation(
        ApplicationMemberIds.ProfileIgnoreAddById,
        "Ignore user by id",
        "Adds a user identifier to the local account's ignore list.",
        MessageKeys.Users.Ignore.AddByIdRequest,
        ApplicationStateKey.ProfileIgnoreListLoaded);

    public static ApplicationDescriptor IgnoreAddByName { get; } = new(
        ApplicationMemberIds.ProfileIgnoreAddByName,
        "Ignore user by name",
        "Adds a hotel user name to the local account's ignore list when the active dialect supports it.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(ProfileUserNameRequest),
        typeof(ProfileDispatchResult),
        [RequiredText("user_name", "Hotel user name.")],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.ProfileIgnoreListLoaded, ApplicationStateEffectKind.Changes)],
        [Send(MessageKeys.Users.Ignore.AddByNameRequest)],
        new(false, true, false, true));

    public static ApplicationDescriptor IgnoreRemove { get; } = new(
        ApplicationMemberIds.ProfileIgnoreRemove,
        "Unignore user",
        "Removes exactly one user identifier or user name through a verified dialect route.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(ProfileIgnoreRemoveRequest),
        typeof(ProfileDispatchResult),
        [
            new("kind", typeof(ProfileIdentityKind), true, null, "Identity representation carried by the request."),
            RequiredText("identity", "Positive decimal user identifier or hotel user name selected by kind.")
        ],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.ProfileIgnoreListLoaded, ApplicationStateEffectKind.Changes)],
        [Send(MessageKeys.Users.Ignore.Remove)],
        new(false, true, false, true));

    public static ApplicationDescriptor FigureSetsList { get; } = Query<ProfileFigureSetsRequest, ProfileFigureSetsPage>(
        ApplicationMemberIds.ProfileFigureSetsList,
        "Owned figure sets",
        "Reads bounded pages of owned figure sets and Flash-bound furniture names.",
        PagingParameters(200),
        [new(ApplicationStateKey.ProfileFigureSetsLoaded, ApplicationStateEffectKind.Reads)]);

    public static ApplicationDescriptor SanctionsList { get; } = Query<ProfileSanctionsRequest, ProfileSanctionsPage>(
        ApplicationMemberIds.ProfileSanctionsList,
        "Account sanctions",
        "Reads a bounded sanction page or the active Unity call-for-help status.",
        PagingParameters(100),
        [new(ApplicationStateKey.ProfileSanctionsLoaded, ApplicationStateEffectKind.Reads)]);

    public static ApplicationDescriptor SanctionsRefresh { get; } = new(
        ApplicationMemberIds.ProfileSanctionsRefresh,
        "Refresh account sanctions",
        "Loads the active dialect's account-sanction status and returns a bounded view.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(ProfileSanctionsRefreshRequest),
        typeof(ProfileSanctionsPage),
        [.. PagingParameters(100), TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.ProfileSanctionsLoaded, ApplicationStateEffectKind.Changes)],
        [
            Send(MessageKeys.Users.Sanctions.Request),
            Observe(MessageKeys.Users.Sanctions.Snapshot)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor WardrobeGet { get; } = new(
        ApplicationMemberIds.ProfileWardrobeGet,
        "Wardrobe snapshot",
        "Creates one immutable session-bound wardrobe snapshot at offset zero, then returns bounded pages from that snapshot revision.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(ProfileWardrobeRequest),
        typeof(ProfileWardrobePage),
        [
            .. PagingParameters(100),
            TimeoutParameter(),
            new(
                "snapshot_revision",
                typeof(long?),
                false,
                null,
                "Snapshot revision returned by the first page; required for every continuation page.",
                new(Minimum: 1))
        ],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            Send(MessageKeys.Wardrobe.Request),
            Observe(MessageKeys.Wardrobe.Snapshot)
        ],
        tool_hints: new(true, false, true, true));

    public static ApplicationDescriptor MottoSet { get; } = Dispatch<ProfileMottoSetRequest>(
        ApplicationMemberIds.ProfileMottoSet,
        "Set motto",
        "Changes the local account's motto.",
        [new("motto", typeof(string), true, null, "New motto.", TextConstraints(true))],
        MessageKeys.Users.MottoUpdate,
        ApplicationStateKey.ProfileLoaded);

    public static ApplicationDescriptor FigureSet { get; } = Dispatch<ProfileFigureSetRequest>(
        ApplicationMemberIds.ProfileFigureSet,
        "Set figure",
        "Changes the local account's figure and gender through the active dialect projection.",
        [
            RequiredText("gender", "Gender code."),
            RequiredText("figure", "Figure string.")
        ],
        MessageKeys.Wardrobe.FigureUpdate,
        ApplicationStateKey.ProfileLoaded);

    public static ApplicationDescriptor OutfitSave { get; } = Dispatch<ProfileOutfitSaveRequest>(
        ApplicationMemberIds.ProfileWardrobeOutfitSave,
        "Save wardrobe outfit",
        "Saves a figure and gender into a wardrobe slot.",
        [
            new("slot_id", typeof(int), true, null, "Wardrobe slot identifier.", new(Minimum: 0)),
            RequiredText("figure", "Figure string."),
            RequiredText("gender", "Gender code.")
        ],
        MessageKeys.Wardrobe.OutfitSave);

    public static ApplicationDescriptor FavoriteGroupSelect { get; } = Dispatch<ProfileFavoriteGroupRequest>(
        ApplicationMemberIds.ProfileFavoriteGroupSelect,
        "Select favorite group",
        "Selects the local account's favorite group.",
        [RequiredId("group_id", "Group identifier.")],
        MessageKeys.Users.FavoriteGroup.Select);

    public static ApplicationDescriptor FavoriteGroupDeselect { get; } = Dispatch<ProfileFavoriteGroupRequest>(
        ApplicationMemberIds.ProfileFavoriteGroupDeselect,
        "Deselect favorite group",
        "Deselects the local account's favorite group.",
        [RequiredId("group_id", "Group identifier.")],
        MessageKeys.Users.FavoriteGroup.Deselect);

    public static ApplicationDescriptor Changed { get; } = Event<ProfileChanged>(
        ApplicationMemberIds.ProfileChanged,
        "Profile changed",
        "Publishes immutable local profile, list, figure-set, sanction and reset summaries.",
        [
            Observe(MessageKeys.Users.ProfileSnapshot, false),
            Observe(MessageKeys.Users.Block.ListSnapshot, false),
            Observe(MessageKeys.Users.Block.Updated, false),
            Observe(MessageKeys.Users.Ignore.ListSnapshot, false),
            Observe(MessageKeys.Users.Ignore.Updated, false),
            Observe(MessageKeys.Users.FigureSets.Added, false),
            Observe(MessageKeys.Users.FigureSets.Removed, false),
            Observe(MessageKeys.Users.FigureSets.Snapshot, false),
            Observe(MessageKeys.Users.Sanctions.Snapshot, false),
            Observe(MessageKeys.Users.FigureUpdated, false),
            Observe(MessageKeys.Users.NameChangeResult, false),
            Observe(MessageKeys.Users.SafetyLockChanged, false),
            Observe(MessageKeys.Room.Occupants.Identity.Appearance, false),
            Observe(MessageKeys.Room.Occupants.Identity.Name, false)
        ]);

    public static ApplicationDescriptor BlockUpdated { get; } = Event<ProfileBlockUpdated>(
        ApplicationMemberIds.ProfileBlockUpdated,
        "Block result",
        "Publishes hotel block and unblock results with the committed profile revision.",
        [Observe(MessageKeys.Users.Block.Updated)]);

    public static ApplicationDescriptor IgnoreUpdated { get; } = Event<ProfileIgnoreUpdated>(
        ApplicationMemberIds.ProfileIgnoreUpdated,
        "Ignore result",
        "Publishes hotel ignore results with the committed profile revision.",
        [Observe(MessageKeys.Users.Ignore.Updated)]);

    private static ApplicationDescriptor Query<TRequest, TResult>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        IReadOnlyList<ApplicationStateEffect> effects) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Query,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(TResult),
            parameters,
            state_effects: effects,
            tool_hints: new(true, false, true, false),
            invocation_scope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor RefreshIds(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey snapshot_key,
        ApplicationStateKey state) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(ProfileIdRefreshRequest),
            typeof(ProfileIdPage),
            [.. PagingParameters(200), TimeoutParameter()],
            [ApplicationStateKey.HotelConnected],
            [new(state, ApplicationStateEffectKind.Changes)],
            [Send(request_key), Observe(snapshot_key)],
            new(true, false, true, true));

    private static ApplicationDescriptor UserOperation(
        string id,
        string title,
        string description,
        MessageKey key,
        ApplicationStateKey state) => Dispatch<ProfileUserRequest>(
            id,
            title,
            description,
            [RequiredId("user_id", "User identifier.")],
            key,
            state);

    private static ApplicationDescriptor Dispatch<TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        MessageKey key,
        ApplicationStateKey? state = null) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(ProfileDispatchResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            state is ApplicationStateKey changed
                ? [new(changed, ApplicationStateEffectKind.Changes)]
                : [],
            [Send(key)],
            new(false, true, false, true));

    private static ApplicationDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationMessageRequirement> messages) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Event,
            event_exposure,
            null,
            typeof(TEvent),
            messages: messages);

    private static ApplicationParameterDescriptor[] PagingParameters(int limit) =>
    [
        new("offset", typeof(int), false, 0, "Zero-based result offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, limit, "Maximum number of entries to return.", new(Minimum: 1, Maximum: 500))
    ];

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor RequiredId(string name, string description) => new(
        name,
        typeof(Id),
        true,
        null,
        description,
        IdConstraints());

    private static ApplicationParameterConstraints IdConstraints() => new(
        Pattern: "^[1-9][0-9]*$");

    private static ApplicationParameterDescriptor RequiredText(string name, string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        TextConstraints(false));

    private static ApplicationParameterConstraints TextConstraints(bool allow_empty) => new(
        MinLength: allow_empty ? 0 : 1,
        MaxUtf8Bytes: ushort.MaxValue,
        Pattern: allow_empty ? null : @".*\S.*");

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);
}

internal static class GroupMembershipApplicationDescriptors
{
    public static ApplicationDescriptor Join { get; } = Operation<GroupJoinRequest>(
        ApplicationMemberIds.GroupMembershipJoin,
        "Join group",
        "Requests membership in a group.",
        [RequiredId("group_id", "Group identifier.")],
        MessageKeys.Groups.Membership.Join);

    public static ApplicationDescriptor Kick { get; } = Operation<GroupMemberKickRequest>(
        ApplicationMemberIds.GroupMembershipKick,
        "Kick group member",
        "Removes a user from a group and optionally blocks rejoining.",
        [
            RequiredId("group_id", "Group identifier."),
            RequiredId("user_id", "User identifier."),
            new("block_rejoin", typeof(bool), false, false, "Prevent the user from rejoining.")
        ],
        MessageKeys.Groups.Membership.Kick);

    public static ApplicationDescriptor Approve { get; } = Operation<GroupMemberRequest>(
        ApplicationMemberIds.GroupMembershipApprove,
        "Approve group member",
        "Approves a pending group membership request.",
        [
            RequiredId("group_id", "Group identifier."),
            RequiredId("user_id", "User identifier.")
        ],
        MessageKeys.Groups.Membership.Approve);

    public static ApplicationDescriptor Reject { get; } = Operation<GroupMemberRequest>(
        ApplicationMemberIds.GroupMembershipReject,
        "Reject group member",
        "Rejects a pending group membership request.",
        [
            RequiredId("group_id", "Group identifier."),
            RequiredId("user_id", "User identifier.")
        ],
        MessageKeys.Groups.Membership.Reject);

    private static ApplicationDescriptor Operation<TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        MessageKey key) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(GroupMembershipDispatchResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages: [new(key, Direction.Out, ApplicationMessageRole.Send)],
            tool_hints: new(false, true, false, true));

    private static ApplicationParameterDescriptor RequiredId(string name, string description) => new(
        name,
        typeof(Id),
        true,
        null,
        description,
        new(Pattern: "^[1-9][0-9]*$"));
}

internal static class RemotePeopleApplicationDescriptors
{
    public static ApplicationDescriptor ProfileGet { get; } = Read<RemoteProfileGetRequest, RemoteProfileResult>(
        ApplicationMemberIds.PeopleProfileGet,
        "Remote profile",
        "Loads an immutable remote-user profile for the active hotel session.",
        MessageKeys.Users.ExtendedProfileRequest,
        MessageKeys.Users.ExtendedProfileSnapshot);

    public static ApplicationDescriptor RelationshipGet { get; } = Read<RemoteRelationshipGetRequest, RemoteRelationshipResult>(
        ApplicationMemberIds.PeopleRelationshipGet,
        "Remote relationship status",
        "Loads the relationship summary for a remote user in the active hotel session.",
        MessageKeys.Users.Relationship.Request,
        MessageKeys.Users.Relationship.Snapshot);

    public static ApplicationDescriptor BadgesGet { get; } = Read<RemoteBadgesGetRequest, RemoteBadgesResult>(
        ApplicationMemberIds.PeopleBadgesGet,
        "Remote selected badges",
        "Loads the selected badge set for a remote user in the active hotel session.",
        MessageKeys.Badges.SelectedRequest,
        MessageKeys.Badges.Selected);

    public static ApplicationDescriptor ProfileOpen { get; } = new(
        ApplicationMemberIds.PeopleProfileOpen,
        "Open remote profile",
        "Opens a remote user's profile in the active game client.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RemoteProfileOpenRequest),
        typeof(RemoteProfileOpenReceipt),
        [UserId(), SessionGeneration()],
        [ApplicationStateKey.HotelConnected],
        messages: [Send(MessageKeys.Users.ExtendedProfileRequest)],
        tool_hints: new(false, false, true, true));

    private static ApplicationDescriptor Read<TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey snapshot_key) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(TResult),
            [UserId(), Timeout(), SessionGeneration()],
            [ApplicationStateKey.HotelConnected],
            messages: [Send(request_key), Observe(snapshot_key)],
            tool_hints: new(true, false, true, true));

    private static ApplicationParameterDescriptor UserId() => new(
        "user_id",
        typeof(Id),
        true,
        null,
        "Positive hotel user identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApplicationParameterDescriptor Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time for locking, dispatch, retries and the matching hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, Direction.In, ApplicationMessageRole.Observe);
}

internal static class GroupReadsApplicationDescriptors
{
    public static ApplicationDescriptor DetailsGet { get; } = Read<GroupDetailsGetRequest, GroupDetailsResult>(
        ApplicationMemberIds.GroupsDetailsGet,
        "Group details",
        "Loads immutable details for a group in the active hotel session.",
        [GroupId(), Timeout(), SessionGeneration()],
        MessageKeys.Groups.Details.Request,
        MessageKeys.Groups.Details.Snapshot);

    public static ApplicationDescriptor MembersPage { get; } = Read<GroupMembersPageRequest, GroupMembersPage>(
        ApplicationMemberIds.GroupsMembersPage,
        "Group members page",
        "Loads one authoritative hotel page of group members.",
        [
            GroupId(),
            new("page_index", typeof(int), false, 0, "Zero-based hotel page index.", new(Minimum: 0)),
            new(
                "user_name_filter",
                typeof(string),
                false,
                string.Empty,
                "Exact hotel user-name filter carried by the request.",
                new(MinLength: 0, MaxUtf8Bytes: ushort.MaxValue)),
            new(
                "search_type",
                typeof(GuildMemberSearchType),
                false,
                GuildMemberSearchType.All,
                "Requested group-member category; Unity supports All only."),
            Timeout(),
            SessionGeneration()
        ],
        MessageKeys.Groups.Members.Request,
        MessageKeys.Groups.Members.Snapshot);

    public static ApplicationDescriptor MembershipsGet { get; } = Read<GroupMembershipsGetRequest, GroupMembershipsPage>(
        ApplicationMemberIds.GroupsMembershipsGet,
        "Group memberships",
        "Loads or continues a bounded page from a session-bound immutable membership snapshot.",
        [
            new("offset", typeof(int), false, 0, "Zero-based snapshot offset.", new(Minimum: 0)),
            new("limit", typeof(int), false, 500, "Maximum memberships to return.", new(Minimum: 1, Maximum: 500)),
            Timeout(),
            new(
                "snapshot_revision",
                typeof(long?),
                false,
                null,
                "Positive revision required for every continuation page.",
                new(Minimum: 1)),
            SessionGeneration()
        ],
        MessageKeys.Groups.Memberships.Request,
        MessageKeys.Groups.Memberships.Snapshot);

    private static ApplicationDescriptor Read<TRequest, TResult>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        MessageKey request_key,
        MessageKey snapshot_key) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(TResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages: [Send(request_key), Observe(snapshot_key)],
            tool_hints: new(true, false, true, true));

    private static ApplicationParameterDescriptor GroupId() => new(
        "group_id",
        typeof(Id),
        true,
        null,
        "Positive group identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApplicationParameterDescriptor Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time for locking, dispatch, retries and the matching hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, Direction.In, ApplicationMessageRole.Observe);
}
