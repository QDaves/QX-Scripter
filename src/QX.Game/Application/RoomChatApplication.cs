using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

internal interface IRoomChatOperations
{
    RoomChatSendResult Talk(
        RoomChatTalkRequest request,
        CancellationToken cancellation_token = default);

    RoomChatSendResult Shout(
        RoomChatShoutRequest request,
        CancellationToken cancellation_token = default);
}

internal sealed class RoomChatApplication : IApplicationFeature, IRoomChatOperations
{
    private readonly IConnection _connection;
    private readonly GameState _game;
    private readonly RoomChatJournal _journal;
    private readonly TimeProvider _time_provider;
    private int _disposed;

    public RoomChatApplication(
        IInterceptor interceptor,
        GameState game,
        TimeProvider? time_provider = null,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        _connection = interceptor;
        _game = game;
        _time_provider = time_provider ?? TimeProvider.System;
        _journal = new RoomChatJournal(
            interceptor,
            game.Room,
            _time_provider,
            observer_error: observer_error);

        try
        {
            ApplicationDescriptor history = HistoryDescriptor();
            ApplicationDescriptor talk = TalkDescriptor();
            ApplicationDescriptor shout = ShoutDescriptor();
            ApplicationDescriptor whisper = WhisperDescriptor();
            ApplicationDescriptor received = ReceivedDescriptor();
            Bindings = Array.AsReadOnly<IApplicationBinding>(
            [
                new ApplicationCallBinding<RoomChatHistoryRequest, RoomChatHistoryPage>(
                    history,
                    (request, _) => ValueTask.FromResult(History(request))),
                new ApplicationCallBinding<RoomChatTalkRequest, RoomChatSendResult>(
                    talk,
                    (request, cancellation_token) =>
                        ValueTask.FromResult(Talk(request, cancellation_token))),
                new ApplicationCallBinding<RoomChatShoutRequest, RoomChatSendResult>(
                    shout,
                    (request, cancellation_token) =>
                        ValueTask.FromResult(Shout(request, cancellation_token))),
                new ApplicationCallBinding<RoomChatWhisperRequest, RoomChatWhisperResult>(
                    whisper,
                    (request, cancellation_token) =>
                        ValueTask.FromResult(Whisper(request, cancellation_token))),
                new ApplicationEventBinding<RoomChatEntry>(received, Subscribe)
            ]);
            game.BindRoomChatOperations(this);
        }
        catch
        {
            _journal.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomChatHistoryPage History(RoomChatHistoryRequest request)
    {
        ThrowIfDisposed();
        return _journal.History(request);
    }

    public RoomChatSendResult Talk(
        RoomChatTalkRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("The message cannot be empty.", nameof(request.Message));
        (Session session, Id? room_id, long room_generation) = CaptureRoom();
        _game.RoomActions.Talk(
            request.Message,
            request.Bubble,
            session,
            room_generation,
            cancellation_token);
        return new RoomChatSendResult(
            session.Client,
            room_id,
            room_generation,
            true,
            false,
            _time_provider.GetUtcNow());
    }

    public RoomChatSendResult Shout(
        RoomChatShoutRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("The message cannot be empty.", nameof(request.Message));
        (Session session, Id? room_id, long room_generation) = CaptureRoom();
        _game.RoomActions.Shout(
            request.Message,
            request.Bubble,
            session,
            room_generation,
            cancellation_token);
        return new RoomChatSendResult(
            session.Client,
            room_id,
            room_generation,
            true,
            false,
            _time_provider.GetUtcNow());
    }

    public RoomChatWhisperResult Whisper(
        RoomChatWhisperRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Recipient))
            throw new ArgumentException("The recipient cannot be empty.", nameof(request.Recipient));
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("The message cannot be empty.", nameof(request.Message));

        (Session session, Id? room_id, long room_generation) = CaptureRoom();
        _game.RoomActions.Whisper(
            request.Recipient,
            request.Message,
            request.Bubble,
            session,
            room_generation,
            cancellation_token);
        return new RoomChatWhisperResult(
            session.Client,
            room_id,
            room_generation,
            true,
            false,
            _time_provider.GetUtcNow());
    }

