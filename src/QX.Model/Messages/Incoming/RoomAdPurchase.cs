using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents a room the user may advertise with a room event.</summary>
/// <param name="RoomId">The room's identifier.</param>
/// <param name="RoomName">The room's name.</param>
/// <param name="HasControllers">Whether the room has anyone with rights besides the owner.</param>
public sealed record RoomAdRoom(Id RoomId, string RoomName, bool HasControllers)
    : IParserComposer<RoomAdRoom>
{
    private string room_name = RoomName ?? throw new ArgumentNullException(nameof(RoomName));

    /// <summary>Gets the room's name.</summary>
    public string RoomName
    {
        get => room_name;
        init => room_name = value ?? throw new ArgumentNullException(nameof(RoomName));
    }

    /// <summary>Parses the room from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RoomAdRoom Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RoomAdRoom ParseFlash(in PacketReader p)
    {
        var strings = new RoomObjectReadStringBudget();
        RoomAdRoom value = ParseWire(in p, ref strings, 0);
        RoomObjectReadWire.RequireEmpty(in p, nameof(RoomAdRoom));
        return value;
    }

    /// <summary>Composes the room into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RoomAdRoom value, in PacketWriter p)
    {
        var strings = new RoomObjectReadStringBudget();
        RoomAdRoomWireSnapshot snapshot = Prepare(value, in p, ref strings);
        ComposeWire(snapshot, in p);
    }

    internal static RoomAdRoom ParseWire(
        in PacketReader p,
        ref RoomObjectReadStringBudget strings,
        int trailing_bytes)
    {
        RoomObjectReadWire.RequireRemaining(
            in p,
            checked(sizeof(int) + sizeof(short) + sizeof(bool)),
            trailing_bytes,
            nameof(RoomAdRoom));
        Id room_id = p.ReadId();
        string room_name = strings.Read(
            in p,
            checked(sizeof(bool) + trailing_bytes),
            nameof(RoomName));
        bool has_controllers = p.ReadBool();
        return new RoomAdRoom(room_id, room_name, has_controllers);
    }

    internal static RoomAdRoomWireSnapshot Prepare(
        RoomAdRoom value,
        in PacketWriter p,
        ref RoomObjectReadStringBudget strings)
    {
        ArgumentNullException.ThrowIfNull(value);
        RoomObjectReadWire.RequireWireId(value.RoomId, nameof(RoomId));
        strings.Require(value.RoomName, in p, nameof(RoomName));
        return new RoomAdRoomWireSnapshot(value.RoomId, value.RoomName, value.HasControllers);
    }

    internal static void ComposeWire(RoomAdRoomWireSnapshot value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteString(value.RoomName);
        p.WriteBool(value.HasControllers);
    }
}

internal readonly record struct RoomAdRoomWireSnapshot(
    Id RoomId,
    string RoomName,
    bool HasControllers);

/// <summary>Represents the <c>RoomAdPurchaseInfo</c> message, received with the rooms the user may advertise before a room event purchase.</summary>
/// <remarks>
/// Read this before buying: the purchase names a room, and only the rooms listed here are
/// eligible. Membership decides how long the event runs, which is what <paramref name="IsVip"/>
/// reports.
/// </remarks>
/// <param name="IsVip">Whether the account holds the membership that extends an event.</param>
/// <param name="Rooms">The rooms that may be advertised.</param>
public sealed record RoomAdPurchaseInfo(bool IsVip, IReadOnlyList<RoomAdRoom> Rooms)
    : IParserComposer<RoomAdPurchaseInfo>
{
    private IReadOnlyList<RoomAdRoom> rooms = Freeze(Rooms);

    /// <summary>Gets the rooms that may be advertised.</summary>
    public IReadOnlyList<RoomAdRoom> Rooms
    {
        get => rooms;
        init => rooms = Freeze(value);
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RoomAdPurchaseInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RoomAdPurchaseInfo ParseFlash(in PacketReader p)
    {
        RoomObjectReadWire.RequireRemaining(
            in p,
            checked(sizeof(bool) + sizeof(int)),
            0,
            nameof(RoomAdPurchaseInfo));
        bool isVip = p.ReadBool();
        int count = p.ReadInt();
        if (count is < 0 or > RoomObjectReadWire.MaximumCollectionCount)
            throw new InvalidDataException("The room advertisement count is outside the supported range.");
        const int minimum_room_bytes = sizeof(int) + sizeof(short) + sizeof(bool);
        if (count > p.Available / minimum_room_bytes)
            throw new InvalidDataException("The room advertisement count exceeds the remaining payload capacity.");
        var rooms = new RoomAdRoom[count];
        var strings = new RoomObjectReadStringBudget();
        for (int i = 0; i < count; i++)
        {
            int trailing_bytes = checked((count - i - 1) * minimum_room_bytes);
            rooms[i] = RoomAdRoom.ParseWire(in p, ref strings, trailing_bytes);
        }
        RoomObjectReadWire.RequireEmpty(in p, nameof(RoomAdPurchaseInfo));
        return new RoomAdPurchaseInfo(isVip, Array.AsReadOnly(rooms));
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RoomAdPurchaseInfo value, in PacketWriter p)
    {
        RoomAdPurchaseInfoWireSnapshot snapshot = Prepare(value, in p);
        p.WriteBool(snapshot.IsVip);
        p.WriteInt(snapshot.Rooms.Count);
        foreach (RoomAdRoomWireSnapshot room in snapshot.Rooms)
            RoomAdRoom.ComposeWire(room, in p);
    }

    private static RoomAdPurchaseInfoWireSnapshot Prepare(
        RoomAdPurchaseInfo value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Rooms.Count > RoomObjectReadWire.MaximumCollectionCount)
            throw new InvalidDataException("The room advertisement count exceeds the supported limit.");
        var strings = new RoomObjectReadStringBudget();
        var rooms = new RoomAdRoomWireSnapshot[value.Rooms.Count];
        for (int index = 0; index < rooms.Length; index++)
            rooms[index] = RoomAdRoom.Prepare(value.Rooms[index], in p, ref strings);
        return new RoomAdPurchaseInfoWireSnapshot(
            value.IsVip,
            Array.AsReadOnly(rooms));
    }

    private static IReadOnlyList<RoomAdRoom> Freeze(IReadOnlyList<RoomAdRoom> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > RoomObjectReadWire.MaximumCollectionCount)
            throw new ArgumentOutOfRangeException(nameof(values));
        var copy = new RoomAdRoom[values.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            RoomAdRoom value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }
}

internal readonly record struct RoomAdPurchaseInfoWireSnapshot(
    bool IsVip,
    IReadOnlyList<RoomAdRoomWireSnapshot> Rooms);
