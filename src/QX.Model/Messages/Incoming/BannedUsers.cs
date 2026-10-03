using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Represents the <c>BannedUsersFromRoom</c> message, received with everyone banned from a room in answer to a
/// request for the ban list.
/// </summary>
/// <remarks>
/// <para>
/// The Flash client's parser reads the room and the count as integers and hands each pair to a
/// small record of its own, which is the shape below.
/// </para>
/// <para>
/// The hotel sends this only in answer to a request, and only to someone with rights in the room;
/// a request made without them is answered with nothing rather than refused.
/// </para>
/// </remarks>
public sealed record BannedUsersFromRoom : IParserComposer<BannedUsersFromRoom>
{
    private IReadOnlyList<IdName> _users = Array.Empty<IdName>();

    /// <summary>Initializes a new instance of the <see cref="BannedUsersFromRoom"/> record.</summary>
    /// <param name="roomId">The identifier of the room.</param>
    /// <param name="users">The banned users, copied into a read only list.</param>
    public BannedUsersFromRoom(Id roomId, IReadOnlyList<IdName> users)
    {
        RoomId = roomId;
        Users = users;
    }

    /// <summary>Gets the identifier of the room.</summary>
    public Id RoomId { get; init; }

    /// <summary>Gets the identifier and name of each banned user, as a read only copy.</summary>
    public IReadOnlyList<IdName> Users
    {
        get => _users;
        init => _users = RoomBanWire.FreezeUsers(value, nameof(Users));
    }

    /// <summary>Deconstructs the message into its room and users.</summary>
    /// <param name="roomId">The identifier of the room.</param>
    /// <param name="users">The banned users.</param>
    public void Deconstruct(out Id roomId, out IReadOnlyList<IdName> users)
    {
        roomId = RoomId;
        users = Users;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BannedUsersFromRoom Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BannedUsersFromRoom ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        int count = RoomBanWire.RequireCount(
            p.ReadInt(),
            p.Available,
            RoomBanWire.FlashBanMinimumBytes,
            nameof(Users));
        var users = new IdName[count];
        for (int index = 0; index < users.Length; index++)
        {
            Id user_id = p.ReadInt();
            users[index] = new IdName(user_id, p.ReadString());
        }
        var value = new BannedUsersFromRoom(room_id, users);
        RoomBanWire.RequireEmpty(in p, nameof(BannedUsersFromRoom));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BannedUsersFromRoom value, in PacketWriter p)
    {
        int room_id = RoomBanWire.RequireFlashId(value.RoomId, nameof(RoomId));
        IdName[] users = RoomBanWire.PrepareUsers(value.Users, in p);
        p.WriteInt(room_id);
        p.WriteInt(users.Length);
        foreach (IdName user in users)
        {
            p.WriteInt(unchecked((int)(long)user.Id));
            p.WriteString(user.Name);
        }
    }
}

/// <summary>
/// Represents the <c>UserUnbannedFromRoom</c> message, received when a user is let back into a room.
/// </summary>
/// <remarks>The hotel pushes this as it happens rather than in answer to a request.</remarks>
/// <param name="RoomId">The identifier of the room.</param>
/// <param name="UserId">The identifier of the unbanned user.</param>
public sealed record UserUnbannedFromRoom(Id RoomId, Id UserId)
    : IParserComposer<UserUnbannedFromRoom>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UserUnbannedFromRoom Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UserUnbannedFromRoom ParseFlash(in PacketReader p)
    {
        var value = new UserUnbannedFromRoom(p.ReadInt(), p.ReadInt());
        RoomBanWire.RequireEmpty(in p, nameof(UserUnbannedFromRoom));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UserUnbannedFromRoom value, in PacketWriter p)
    {
        int room_id = RoomBanWire.RequireFlashId(value.RoomId, nameof(RoomId));
        int user_id = RoomBanWire.RequireFlashId(value.UserId, nameof(UserId));
        p.WriteInt(room_id);
        p.WriteInt(user_id);
    }
}

internal static class RoomBanWire
{
    internal const int FlashBanMinimumBytes = sizeof(int) + sizeof(short);

    internal static int RequireCount(int count, int available, int minimum_bytes, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (available < 0 || minimum_bytes <= 0 || count > available / minimum_bytes)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
        }
        return count;
    }

    internal static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    internal static int RequireFlashId(Id value, string name)
    {
        try
        {
            return checked((int)(long)value);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{name} does not fit the Flash wire format.", exception);
        }
    }

    internal static IReadOnlyList<IdName> FreezeUsers(IReadOnlyList<IdName> values, string name)
    {
        IdName[] users = SnapshotUsers(values, name);
        return Array.AsReadOnly(users);
    }

    internal static IdName[] PrepareUsers(
        IReadOnlyList<IdName> values,
        in PacketWriter p)
    {
        IdName[] users = SnapshotUsers(values, nameof(BannedUsersFromRoom.Users));
        foreach (IdName user in users)
        {
            _ = RequireFlashId(user.Id, nameof(IdName.Id));
            RequireString(user.Name, nameof(IdName.Name), in p);
        }
        return users;
    }

    private static IdName[] SnapshotUsers(IReadOnlyList<IdName> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return values.ToArray();
    }

    private static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
