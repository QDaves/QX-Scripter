using System.Globalization;
using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record OpenFlatConnection(Id RoomId, string Password, long EntryPoint)
    : IParserComposer<OpenFlatConnection>
{
    public static OpenFlatConnection Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OpenFlatConnection ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt());

    private static OpenFlatConnection ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OpenFlatConnection value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteString(value.Password);
        p.WriteInt(checked((int)value.EntryPoint));
    }

    private static void ComposeUnity(OpenFlatConnection value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteString(value.Password);
        p.WriteLong(value.EntryPoint);
    }
}

public sealed record AnswerDoorbellRequest(string UserName, bool Allow)
    : IParserComposer<AnswerDoorbellRequest>
{
    public static AnswerDoorbellRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AnswerDoorbellRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadBool());

    private static AnswerDoorbellRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AnswerDoorbellRequest value, in PacketWriter p)
    {
        p.WriteString(value.UserName);
        p.WriteBool(value.Allow);
    }

    private static void ComposeUnity(AnswerDoorbellRequest value, in PacketWriter p)
    {
        p.WriteString(value.UserName);
        p.WriteBool(value.Allow);
    }
}

public sealed record RateRoomRequest(int Rating)
    : IParserComposer<RateRoomRequest>
{
    public static RateRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RateRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static RateRoomRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RateRoomRequest value, in PacketWriter p) =>
        p.WriteInt(value.Rating);

    private static void ComposeUnity(RateRoomRequest value, in PacketWriter p) =>
        p.WriteInt(value.Rating);
}

public sealed record ToggleRoomStaffPickRequest(Id RoomId, bool CurrentlyPicked)
    : IParserComposer<ToggleRoomStaffPickRequest>
{
    public static ToggleRoomStaffPickRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ToggleRoomStaffPickRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    private static ToggleRoomStaffPickRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ToggleRoomStaffPickRequest value, in PacketWriter p)
    {
        int room_id = checked((int)value.RoomId);
        p.WriteInt(room_id);
        p.WriteBool(value.CurrentlyPicked);
    }

    private static void ComposeUnity(ToggleRoomStaffPickRequest value, in PacketWriter p)
    {
        p.WriteLong(value.RoomId);
        p.WriteBool(value.CurrentlyPicked);
    }
}

public sealed record RespectUserRequest(Id UserId)
    : IParserComposer<RespectUserRequest>
{
    public static RespectUserRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RespectUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static RespectUserRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RespectUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));

    private static void ComposeUnity(RespectUserRequest value, in PacketWriter p) =>
        p.WriteLong(value.UserId);
}

public sealed record RespectPetRequest(Id PetId)
    : IParserComposer<RespectPetRequest>
{
    public static RespectPetRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RespectPetRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static RespectPetRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RespectPetRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.PetId));

    private static void ComposeUnity(RespectPetRequest value, in PacketWriter p) =>
        p.WriteLong(value.PetId);
}

public sealed record MountPetRequest(Id PetId, bool Mount)
    : IParserComposer<MountPetRequest>
{
    public static MountPetRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MountPetRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    private static MountPetRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MountPetRequest value, in PacketWriter p)
    {
        int pet_id = checked((int)value.PetId);
        p.WriteInt(pet_id);
        p.WriteBool(value.Mount);
    }

    private static void ComposeUnity(MountPetRequest value, in PacketWriter p)
    {
        p.WriteLong(value.PetId);
        p.WriteBool(value.Mount);
    }
}

public sealed record RemovePetFromRoomRequest(Id PetId)
    : IParserComposer<RemovePetFromRoomRequest>
{
    public static RemovePetFromRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RemovePetFromRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static RemovePetFromRoomRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RemovePetFromRoomRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.PetId));

    private static void ComposeUnity(RemovePetFromRoomRequest value, in PacketWriter p) =>
        p.WriteLong(value.PetId);
}

public sealed record GiveRoomRightsRequest(Id UserId)
    : IParserComposer<GiveRoomRightsRequest>
{
    public static GiveRoomRightsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GiveRoomRightsRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static GiveRoomRightsRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GiveRoomRightsRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));

    private static void ComposeUnity(GiveRoomRightsRequest value, in PacketWriter p) =>
        p.WriteLong(value.UserId);
}

public sealed record EnterOneWayDoorRequest(Id ItemId)
    : IParserComposer<EnterOneWayDoorRequest>
{
    public static EnterOneWayDoorRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static EnterOneWayDoorRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static EnterOneWayDoorRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(EnterOneWayDoorRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));

    private static void ComposeUnity(EnterOneWayDoorRequest value, in PacketWriter p) =>
        p.WriteLong(value.ItemId);
}

public sealed record ThrowDiceRequest(Id ItemId)
    : IParserComposer<ThrowDiceRequest>
{
    public static ThrowDiceRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ThrowDiceRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static ThrowDiceRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ThrowDiceRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));

    private static void ComposeUnity(ThrowDiceRequest value, in PacketWriter p) =>
        p.WriteLong(value.ItemId);
}

public sealed record DiceOffRequest(Id ItemId)
    : IParserComposer<DiceOffRequest>
{
    public static DiceOffRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DiceOffRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static DiceOffRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DiceOffRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));

    private static void ComposeUnity(DiceOffRequest value, in PacketWriter p) =>
        p.WriteLong(value.ItemId);
}

public sealed record RemoveWallItemRequest(Id ItemId)
    : IParserComposer<RemoveWallItemRequest>
{
    public static RemoveWallItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RemoveWallItemRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static RemoveWallItemRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RemoveWallItemRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));

    private static void ComposeUnity(RemoveWallItemRequest value, in PacketWriter p) =>
        p.WriteLong(value.ItemId);
}

