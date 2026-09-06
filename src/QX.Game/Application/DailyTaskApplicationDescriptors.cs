using Qx.Protocol;

namespace Qx.Game.Application;

internal static class DailyTaskApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui |
        ApplicationExposure.Cli |
        ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.DailyTasksState,
        "Daily task state",
        "Reads one bounded state view from a current or retained immutable daily task snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(DailyTaskStateRequest),
        typeof(DailyTaskStateView),
        [SnapshotRevisionParameter(false)],
        state_effects: [ReadEffect()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Entries { get; } = new(
        ApplicationMemberIds.DailyTasksEntriesList,
        "Daily task page",
        "Reads one hotel-ordered daily task page from an immutable snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(DailyTaskPageRequest),
        typeof(DailyTaskPage),
        PageParameters(),
        state_effects: [ReadEffect()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.DailyTasksRefresh,
        "Refresh daily tasks",
        "Dispatches one trusted list request after earlier passive requests drain and returns the first full snapshot committed after dispatch. The response carries no request identifier.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(DailyTaskRefreshRequest),
        typeof(DailyTaskRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.DailyTasks.Request),
            Observe(MessageKeys.DailyTasks.Snapshot)
        ],
        new ApplicationToolHints(false, false, true, true));

    public static ApplicationDescriptor Claim { get; } = new(
        ApplicationMemberIds.DailyTasksClaim,
        "Claim daily task",
        "Dispatches exactly one claim using the protocol's 32-bit task identifier projection and returns at the send boundary without attributing a later task update.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(DailyTaskClaimActionRequest),
        typeof(DailyTaskClaimDispatchReceipt),
        ClaimParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [Send(MessageKeys.DailyTasks.Claim)],
        new ApplicationToolHints(false, true, false, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.DailyTasksChanged,
        "Daily tasks changed",
        "Publishes bounded passive list, addition, progress, completion, claim, and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(DailyTaskChanged),
        state_effects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable daily task snapshot.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> RefreshParameters() =>
    [
        LimitParameter(),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> ClaimParameters() =>
    [
        new(
            "task_id",
            typeof(long),
            true,
            null,
            "Task identifier narrowed to the low signed 32 bits by the Flash wire composer."),
        SessionGenerationParameter()
    ];

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum tasks returned from the first page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum wait for this request and its correlated full snapshot.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor SessionGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation required through dispatch.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first daily task read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current daily task state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.DailyTasks.Snapshot),
        Observe(MessageKeys.DailyTasks.Added),
        Observe(MessageKeys.DailyTasks.Updated)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, Direction.In, ApplicationMessageRole.Observe);

    private static ApplicationStateEffect ReadEffect() =>
        new(ApplicationStateKey.DailyTasks, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect ChangeEffect() =>
        new(ApplicationStateKey.DailyTasks, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);
}
