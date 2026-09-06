using Qx.Protocol;

namespace Qx.Game.Application;

internal static class LeaderboardApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.LeaderboardsState,
        "Leaderboard state",
        "Reads one route from a current or retained immutable leaderboard snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(LeaderboardStateRequest),
        typeof(LeaderboardStateView),
        [ScopeParameter(), WeeklyParameter(), SnapshotRevisionParameter(false)],
        state_effects: [ReadEffect()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Entries { get; } = new(
        ApplicationMemberIds.LeaderboardsEntriesList,
        "Leaderboard entries",
        "Reads one hotel-ordered entry page for a route from an immutable snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(LeaderboardEntryPageRequest),
        typeof(LeaderboardEntryPage),
        [ScopeParameter(), WeeklyParameter(), OffsetParameter(), LimitParameter(), SnapshotRevisionParameter(true)],
        state_effects: [ReadEffect()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.LeaderboardsRefresh,
        "Refresh leaderboard",
        "Dispatches one route request after passive requests drain and returns the first fresh route response matching the game and week. Responses do not identify the requested rank or direction.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(LeaderboardRefreshRequest),
        typeof(LeaderboardRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        AllMessages(),
        new ApplicationToolHints(false, false, true, true));

    public static ApplicationDescriptor WeekOffsetSet { get; } = new(
        ApplicationMemberIds.LeaderboardsWeekOffsetSet,
        "Set leaderboard week offset",
        "Sets the local weekly-board offset, clamped to the latest observed hotel maximum.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(LeaderboardWeekOffsetRequest),
        typeof(LeaderboardWeekOffsetResult),
        [new ApplicationParameterDescriptor("offset", typeof(int), true, null, "Non-negative week offset.", new(Minimum: 0))],
        state_effects: [ChangeEffect()],
        messages: OptionalObservedMessages(),
        tool_hints: new ApplicationToolHints(false, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.LeaderboardsChanged,
        "Leaderboard changed",
        "Publishes bounded route snapshots, week-setting changes, and reset transitions.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(LeaderboardChanged),
        state_effects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static IReadOnlyList<ApplicationParameterDescriptor> RefreshParameters() =>
    [
        new("game_type_id", typeof(int), true, null, "Game type identifier."),
        ScopeParameter(),
        WeeklyParameter(),
        new("start_rank", typeof(int), false, -1, "Requested rank anchor; the response does not echo it."),
        new("direction", typeof(int), false, 0, "Zero walks down and one walks up.", new(Minimum: 0, Maximum: 1)),
        LimitParameter(),
        new("timeout_milliseconds", typeof(int), false, 10000, "Total response budget.", new(Minimum: 1, Maximum: 120000)),
        new("expected_session_generation", typeof(long?), false, null, "Optional active session generation required through dispatch.", new(Minimum: 1))
    ];

    private static ApplicationParameterDescriptor ScopeParameter() => new(
        "scope",
        typeof(LeaderboardScope),
        false,
        LeaderboardScope.Total,
        "Total, friends, or groups leaderboard.");

    private static ApplicationParameterDescriptor WeeklyParameter() => new(
        "weekly",
        typeof(bool),
        false,
        false,
        "Whether to use the weekly route.");

    private static ApplicationParameterDescriptor OffsetParameter() => new(
        "offset",
        typeof(int),
        false,
        0,
        "Zero-based entry offset in the immutable snapshot.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum entries returned.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter(bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApplicationMessageRequirement> AllMessages() =>
    [
        Send(MessageKeys.Leaderboards.Total.Request),
        Observe(MessageKeys.Leaderboards.Total.Snapshot),
        Send(MessageKeys.Leaderboards.Friends.Request),
        Observe(MessageKeys.Leaderboards.Friends.Snapshot),
        Send(MessageKeys.Leaderboards.Groups.Request),
        Observe(MessageKeys.Leaderboards.Groups.Snapshot),
        Send(MessageKeys.Leaderboards.WeeklyTotal.Request),
        Observe(MessageKeys.Leaderboards.WeeklyTotal.Snapshot),
        Send(MessageKeys.Leaderboards.WeeklyFriends.Request),
        Observe(MessageKeys.Leaderboards.WeeklyFriends.Snapshot),
        Send(MessageKeys.Leaderboards.WeeklyGroups.Request),
        Observe(MessageKeys.Leaderboards.WeeklyGroups.Snapshot)
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
        AllMessages().Where(message => message.Role is ApplicationMessageRole.Observe).ToArray();

    private static IReadOnlyList<ApplicationMessageRequirement> OptionalObservedMessages() =>
        ObservedMessages()
            .Select(message => message with { Required = false })
            .ToArray();

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, Direction.In, ApplicationMessageRole.Observe);

    private static ApplicationStateEffect ReadEffect() =>
        new(ApplicationStateKey.Leaderboards, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect ChangeEffect() =>
        new(ApplicationStateKey.Leaderboards, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);
}