public sealed record SetStickyDataRequest(Id ItemId, string Color, string Text)
    : IParserComposer<SetStickyDataRequest>
{
    public static SetStickyDataRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SetStickyDataRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    private static SetStickyDataRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SetStickyDataRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        int item_id = checked((int)value.ItemId);
        p.WriteInt(item_id);
        p.WriteString(value.Color);
        p.WriteString(value.Text);
    }

    private static void ComposeUnity(SetStickyDataRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteLong(value.ItemId);
        p.WriteString(value.Color);
        p.WriteString(value.Text);
    }

    private static void ValidateStrings(SetStickyDataRequest value, in PacketWriter p)
    {
        ValidateString(value.Color, nameof(Color), in p);
        ValidateString(value.Text, nameof(Text), in p);
    }

    private static void ValidateString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                name);
        }
    }
}

public sealed record GetStickyDataRequest(Id ItemId) : IParserComposer<GetStickyDataRequest>
{
    public static GetStickyDataRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseMessage, ParseMessage);

    private static GetStickyDataRequest ParseMessage(in PacketReader p) =>
        new(RoomObjectReadWire.ReadRootId(in p, nameof(GetStickyDataRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeMessage, ComposeMessage);

    private static void ComposeMessage(GetStickyDataRequest value, in PacketWriter p) =>
        RoomObjectReadWire.WriteRootId(value, value.ItemId, in p);
}

public sealed record GetPetInfoRequest(Id PetId) : IParserComposer<GetPetInfoRequest>
{
    public static GetPetInfoRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseMessage, ParseMessage);

    private static GetPetInfoRequest ParseMessage(in PacketReader p) =>
        new(RoomObjectReadWire.ReadRootId(in p, nameof(GetPetInfoRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeMessage, ComposeMessage);

    private static void ComposeMessage(GetPetInfoRequest value, in PacketWriter p) =>
        RoomObjectReadWire.WriteRootId(value, value.PetId, in p);
}

public sealed record PlacePostItRequest(Id ItemId, string WallLocation)
    : IParserComposer<PlacePostItRequest>
{
    public static PlacePostItRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PlacePostItRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString());

    private static PlacePostItRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PlacePostItRequest value, in PacketWriter p)
    {
        ValidateWallLocation(value.WallLocation, in p);
        int item_id = checked((int)value.ItemId);
        p.WriteInt(item_id);
        p.WriteString(value.WallLocation);
    }

    private static void ComposeUnity(PlacePostItRequest value, in PacketWriter p)
    {
        ValidateWallLocation(value.WallLocation, in p);
        p.WriteLong(value.ItemId);
        p.WriteString(value.WallLocation);
    }

    private static void ValidateWallLocation(string value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(WallLocation));
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                nameof(WallLocation));
        }
    }
}

public sealed record AddSpamWallPostItRequest(
    Id ItemId,
    string WallLocation,
    string Color,
    string Text) : IParserComposer<AddSpamWallPostItRequest>
{
    public static AddSpamWallPostItRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AddSpamWallPostItRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString(), p.ReadString());

    private static AddSpamWallPostItRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadString(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AddSpamWallPostItRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        int item_id = checked((int)value.ItemId);
        p.WriteInt(item_id);
        p.WriteString(value.WallLocation);
        p.WriteString(value.Color);
        p.WriteString(value.Text);
    }

    private static void ComposeUnity(AddSpamWallPostItRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteLong(value.ItemId);
        p.WriteString(value.WallLocation);
        p.WriteString(value.Color);
        p.WriteString(value.Text);
    }

    private static void ValidateStrings(AddSpamWallPostItRequest value, in PacketWriter p)
    {
        ValidateString(value.WallLocation, nameof(WallLocation), in p);
        ValidateString(value.Color, nameof(Color), in p);
        ValidateString(value.Text, nameof(Text), in p);
    }

    private static void ValidateString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                name);
        }
    }
}

public sealed record UseFloorItemRequest(Id ItemId, int State)
    : IParserComposer<UseFloorItemRequest>
{
    public static UseFloorItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UseFloorItemRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    private static UseFloorItemRequest ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UseFloorItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.State);
    }

    private static void ComposeUnity(UseFloorItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.State);
    }
}

public sealed record UseWallItemRequest(Id ItemId, int State)
    : IParserComposer<UseWallItemRequest>
{
    public static UseWallItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UseWallItemRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    private static UseWallItemRequest ParseUnity(in PacketReader p) =>
        new(p.ReadId(), 0);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UseWallItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.State);
    }

    private static void ComposeUnity(UseWallItemRequest value, in PacketWriter p)
    {
        if (value.State != 0)
        {
            throw new NotSupportedException(
                "Unity wall-item use cannot represent a nonzero state.");
        }
        p.WriteId(value.ItemId);
    }
}

public enum RoomItemPlacementKind
{
    Floor,
    Wall
}

public sealed record PlaceRoomItemRequest : IParserComposer<PlaceRoomItemRequest>
{
    private PlaceRoomItemRequest(
        RoomItemPlacementKind kind,
        Id item_id,
        int x,
        int y,
        int direction,
        WallLocation? wall_location)
    {
        Kind = kind;
        ItemId = item_id;
        X = x;
        Y = y;
        Direction = direction;
        WallLocation = wall_location;
    }

    public RoomItemPlacementKind Kind { get; }
    public Id ItemId { get; }
    public int X { get; }
    public int Y { get; }
    public int Direction { get; }
    public WallLocation? WallLocation { get; }

    public static PlaceRoomItemRequest Floor(Id item_id, int x, int y, int direction) =>
        new(RoomItemPlacementKind.Floor, item_id, x, y, direction, null);

    public static PlaceRoomItemRequest Wall(Id item_id, WallLocation wall_location) =>
        new(RoomItemPlacementKind.Wall, item_id, 0, 0, 0, wall_location);

