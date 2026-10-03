using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class RoomVariableFxApplication : IApplicationFeature
{
    private readonly GameState _game;
    private readonly ApplicationEventSource<RoomVariableFxChange> _changes;
    private int _disposed;

    public RoomVariableFxApplication(GameState game, Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
        _changes = new ApplicationEventSource<RoomVariableFxChange>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<RoomVariableFxStateRequest, RoomVariableFxState>(
                StateDescriptor(),
                (request, _) => ValueTask.FromResult(State(request))),
            new ApplicationEventBinding<RoomVariableFxChange>(ChangedDescriptor(), Subscribe)
        ]);
        _game.Room.VariableFxChanged += OnChanged;
        _game.Room.VariableFxRemoved += OnRemoved;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomVariableFxState State(RoomVariableFxStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserIndex is not null && request.FurniId is not null)
            throw new ArgumentException("Filter by a user or by a furni, not both.", nameof(request));
        return _game.Room.Capture(room =>
        {
            IReadOnlyList<VariableFxValue> values = request switch
            {
                { UserIndex: { } index } => room.VariableFxOf(true, index),
                { FurniId: { } furni } => room.VariableFxOf(false, furni),
                _ => room.VariableFxValues
            };
            return new RoomVariableFxState(
                room.RoomId == 0 ? null : (Id)room.RoomId,
                room.Generation,
                room.VariableFxConfigs,
                string.IsNullOrEmpty(request.Variable) ? values : [.. values.Where(value => value.Matches(request.Variable))]);
        });
    }

    public IDisposable Subscribe(Action<RoomVariableFxChange> listener)
    {
        ThrowIfDisposed();
        return _changes.Subscribe(listener);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _game.Room.VariableFxChanged -= OnChanged;
        _game.Room.VariableFxRemoved -= OnRemoved;
        _changes.Dispose();
    }

    private void OnChanged(VariableFxValue value, VariableFxValue? previous) =>
        _changes.Publish(new RoomVariableFxChange(value, previous, false));

    private void OnRemoved(VariableFxValue value) =>
        _changes.Publish(new RoomVariableFxChange(value, null, true));

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private static ApplicationDescriptor StateDescriptor() => new(
        ApplicationMemberIds.RoomVariableFxState,
        "Room variable Fx",
        "Reads the Fx bar of the current room: every configuration and every value on avatars and furni.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(RoomVariableFxStateRequest),
        typeof(RoomVariableFxState),
        [
            new("user_index", typeof(int?), false, null, "Only the values of the avatar with this room index."),
            new("furni_id", typeof(Id?), false, null, "Only the values of the furni with this item id."),
            new("variable", typeof(string), false, null, "Only the values of the variable with this id or drawn with this icon.")
        ],
        messages: Messages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor ChangedDescriptor() => new(
        ApplicationMemberIds.RoomVariableFxChanged,
        "Room variable Fx changed",
        "Publishes every Fx bar value that changes or is removed in the current room.",
        ApplicationMemberKind.Event,
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting,
        null,
        typeof(RoomVariableFxChange),
        messages: Messages());

    private static ApplicationMessageRequirement[] Messages() =>
    [
        new(MessageKeys.Wired.VariableFx.Configs, MessageDirection.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Wired.VariableFx.ConfigsRemoved, MessageDirection.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Wired.VariableFx.Statuses, MessageDirection.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Wired.VariableFx.StatusesRemoved, MessageDirection.In, ApplicationMessageRole.Observe)
    ];
}
