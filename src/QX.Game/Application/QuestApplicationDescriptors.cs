using Qx.Protocol;

namespace Qx.Game.Application;

internal static class QuestApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui |
        ApplicationExposure.Cli |
        ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.QuestsState,
        "Quest state",
        "Reads one bounded state view from a current or retained immutable quest snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(QuestStateRequest),
        typeof(QuestStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Entries { get; } = new(
        ApplicationMemberIds.QuestsEntriesList,
        "Quest entry page",
        "Reads one hotel-ordered available, seasonal, or combined quest page from an immutable snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(QuestEntryPageRequest),
        typeof(QuestEntryPage),
        PageParameters(),
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor AvailableRefresh { get; } = new(
        ApplicationMemberIds.QuestsAvailableRefresh,
        "Refresh available quests",
        "Reloads the available quests from the server. Shares one trusted available-quest request after earlier passive requests drain and returns the first correlated full snapshot; the response carries no request identifier.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(QuestAvailableRefreshRequest),
        typeof(QuestAvailableRefreshResult),
        ListRefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Quests.Request),
            Observe(MessageKeys.Quests.Snapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor SeasonalRefresh { get; } = new(
        ApplicationMemberIds.QuestsSeasonalRefresh,
        "Refresh seasonal quests",
        "Reloads the seasonal quests from the server. Shares one trusted seasonal-quest request after earlier passive requests drain and returns the first correlated full snapshot; the response carries no request identifier.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(QuestSeasonalRefreshRequest),
        typeof(QuestSeasonalRefreshResult),
        ListRefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Quests.SeasonalRequest),
            Observe(MessageKeys.Quests.SeasonalSnapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor DailyRefresh { get; } = new(
        ApplicationMemberIds.QuestsDailyRefresh,
        "Refresh daily quest",
        "Requests a daily quest from the server. Queues daily requests by their pool and index because the response carries no request identifier; identical requests share one flight.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(QuestDailyRefreshRequest),
        typeof(QuestDailyRefreshResult),
        DailyRefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Quests.DailyRequest),
            Observe(MessageKeys.Quests.Daily)
        ],
        RefreshHints());

    public static ApplicationDescriptor Accept { get; } = SelectionAction(
        ApplicationMemberIds.QuestsAccept,
        "Accept quest",
        "Dispatches exactly one quest acceptance and returns at the send boundary.",
        MessageKeys.Quests.Accept);

    public static ApplicationDescriptor Activate { get; } = SelectionAction(
        ApplicationMemberIds.QuestsActivate,
        "Activate quest",
        "Dispatches exactly one quest activation and returns at the send boundary.",
        MessageKeys.Quests.Activate);

    public static ApplicationDescriptor Reject { get; } = SelectionAction(
        ApplicationMemberIds.QuestsReject,
        "Reject quest",
        "Dispatches exactly one quest rejection and returns at the send boundary.",
        MessageKeys.Quests.Reject);

    public static ApplicationDescriptor Cancel { get; } = EmptyAction(
        ApplicationMemberIds.QuestsCancel,
        "Cancel quest",
        "Dispatches exactly one cancellation for the active quest and returns at the send boundary.",
        MessageKeys.Quests.Cancel,
        true);

    public static ApplicationDescriptor TrackerOpen { get; } = EmptyAction(
        ApplicationMemberIds.QuestsTrackerOpen,
        "Open quest tracker",
        "Dispatches exactly one tracker-open notification and returns at the send boundary.",
        MessageKeys.Quests.TrackerOpen,
        false);

    public static ApplicationDescriptor FriendRequestComplete { get; } = EmptyAction(
        ApplicationMemberIds.QuestsFriendRequestComplete,
        "Complete friend-request quest step",
        "Dispatches exactly one friend-request quest progress notification and returns at the send boundary.",
        MessageKeys.Quests.FriendRequestCompleted,
        true);

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.QuestsChanged,
        "Quests changed",
        "Publishes bounded available, seasonal, current, completion, cancellation, daily, and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(QuestChanged),
        stateEffects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocationScope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor SelectionAction(
        string id,
        string title,
        string description,
        MessageKey key) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(QuestSelectionActionRequest),
            typeof(QuestSelectionDispatchReceipt),
            SelectionParameters(),
            [ApplicationStateKey.HotelConnected],
            [ChangeEffect()],
            [Send(key)],
            ActionHints(true));

    private static ApplicationDescriptor EmptyAction(
        string id,
        string title,
        string description,
        MessageKey key,
        bool destructive) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(QuestDispatchRequest),
            typeof(QuestDispatchReceipt),
            [SessionGenerationParameter()],
            [ApplicationStateKey.HotelConnected],
            [ChangeEffect()],
            [Send(key)],
            ActionHints(destructive));

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new(
            "collection",
            typeof(QuestCollection),
            false,
            QuestCollection.Available,
            "Available, seasonal, or combined hotel-order view."),
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable quest collection.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> ListRefreshParameters() =>
    [
        LimitParameter(),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> DailyRefreshParameters() =>
    [
        new(
            "is_easy",
            typeof(bool),
            true,
            null,
            "Whether the daily quest is requested from the easy pool."),
        new(
            "index",
            typeof(int),
            true,
            null,
            "Hotel-defined index within the selected daily quest pool."),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> SelectionParameters() =>
    [
        new(
            "quest_id",
            typeof(long),
            true,
            null,
            "Quest identifier, sent as a 32-bit integer."),
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
            ? "Snapshot revision returned by the first quest read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current quest state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Quests.Snapshot),
        Observe(MessageKeys.Quests.SeasonalSnapshot),
        Observe(MessageKeys.Quests.Updated),
        Observe(MessageKeys.Quests.Completed),
        Observe(MessageKeys.Quests.Cancelled),
        Observe(MessageKeys.Quests.Daily)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApplicationMessageRole.Observe);

    private static ApplicationStateEffect ReadEffect() =>
        new(ApplicationStateKey.Quests, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect ChangeEffect() =>
        new(ApplicationStateKey.Quests, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);

    private static ApplicationToolHints RefreshHints() => new(true, false, true, true);

    private static ApplicationToolHints ActionHints(bool destructive) =>
        new(false, destructive, false, true);
}