    public static PlaceRoomItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public static PlaceRoomItemRequest ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, sizeof(short), nameof(PlaceRoomItemRequest));
        string payload = p.ReadString();
        RoomPlacementWire.RequireEmpty(in p, nameof(PlaceRoomItemRequest));

        int separator = payload.IndexOf(' ');
        if (separator <= 0 ||
            !int.TryParse(
                payload.AsSpan(0, separator),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int item_id))
        {
            throw new InvalidDataException("Flash room-item placement contains an invalid item identifier.");
        }

        string placement = payload[(separator + 1)..];
        if (placement.StartsWith(":w=", StringComparison.Ordinal))
            return Wall(item_id, Qx.Model.WallLocation.ParseString(placement));

        string[] values = placement.Split(' ');
        if (values.Length != 3 ||
            !TryParseInt(values[0], out int x) ||
            !TryParseInt(values[1], out int y) ||
            !TryParseInt(values[2], out int direction))
        {
            throw new InvalidDataException("Flash floor-item placement contains an invalid location.");
        }
        return Floor(item_id, x, y, direction);
    }

    public static PlaceRoomItemRequest ParseUnity(in PacketReader p)
    {
        if (p.Context?.Messages.TryGetIdentifier(p.Header, out Identifier identifier) is true)
        {
            if (identifier.Name.Equals("PlaceRoomItem", StringComparison.OrdinalIgnoreCase))
                return ParseUnityFloor(in p);
            if (identifier.Name.Equals("PlaceWallItem", StringComparison.OrdinalIgnoreCase))
                return ParseUnityWall(in p);
            throw new InvalidDataException(
                $"Unity room-item placement cannot classify header {p.Header.Value} as a placement route.");
        }

        return p.Available switch
        {
            20 => ParseUnityFloor(in p),
            27 => ParseUnityWall(in p),
            _ => throw new InvalidDataException(
                $"Unity room-item placement has unsupported payload size {p.Available}.")
        };
    }

    private static PlaceRoomItemRequest ParseUnityFloor(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 20, nameof(PlaceRoomItemRequest));
        var result = Floor(p.ReadId(), p.ReadInt(), p.ReadInt(), p.ReadInt());
        RoomPlacementWire.RequireEmpty(in p, nameof(PlaceRoomItemRequest));
        return result;
    }

    private static PlaceRoomItemRequest ParseUnityWall(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 27, nameof(PlaceRoomItemRequest));
        Id item_id = p.ReadId();
        WallLocation location = RoomPlacementWire.ReadUnityWallLocation(in p);
        RoomPlacementWire.RequireEmpty(in p, nameof(PlaceRoomItemRequest));
        return Wall(item_id, location);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    public static void ComposeFlash(PlaceRoomItemRequest value, in PacketWriter p)
    {
        int item_id = checked((int)(long)value.ItemId);
        string payload = value.Kind switch
        {
            RoomItemPlacementKind.Floor => FormattableString.Invariant(
                $"{item_id} {value.X} {value.Y} {value.Direction}"),
            RoomItemPlacementKind.Wall => FormattableString.Invariant(
                $"{item_id} {RequireWallLocation(value)}"),
            _ => throw new InvalidDataException(
                $"Unsupported room-item placement kind {value.Kind}.")
        };
        RoomPlacementWire.RequireString(payload, nameof(PlaceRoomItemRequest), in p);
        p.WriteString(payload);
    }

    public static void ComposeUnity(PlaceRoomItemRequest value, in PacketWriter p)
    {
        Qx.Model.WallLocation wall_location = value.Kind switch
        {
            RoomItemPlacementKind.Floor => default,
            RoomItemPlacementKind.Wall => RequireWallLocation(value),
            _ => throw new InvalidDataException(
                $"Unsupported room-item placement kind {value.Kind}.")
        };
        p.WriteId(value.ItemId);
        switch (value.Kind)
        {
            case RoomItemPlacementKind.Floor:
                p.WriteInt(value.X);
                p.WriteInt(value.Y);
                p.WriteInt(value.Direction);
                break;
            case RoomItemPlacementKind.Wall:
                wall_location.Compose(in p);
                break;
        }
    }

    private static WallLocation RequireWallLocation(PlaceRoomItemRequest value) =>
        RoomPlacementWire.RequireWallLocation(
            value.WallLocation ?? throw new InvalidDataException(
                "Wall-item placement requires a wall location."),
            nameof(PlaceRoomItemRequest));

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
}

public sealed record MoveFloorItemRequest(Id ItemId, int X, int Y, int Direction)
    : IParserComposer<MoveFloorItemRequest>
{
    public static MoveFloorItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MoveFloorItemRequest ParseFlash(in PacketReader p) => ParseItem(in p, 16);

    private static MoveFloorItemRequest ParseUnity(in PacketReader p) => ParseItem(in p, 20);

    private static MoveFloorItemRequest ParseItem(in PacketReader p, int size)
    {
        RoomPlacementWire.RequireSize(in p, size, nameof(MoveFloorItemRequest));
        var result = new MoveFloorItemRequest(
            p.ReadId(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());
        RoomPlacementWire.RequireEmpty(in p, nameof(MoveFloorItemRequest));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MoveFloorItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
        p.WriteInt(value.Direction);
    }

    private static void ComposeUnity(MoveFloorItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
        p.WriteInt(value.Direction);
    }
}

public sealed record MoveWallItemRequest(Id ItemId, WallLocation Location)
    : IParserComposer<MoveWallItemRequest>
{
    public static MoveWallItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MoveWallItemRequest ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 8, nameof(MoveWallItemRequest));
        var result = new MoveWallItemRequest(p.ReadId(), WallLocation.Parse(in p));
        RoomPlacementWire.RequireEmpty(in p, nameof(MoveWallItemRequest));
        return result;
    }

    private static MoveWallItemRequest ParseUnity(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 27, nameof(MoveWallItemRequest));
        var result = new MoveWallItemRequest(
            p.ReadId(),
            RoomPlacementWire.ReadUnityWallLocation(in p));
        RoomPlacementWire.RequireEmpty(in p, nameof(MoveWallItemRequest));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MoveWallItemRequest value, in PacketWriter p)
    {
        WallLocation location = RoomPlacementWire.RequireWallLocation(
            value.Location,
            nameof(MoveWallItemRequest));
        string payload = location.ToString();
        RoomPlacementWire.RequireString(payload, nameof(MoveWallItemRequest), in p);
        p.WriteId(value.ItemId);
        p.WriteString(payload);
    }

    private static void ComposeUnity(MoveWallItemRequest value, in PacketWriter p)
    {
        WallLocation location = RoomPlacementWire.RequireWallLocation(
            value.Location,
            nameof(MoveWallItemRequest));
        p.WriteId(value.ItemId);
        location.Compose(in p);
    }
}