    public IDisposable Subscribe(Action<RoomChatEntry> listener)
    {
        ThrowIfDisposed();
        return _journal.Subscribe(listener);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _game.UnbindRoomChatOperations(this);
        _journal.Dispose();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private static ApplicationDescriptor HistoryDescriptor() => new(
        ApplicationMemberIds.RoomChatHistory,
        "Room chat history",
        "Reads the bounded room-chat journal with stable cursor paging.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(RoomChatHistoryRequest),
        typeof(RoomChatHistoryPage),
        [
            new(
                "after_sequence",
                typeof(long),
                false,
                0L,
                "Return entries after this sequence.",
                new(Pattern: "^[0-9]+$")),
            new(
                "limit",
                typeof(int),
                false,
                100,
                "Maximum number of entries to return.",
                new(Minimum: 1, Maximum: 500))
        ],
        messages: ChatMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor TalkDescriptor() => SendDescriptor<RoomChatTalkRequest>(
        ApplicationMemberIds.RoomChatTalk,
        "Room chat message",
        "Sends a public message in the current room.",
        MessageKeys.Room.Chat.TalkSend);

    private static ApplicationDescriptor ShoutDescriptor() => SendDescriptor<RoomChatShoutRequest>(
        ApplicationMemberIds.RoomChatShout,
        "Room shout",
        "Sends a public shout in the current room.",
        MessageKeys.Room.Chat.ShoutSend);

    private static ApplicationDescriptor WhisperDescriptor() => new(
        ApplicationMemberIds.RoomChatWhisper,
        "Private room message",
        "Sends a private room-chat message through the active client dialect.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomChatWhisperRequest),
        typeof(RoomChatWhisperResult),
        [
            new(
                "recipient",
                typeof(string),
                true,
                null,
                "Recipient name in the current room.",
                new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*")),
            new(
                "message",
                typeof(string),
                true,
                null,
                "Message text.",
                new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*")),
            new("bubble", typeof(int), false, 0, "Chat bubble style identifier.")
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        messages:
        [
            new(
                MessageKeys.Room.Chat.WhisperSend,
                Direction.Out,
                ApplicationMessageRole.Send)
        ],
        tool_hints: new(false, true, false, true));

    private static ApplicationDescriptor ReceivedDescriptor() => new(
        ApplicationMemberIds.RoomChatReceived,
        "Room chat received",
        "Publishes immutable room-chat entries from talk, shout and whisper messages.",
        ApplicationMemberKind.Event,
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting,
        null,
        typeof(RoomChatEntry),
        messages: ChatMessages());

    private static ApplicationDescriptor SendDescriptor<TRequest>(
        string id,
        string title,
        string description,
        MessageKey message_key) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(RoomChatSendResult),
            [
                new(
                    "message",
                    typeof(string),
                    true,
                    null,
                    "Message text.",
                    new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*")),
                new("bubble", typeof(int), false, 0, "Chat bubble style identifier.")
            ],
            [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
            messages:
            [
                new(message_key, Direction.Out, ApplicationMessageRole.Send)
            ],
            tool_hints: new(false, true, false, true));

    private (Session Session, Id? RoomId, long RoomGeneration) CaptureRoom()
    {
        Session session = _connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        var room_state = _game.Room.Capture(room =>
        {
            Id? room_id = room.RoomId == 0 ? null : (Id)room.RoomId;
            return (room.IsReady, room_id, room.Generation);
        });
        if (!room_state.IsReady)
            throw new InvalidOperationException("A ready room is required.");
        return (session, room_state.room_id, room_state.Generation);
    }

    private static ApplicationMessageRequirement[] ChatMessages() =>
    [
        new(MessageKeys.Room.Chat.Talk, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Room.Chat.Shout, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Room.Chat.Whisper, Direction.In, ApplicationMessageRole.Observe)
    ];
}
