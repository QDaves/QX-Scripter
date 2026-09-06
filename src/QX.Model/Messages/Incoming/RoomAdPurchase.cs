using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>One room the local user may advertise.</summary>
/// <param name="RoomId">The room's identifier.</param>
/// <param name="RoomName">Its name.</param>
/// <param name="HasControllers">Whether the room has anyone with rights besides the owner.</param>
public sealed record RoomAdRoom(Id RoomId, string RoomName, bool HasControllers)
    : IParserComposer<RoomAdRoom>
{
    private string room_name = RoomName ?? throw new ArgumentNullException(nameof(RoomName));

    public string RoomName
    {
        get => room_name;
        init => room_name = value ?? throw new ArgumentNullException(nameof(RoomName));
    }

    public static RoomAdRoom Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static RoomAdRoom ParseFlash(in PacketReader p)
    {
        var strings = new RoomObjectReadStringBudget();
        RoomAdRoom value = ParseWire(in p, ref strings, 0);
        RoomObjectReadWire.RequireEmpty(in p, nameof(RoomAdRoom));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

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
        RoomObjectReadWire.RequireWireId(p.Client, value.RoomId, nameof(RoomId));
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

/// <summary>
/// Which rooms may be advertised, answered before a room-event purchase.
/// </summary>
/// <remarks>
/// id 3787. Read this before buying: the purchase names a room, and only the rooms listed here are
/// eligible. Membership decides how long the event runs, which is what <paramref name="IsVip"/>
/// reports.
/// </remarks>
/// <param name="IsVip">Whether the account holds the membership that extends an event.</param>
/// <param name="Rooms">The rooms that may be advertised.</param>
public sealed record RoomAdPurchaseInfo(bool IsVip, IReadOnlyList<RoomAdRoom> Rooms)
    : IParserComposer<RoomAdPurchaseInfo>
{
    private IReadOnlyList<RoomAdRoom> rooms = Freeze(Rooms);

    public IReadOnlyList<RoomAdRoom> Rooms
    {
        get => rooms;
        init => rooms = Freeze(value);
    }

    public static RoomAdPurchaseInfo Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

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

/// <summary>Asks which rooms may be advertised.</summary>
public sealed record GetRoomAdPurchaseInfo : IParserComposer<GetRoomAdPurchaseInfo>
{
    public static GetRoomAdPurchaseInfo Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static GetRoomAdPurchaseInfo ParseFlash(in PacketReader p)
    {
        RoomObjectReadWire.RequireEmpty(in p, nameof(GetRoomAdPurchaseInfo));
        return new GetRoomAdPurchaseInfo();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(GetRoomAdPurchaseInfo value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);
}

/// <summary>
/// Buys a room event, which advertises a room in the navigator for a while.
/// </summary>
/// <remarks>
/// id 2928. Argument order taken from the client's own call, which passes the page and offer it is
/// buying from followed by the event's own details. The hotel answers with the ordinary catalog
/// purchase messages, so the outcome arrives the same way any other purchase does.
/// </remarks>
/// <param name="PageId">The catalog page the offer sits on.</param>
/// <param name="OfferId">The offer to buy.</param>
/// <param name="RoomId">Which room to advertise; must be one the hotel listed as eligible.</param>
/// <param name="Name">The event's title as shown in the navigator.</param>
/// <param name="Extended">
/// Whether to run the longer form. The client clears this on its own when the account's membership
/// has already expired, so a script should read the purchase info first rather than assume it.
/// </param>
/// <param name="Description">The event's description.</param>
/// <param name="CategoryId">Which navigator category it is listed under.</param>
public sealed record PurchaseRoomAd(
    int PageId,
    int OfferId,
    Id RoomId,
    string Name,
    bool Extended,
    string Description,
    int CategoryId) : IParserComposer<PurchaseRoomAd>
{
    public static PurchaseRoomAd Parse(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadId(), p.ReadString(), p.ReadBool(), p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(PageId);
        p.WriteInt(OfferId);
        p.WriteId(RoomId);
        p.WriteString(Name);
        p.WriteBool(Extended);
        p.WriteString(Description);
        p.WriteInt(CategoryId);
    }
}