public sealed record PickupRoomItemRequest(int Category, Id ItemId, bool Confirmed)
    : IParserComposer<PickupRoomItemRequest>
{
    public static PickupRoomItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PickupRoomItemRequest ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 9, nameof(PickupRoomItemRequest));
        var result = new PickupRoomItemRequest(
            RoomPlacementWire.RequireCategory(p.ReadInt(), nameof(PickupRoomItemRequest)),
            p.ReadId(),
            p.ReadBool());
        RoomPlacementWire.RequireEmpty(in p, nameof(PickupRoomItemRequest));
        return result;
    }

    private static PickupRoomItemRequest ParseUnity(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 12, nameof(PickupRoomItemRequest));
        var result = new PickupRoomItemRequest(
            RoomPlacementWire.RequireCategory(p.ReadInt(), nameof(PickupRoomItemRequest)),
            p.ReadId(),
            false);
        RoomPlacementWire.RequireEmpty(in p, nameof(PickupRoomItemRequest));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PickupRoomItemRequest value, in PacketWriter p)
    {
        _ = checked((int)(long)value.ItemId);
        p.WriteInt(RoomPlacementWire.RequireCategory(
            value.Category,
            nameof(PickupRoomItemRequest)));
        p.WriteId(value.ItemId);
        p.WriteBool(value.Confirmed);
    }

    private static void ComposeUnity(PickupRoomItemRequest value, in PacketWriter p)
    {
        if (value.Confirmed)
        {
            throw new NotSupportedException(
                "Unity room-item pickup cannot represent Flash confirmation.");
        }
        p.WriteInt(RoomPlacementWire.RequireCategory(
            value.Category,
            nameof(PickupRoomItemRequest)));
        p.WriteId(value.ItemId);
    }
}

public sealed record DropHandItemRequest : IParserComposer<DropHandItemRequest>
{
    public static DropHandItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DropHandItemRequest ParseFlash(in PacketReader p) => new();

    private static DropHandItemRequest ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DropHandItemRequest value, in PacketWriter p)
    {
    }

    private static void ComposeUnity(DropHandItemRequest value, in PacketWriter p)
    {
    }
}

public sealed record PassHandItemRequest(Id RecipientId) : IParserComposer<PassHandItemRequest>
{
    public static PassHandItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PassHandItemRequest ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static PassHandItemRequest ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PassHandItemRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.RecipientId));

    private static void ComposeUnity(PassHandItemRequest value, in PacketWriter p) =>
        p.WriteLong(value.RecipientId);
}

