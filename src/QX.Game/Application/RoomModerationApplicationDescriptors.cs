using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class RoomModerationApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.RoomModerationState,
        "Room moderation state",
        "Reads one bounded page of the immutable session-bound ban list.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(RoomModerationStateRequest),
        typeof(RoomModerationStateView),
        PageParameters(),
        state_effects:
        [new(ApplicationStateKey.RoomBansLoaded, ApplicationStateEffectKind.Reads)],
        messages: StateMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.RoomModerationRefresh,
        "Refresh room ban list",
        "Requests the current ready room's ban list and returns its first bounded page.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomModerationRefreshRequest),
        typeof(RoomModerationStateView),
        [
            Limit(),
            Timeout(),
            SessionGeneration(),
            ExpectedRoomId(),
            RoomGeneration()
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [new(ApplicationStateKey.RoomBansLoaded, ApplicationStateEffectKind.Changes)],
        [
            Send(MessageKeys.Room.Moderation.BansRequest),
            Observe(MessageKeys.Room.Moderation.BansSnapshot)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor Mute { get; } = Action<RoomModerationMuteRequest>(
        ApplicationMemberIds.RoomModerationMute,
        "Mute room user",
        "Dispatches a bounded mute duration for one current room user without assuming hotel acceptance.",
        [
            UserId(),
            new("minutes", typeof(int), true, null, "Mute duration in whole minutes; zero removes the mute.", new(Minimum: 0, Maximum: 1440)),
            .. TargetGuards()
        ],
        [Send(MessageKeys.Room.Moderation.Mute)]);

    public static ApplicationDescriptor Kick { get; } = Action<RoomModerationTargetRequest>(
        ApplicationMemberIds.RoomModerationKick,
        "Kick room user",
        "Dispatches a kick for one current room user without assuming hotel acceptance.",
        [UserId(), .. TargetGuards()],
        [Send(MessageKeys.Room.Moderation.Kick)]);

    public static ApplicationDescriptor Ban { get; } = Action<RoomModerationBanRequest>(
        ApplicationMemberIds.RoomModerationBan,
        "Ban room user",
        "Dispatches one verified room-ban duration for a current room user without assuming hotel acceptance.",
        [
            UserId(),
            new("length", typeof(BanLength), true, null, "Hour, day or permanent ban duration."),
            .. TargetGuards()
        ],
        [Send(MessageKeys.Room.Moderation.Ban)],
        ApplicationStateEffectKind.Invalidates);

    public static ApplicationDescriptor Unban { get; } = new(
        ApplicationMemberIds.RoomModerationUnban,
        "Unban room user",
        "Dispatches an unban for an explicit room and user without assuming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomModerationUnbanRequest),
        typeof(RoomModerationDispatchResult),
        [
            UserId(),
            new("room_id", typeof(Id), true, null, "Positive room identifier.", IdConstraint()),
            SessionGeneration(),
            RoomGeneration(),
            new("expected_snapshot_revision", typeof(long?), false, null, "Optional active-room ban snapshot revision guard.", new(Minimum: 0))
        ],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.RoomBansLoaded, ApplicationStateEffectKind.Invalidates)],
        [Send(MessageKeys.Room.Moderation.Unban)],
        new(false, true, true, true));

    public static ApplicationDescriptor Bounce { get; } = Action<RoomModerationTargetRequest>(
        ApplicationMemberIds.RoomModerationBounce,
        "Bounce room user",
        "Dispatches an hour ban followed by its unban for one pinned current room user.",
        [UserId(), .. TargetGuards()],
        [
            Send(MessageKeys.Room.Moderation.Ban),
            Send(MessageKeys.Room.Moderation.Unban)
        ],
        ApplicationStateEffectKind.Invalidates);

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.RoomModerationChanged,
        "Room moderation changed",
        "Publishes ordered bounded ban-list state summaries.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomModerationChanged),
        state_effects:
        [new(ApplicationStateKey.RoomBansLoaded, ApplicationStateEffectKind.Changes)],
        messages: StateMessages());

    private static ApplicationDescriptor Action<TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        IReadOnlyList<ApplicationMessageRequirement> messages,
        ApplicationStateEffectKind? ban_effect = null) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(TRequest),
        typeof(RoomModerationDispatchResult),
        parameters,
        [
            ApplicationStateKey.HotelConnected,
            ApplicationStateKey.RoomReady,
            ApplicationStateKey.ProfileLoaded
        ],
        ban_effect is null
            ? [new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)]
            : [
                new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads),
                new(ApplicationStateKey.RoomBansLoaded, ban_effect.Value)
            ],
        messages,
        new(false, true, false, true));

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based offset within the current immutable snapshot.", new(Minimum: 0)),
        Limit(),
        new("snapshot_revision", typeof(long?), false, null, "Exact snapshot revision required for continuation pages.", new(Minimum: 0))
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> TargetGuards() =>
    [
        SessionGeneration(),
        ExpectedRoomId(),
        RoomGeneration(),
        new("expected_user_index", typeof(int?), false, null, "Optional current room-index guard for the target identity.", new(Minimum: 0))
    ];

    private static ApplicationParameterDescriptor UserId() => new(
        "user_id",
        typeof(Id),
        true,
        null,
        "Positive hotel user identifier.",
        IdConstraint());

    private static ApplicationParameterDescriptor SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional session-generation guard from room.moderation.state or room.moderation.changed.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor ExpectedRoomId() => new(
        "expected_room_id",
        typeof(Id?),
        false,
        null,
        "Optional active room identity guard.",
        IdConstraint());

    private static ApplicationParameterDescriptor RoomGeneration() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional ready-room generation guard.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor Limit() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum bans returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the correlated hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static IReadOnlyList<ApplicationMessageRequirement> StateMessages() =>
    [
        Observe(MessageKeys.Room.Moderation.BansSnapshot, false),
        Observe(MessageKeys.Room.Moderation.UserUnbanned, false)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);

    private static ApplicationParameterConstraints IdConstraint() =>
        new(Pattern: "^[1-9][0-9]*$");
}
