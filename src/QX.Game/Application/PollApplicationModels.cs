using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the current poll state.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PollsState"/>, which returns a <see cref="PollStateView"/>
/// without sending anything to the hotel.
/// </remarks>
public sealed record PollStateRequest;

/// <summary>
/// Represents a request to accept a poll offer.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PollsStart"/>. The start message is sent without waiting for
/// the poll contents, which arrive later as a <see cref="PollChangeKind.Contents"/> change.
/// </remarks>
/// <param name="PollId">
/// The id of the poll taken from the offer. Must be greater than 0 and fit in a 32-bit integer.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// Must not be negative.
/// </param>
public sealed record PollStartRequest(
    Id PollId,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to accept a poll offer and wait for its contents.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PollsContentsGet"/>. One start message is sent, and the
/// request completes with the <see cref="PollStateView"/> of the first contents received afterwards
/// with the same poll id. The contents also reach the game client.
/// </remarks>
/// <param name="PollId">The id of the poll taken from the offer, from 1 to <see cref="int.MaxValue"/>.</param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the contents in milliseconds, from 1 to 120000.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// Must not be negative.
/// </param>
public sealed record PollContentsGetRequest(
    Id PollId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to decline a poll offer.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PollsReject"/>. The message is sent without waiting for a
/// response, and the hotel sends none.
/// </remarks>
/// <param name="PollId">
/// The id of the poll taken from the offer. Must be greater than 0 and fit in a 32-bit integer.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// Must not be negative.
/// </param>
public sealed record PollRejectRequest(
    Id PollId,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the answers to one poll question.
/// </summary>
/// <param name="QuestionId">
/// The id of the question. Must be greater than 0 and fit in a 32-bit integer.
/// </param>
/// <param name="Answers">
/// The answers, at most 500, each at most 65535 UTF-8 bytes. For a choice question an answer is the
/// value of the choice, not its text.
/// </param>
public sealed record PollResponseInput(
    Id QuestionId,
    IReadOnlyList<string> Answers);

/// <summary>
/// Represents a request to send answers to a poll.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PollsAnswer"/>. One message is sent per response. Nothing waits
/// for a response.
/// </remarks>
/// <param name="PollId">
/// The id of the poll. Must be greater than 0 and fit in a 32-bit integer.
/// </param>
/// <param name="Responses">The responses to send, at least one and at most 500.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// Must not be negative.
/// </param>
public sealed record PollAnswerRequest(
    Id PollId,
    IReadOnlyList<PollResponseInput> Responses,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the result of a poll request that was sent to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PollsStart"/>, <see cref="ApplicationMemberIds.PollsReject"/>
/// and <see cref="ApplicationMemberIds.PollsAnswer"/>. It confirms the messages were sent, not that the
/// hotel accepted them.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the request was sent in.</param>
/// <param name="PollId">The id of the poll.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
/// <param name="DispatchedAtUtc">The time the request was sent.</param>
public sealed record PollDispatchReceipt(
    long SessionGeneration,
    Id PollId,
    int MessagesDispatched,
    DateTimeOffset DispatchedAtUtc);

/// <summary>
/// Represents a poll offer received from the hotel.
/// </summary>
/// <param name="PollId">The id of the poll.</param>
/// <param name="Type">The poll type sent by the hotel.</param>
/// <param name="Headline">The headline of the offer.</param>
/// <param name="Summary">The summary text of the offer.</param>
public sealed record PollOfferView(
    Id PollId,
    string Type,
    string Headline,
    string Summary);

/// <summary>
/// Represents one choice of a poll question.
/// </summary>
/// <param name="Value">The value sent as the answer when the choice is selected.</param>
/// <param name="Text">The text shown for the choice.</param>
/// <param name="Type">The choice type sent by the hotel.</param>
public sealed record PollChoiceView(
    string Value,
    string Text,
    int Type);

/// <summary>
/// Represents one poll question.
/// </summary>
/// <param name="QuestionId">The id of the question.</param>
/// <param name="SortOrder">The position of the question in the poll.</param>
/// <param name="Type">The kind of input the question takes.</param>
/// <param name="Text">The question text.</param>
/// <param name="Category">The question category sent by the hotel.</param>
/// <param name="Choices">The choices of a radio button or checkbox question, empty for a text question.</param>
/// <param name="FlashAnswerType">
/// The answer type field of the Flash message, or <see langword="null"/> when the question did not
/// come from a Flash message.
/// </param>
/// <param name="FlashAnswerCount">
/// The answer count field of the Flash message, which equals the number of choices for a choice
/// question, or <see langword="null"/> when the question did not come from a Flash message.
/// </param>
public sealed record PollQuestionView(
    Id QuestionId,
    int SortOrder,
    PollQuestionType Type,
    string Text,
    int Category,
    IReadOnlyList<PollChoiceView> Choices,
    int? FlashAnswerType,
    int? FlashAnswerCount);

/// <summary>
/// Represents a poll question together with its follow-up questions.
/// </summary>
/// <param name="Question">The question.</param>
/// <param name="Children">The follow-up questions, in the order the hotel sent them.</param>
public sealed record PollQuestionGroupView(
    PollQuestionView Question,
    IReadOnlyList<PollQuestionView> Children);

/// <summary>
/// Represents the contents of a started poll.
/// </summary>
/// <param name="PollId">The id of the poll.</param>
/// <param name="StartMessage">The message shown before the questions.</param>
/// <param name="EndMessage">The message shown after the poll is answered.</param>
/// <param name="Questions">The question groups, in the order the hotel sent them.</param>
/// <param name="IsNetPromoterScore">Whether the poll is a net promoter score poll.</param>
public sealed record PollContentsView(
    Id PollId,
    string StartMessage,
    string EndMessage,
    IReadOnlyList<PollQuestionGroupView> Questions,
    bool IsNetPromoterScore);

/// <summary>
/// Represents the poll state of the hotel session.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PollsState"/> and
/// <see cref="ApplicationMemberIds.PollsContentsGet"/>. The offer and contents stay set until newer
/// ones replace them or the hotel session changes.
/// </remarks>
/// <param name="Connected">Whether the state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the state belongs to.</param>
/// <param name="Revision">
/// The poll state revision, increased when an offer, contents or an error is received and when the state resets.
/// </param>
/// <param name="Offer">The last poll offer received in the session, or <see langword="null"/> when none was received.</param>
/// <param name="Contents">
/// The last poll contents received in the session, or <see langword="null"/> when none were received.
/// </param>
/// <param name="LastRequestFailed">
/// Whether the hotel reported a poll error after the last offer or contents was received.
/// </param>
/// <param name="UpdatedAtUtc">
/// The time the revision was committed, or the time of the read when the revision is older than the
/// last 64 revisions.
/// </param>
public sealed record PollStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    PollOfferView? Offer,
    PollContentsView? Contents,
    bool LastRequestFailed,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Specifies the kind of change reported by <see cref="PollChanged"/>.
/// </summary>
public enum PollChangeKind
{
    /// <summary>A poll offer was received.</summary>
    Offer,
    /// <summary>Poll contents were received.</summary>
    Contents,
    /// <summary>The hotel reported a poll error, which carries no poll id or reason.</summary>
    Error,
    /// <summary>The state was cleared because the hotel session changed.</summary>
    Reset
}

/// <summary>
/// Represents a change of the poll state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.PollsChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="State">The poll state after the change.</param>
public sealed record PollChanged(
    PollChangeKind Kind,
    PollStateView State);