public sealed record GetRoomBansRequest(Id RoomId)
    : IParserComposer<GetRoomBansRequest>
{
    public static GetRoomBansRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetRoomBansRequest ParseFlash(in PacketReader p)
    {
        var value = new GetRoomBansRequest(p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(GetRoomBansRequest));
        return value;
    }

    private static GetRoomBansRequest ParseUnity(in PacketReader p)
    {
        var value = new GetRoomBansRequest(p.ReadLong());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(GetRoomBansRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetRoomBansRequest value, in PacketWriter p)
    {
        int room_id = RoomModerationRequestWire.RequireFlashId(
            value.RoomId,
            nameof(RoomId));
        p.WriteInt(room_id);
    }

    private static void ComposeUnity(GetRoomBansRequest value, in PacketWriter p)
    {
        p.WriteLong(value.RoomId);
    }
}

public sealed record GetFlatControllersRequest(Id RoomId)
    : IParserComposer<GetFlatControllersRequest>
{
    public static GetFlatControllersRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetFlatControllersRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static GetFlatControllersRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetFlatControllersRequest value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(GetFlatControllersRequest value, in PacketWriter p) =>
        p.WriteLong(value.RoomId);
}

public sealed record MuteRoomUserRequest(Id UserId, Id RoomId, int Minutes)
    : IParserComposer<MuteRoomUserRequest>
{
    public static MuteRoomUserRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MuteRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new MuteRoomUserRequest(p.ReadInt(), p.ReadInt(), p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(MuteRoomUserRequest));
        return value;
    }

    private static MuteRoomUserRequest ParseUnity(in PacketReader p)
    {
        var value = new MuteRoomUserRequest(p.ReadLong(), p.ReadLong(), p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(MuteRoomUserRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MuteRoomUserRequest value, in PacketWriter p)
    {
        int user_id = RoomModerationRequestWire.RequireFlashId(
            value.UserId,
            nameof(UserId));
        int room_id = RoomModerationRequestWire.RequireFlashId(
            value.RoomId,
            nameof(RoomId));
        p.WriteInt(user_id);
        p.WriteInt(room_id);
        p.WriteInt(value.Minutes);
    }

    private static void ComposeUnity(MuteRoomUserRequest value, in PacketWriter p)
    {
        p.WriteLong(value.UserId);
        p.WriteLong(value.RoomId);
        p.WriteInt(value.Minutes);
    }
}

public sealed record KickRoomUserRequest(Id UserId)
    : IParserComposer<KickRoomUserRequest>
{
    public static KickRoomUserRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static KickRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new KickRoomUserRequest(p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(KickRoomUserRequest));
        return value;
    }

    private static KickRoomUserRequest ParseUnity(in PacketReader p)
    {
        var value = new KickRoomUserRequest(p.ReadLong());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(KickRoomUserRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(KickRoomUserRequest value, in PacketWriter p)
    {
        int user_id = RoomModerationRequestWire.RequireFlashId(
            value.UserId,
            nameof(UserId));
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(KickRoomUserRequest value, in PacketWriter p)
    {
        p.WriteLong(value.UserId);
    }
}

public sealed record BanRoomUserRequest(Id UserId, Id RoomId, string Duration)
    : IParserComposer<BanRoomUserRequest>
{
    public static BanRoomUserRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BanRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new BanRoomUserRequest(p.ReadInt(), p.ReadInt(), p.ReadString());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(BanRoomUserRequest));
        return value;
    }

    private static BanRoomUserRequest ParseUnity(in PacketReader p)
    {
        var value = new BanRoomUserRequest(p.ReadLong(), p.ReadLong(), p.ReadString());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(BanRoomUserRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BanRoomUserRequest value, in PacketWriter p)
    {
        int user_id = RoomModerationRequestWire.RequireFlashId(
            value.UserId,
            nameof(UserId));
        int room_id = RoomModerationRequestWire.RequireFlashId(
            value.RoomId,
            nameof(RoomId));
        RoomModerationRequestWire.RequireString(value.Duration, nameof(Duration), in p);
        p.WriteInt(user_id);
        p.WriteInt(room_id);
        p.WriteString(value.Duration);
    }

    private static void ComposeUnity(BanRoomUserRequest value, in PacketWriter p)
    {
        RoomModerationRequestWire.RequireString(value.Duration, nameof(Duration), in p);
        p.WriteLong(value.UserId);
        p.WriteLong(value.RoomId);
        p.WriteString(value.Duration);
    }
}

public sealed record UnbanRoomUserRequest(Id UserId, Id RoomId)
    : IParserComposer<UnbanRoomUserRequest>
{
    public static UnbanRoomUserRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UnbanRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new UnbanRoomUserRequest(p.ReadInt(), p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(UnbanRoomUserRequest));
        return value;
    }

    private static UnbanRoomUserRequest ParseUnity(in PacketReader p)
    {
        var value = new UnbanRoomUserRequest(p.ReadLong(), p.ReadLong());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(UnbanRoomUserRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UnbanRoomUserRequest value, in PacketWriter p)
    {
        int user_id = RoomModerationRequestWire.RequireFlashId(
            value.UserId,
            nameof(UserId));
        int room_id = RoomModerationRequestWire.RequireFlashId(
            value.RoomId,
            nameof(RoomId));
        p.WriteInt(user_id);
        p.WriteInt(room_id);
    }

    private static void ComposeUnity(UnbanRoomUserRequest value, in PacketWriter p)
    {
        p.WriteLong(value.UserId);
        p.WriteLong(value.RoomId);
    }
}

internal static class RoomModerationRequestWire
{
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

    internal static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}

public sealed record AvatarExpressionRequest(int Expression)
    : IParserComposer<AvatarExpressionRequest>
{
    public static AvatarExpressionRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarExpressionRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static AvatarExpressionRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarExpressionRequest value, in PacketWriter p) =>
        p.WriteInt(value.Expression);

    private static void ComposeUnity(AvatarExpressionRequest value, in PacketWriter p) =>
        p.WriteInt(value.Expression);
}

public sealed record AvatarDanceRequest(int Style)
    : IParserComposer<AvatarDanceRequest>
{
    public static AvatarDanceRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarDanceRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static AvatarDanceRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarDanceRequest value, in PacketWriter p) =>
        p.WriteInt(value.Style);

    private static void ComposeUnity(AvatarDanceRequest value, in PacketWriter p) =>
        p.WriteInt(value.Style);
}

public sealed record AvatarSignRequest(int Sign)
    : IParserComposer<AvatarSignRequest>
{
    public static AvatarSignRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarSignRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static AvatarSignRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarSignRequest value, in PacketWriter p) =>
        p.WriteInt(value.Sign);

    private static void ComposeUnity(AvatarSignRequest value, in PacketWriter p) =>
        p.WriteInt(value.Sign);
}

public sealed record AvatarEffectSelectionRequest(int Effect)
    : IParserComposer<AvatarEffectSelectionRequest>
{
    public static AvatarEffectSelectionRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarEffectSelectionRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static AvatarEffectSelectionRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarEffectSelectionRequest value, in PacketWriter p) =>
        p.WriteInt(value.Effect);

    private static void ComposeUnity(AvatarEffectSelectionRequest value, in PacketWriter p) =>
        p.WriteInt(value.Effect);
}

public sealed record AvatarPostureRequest(int Posture)
    : IParserComposer<AvatarPostureRequest>
{
    public static AvatarPostureRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarPostureRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static AvatarPostureRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarPostureRequest value, in PacketWriter p) =>
        p.WriteInt(value.Posture);

    private static void ComposeUnity(AvatarPostureRequest value, in PacketWriter p) =>
        p.WriteInt(value.Posture);
}

public sealed record WalkRequest(int X, int Y) : IParserComposer<WalkRequest>
{
    public static WalkRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WalkRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static WalkRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WalkRequest value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
    }

    private static void ComposeUnity(WalkRequest value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
    }
}

