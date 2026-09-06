using Qx.Model;
using Qx.Model.Polls;

namespace Qx.Game.Application;

public sealed record PollStateRequest;

public sealed record PollStartRequest(
    Id PollId,
    long? ExpectedSessionGeneration = null);

public sealed record PollContentsGetRequest(
    Id PollId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record PollRejectRequest(
    Id PollId,
    long? ExpectedSessionGeneration = null);

public sealed record PollResponseInput(
    Id QuestionId,
    IReadOnlyList<string> Answers);

public sealed record PollAnswerRequest(
    Id PollId,
    IReadOnlyList<PollResponseInput> Responses,
    long? ExpectedSessionGeneration = null);

public sealed record PollDispatchReceipt(
    ClientType Client,
    long SessionGeneration,
    Id PollId,
    int MessagesDispatched,
    DateTimeOffset DispatchedAtUtc);

public sealed record PollOfferView(
    Id PollId,
    string Type,
    string Headline,
    string Summary);

public sealed record PollChoiceView(
    string Value,
    string Text,
    int Type);

public sealed record PollQuestionView(
    Id QuestionId,
    int SortOrder,
    PollQuestionType Type,
    string Text,
    int Category,
    IReadOnlyList<PollChoiceView> Choices,
    int? FlashAnswerType,
    int? FlashAnswerCount);

public sealed record PollQuestionGroupView(
    PollQuestionView Question,
    IReadOnlyList<PollQuestionView> Children);

public sealed record PollContentsView(
    Id PollId,
    string StartMessage,
    string EndMessage,
    IReadOnlyList<PollQuestionGroupView> Questions,
    bool IsNetPromoterScore);

public sealed record PollStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    PollOfferView? Offer,
    PollContentsView? Contents,
    bool LastRequestFailed,
    DateTimeOffset UpdatedAtUtc);

public enum PollChangeKind
{
    Offer,
    Contents,
    Error,
    Reset
}

public sealed record PollChanged(
    PollChangeKind Kind,
    PollStateView State);
