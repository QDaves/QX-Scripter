using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class AchievementApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui |
        ApplicationExposure.Cli |
        ApplicationExposure.Scripting;

    public static ApplicationDescriptor AchievementState { get; } = new(
        ApplicationMemberIds.AchievementsState,
        "Achievement state",
        "Reads one bounded state view from a current or retained immutable achievement snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(AchievementStateRequest),
        typeof(AchievementStateView),
        [SnapshotRevisionParameter(false)],
        state_effects: [AchievementRead()],
        messages: AchievementObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor AchievementList { get; } = new(
        ApplicationMemberIds.AchievementsList,
        "Achievement page",
        "Reads one hotel-ordered achievement page from an immutable achievement-domain snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(AchievementPageRequest),
        typeof(AchievementPage),
        PageParameters(),
        state_effects: [AchievementRead()],
        messages:
        [
            Observe(MessageKeys.Achievements.Snapshot),
            Observe(MessageKeys.Achievements.Updated)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor AchievementPointLimitsList { get; } = new(
        ApplicationMemberIds.AchievementPointLimitsList,
        "Achievement point-limit page",
        "Reads one globally ordered point-limit page from the same immutable achievement-domain snapshot used by state and achievement pages.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(AchievementPointLimitPageRequest),
        typeof(AchievementPointLimitPage),
        PageParameters(),
        state_effects: [AchievementRead()],
        messages:
        [
            Observe(MessageKeys.Achievements.PointLimits)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor AchievementRefresh { get; } = new(
        ApplicationMemberIds.AchievementsRefresh,
        "Refresh achievements",
        "Coalesces callers within one hotel session and returns only a fresh full achievement snapshot for the dispatched request epoch; achievement updates never establish the baseline. Caller cancellation and timeout detach only that waiter.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(AchievementRefreshRequest),
        typeof(AchievementRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [AchievementChange()],
        [
            Send(MessageKeys.Achievements.Request),
            Observe(MessageKeys.Achievements.Snapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor AchievementPointLimitsRefresh { get; } = new(
        ApplicationMemberIds.AchievementPointLimitsRefresh,
        "Refresh achievement point limits",
        "Coalesces callers within one hotel session and returns only a fresh point-limit response for the dispatched request epoch. Caller cancellation and timeout detach only that waiter.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(AchievementPointLimitsRefreshRequest),
        typeof(AchievementPointLimitsRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [AchievementChange()],
        [
            Send(MessageKeys.Achievements.PointLimitsRequest),
            Observe(MessageKeys.Achievements.PointLimits)
        ],
        RefreshHints());

    public static ApplicationDescriptor AchievementChanged { get; } = new(
        ApplicationMemberIds.AchievementsChanged,
        "Achievements changed",
        "Publishes bounded passive achievement-domain changes, including full snapshots, incremental updates, optional Flash score data, point limits, game-data new codes, and resets.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(AchievementChanged),
        state_effects: [AchievementChange()],
        messages: AchievementObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor BadgeState { get; } = new(
        ApplicationMemberIds.BadgesState,
        "Badge state",
        "Reads one bounded state view from a current or retained immutable badge snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(BadgeStateRequest),
        typeof(BadgeStateView),
        [SnapshotRevisionParameter(false)],
        state_effects: [BadgeRead()],
        messages: BadgeObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor OwnedBadgeList { get; } = new(
        ApplicationMemberIds.BadgesOwnedList,
        "Owned badge page",
        "Reads one fragment- and insertion-ordered owned-badge page from an immutable badge-domain snapshot while preserving each badge's rarity-data presence.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(OwnedBadgePageRequest),
        typeof(OwnedBadgePage),
        PageParameters(),
        state_effects: [BadgeRead()],
        messages:
        [
            Observe(MessageKeys.Badges.Snapshot),
            Observe(MessageKeys.Badges.Received),
            Observe(MessageKeys.Achievements.Notification)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor BadgeSelectedSetsList { get; } = new(
        ApplicationMemberIds.BadgesSelectedSetsList,
        "Selected badge-set page",
        "Reads one user-id ordered page of retained selected-badge sets from an immutable badge-domain snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(BadgeSelectedSetPageRequest),
        typeof(BadgeSelectedSetPage),
        PageParameters(),
        state_effects: [BadgeRead()],
        messages:
        [
            Observe(MessageKeys.Badges.Selected)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor BadgeSelectedList { get; } = new(
        ApplicationMemberIds.BadgesSelectedList,
        "Selected badge page",
        "Reads one wire-ordered selected-badge page for a retained user from the same immutable badge-domain snapshot used by owned badges and selected sets.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(BadgeSelectedPageRequest),
        typeof(BadgeSelectedPage),
        [UserIdParameter(), .. PageParameters()],
        state_effects: [BadgeRead()],
        messages:
        [
            Observe(MessageKeys.Badges.Selected)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor BadgeRefresh { get; } = new(
        ApplicationMemberIds.BadgesRefresh,
        "Refresh owned badges",
        "Coalesces callers within one hotel session and publishes an owned-badge baseline only after every out-of-order fragment is present. Caller cancellation and timeout detach only that waiter; a response-free lane can retire after thirty seconds and surfaces ambiguous retired-baseline correlation explicitly.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(BadgeRefreshRequest),
        typeof(BadgeRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [BadgeChange()],
        [
            Send(MessageKeys.Badges.Request),
            Observe(MessageKeys.Badges.Snapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor BadgeChanged { get; } = new(
        ApplicationMemberIds.BadgesChanged,
        "Badges changed",
        "Publishes bounded badge loading, atomic baseline, live ownership, selected-set, correlation-failure, and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(BadgeChanged),
        state_effects: [BadgeChange()],
        messages: BadgeObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable domain snapshot.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> RefreshParameters() =>
    [
        LimitParameter(),
        new(
            "timeout_milliseconds",
            typeof(int),
            false,
            10000,
            "Maximum wait for this caller; timing out does not cancel the shared session request.",
            new(Minimum: 1, Maximum: 120000)),
        new(
            "expected_session_generation",
            typeof(long?),
            false,
            null,
            "Optional active hotel-session generation required through dispatch and response.",
            new(Minimum: 1))
    ];

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum rows returned from one collection.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first domain read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current domain state.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor UserIdParameter() => new(
        "user_id",
        typeof(Id),
        true,
        null,
        "User identifier whose retained selected-badge set is read.",
        new(Pattern: "^-?[0-9]+$"));

    private static IReadOnlyList<ApplicationMessageRequirement>
        AchievementObservedMessages() =>
    [
        Observe(MessageKeys.Achievements.Snapshot),
        Observe(MessageKeys.Achievements.Updated),
        Observe(MessageKeys.Achievements.Score, false),
        Observe(MessageKeys.Achievements.PointLimits)
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> BadgeObservedMessages() =>
    [
        Observe(MessageKeys.Badges.Snapshot),
        Observe(MessageKeys.Badges.Received),
        Observe(MessageKeys.Badges.Selected),
        Observe(MessageKeys.Achievements.Notification)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);

    private static ApplicationStateEffect AchievementRead() =>
        new(ApplicationStateKey.Achievements, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect AchievementChange() =>
        new(ApplicationStateKey.Achievements, ApplicationStateEffectKind.Changes);

    private static ApplicationStateEffect BadgeRead() =>
        new(ApplicationStateKey.BadgeInventory, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect BadgeChange() =>
        new(ApplicationStateKey.BadgeInventory, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);
    private static ApplicationToolHints RefreshHints() => new(false, false, true, true);
}
