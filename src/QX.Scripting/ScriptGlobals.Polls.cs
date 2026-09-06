using Qx.Model.Messages.Incoming;
using Qx.Model.Polls;
using Qx.Game.Application;

namespace Qx.Scripting;

/// <content>
/// Polls — the questionnaires the hotel offers through a dialog. Available on both the Flash and
/// the Unity client.
/// <para>
/// A poll runs in three steps: the server offers a poll, the client accepts it and receives the
/// questions, then the client sends the answers. The server does not acknowledge the answers.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    public PollOffer? LatestPollOffer => ReadPollState().Offer is { } offer
        ? LegacyPollOffer(offer)
        : null;

    public PollContents? LatestPoll => ReadPollState().Contents is { } contents
        ? LegacyPollContents(contents)
        : null;

    /// <summary>
    /// The identifier of the most recently started poll, or <see langword="null"/> when no poll
    /// contents have been received. This is the id every answer message has to carry.
    /// </summary>
    public Id? LatestPollId => LatestPoll?.PollId;

    /// <summary>
    /// Every question of the most recently started poll flattened into wire order: each group
    /// contributes its own question first, then its follow-up questions.
    /// </summary>
    /// <returns>
    /// The flattened question list, or an empty list when no poll contents have been received.
    /// </returns>
    public IReadOnlyList<PollQuestion> LatestPollQuestions
    {
        get
        {
            if (LatestPoll is not { } poll)
                return [];
            var questions = new List<PollQuestion>();
            foreach (PollQuestionGroup group in poll.Questions)
            {
                questions.Add(group.Question);
                questions.AddRange(group.Children);
            }
            return Array.AsReadOnly(questions.ToArray());
        }
    }

    /// <summary>
    /// Whether the server reported a poll error more recently than it offered or sent a poll. The
    /// error message carries no payload, so it only says that the last poll request was refused.
    /// The flag clears as soon as an offer or poll contents arrive.
    /// </summary>
    public bool LastPollFailed => ReadPollState().LastRequestFailed;

    /// <summary>
    /// Accepts a poll offer, which makes the server send the poll contents. This is what the game
    /// client sends when the user clicks through a poll offer dialog. Returns immediately; the
    /// questions arrive later as poll contents.
    /// </summary>
    /// <param name="poll_id">The poll id taken from the offer.</param>
    public void AcceptPoll(Id poll_id)
    {
        _ = Application.Invoke<PollStartRequest, PollDispatchReceipt>(
            ApplicationMemberIds.PollsStart,
            new PollStartRequest(poll_id),
            Ct);
    }

    /// <summary>
    /// Declines a poll offer without answering any question. Returns immediately; the server sends
    /// nothing back.
    /// </summary>
    /// <param name="poll_id">The poll id taken from the offer.</param>
    public void RejectPoll(Id poll_id)
    {
        _ = Application.Invoke<PollRejectRequest, PollDispatchReceipt>(
            ApplicationMemberIds.PollsReject,
            new PollRejectRequest(poll_id),
            Ct);
    }

    /// <summary>
    /// Accepts a poll offer and waits for the poll contents the server sends back — the awaitable
    /// form of accepting a poll.
    /// </summary>
    /// <param name="poll_id">The poll id taken from the offer.</param>
    /// <param name="timeout_ms">How long to wait for the contents, in milliseconds.</param>
    /// <returns>The poll contents whose poll id matches <paramref name="poll_id"/>.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No matching poll contents arrived in time.</exception>
    /// <exception cref="Qx.Game.RequestDisconnectedException">The connection closed while waiting.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    /// <remarks>
    /// The reply is not blocked, so the game client still receives and shows the poll as usual.
    /// </remarks>
    public async Task<PollContents> RequestPollContents(Id poll_id, int timeout_ms = 10000)
    {
        PollStateView state = await Application
            .InvokeAsync<PollContentsGetRequest, PollStateView>(
                ApplicationMemberIds.PollsContentsGet,
                new PollContentsGetRequest(poll_id, timeout_ms),
                Ct)
            .ConfigureAwait(false);
        if (state.Contents is not { } contents || contents.PollId != poll_id)
            throw new InvalidOperationException("The poll request returned different contents.");
        return LegacyPollContents(contents);
    }

    /// <summary>
    /// Answers a single poll question. On Flash the client sends one message per question, so a
    /// multi-question poll is answered by calling this once per question.
    /// </summary>
    /// <param name="poll_id">The poll being answered.</param>
    /// <param name="question_id">The question being answered.</param>
    /// <param name="answers">
    /// The answers. Radio-button and text questions take exactly one entry; checkbox questions may
    /// take several. For choice questions the answer is the choice's value string, not its display
    /// text.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="answers"/> is null.</exception>
    public void AnswerPoll(Id poll_id, Id question_id, params string[] answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        AnswerPoll(new PollAnswer(poll_id, [new PollResponse(question_id, answers)]));
    }

    /// <summary>
    /// Answers a question of the most recently received poll, taking the poll id from that poll so
    /// only the question has to be supplied.
    /// </summary>
    /// <param name="question">A question from the flattened question list.</param>
    /// <param name="answers">The answers; the rules are the same as for the question-id overload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No poll contents have been received, so there is no poll id to answer for.
    /// </exception>
    public void AnswerPoll(PollQuestion question, params string[] answers)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answers);
        PollStateView state = ReadPollState();
        if (state.Contents is not { } contents)
            throw new InvalidOperationException("No poll contents have been received.");
        SendPollAnswer(
            new PollAnswer(
                contents.PollId,
                [new PollResponse(question.QuestionId, answers)]),
            state.SessionGeneration);
    }

    /// <summary>
    /// Sends a prepared poll answer. Returns immediately; the server sends no acknowledgement.
    /// </summary>
    /// <param name="answer">The poll id and the question responses to send.</param>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    /// The session is Flash and the answer does not hold exactly one question response.
    /// </exception>
    /// <remarks>
    /// The wire shape differs by client: the Flash message carries exactly one question response,
    /// the Unity message carries an array of them.
    /// </remarks>
    public void AnswerPoll(PollAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(answer.Responses);
        PollStateView state = ReadPollState();
        if (state.Client is ClientType.Flash && answer.Responses.Count != 1)
        {
            throw new InvalidDataException(
                "Flash PollAnswer requires exactly one question response.");
        }
        SendPollAnswer(answer, state.SessionGeneration);
    }

    /// <summary>
    /// Raised when the server offers a poll, carrying the poll id, type, headline and summary.
    /// </summary>
    /// <param name="handler">Receives the offer.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnPollOffer(Action<PollOffer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<PollChanged>(
            ApplicationMemberIds.PollsChanged,
            Guarded<PollChanged>(change =>
            {
                if (change.Kind is PollChangeKind.Offer && change.State.Offer is { } offer)
                    handler(LegacyPollOffer(offer));
            })));
    }

    /// <summary>Raised when the server sends the questions of a started poll.</summary>
    /// <param name="handler">Receives the poll contents.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnPollContents(Action<PollContents> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<PollChanged>(
            ApplicationMemberIds.PollsChanged,
            Guarded<PollChanged>(change =>
            {
                if (change.Kind is PollChangeKind.Contents &&
                    change.State.Contents is { } contents)
                {
                    handler(LegacyPollContents(contents));
                }
            })));
    }

    /// <summary>
    /// Raised when the server refuses a poll request. The message has no payload, so it identifies
    /// neither a poll nor a reason.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnPollError(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<PollChanged>(
            ApplicationMemberIds.PollsChanged,
            Guarded<PollChanged>(change =>
            {
                if (change.Kind is PollChangeKind.Error)
                    handler();
            })));
    }

    private PollStateView ReadPollState() =>
        Application.Invoke<PollStateRequest, PollStateView>(
            ApplicationMemberIds.PollsState,
            new PollStateRequest(),
            Ct);

    private void SendPollAnswer(PollAnswer answer, long? expected_session_generation)
    {
        var responses = new PollResponseInput[answer.Responses.Count];
        for (int index = 0; index < responses.Length; index++)
        {
            PollResponse response = answer.Responses[index]
                ?? throw new ArgumentException(
                    "Poll answers cannot contain null responses.",
                    nameof(answer));
            responses[index] = new PollResponseInput(
                response.QuestionId,
                response.Answers);
        }
        _ = Application.Invoke<PollAnswerRequest, PollDispatchReceipt>(
            ApplicationMemberIds.PollsAnswer,
            new PollAnswerRequest(
                answer.PollId,
                Array.AsReadOnly(responses),
                expected_session_generation),
            Ct);
    }

    private static PollOffer LegacyPollOffer(PollOfferView offer) => new(
        offer.PollId,
        offer.Type,
        offer.Headline,
        offer.Summary);

    private static PollContents LegacyPollContents(PollContentsView contents)
    {
        var groups = new PollQuestionGroup[contents.Questions.Count];
        for (int index = 0; index < groups.Length; index++)
        {
            PollQuestionGroupView group = contents.Questions[index];
            var children = new PollQuestion[group.Children.Count];
            for (int child_index = 0; child_index < children.Length; child_index++)
                children[child_index] = LegacyPollQuestion(group.Children[child_index]);
            groups[index] = new PollQuestionGroup(
                LegacyPollQuestion(group.Question),
                Array.AsReadOnly(children));
        }
        return new PollContents(
            contents.PollId,
            contents.StartMessage,
            contents.EndMessage,
            Array.AsReadOnly(groups),
            contents.IsNetPromoterScore);
    }

    private static PollQuestion LegacyPollQuestion(PollQuestionView question)
    {
        var choices = new PollChoice[question.Choices.Count];
        for (int index = 0; index < choices.Length; index++)
        {
            PollChoiceView choice = question.Choices[index];
            choices[index] = new PollChoice(choice.Value, choice.Text, choice.Type);
        }
        return new PollQuestion(
            question.QuestionId,
            question.SortOrder,
            question.Type,
            question.Text,
            question.Category,
            Array.AsReadOnly(choices),
            question.FlashAnswerType,
            question.FlashAnswerCount);
    }
}