public sealed record LookToRequest(int X, int Y) : IParserComposer<LookToRequest>
{
    public static LookToRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static LookToRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static LookToRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(LookToRequest value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
    }

    private static void ComposeUnity(LookToRequest value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
    }
}

public sealed record TalkRequest(string Text, int BubbleStyle, int TrackingId)
    : IParserComposer<TalkRequest>
{
    public static TalkRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TalkRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    private static TalkRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TalkRequest value, in PacketWriter p)
    {
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
        p.WriteInt(value.TrackingId);
    }

    private static void ComposeUnity(TalkRequest value, in PacketWriter p)
    {
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
        p.WriteInt(value.TrackingId);
    }
}

public sealed record ShoutRequest(string Text, int BubbleStyle)
    : IParserComposer<ShoutRequest>
{
    public static ShoutRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ShoutRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    private static ShoutRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ShoutRequest value, in PacketWriter p)
    {
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
    }

    private static void ComposeUnity(ShoutRequest value, in PacketWriter p)
    {
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
    }
}

public sealed record WhisperRequest(string Recipient, string Text, int BubbleStyle)
    : IParserComposer<WhisperRequest>
{
    public static WhisperRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WhisperRequest ParseFlash(in PacketReader p)
    {
        string combined = p.ReadString();
        int separator = combined.IndexOf(' ');
        if (separator < 0)
            throw new InvalidDataException("A Flash whisper requires a recipient separator.");
        return new(combined[..separator], combined[(separator + 1)..], p.ReadInt());
    }

    private static WhisperRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WhisperRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Recipient, nameof(Recipient));
        ArgumentNullException.ThrowIfNull(value.Text, nameof(Text));
        string combined = $"{value.Recipient} {value.Text}";
        ValidateString(combined, nameof(Text), in p);
        p.WriteString(combined);
        p.WriteInt(value.BubbleStyle);
    }

    private static void ComposeUnity(WhisperRequest value, in PacketWriter p)
    {
        ValidateString(value.Recipient, nameof(Recipient), in p);
        ValidateString(value.Text, nameof(Text), in p);
        p.WriteString(value.Recipient);
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
    }

    private static void ValidateString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                name);
        }
    }
}

public sealed record StartTypingRequest : IParserComposer<StartTypingRequest>
{
    public static StartTypingRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static StartTypingRequest ParseFlash(in PacketReader p) => new();

    private static StartTypingRequest ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(StartTypingRequest value, in PacketWriter p)
    {
    }

    private static void ComposeUnity(StartTypingRequest value, in PacketWriter p)
    {
    }
}

public sealed record CancelTypingRequest : IParserComposer<CancelTypingRequest>
{
    public static CancelTypingRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CancelTypingRequest ParseFlash(in PacketReader p) => new();

    private static CancelTypingRequest ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CancelTypingRequest value, in PacketWriter p)
    {
    }

    private static void ComposeUnity(CancelTypingRequest value, in PacketWriter p)
    {
    }
}

public sealed record QuitRoomRequest : IParserComposer<QuitRoomRequest>
{
    public static QuitRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static QuitRoomRequest ParseFlash(in PacketReader p) => new();

    private static QuitRoomRequest ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(QuitRoomRequest value, in PacketWriter p)
    {
    }

    private static void ComposeUnity(QuitRoomRequest value, in PacketWriter p)
    {
    }
}

public sealed record GetGuestRoomRequest(Id RoomId, bool EnterRoom, bool RoomForward)
    : IParserComposer<GetGuestRoomRequest>
{
    public static GetGuestRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetGuestRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt() != 0, p.ReadInt() != 0);

    private static GetGuestRoomRequest ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadBool(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetGuestRoomRequest value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteInt(value.EnterRoom ? 1 : 0);
        p.WriteInt(value.RoomForward ? 1 : 0);
    }

    private static void ComposeUnity(GetGuestRoomRequest value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteBool(value.EnterRoom);
        p.WriteBool(value.RoomForward);
    }
}

public sealed record GetRoomSettingsRequest(Id RoomId)
    : IParserComposer<GetRoomSettingsRequest>
{
    public static GetRoomSettingsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetRoomSettingsRequest ParseFlash(in PacketReader p)
    {
        var value = new GetRoomSettingsRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    private static GetRoomSettingsRequest ParseUnity(in PacketReader p)
    {
        var value = new GetRoomSettingsRequest(p.ReadLong());
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetRoomSettingsRequest value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        p.WriteInt(room_id);
    }

    private static void ComposeUnity(GetRoomSettingsRequest value, in PacketWriter p) =>
        p.WriteLong(value.RoomId);

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(GetRoomSettingsRequest)} contains {p.Available} unexpected bytes.");
    }
}

internal enum UnityRoomSettingsSaveWireLayout
{
    Legacy,
    Modern
}

public sealed record SaveRoomSettingsRequest : IParserComposer<SaveRoomSettingsRequest>
{
    private const int FlashFixedTailBytes = 44;

    private IReadOnlyList<string> _tags = Array.AsReadOnly(Array.Empty<string>());
    private IReadOnlyList<Id> _nft_group_ids = Array.AsReadOnly(Array.Empty<Id>());

