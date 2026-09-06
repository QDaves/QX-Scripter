using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Polls;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class PollApplication : IApplicationFeature
{
    private const int maximum_responses = 500;
    private const int maximum_answers = 500;
    private const int timestamp_limit = 64;
    private readonly object lifecycle_sync = new();
    private readonly object operations_sync = new();
    private readonly IConnection connection;
    private readonly PollManager polls;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<PollChanged> changed;
    private readonly HashSet<PollGetOperation> pending_gets = [];
    private readonly SortedDictionary<long, DateTimeOffset> timestamps = [];
    private int active_dispatches;
    private int disposed;

    public PollApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        polls = game.Polls;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<PollChanged>(observer_error);
        PollState initial = polls.State;
        timestamps[initial.Revision] = time_provider.GetUtcNow();

        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<PollStateRequest, PollStateView>(
                PollApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<PollStartRequest, PollDispatchReceipt>(
                PollApplicationDescriptors.Start,
                (request, cancellation_token) =>
                    ValueTask.FromResult(Start(request, cancellation_token))),
            new ApplicationCallBinding<PollContentsGetRequest, PollStateView>(
                PollApplicationDescriptors.ContentsGet,
                GetContents),
            new ApplicationCallBinding<PollRejectRequest, PollDispatchReceipt>(
                PollApplicationDescriptors.Reject,
                (request, cancellation_token) =>
                    ValueTask.FromResult(Reject(request, cancellation_token))),
            new ApplicationCallBinding<PollAnswerRequest, PollDispatchReceipt>(
                PollApplicationDescriptors.Answer,
                (request, cancellation_token) =>
                    ValueTask.FromResult(Answer(request, cancellation_token))),
            new ApplicationEventBinding<PollChanged>(
                PollApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);

        polls.StateCommitted += OnStateCommitted;
        polls.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public PollStateView ReadState(PollStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        PollStateView view = CaptureView();
        ThrowIfDisposed();
        return view;
    }

    public PollDispatchReceipt Start(
        PollStartRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PollOperationScope scope = CaptureScope(
            request.PollId,
            request.ExpectedSessionGeneration,
            cancellation_token);
        Dispatch(
            MessageContracts.Polls.Start,
            new StartPoll(request.PollId),
            scope,
            cancellation_token);
        return Receipt(scope, 1);
    }

    public async ValueTask<PollStateView> GetContents(
        PollContentsGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        if ((long)request.PollId > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PollId),
                "Poll contents identify polls with the 32-bit wire format.");
        }
        long started = time_provider.GetTimestamp();
        PollOperationScope scope = CaptureScope(
            request.PollId,
            request.ExpectedSessionGeneration,
            cancellation_token);
        var operation = new PollGetOperation(scope);
        lock (operations_sync)
        {
            ThrowIfDisposed();
            pending_gets.Add(operation);
        }

        try
        {
            EnterDispatch();
            try
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Polls.Start,
                    new StartPoll(request.PollId),
                    scope.Session,
                    cancellation_token,
                    () => Arm(operation));
            }
            finally
            {
                ExitDispatch();
            }

            PollState state;
            try
            {
                TimeSpan remaining = TimeSpan.FromMilliseconds(request.TimeoutMilliseconds) -
                    time_provider.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException();
                state = await operation.Completion.Task.WaitAsync(
                    remaining,
                    time_provider,
                    cancellation_token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new RequestTimeoutException(
                    MessageKeys.Polls.Start.Value,
                    MessageKeys.Polls.Contents.Value,
                    request.TimeoutMilliseconds);
            }
            catch (OperationCanceledException) when (Volatile.Read(ref disposed) != 0)
            {
                throw Disposed();
            }

            RequireScope(scope);
            PollStateView view = StateView(state, Timestamp(state.Revision));
            RequireScope(scope);
            return view;
        }
        finally
        {
            lock (operations_sync)
                pending_gets.Remove(operation);
        }
    }

    public PollDispatchReceipt Reject(
        PollRejectRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PollOperationScope scope = CaptureScope(
            request.PollId,
            request.ExpectedSessionGeneration,
            cancellation_token);
        Dispatch(
            MessageContracts.Polls.Reject,
            new RejectPoll(request.PollId),
            scope,
            cancellation_token);
        return Receipt(scope, 1);
    }

    public PollDispatchReceipt Answer(
        PollAnswerRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        PollOperationScope scope = CaptureScope(
            request.PollId,
            request.ExpectedSessionGeneration,
            cancellation_token);
        PollResponse[] responses = PrepareResponses(request.Responses, scope.Session.Client);
        int messages_dispatched = 0;
        EnterDispatch();
        try
        {
            if (scope.Session.Client is ClientType.Flash)
            {
                foreach (PollResponse response in responses)
                {
                    message_dispatcher.Dispatch(
                        MessageContracts.Polls.Answer,
                        new PollAnswer(request.PollId, Array.AsReadOnly([response])),
                        scope.Session,
                        cancellation_token,
                        () => RequireScope(scope));
                    messages_dispatched++;
                }
            }
            else
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Polls.Answer,
                    new PollAnswer(request.PollId, Array.AsReadOnly(responses)),
                    scope.Session,
                    cancellation_token,
                    () => RequireScope(scope));
                messages_dispatched = 1;
            }
        }
        finally
        {
            ExitDispatch();
        }
        return Receipt(scope, messages_dispatched);
    }

    public void Dispose()
    {
        lock (lifecycle_sync)
        {
            if (disposed != 0)
                return;
            Volatile.Write(ref disposed, 1);
            while (active_dispatches != 0)
                Monitor.Wait(lifecycle_sync);
        }

        polls.StateCommitted -= OnStateCommitted;
        polls.StateChanged -= OnStateChanged;
        PollGetOperation[] operations;
        lock (operations_sync)
        {
            operations = pending_gets.ToArray();
            pending_gets.Clear();
        }
        foreach (PollGetOperation operation in operations)
            operation.Completion.TrySetException(Disposed());
        changed.Dispose();
    }

    private void Dispatch<T>(
        MessageContract<T> contract,
        T message,
        PollOperationScope scope,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        EnterDispatch();
        try
        {
            message_dispatcher.Dispatch(
                contract,
                message,
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        }
        finally
        {
            ExitDispatch();
        }
    }

    private PollOperationScope CaptureScope(
        Id poll_id,
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        ValidateId(poll_id, nameof(poll_id));
        ValidateGeneration(expected_session_generation, nameof(expected_session_generation));
        PollState state = polls.State;
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(state.Session, session))
            throw new InvalidOperationException("The poll state is not bound to the active hotel session.");
        if (expected_session_generation is long generation && state.SessionGeneration != generation)
            throw new InvalidOperationException("The expected hotel-session generation is no longer active.");
        ValidateWireId(session.Client, poll_id, nameof(poll_id));
        return new PollOperationScope(session, state.SessionGeneration, state.Revision, poll_id);
    }

    private void RequireScope(PollOperationScope scope)
    {
        ThrowIfDisposed();
        PollState state = polls.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw Disconnected();
        }
    }

    private void Arm(PollGetOperation operation)
    {
        RequireScope(operation.Scope);
        lock (operations_sync)
        {
            ThrowIfDisposed();
            if (!pending_gets.Contains(operation))
                throw new InvalidOperationException("The poll-contents request is no longer active.");
            operation.BaselineRevision = polls.State.Revision;
            operation.Armed = true;
        }
    }

    private void OnStateCommitted(PollStateUpdate update)
    {
        DateTimeOffset committed_at = time_provider.GetUtcNow();
        PollGetOperation[] completed = [];
        PollGetOperation[] disconnected = [];
        lock (operations_sync)
        {
            timestamps[update.State.Revision] = committed_at;
            while (timestamps.Count > timestamp_limit)
                timestamps.Remove(timestamps.Keys.First());
            if (disposed != 0)
                return;
            if (update.Kind is PollStateChangeKind.Contents &&
                update.State.Contents is { } contents)
            {
                completed = pending_gets
                    .Where(operation =>
                        operation.Armed &&
                        update.State.Revision > operation.BaselineRevision &&
                        ReferenceEquals(update.State.Session, operation.Scope.Session) &&
                        update.State.SessionGeneration == operation.Scope.SessionGeneration &&
                        contents.PollId == operation.Scope.PollId)
                    .ToArray();
            }
            if (update.Kind is PollStateChangeKind.Reset)
            {
                disconnected = pending_gets
                    .Where(operation =>
                        !ReferenceEquals(update.State.Session, operation.Scope.Session) ||
                        update.State.SessionGeneration != operation.Scope.SessionGeneration)
                    .ToArray();
            }
        }
        foreach (PollGetOperation operation in completed)
            operation.Completion.TrySetResult(update.State);
        foreach (PollGetOperation operation in disconnected)
            operation.Completion.TrySetException(Disconnected());
    }

    private void OnStateChanged(PollStateUpdate update)
    {
        if (Volatile.Read(ref disposed) != 0)
            return;
        changed.Publish(new PollChanged(
            ChangeKind(update.Kind),
            StateView(update.State, Timestamp(update.State.Revision))));
    }

    private PollStateView CaptureView()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Session? before = connection.Session;
            PollState state = polls.State;
            Session? after = connection.Session;
            if (ReferenceEquals(before, after))
                return StateView(state, Timestamp(state.Revision), before);
        }
        throw new InvalidOperationException("The hotel session changed while the poll state was being read.");
    }

    private PollStateView StateView(
        PollState state,
        DateTimeOffset updated_at,
        Session? active_session = null)
    {
        active_session ??= connection.Session;
        bool connected = state.Session is not null && ReferenceEquals(state.Session, active_session);
        return new PollStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.Offer is null ? null : Offer(state.Offer),
            state.Contents is null ? null : Contents(state.Contents),
            state.LastRequestFailed,
            updated_at);
    }

    private static PollOfferView Offer(PollOffer offer) => new(
        offer.PollId,
        offer.Type,
        offer.Headline,
        offer.Summary);

    private static PollContentsView Contents(PollContents contents)
    {
        var groups = new PollQuestionGroupView[contents.Questions.Count];
        for (int index = 0; index < groups.Length; index++)
        {
            PollQuestionGroup group = contents.Questions[index];
            var children = new PollQuestionView[group.Children.Count];
            for (int child_index = 0; child_index < children.Length; child_index++)
                children[child_index] = Question(group.Children[child_index]);
            groups[index] = new PollQuestionGroupView(
                Question(group.Question),
                Array.AsReadOnly(children));
        }
        return new PollContentsView(
            contents.PollId,
            contents.StartMessage,
            contents.EndMessage,
            Array.AsReadOnly(groups),
            contents.IsNetPromoterScore);
    }

    private static PollQuestionView Question(PollQuestion question)
    {
        var choices = new PollChoiceView[question.Choices.Count];
        for (int index = 0; index < choices.Length; index++)
        {
            PollChoice choice = question.Choices[index];
            choices[index] = new PollChoiceView(choice.Value, choice.Text, choice.Type);
        }
        return new PollQuestionView(
            question.QuestionId,
            question.SortOrder,
            question.Type,
            question.Text,
            question.Category,
            Array.AsReadOnly(choices),
            question.FlashAnswerType,
            question.FlashAnswerCount);
    }

    private PollDispatchReceipt Receipt(PollOperationScope scope, int messages_dispatched) => new(
        scope.Session.Client,
        scope.SessionGeneration,
        scope.PollId,
        messages_dispatched,
        time_provider.GetUtcNow());

    private DateTimeOffset Timestamp(long revision)
    {
        lock (operations_sync)
            return timestamps.GetValueOrDefault(revision, time_provider.GetUtcNow());
    }

    private static PollResponse[] PrepareResponses(
        IReadOnlyList<PollResponseInput>? values,
        ClientType client)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > maximum_responses)
            throw new ArgumentOutOfRangeException(nameof(values));
        if (client is ClientType.Flash && values.Count == 0)
            throw new InvalidDataException("Flash poll answers require at least one response.");
        var responses = new PollResponse[values.Count];
        for (int index = 0; index < responses.Length; index++)
        {
            PollResponseInput value = values[index]
                ?? throw new ArgumentException("Poll responses cannot contain null entries.", nameof(values));
            ValidateId(value.QuestionId, nameof(value.QuestionId));
            ValidateWireId(client, value.QuestionId, nameof(value.QuestionId));
            ArgumentNullException.ThrowIfNull(value.Answers);
            if (value.Answers.Count > maximum_answers)
                throw new ArgumentOutOfRangeException(nameof(value.Answers));
            string[] answers = value.Answers.ToArray();
            foreach (string answer in answers)
            {
                ArgumentNullException.ThrowIfNull(answer);
                if (System.Text.Encoding.UTF8.GetByteCount(answer) > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(value.Answers));
            }
            responses[index] = new PollResponse(
                value.QuestionId,
                Array.AsReadOnly(answers));
        }
        return responses;
    }

    private void EnterDispatch()
    {
        lock (lifecycle_sync)
        {
            ThrowIfDisposed();
            active_dispatches = checked(active_dispatches + 1);
        }
    }

    private void ExitDispatch()
    {
        lock (lifecycle_sync)
        {
            active_dispatches--;
            if (active_dispatches < 0)
                throw new InvalidOperationException("The poll dispatch count became negative.");
            if (active_dispatches == 0)
                Monitor.PulseAll(lifecycle_sync);
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static ObjectDisposedException Disposed() => new(nameof(PollApplication));

    private static RequestDisconnectedException Disconnected() => new(
        MessageKeys.Polls.Start.Value,
        MessageKeys.Polls.Contents.Value);

    private static void ValidateId(Id id, string name)
    {
        if ((long)id <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateWireId(ClientType client, Id id, string name)
    {
        if (client is ClientType.Flash && (long)id > int.MaxValue)
            throw new ArgumentOutOfRangeException(name, "The identifier does not fit the Flash wire format.");
    }

    private static void ValidateGeneration(long? generation, string name)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static PollChangeKind ChangeKind(PollStateChangeKind kind) => kind switch
    {
        PollStateChangeKind.Offer => PollChangeKind.Offer,
        PollStateChangeKind.Contents => PollChangeKind.Contents,
        PollStateChangeKind.Error => PollChangeKind.Error,
        PollStateChangeKind.Reset => PollChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private readonly record struct PollOperationScope(
        Session Session,
        long SessionGeneration,
        long Revision,
        Id PollId);

    private sealed class PollGetOperation(PollOperationScope scope)
    {
        public TaskCompletionSource<PollState> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public PollOperationScope Scope { get; } = scope;
        public long BaselineRevision { get; set; }
        public bool Armed { get; set; }
    }
}
