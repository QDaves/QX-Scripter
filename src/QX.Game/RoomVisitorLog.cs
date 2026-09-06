using Qx.Messages;
using Qx.Model;

namespace Qx.Game;

/// <summary>One person seen in the room, and when.</summary>
public sealed class RoomVisitor(Id userId, string name)
{
    public Id UserId { get; } = userId;
    public string Name { get; } = name;

    /// <summary>Where they last stood in the room's own numbering; used to order the list.</summary>
    public int Index { get; internal set; }

    public DateTime? Entered { get; internal set; }
    public DateTime? Left { get; internal set; }

    /// <summary>How many separate times they have come in while the log has been running.</summary>
    public int Visits { get; internal set; } = 1;

    public bool IsHere => Left is null;
}

/// <summary>
/// Who has been in the room since the session opened it.
/// </summary>
/// <remarks>
/// <para>
/// Kept here rather than asked for, because there is nothing to ask. The hotel has no message for
/// "who has been in this room" — <c>RoomVisits</c> is your own history of rooms you went to, which
/// is a different question — so the only way to answer it is to watch people arrive and leave and
/// remember. That also means the log begins when the room is opened and not before.
/// </para>
/// <para>
/// Keyed by name, not by id. A visitor who leaves and comes back is the same person and should
/// raise the count on one row rather than add a second, and the room hands out a fresh index each
/// time, so the index cannot be the key. Bots and pets are left out: they are placed by the room,
/// not visiting it.
/// </para>
/// </remarks>
public sealed class RoomVisitorLog
{
    private readonly object _sync = new();
    private readonly Dictionary<string, RoomVisitor> _visitors = new(StringComparer.OrdinalIgnoreCase);
    private RoomManager? _room;
    private Func<string?>? _ownName;

    public event Action? Changed;

    /// <summary>Everyone seen, most recently arrived first.</summary>
    public IReadOnlyList<RoomVisitor> Visitors
    {
        get
        {
            lock (_sync)
                return [.. _visitors.Values.OrderByDescending(visitor => visitor.Index)];
        }
    }

    public int Count
    {
        get { lock (_sync) return _visitors.Count; }
    }

    /// <summary>Starts watching a room. Called once, when the game state is wired up.</summary>
    public void Watch(RoomManager room, Func<string?> ownName)
    {
        ArgumentNullException.ThrowIfNull(room);

        _room = room;
        _ownName = ownName;

        room.AvatarsAdded += Arrived;
        room.AvatarRemoved += Departed;
        room.Left += Clear;
    }

    public void Clear()
    {
        lock (_sync)
        {
            if (_visitors.Count == 0)
                return;
            _visitors.Clear();
        }
        Changed?.Invoke();
    }

    private void Arrived(IReadOnlyList<Avatar> avatars)
    {
        DateTime now = DateTime.Now;

        // Entering a room delivers everybody already standing in it in one go. They were not seen
        // arriving, so only the moment we came in is ours to record; theirs is left unknown rather
        // than stamped with a time that would be a lie.
        bool loading = _room is { State: not RoomSessionState.Ready };
        string? own = _ownName?.Invoke();
        bool changed = false;

        lock (_sync)
        {
            foreach (Avatar avatar in avatars)
            {
                if (avatar is not User user || user.Name.Length == 0)
                    continue;

                if (_visitors.TryGetValue(user.Name, out RoomVisitor? visitor))
                {
                    visitor.Visits++;
                    visitor.Index = user.Index;
                    visitor.Entered = now;
                    visitor.Left = null;
                }
                else
                {
                    _visitors[user.Name] = new RoomVisitor(user.Id, user.Name)
                    {
                        Index = user.Index,
                        Entered = !loading || string.Equals(user.Name, own, StringComparison.OrdinalIgnoreCase)
                            ? now
                            : null
                    };
                }
                changed = true;
            }
        }

        if (changed)
            Changed?.Invoke();
    }

    private void Departed(Avatar avatar)
    {
        if (avatar is not User user)
            return;

        bool changed = false;
        lock (_sync)
        {
            if (_visitors.TryGetValue(user.Name, out RoomVisitor? visitor) && visitor.Left is null)
            {
                visitor.Left = DateTime.Now;
                changed = true;
            }
        }

        if (changed)
            Changed?.Invoke();
    }
}
