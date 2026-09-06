using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class PollApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.PollsState,
        "Poll state",
        "Reads the latest immutable poll offer, contents and request-error state for the active hotel session.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(PollStateRequest),
        typeof(PollStateView),
        state_effects: [],
        messages: ObservedMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Start { get; } = Dispatch(
        ApplicationMemberIds.PollsStart,
        "Start poll",
        "Dispatches a poll-start request without assuming that contents will arrive.",
        typeof(PollStartRequest),
        MessageKeys.Polls.Start,
        false);

    public static ApplicationDescriptor ContentsGet { get; } = new(
        ApplicationMemberIds.PollsContentsGet,
        "Get poll contents",
        "Dispatches one poll-start request and waits for later contents with the same poll identifier.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(PollContentsGetRequest),
        typeof(PollStateView),
        [
            PollId(),
            Timeout(),
            SessionGeneration()
        ],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            Send(MessageKeys.Polls.Start),
            Observe(MessageKeys.Polls.Contents),
            new(
                MessageKeys.Polls.Error,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        tool_hints: new(true, false, true, true));

    public static ApplicationDescriptor Reject { get; } = Dispatch(
        ApplicationMemberIds.PollsReject,
        "Reject poll",
        "Dispatches a poll rejection without assuming a server acknowledgement.",
        typeof(PollRejectRequest),
        MessageKeys.Polls.Reject,
        true);

    public static ApplicationDescriptor Answer { get; } = new(
        ApplicationMemberIds.PollsAnswer,
        "Answer poll",
        "Dispatches bounded poll responses without assuming a server acknowledgement.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(PollAnswerRequest),
        typeof(PollDispatchReceipt),
        [
            PollId(),
            new(
                "responses",
                typeof(IReadOnlyList<PollResponseInput>),
                true,
                null,
                "Bounded question responses; Unity permits an empty array.",
                new(MinItems: 0, MaxItems: 500)),
            SessionGeneration()
        ],
        [ApplicationStateKey.HotelConnected],
        messages: [Send(MessageKeys.Polls.Answer)],
        tool_hints: new(false, false, false, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.PollsChanged,
        "Poll changed",
        "Publishes immutable offer, contents, error and session-reset state changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(PollChanged),
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor Dispatch(
        string id,
        string title,
        string description,
        Type request_type,
        MessageKey key,
        bool destructive) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        request_type,
        typeof(PollDispatchReceipt),
        [PollId(), SessionGeneration()],
        [ApplicationStateKey.HotelConnected],
        messages: [Send(key)],
        tool_hints: new(false, destructive, false, true));

    private static ApplicationParameterDescriptor PollId() => new(
        "poll_id",
        typeof(Id),
        true,
        null,
        "Positive poll identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApplicationParameterDescriptor Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for matching poll contents.",
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

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Polls.Offer),
        Observe(MessageKeys.Polls.Contents),
        Observe(MessageKeys.Polls.Error)
    ];
}
