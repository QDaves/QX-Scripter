using Qx.Messages;
using Qx.Model;

namespace Qx.Game;

/// <summary>Represents a user seen in the current room.</summary>
/// <param name="userId">The id of the user.</param>
/// <param name="name">The name of the user.</param>
public sealed class RoomVisitor(Id userId, string name)
{
    /// <summary>Gets the id of the user.</summary>
    public Id UserId { get; } = userId;
    /// <summary>Gets the name of the user.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the room index the user had when last seen entering.</summary>
    /// <remarks><see cref="RoomVisitorLog.Visitors"/> is ordered by this index, highest first.</remarks>
    public int Index { get; internal set; }

    /// <summary>Gets the local time the user last entered the room, or <see langword="null"/> when the user was already there when the room loaded.</summary>
    public DateTime? Entered { get; internal set; }
    /// <summary>Gets the local time the user last left the room, or <see langword="null"/> while the user is in the room.</summary>
    public DateTime? Left { get; internal set; }

    /// <summary>Gets the number of times the user has entered the room while the log was recording.</summary>
    public int Visits { get; internal set; } = 1;

    /// <summary>Gets whether the user is in the room.</summary>
    public bool IsHere => Left is null;
}

/// <summary>Represents a log of the users seen in the current room.</summary>
/// <remarks>
/// <para>
/// The hotel has no message that lists past visitors of a room, so the log records users as they
/// arrive and leave. It starts when the room is entered and is cleared when the room is left.
/// </para>
/// <para>
/// Visitors are keyed by name, ignoring case, because the room assigns a new index on every
/// entry. A user who leaves and returns keeps one entry and its <see cref="RoomVisitor.Visits"/>
/// count increases. Bots and pets are not recorded.
/// </para>
/// </remarks>
public sealed class RoomVisitorLog
{
    private readonly object _sync = new();
    private readonly Dictionary<string, RoomVisitor> _visitors = new(StringComparer.OrdinalIgnoreCase);
    private RoomManager? _room;
    private Func<string?>? _own_name;

    internal RoomVisitorLog()
    {
    }

    /// <summary>Occurs when a visitor enters or leaves the room, or when the log is cleared.</summary>
    public event Action? Changed;

    /// <summary>Gets a copy of the visitors, most recently arrived first.</summary>
    /// <remarks>
    /// The list is ordered by <see cref="RoomVisitor.Index"/>, highest first. The visitors in it are
    /// the live entries the log keeps updating.
    /// </remarks>
    public IReadOnlyList<RoomVisitor> Visitors
    {
        get
        {
            lock (_sync)
                return [.. _visitors.Values.OrderByDescending(visitor => visitor.Index)];
        }
    }

    /// <summary>Gets the number of visitors in the log.</summary>
    public int Count
    {
        get { lock (_sync) return _visitors.Count; }
    }

    internal void Watch(RoomManager room, Func<string?> own_name)
    {
        ArgumentNullException.ThrowIfNull(room);

        _room = room;
        _own_name = own_name;

        room.AvatarsAdded += Arrived;
        room.AvatarRemoved += Departed;
        room.Left += Clear;
    }

    /// <summary>Removes all visitors from the log.</summary>
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
        string? own = _own_name?.Invoke();
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
