using System.Runtime.ExceptionServices;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

internal sealed record RoomBan(Id UserId, string Name);

internal sealed record RoomBanState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long RoomGeneration,
    Id RoomId,
    bool Loaded,
    IReadOnlyList<RoomBan> Bans);

internal enum RoomBanStateChangeKind
{
    Refreshed,
    UserUnbanned,
    Invalidated,
    RoomChanged,
    Reset
}

internal sealed record RoomBanStateUpdate(
    RoomBanStateChangeKind Kind,
    RoomBanState State,
    Id? UserId = null);

internal readonly record struct RoomBanRoomScope(
    long Generation,
    Id RoomId,
    bool Active);

internal sealed class RoomBanManager : GameStateManager
{
    private readonly object state_sync = new();
    private readonly Queue<RoomBanStateUpdate> publications = [];
    private RoomBanState state = new(
        null,
        0,
        0,
        0,
        0,
        false,
        ReadOnly<RoomBan>([]));
    private bool publishing;

    internal RoomBanState State => Volatile.Read(ref state);
    internal Func<RoomBanRoomScope>? RoomScope { get; set; }
    internal event Action<RoomBanStateUpdate>? StateCommitted;
    internal event Action<RoomBanStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(CommitReset);
        OnIncoming(MessageContracts.Room.Moderation.BansSnapshot, ApplySnapshot);
        OnIncoming(MessageContracts.Room.Moderation.UserUnbanned, ApplyUnbanned);
    }

    internal void EnterRoom(Id _) => SynchronizeRoom();

    internal void LeaveRoom() => SynchronizeRoom();

    internal void Invalidate(
        Session session,
        long session_generation,
        long room_generation,
        Id room_id)
    {
        RoomBanRoomScope scope = CaptureRoomScope();
        if (!scope.Active ||
            scope.Generation != room_generation ||
            scope.RoomId != room_id)
        {
            return;
        }
        Store(
            RoomBanStateChangeKind.Invalidated,
            session,
            session_generation,
            current => current.RoomGeneration != room_generation ||
                current.RoomId != room_id ||
                !current.Loaded
                ? null
                : current with
                {
                    Loaded = false,
                    Bans = ReadOnly<RoomBan>([])
                });
    }

    internal static bool Equivalent(RoomBanState state, BannedUsersFromRoom message) =>
        state.RoomId == message.RoomId && state.Bans.SequenceEqual(Normalize(message.Users));

    protected override void Reset() => CommitReset(CurrentSession);

    private void ApplySnapshot(BannedUsersFromRoom message)
    {
        Session? session = CurrentSession;
        if (session is null)
            return;
        RoomBanRoomScope scope = CaptureRoomScope();
        if (!scope.Active || scope.RoomId != message.RoomId)
            return;
        IReadOnlyList<RoomBan> bans = Normalize(message.Users);
        Store(
            RoomBanStateChangeKind.Refreshed,
            session,
            null,
            current => current.RoomGeneration != scope.Generation || current.RoomId != scope.RoomId
                ? null
                : current with
                {
                    Loaded = true,
                    Bans = bans
                });
    }

    private void ApplyUnbanned(UserUnbannedFromRoom message)
    {
        Session? session = CurrentSession;
        if (session is null)
            return;
        RoomBanRoomScope scope = CaptureRoomScope();
        if (!scope.Active || scope.RoomId != message.RoomId)
            return;
        Store(
            RoomBanStateChangeKind.UserUnbanned,
            session,
            null,
            current =>
            {
                if (!current.Loaded ||
                    current.RoomGeneration != scope.Generation ||
                    current.RoomId != message.RoomId)
                {
                    return null;
                }
                RoomBan[] bans = current.Bans
                    .Where(ban => ban.UserId != message.UserId)
                    .ToArray();
                return bans.Length == current.Bans.Count
                    ? null
                    : current with { Bans = Array.AsReadOnly(bans) };
            },
            message.UserId);
    }

    private void SynchronizeRoom()
    {
        Session? session = CurrentSession;
        if (session is null)
            return;
        RoomBanRoomScope scope = CaptureRoomScope();
        Store(
            RoomBanStateChangeKind.RoomChanged,
            session,
            null,
            current => current.RoomGeneration == scope.Generation &&
                current.RoomId == scope.RoomId &&
                !current.Loaded
                    ? null
                    : current with
                    {
                        RoomGeneration = scope.Generation,
                        RoomId = scope.Active ? scope.RoomId : 0,
                        Loaded = false,
                        Bans = ReadOnly<RoomBan>([])
                    });
    }

    private void CommitReset(Session? session)
    {
        RoomBanRoomScope scope = CaptureRoomScope();
        bool drain;
        lock (state_sync)
        {
            RoomBanState current = state;
            bool session_changed = !ReferenceEquals(current.Session, session);
            Id room_id = session is not null && scope.Active ? scope.RoomId : 0;
            if (!session_changed &&
                current.RoomGeneration == scope.Generation &&
                current.RoomId == room_id &&
                !current.Loaded &&
                current.Bans.Count == 0)
            {
                return;
            }
            RoomBanState updated = current with
            {
                Session = session,
                SessionGeneration = session_changed
                    ? checked(current.SessionGeneration + 1)
                    : current.SessionGeneration,
                Revision = checked(current.Revision + 1),
                RoomGeneration = scope.Generation,
                RoomId = room_id,
                Loaded = false,
                Bans = ReadOnly<RoomBan>([])
            };
            Volatile.Write(ref state, updated);
            var update = new RoomBanStateUpdate(RoomBanStateChangeKind.Reset, updated);
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private void Store(
        RoomBanStateChangeKind kind,
        Session session,
        long? session_generation,
        Func<RoomBanState, RoomBanState?> mutation,
        Id? user_id = null)
    {
        bool drain;
        lock (state_sync)
        {
            RoomBanState current = state;
            if (!ReferenceEquals(current.Session, session) ||
                session_generation is long expected_generation &&
                current.SessionGeneration != expected_generation)
            {
                return;
            }
            RoomBanState? candidate = mutation(current);
            if (candidate is null)
                return;
            RoomBanState updated = candidate with
            {
                Revision = checked(current.Revision + 1)
            };
            Volatile.Write(ref state, updated);
            var update = new RoomBanStateUpdate(kind, updated, user_id);
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private bool Enqueue(RoomBanStateUpdate update)
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
            RoomBanStateUpdate update;
            lock (state_sync)
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

    private RoomBanRoomScope CaptureRoomScope() =>
        RoomScope?.Invoke() ?? new RoomBanRoomScope(0, 0, false);

    private static IReadOnlyList<RoomBan> Normalize(IEnumerable<IdName> users)
    {
        var bans = new Dictionary<Id, RoomBan>();
        foreach (IdName user in users)
            bans[user.Id] = new RoomBan(user.Id, user.Name);
        return Array.AsReadOnly(
            bans.Values
                .OrderBy(ban => ban.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(ban => (long)ban.UserId)
                .ToArray());
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}