    public Id RoomId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public RoomDoorMode DoorMode { get; init; }
    public string Password { get; init; } = "";
    public int MaximumVisitors { get; init; }
    public int CategoryId { get; init; }
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        init => _tags = Freeze(value, nameof(Tags));
    }
    public RoomTradeMode TradeMode { get; init; }
    public bool AllowPets { get; init; }
    public bool AllowFoodConsume { get; init; }
    public bool AllowWalkThrough { get; init; }
    public bool HideWalls { get; init; }
    public RoomThickness WallThickness { get; init; }
    public RoomThickness FloorThickness { get; init; }
    public RoomModerationPermission WhoCanMute { get; init; }
    public RoomModerationPermission WhoCanKick { get; init; }
    public RoomModerationPermission WhoCanBan { get; init; }
    public RoomChatFloodSensitivity ChatFloodSensitivity { get; init; }
    public bool LeaveOnDoorTile { get; init; }
    public bool IdleSleepEnabled { get; init; }
    public int IdleSleepTimeoutSeconds { get; init; }
    public bool IdleAutokickEnabled { get; init; }
    public int IdleAutokickTimeoutSeconds { get; init; }
    public bool MuteAllPets { get; init; }
    public IReadOnlyList<Id> NftGroupIds
    {
        get => _nft_group_ids;
        init => _nft_group_ids = Freeze(value, nameof(NftGroupIds));
    }

    public static SaveRoomSettingsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnityUnresolved);

    private static SaveRoomSettingsRequest ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        string name = p.ReadString();
        string description = p.ReadString();
        RoomDoorMode door_mode = (RoomDoorMode)p.ReadInt();
        string password = p.ReadString();
        int maximum_visitors = p.ReadInt();
        int category_id = p.ReadInt();
        int tag_count = p.ReadInt();
        if (tag_count < 0)
            throw new InvalidDataException($"{nameof(SaveRoomSettingsRequest)} has a negative tag count.");
        long minimum_remaining = FlashFixedTailBytes + (long)tag_count * 2;
        if (p.Available < minimum_remaining)
        {
            throw new InvalidDataException(
                $"{nameof(SaveRoomSettingsRequest)} tag count exceeds the remaining payload.");
        }

        var tags = new string[tag_count];
        for (int i = 0; i < tags.Length; i++)
            tags[i] = p.ReadString();

        RoomTradeMode trade_mode = (RoomTradeMode)p.ReadInt();
        bool allow_pets = p.ReadBool();
        bool allow_food_consume = p.ReadBool();
        bool allow_walk_through = p.ReadBool();
        bool hide_walls = p.ReadBool();
        RoomThickness wall_thickness = (RoomThickness)p.ReadInt();
        RoomThickness floor_thickness = (RoomThickness)p.ReadInt();
        RoomModerationPermission who_can_mute = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_kick = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_ban = (RoomModerationPermission)p.ReadInt();
        RoomChatFloodSensitivity chat_flood_sensitivity = (RoomChatFloodSensitivity)p.ReadInt();
        bool leave_on_door_tile = p.ReadBool();
        bool idle_sleep_enabled = p.ReadBool();
        int idle_sleep_timeout_seconds = p.ReadInt();
        bool idle_autokick_enabled = p.ReadBool();
        int idle_autokick_timeout_seconds = p.ReadInt();
        bool mute_all_pets = p.ReadBool();
        RequireEmpty(in p);

        return new SaveRoomSettingsRequest
        {
            RoomId = room_id,
            Name = name,
            Description = description,
            DoorMode = door_mode,
            Password = password,
            MaximumVisitors = maximum_visitors,
            CategoryId = category_id,
            Tags = tags,
            TradeMode = trade_mode,
            AllowPets = allow_pets,
            AllowFoodConsume = allow_food_consume,
            AllowWalkThrough = allow_walk_through,
            HideWalls = hide_walls,
            WallThickness = wall_thickness,
            FloorThickness = floor_thickness,
            WhoCanMute = who_can_mute,
            WhoCanKick = who_can_kick,
            WhoCanBan = who_can_ban,
            ChatFloodSensitivity = chat_flood_sensitivity,
            LeaveOnDoorTile = leave_on_door_tile,
            IdleSleepEnabled = idle_sleep_enabled,
            IdleSleepTimeoutSeconds = idle_sleep_timeout_seconds,
            IdleAutokickEnabled = idle_autokick_enabled,
            IdleAutokickTimeoutSeconds = idle_autokick_timeout_seconds,
            MuteAllPets = mute_all_pets
        };
    }

    private static SaveRoomSettingsRequest ParseUnityUnresolved(in PacketReader p) =>
        throw new NotSupportedException(
            "Unity room settings saves require a verified header schema projection.");

    internal static SaveRoomSettingsRequest ParseUnity(
        in PacketReader p,
        UnityRoomSettingsSaveWireLayout layout)
    {
        if (layout is not UnityRoomSettingsSaveWireLayout.Legacy and
            not UnityRoomSettingsSaveWireLayout.Modern)
        {
            throw new ArgumentOutOfRangeException(nameof(layout));
        }

        Id room_id = p.ReadLong();
        string name = p.ReadString();
        string description = p.ReadString();
        RoomDoorMode door_mode = (RoomDoorMode)p.ReadInt();
        string password = p.ReadString();
        int category_id = p.ReadInt();
        bool allow_pets = p.ReadBool();
        RoomModerationPermission who_can_mute = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_kick = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_ban = (RoomModerationPermission)p.ReadInt();
        int maximum_visitors = p.ReadInt();
        RoomTradeMode trade_mode = RoomTradeMode.Disabled;
        bool allow_food_consume = false;
        bool allow_walk_through = false;
        if (layout is UnityRoomSettingsSaveWireLayout.Modern)
        {
            trade_mode = (RoomTradeMode)p.ReadInt();
            allow_food_consume = p.ReadInt() > 0;
            allow_walk_through = p.ReadInt() > 0;
        }

        if (p.Available < 2)
            throw new InvalidDataException($"{nameof(SaveRoomSettingsRequest)} has no NFT group count.");
        int nft_group_count = unchecked((ushort)p.ReadShort());
        if (p.Available != (long)nft_group_count * 8)
        {
            throw new InvalidDataException(
                $"{nameof(SaveRoomSettingsRequest)} NFT group count does not match the remaining payload.");
        }
        var nft_group_ids = new Id[nft_group_count];
        for (int i = 0; i < nft_group_ids.Length; i++)
            nft_group_ids[i] = p.ReadLong();
        RequireEmpty(in p);

        return new SaveRoomSettingsRequest
        {
            RoomId = room_id,
            Name = name,
            Description = description,
            DoorMode = door_mode,
            Password = password,
            CategoryId = category_id,
            AllowPets = allow_pets,
            WhoCanMute = who_can_mute,
            WhoCanKick = who_can_kick,
            WhoCanBan = who_can_ban,
            MaximumVisitors = maximum_visitors,
            TradeMode = trade_mode,
            AllowFoodConsume = allow_food_consume,
            AllowWalkThrough = allow_walk_through,
            NftGroupIds = nft_group_ids
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnityUnresolved);

    private static void ComposeFlash(SaveRoomSettingsRequest value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        string[] tags = [.. value.Tags];
        RequireString(value.Name, nameof(Name), in p);
        RequireString(value.Description, nameof(Description), in p);
        RequireString(value.Password, nameof(Password), in p);
        foreach (string tag in tags)
            RequireString(tag, nameof(Tags), in p);

        p.WriteInt(room_id);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteInt((int)value.DoorMode);
        p.WriteString(value.Password);
        p.WriteInt(value.MaximumVisitors);
        p.WriteInt(value.CategoryId);
        p.WriteInt(tags.Length);
        foreach (string tag in tags)
            p.WriteString(tag);
        p.WriteInt((int)value.TradeMode);
        p.WriteBool(value.AllowPets);
        p.WriteBool(value.AllowFoodConsume);
        p.WriteBool(value.AllowWalkThrough);
        p.WriteBool(value.HideWalls);
        p.WriteInt((int)value.WallThickness);
        p.WriteInt((int)value.FloorThickness);
        p.WriteInt((int)value.WhoCanMute);
        p.WriteInt((int)value.WhoCanKick);
        p.WriteInt((int)value.WhoCanBan);
        p.WriteInt((int)value.ChatFloodSensitivity);
        p.WriteBool(value.LeaveOnDoorTile);
        p.WriteBool(value.IdleSleepEnabled);
        p.WriteInt(value.IdleSleepTimeoutSeconds);
        p.WriteBool(value.IdleAutokickEnabled);
        p.WriteInt(value.IdleAutokickTimeoutSeconds);
        p.WriteBool(value.MuteAllPets);
    }

    private static void ComposeUnityUnresolved(SaveRoomSettingsRequest value, in PacketWriter p) =>
        throw new NotSupportedException(
            "Unity room settings saves require a verified header schema projection.");

    internal void ComposeUnity(in PacketWriter p, UnityRoomSettingsSaveWireLayout layout)
    {
        if (layout is not UnityRoomSettingsSaveWireLayout.Legacy and
            not UnityRoomSettingsSaveWireLayout.Modern)
        {
            throw new ArgumentOutOfRangeException(nameof(layout));
        }

        string[] tags = [.. Tags];
        Id[] nft_group_ids = [.. NftGroupIds];
        RequireString(Name, nameof(Name), in p);
        RequireString(Description, nameof(Description), in p);
        RequireString(Password, nameof(Password), in p);
        if (nft_group_ids.Length > ushort.MaxValue)
            throw new ArgumentException("NftGroupIds exceeds the Unity wire count limit.", nameof(NftGroupIds));
        RequireUnityRepresentability(tags, layout);

        p.WriteLong(RoomId);
        p.WriteString(Name);
        p.WriteString(Description);
        p.WriteInt((int)DoorMode);
        p.WriteString(Password);
        p.WriteInt(CategoryId);
        p.WriteBool(AllowPets);
        p.WriteInt((int)WhoCanMute);
        p.WriteInt((int)WhoCanKick);
        p.WriteInt((int)WhoCanBan);
        p.WriteInt(MaximumVisitors);
        if (layout is UnityRoomSettingsSaveWireLayout.Modern)
        {
            p.WriteInt((int)TradeMode);
            p.WriteInt(AllowFoodConsume ? 1 : 0);
            p.WriteInt(AllowWalkThrough ? 1 : 0);
        }
        p.WriteLength((Length)(ushort)nft_group_ids.Length);
        foreach (Id nft_group_id in nft_group_ids)
            p.WriteLong(nft_group_id);
    }

    private void RequireUnityRepresentability(
        IReadOnlyList<string> tags,
        UnityRoomSettingsSaveWireLayout layout)
    {
        if (tags.Count != 0 ||
            HideWalls ||
            WallThickness is not RoomThickness.Normal ||
            FloorThickness is not RoomThickness.Normal ||
            ChatFloodSensitivity is not RoomChatFloodSensitivity.Strict ||
            LeaveOnDoorTile ||
            IdleSleepEnabled ||
            IdleSleepTimeoutSeconds != 0 ||
            IdleAutokickEnabled ||
            IdleAutokickTimeoutSeconds != 0 ||
            MuteAllPets)
        {
            throw new NotSupportedException(
                "Unity room settings saves cannot represent non-neutral Flash-only settings.");
        }
        if (layout is UnityRoomSettingsSaveWireLayout.Legacy &&
            (TradeMode is not RoomTradeMode.Disabled || AllowFoodConsume || AllowWalkThrough))
        {
            throw new NotSupportedException(
                "The legacy Unity room settings save layout cannot represent trade or consumption settings.");
        }
    }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    private static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
        {
            throw new InvalidDataException(
                $"{nameof(SaveRoomSettingsRequest)} contains {p.Available} unexpected bytes.");
        }
    }
}
