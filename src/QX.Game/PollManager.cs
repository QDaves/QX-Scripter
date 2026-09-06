using System.Runtime.ExceptionServices;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Polls;

namespace Qx.Game;

internal sealed record PollState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    PollOffer? Offer,
    PollContents? Contents,
    bool LastRequestFailed);

internal enum PollStateChangeKind
{
    Offer,
    Contents,
    Error,
    Reset
}

internal sealed record PollStateUpdate(
    PollStateChangeKind Kind,
    PollState State);

internal sealed class PollManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<PollStateUpdate> publications = [];
    private PollState state = new(null, 0, 0, null, null, false);
    private long committed_generation;
    private long reset_generation = -1;
    private bool publishing;

    internal PollState State => Volatile.Read(ref state);
    internal event Action<PollStateUpdate>? StateCommitted;
    internal event Action<PollStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(CommitReset);
        OnIncoming(MessageContracts.Polls.Offer, ApplyOffer);
        OnIncoming(MessageContracts.Polls.Contents, ApplyContents);
        OnIncoming(MessageContracts.Polls.Error, ApplyError);
    }

    protected override void Reset() => CommitReset(CurrentSession);

    private void ApplyOffer(PollOffer message, long state_generation)
    {
        PollOffer offer = Freeze(message);
        Store(
            state_generation,
            PollStateChangeKind.Offer,
            current => current with
            {
                Offer = offer,
                LastRequestFailed = false
            });
    }

    private void ApplyContents(PollContents message, long state_generation)
    {
        PollContents contents = Freeze(message);
        Store(
            state_generation,
            PollStateChangeKind.Contents,
            current => current with
            {
                Contents = contents,
                LastRequestFailed = false
            });
    }

    private void ApplyError(PollError _, long state_generation) => Store(
        state_generation,
        PollStateChangeKind.Error,
        current => current with { LastRequestFailed = true });

    private void CommitReset(Session? active_session)
    {
        long state_generation = CurrentStateGeneration;
        bool drain;
        lock (publication_sync)
        {
            PollState updated;
            lock (state_sync)
            {
                PollState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                committed_generation = state_generation;
                reset_generation = active_session is null ? state_generation : -1;
                updated = current with
                {
                    Session = active_session,
                    SessionGeneration = state_generation,
                    Revision = checked(current.Revision + 1),
                    Offer = null,
                    Contents = null,
                    LastRequestFailed = false
                };
                Volatile.Write(ref state, updated);
            }
            var update = new PollStateUpdate(PollStateChangeKind.Reset, updated);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private void Store(
        long state_generation,
        PollStateChangeKind kind,
        Func<PollState, PollState> mutation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        lock (publication_sync)
        {
            PollState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                PollState current = state;
                if (current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    current = current with
                    {
                        Session = active_session,
                        SessionGeneration = state_generation,
                        Offer = null,
                        Contents = null,
                        LastRequestFailed = false
                    };
                }
                updated = mutation(current) with
                {
                    Revision = checked(state.Revision + 1)
                };
                committed_generation = state_generation;
                reset_generation = -1;
                Volatile.Write(ref state, updated);
            }
            var update = new PollStateUpdate(kind, updated);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private bool EnqueuePublication(PollStateUpdate update)
    {
        publications.Enqueue(update);
        if (publishing)
            return false;
        publishing = true;
        return true;
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            PollStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
            }
            try
            {
                StateChanged?.Invoke(update);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static PollOffer Freeze(PollOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer.Type);
        ArgumentNullException.ThrowIfNull(offer.Headline);
        ArgumentNullException.ThrowIfNull(offer.Summary);
        return new PollOffer(
            offer.PollId,
            offer.Type,
            offer.Headline,
            offer.Summary);
    }

    private static PollContents Freeze(PollContents contents)
    {
        ArgumentNullException.ThrowIfNull(contents.StartMessage);
        ArgumentNullException.ThrowIfNull(contents.EndMessage);
        ArgumentNullException.ThrowIfNull(contents.Questions);
        var groups = new PollQuestionGroup[contents.Questions.Count];
        for (int index = 0; index < groups.Length; index++)
            groups[index] = Freeze(contents.Questions[index]);
        return new PollContents(
            contents.PollId,
            contents.StartMessage,
            contents.EndMessage,
            Array.AsReadOnly(groups),
            contents.IsNetPromoterScore);
    }

    private static PollQuestionGroup Freeze(PollQuestionGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(group.Question);
        ArgumentNullException.ThrowIfNull(group.Children);
        var children = new PollQuestion[group.Children.Count];
        for (int index = 0; index < children.Length; index++)
            children[index] = Freeze(group.Children[index]);
        return new PollQuestionGroup(
            Freeze(group.Question),
            Array.AsReadOnly(children));
    }

    private static PollQuestion Freeze(PollQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(question.Text);
        ArgumentNullException.ThrowIfNull(question.Choices);
        var choices = new PollChoice[question.Choices.Count];
        for (int index = 0; index < choices.Length; index++)
            choices[index] = Freeze(question.Choices[index]);
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

    private static PollChoice Freeze(PollChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(choice.Value);
        ArgumentNullException.ThrowIfNull(choice.Text);
        return new PollChoice(choice.Value, choice.Text, choice.Type);
    }
}
