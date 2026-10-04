using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Game.Application;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the most recent poll offer the server sent in the current session, or
    /// <see langword="null"/> when none has been received.
    /// </summary>
    /// <remarks>
    /// The offer stays set until the next offer replaces it or the session is reset.
    /// </remarks>
    public PollOffer? LatestPollOffer => ReadPollState().Offer is { } offer
        ? LegacyPollOffer(offer)
        : null;

    /// <summary>
    /// Gets the contents of the most recently started poll in the current session, or
    /// <see langword="null"/> when no poll contents have been received.
    /// </summary>
    /// <remarks>
    /// The contents stay set until the next poll contents replace them or the session is reset.
    /// </remarks>
    public PollContents? LatestPoll => ReadPollState().Contents is { } contents
        ? LegacyPollContents(contents)
        : null;

    /// <summary>
    /// Gets the identifier of the most recently started poll, or <see langword="null"/> when no
    /// poll contents have been received.
    /// </summary>
    /// <remarks>This is the id every answer message has to carry.</remarks>
    public Id? LatestPollId => LatestPoll?.PollId;

    /// <summary>
    /// Gets every question of the most recently started poll flattened into wire order, or an
    /// empty list when no poll contents have been received.
    /// </summary>
    /// <remarks>
    /// Each group contributes its own question first, then its follow-up questions.
    /// </remarks>
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
    /// Gets whether the server reported a poll error more recently than it offered or sent a poll.
    /// </summary>
    /// <remarks>
    /// The error message carries no payload, so it only says that the last poll request was
    /// refused. The flag clears as soon as an offer or poll contents arrive.
    /// </remarks>
    public bool LastPollFailed => ReadPollState().LastRequestFailed;

    /// <summary>
    /// Accepts a poll offer, which makes the server send the poll contents.
    /// </summary>
    /// <remarks>
    /// This is what the game client sends when the user clicks through a poll offer dialog. It
    /// returns immediately; the questions arrive later as poll contents.
    /// </remarks>
    /// <param name="pollId">The poll id taken from the offer.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void AcceptPoll(Id pollId)
    {
        _ = _application.Invoke<PollStartRequest, PollDispatchReceipt>(
            ApplicationMemberIds.PollsStart,
            new PollStartRequest(pollId),
            Ct);
    }

    /// <summary>
    /// Declines a poll offer without answering any question.
    /// </summary>
    /// <remarks>It returns immediately; the server sends nothing back.</remarks>
    /// <param name="pollId">The poll id taken from the offer.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void RejectPoll(Id pollId)
    {
        _ = _application.Invoke<PollRejectRequest, PollDispatchReceipt>(
            ApplicationMemberIds.PollsReject,
            new PollRejectRequest(pollId),
            Ct);
    }

    /// <summary>
    /// Accepts a poll offer and waits for the poll contents the server sends back.
    /// </summary>
    /// <param name="pollId">The poll id taken from the offer.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The poll contents whose poll id matches <paramref name="pollId"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no matching poll contents arrived in time.</exception>
    /// <exception cref="Qx.Game.RequestDisconnectedException">Thrown when the connection closed while waiting.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    /// <remarks>
    /// This is the awaited form of <see cref="AcceptPoll"/> and sends the same start message. The
    /// reply is not blocked, so the game client still receives and shows the poll as usual.
    /// </remarks>
    public async Task<PollContents> AcceptPollAsync(Id pollId, int timeoutMs = 10000)
    {
        PollStateView state = await _application
            .InvokeAsync<PollContentsGetRequest, PollStateView>(
                ApplicationMemberIds.PollsContentsGet,
                new PollContentsGetRequest(pollId, timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (state.Contents is not { } contents || contents.PollId != pollId)
            throw new InvalidOperationException("The poll request returned different contents.");
        return LegacyPollContents(contents);
    }

    /// <summary>
    /// Answers a single poll question.
    /// </summary>
    /// <remarks>
    /// The game client sends one message per question, so a multi-question poll is answered by
    /// calling this once per question.
    /// </remarks>
    /// <param name="pollId">The poll being answered.</param>
    /// <param name="questionId">The question being answered.</param>
    /// <param name="answers">
    /// The answers. Radio button and text questions take exactly one entry; checkbox questions may
    /// take several. For choice questions the answer is the choice's value string, not its display
    /// text.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="answers"/> is <see langword="null"/>.</exception>
    public void AnswerPoll(Id pollId, Id questionId, params string[] answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        AnswerPoll(new PollAnswer(pollId, [new PollResponse(questionId, answers)]));
    }

    /// <summary>
    /// Answers a question of the most recently received poll, taking the poll id from that poll.
    /// </summary>
    /// <param name="question">A question from <see cref="LatestPollQuestions"/>.</param>
    /// <param name="answers">The answers, following the same rules as <see cref="AnswerPoll(Id, Id, string[])"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="question"/> or <paramref name="answers"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no poll contents have been received, so there is no poll id to answer for.
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
    /// Sends a prepared poll answer for one question.
    /// </summary>
    /// <remarks>
    /// The answer has to carry exactly one response, because the game client answers one question
    /// per message.
    /// </remarks>
    /// <param name="answer">The poll id and the responses to send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="answer"/> or its responses are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the responses contain a <see langword="null"/> entry.</exception>
    /// <exception cref="InvalidDataException">Thrown when the answer does not carry exactly one response.</exception>
    public void AnswerPoll(PollAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(answer.Responses);
        PollStateView state = ReadPollState();
        if (answer.Responses.Count != 1)
        {
            throw new InvalidDataException(
                "A poll answer carries exactly one question response.");
        }
        SendPollAnswer(answer, state.SessionGeneration);
    }

    /// <summary>
    /// Registers a handler that runs when the server offers a poll.
    /// </summary>
    /// <param name="handler">The handler to call with the offer, which carries the poll id, type, headline and summary.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnPollOffer(Action<PollOffer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<PollChanged>(
            ApplicationMemberIds.PollsChanged,
            Guarded<PollChanged>(change =>
            {
                if (change.Kind is PollChangeKind.Offer && change.State.Offer is { } offer)
                    handler(LegacyPollOffer(offer));
            })));
    }

    /// <summary>Registers a handler that runs when the server sends the questions of a started poll.</summary>
    /// <param name="handler">The handler to call with the poll contents.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnPollContents(Action<PollContents> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<PollChanged>(
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
    /// Registers a handler that runs when the server refuses a poll request.
    /// </summary>
    /// <remarks>
    /// The message has no payload, so it identifies neither a poll nor a reason.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnPollError(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<PollChanged>(
            ApplicationMemberIds.PollsChanged,
            Guarded<PollChanged>(change =>
            {
                if (change.Kind is PollChangeKind.Error)
                    handler();
            })));
    }

    private PollStateView ReadPollState() =>
        _application.Invoke<PollStateRequest, PollStateView>(
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
        _ = _application.Invoke<PollAnswerRequest, PollDispatchReceipt>(
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
