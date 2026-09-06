using Qx.Protocol;

namespace Qx.Game.Application;

internal static class EarningApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui |
        ApplicationExposure.Cli |
        ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.EarningsState,
        "Earning state",
        "Reads one bounded state view from a current or retained immutable earning snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(EarningStateRequest),
        typeof(EarningStateView),
        [SnapshotRevisionParameter(false)],
        state_effects: [ReadEffect()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Entries { get; } = new(
        ApplicationMemberIds.EarningsEntriesList,
        "Earning entry page",
        "Reads one hotel-ordered earning page from an immutable earning snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(EarningEntryPageRequest),
        typeof(EarningEntryPage),
        PageParameters(),
        state_effects: [ReadEffect()],
        messages:
        [
            Observe(MessageKeys.Earnings.StatusSnapshot),
            Observe(MessageKeys.Earnings.Claimed)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.EarningsRefresh,
        "Refresh earnings",
        "Shares one trusted status request within a hotel session after earlier passive requests drain and returns only its correlated full snapshot. Caller cancellation and timeout detach only that waiter.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(EarningRefreshRequest),
        typeof(EarningRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Earnings.StatusRequest),
            Observe(MessageKeys.Earnings.StatusSnapshot)
        ],
        new ApplicationToolHints(false, false, true, true));

    public static ApplicationDescriptor Claim { get; } = new(
        ApplicationMemberIds.EarningsClaim,
        "Claim earnings",
        "Dispatches one claim for a signed wire category after earlier claims for that category drain. Same-category calls remain FIFO and are never coalesced.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(EarningClaimActionRequest),
        typeof(EarningClaimActionResult),
        ClaimParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Earnings.Claim),
            Observe(MessageKeys.Earnings.Claimed)
        ],
        new ApplicationToolHints(false, true, false, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.EarningsChanged,
        "Earnings changed",
        "Publishes bounded passive earning snapshots, claim results, reward notifications, and resets.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(EarningChanged),
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
            "Zero-based offset within the immutable earning snapshot.",
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
            "category",
            typeof(int),
            true,
            null,
            "Signed-byte earning category, including unknown future values and -1 for claim-all.",
            new(Minimum: sbyte.MinValue, Maximum: sbyte.MaxValue)),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum entries returned from the first page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum wait for this caller; timing out does not cancel an already dispatched request.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor SessionGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation required through dispatch and response.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first earning read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current earning state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Earnings.StatusSnapshot),
        Observe(MessageKeys.Earnings.Claimed),
        Observe(MessageKeys.Earnings.Notification, false)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);

    private static ApplicationStateEffect ReadEffect() =>
        new(ApplicationStateKey.Earnings, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect ChangeEffect() =>
        new(ApplicationStateKey.Earnings, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);
}
