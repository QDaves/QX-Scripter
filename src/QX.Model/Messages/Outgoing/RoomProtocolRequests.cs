using System.Globalization;
using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Sent when entering a room.</summary>
/// <remarks>Sent as the Flash <c>OpenFlatConnection</c> message.</remarks>
/// <param name="RoomId">The id of the room to enter.</param>
/// <param name="Password">The room password, or an empty string when the room has none.</param>
/// <param name="EntryPoint">The entry point sent with the request, or -1 for none. It is written as a 32 bit integer and must fit in one.</param>
public sealed record OpenFlatConnection(Id RoomId, string Password, long EntryPoint)
    : IParserComposer<OpenFlatConnection>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static OpenFlatConnection Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenFlatConnection ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenFlatConnection value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteString(value.Password);
        p.WriteInt(checked((int)value.EntryPoint));
    }
}

/// <summary>Sent when answering a user who rings the doorbell of the current room.</summary>
/// <remarks>Sent as the Flash <c>LetUserIn</c> message.</remarks>
/// <param name="UserName">The name of the user at the door.</param>
/// <param name="Allow">Whether to let the user in. <see langword="false"/> turns the user away.</param>
public sealed record AnswerDoorbellRequest(string UserName, bool Allow)
    : IParserComposer<AnswerDoorbellRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AnswerDoorbellRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AnswerDoorbellRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AnswerDoorbellRequest value, in PacketWriter p)
    {
        p.WriteString(value.UserName);
        p.WriteBool(value.Allow);
    }
}

/// <summary>Sent when rating the current room.</summary>
/// <remarks>Sent as the Flash <c>RateFlat</c> message.</remarks>
/// <param name="Rating">The signed rating value.</param>
public sealed record RateRoomRequest(int Rating)
    : IParserComposer<RateRoomRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RateRoomRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RateRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RateRoomRequest value, in PacketWriter p) =>
        p.WriteInt(value.Rating);
}

/// <summary>Sent when toggling whether a room is a staff pick.</summary>
/// <remarks>
/// Sent as the Flash <c>ToggleStaffPick</c> message. The hotel flips the flag, so the request
/// states the value from before the change.
/// </remarks>
/// <param name="RoomId">The id of the room. It must fit in a 32 bit integer.</param>
/// <param name="CurrentlyPicked">Whether the room is a staff pick before the toggle.</param>
public sealed record ToggleRoomStaffPickRequest(Id RoomId, bool CurrentlyPicked)
    : IParserComposer<ToggleRoomStaffPickRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ToggleRoomStaffPickRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ToggleRoomStaffPickRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ToggleRoomStaffPickRequest value, in PacketWriter p)
    {
        int room_id = checked((int)value.RoomId);
        p.WriteInt(room_id);
        p.WriteBool(value.CurrentlyPicked);
    }
}

/// <summary>Sent when giving respect to a user.</summary>
/// <remarks>Sent as the Flash <c>RespectUser</c> message.</remarks>
/// <param name="UserId">The id of the user to respect. It must fit in a 32 bit integer.</param>
public sealed record RespectUserRequest(Id UserId)
    : IParserComposer<RespectUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RespectUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RespectUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RespectUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

/// <summary>Sent when giving respect to a pet.</summary>
/// <remarks>Sent as the Flash <c>RespectPet</c> message.</remarks>
/// <param name="PetId">The id of the pet to respect. It must fit in a 32 bit integer.</param>
public sealed record RespectPetRequest(Id PetId)
    : IParserComposer<RespectPetRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RespectPetRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RespectPetRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RespectPetRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.PetId));
}

/// <summary>Sent when mounting or dismounting a rideable pet.</summary>
/// <remarks>Sent as the Flash <c>MountPet</c> message.</remarks>
/// <param name="PetId">The id of the pet. It must fit in a 32 bit integer.</param>
/// <param name="Mount">Whether to mount the pet. <see langword="false"/> dismounts it.</param>
public sealed record MountPetRequest(Id PetId, bool Mount)
    : IParserComposer<MountPetRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MountPetRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MountPetRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MountPetRequest value, in PacketWriter p)
    {
        int pet_id = checked((int)value.PetId);
        p.WriteInt(pet_id);
        p.WriteBool(value.Mount);
    }
}

/// <summary>Sent when removing a pet from the room.</summary>
/// <remarks>Sent as the Flash <c>RemovePetFromFlat</c> message.</remarks>
/// <param name="PetId">The id of the pet to remove. It must fit in a 32 bit integer.</param>
public sealed record RemovePetFromRoomRequest(Id PetId)
    : IParserComposer<RemovePetFromRoomRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RemovePetFromRoomRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RemovePetFromRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RemovePetFromRoomRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.PetId));
}

/// <summary>Sent when giving a user rights in the current room.</summary>
/// <remarks>Sent as the Flash <c>AssignRights</c> message.</remarks>
/// <param name="UserId">The id of the user who receives rights. It must fit in a 32 bit integer.</param>
public sealed record GiveRoomRightsRequest(Id UserId)
    : IParserComposer<GiveRoomRightsRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GiveRoomRightsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GiveRoomRightsRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GiveRoomRightsRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

/// <summary>Sent when entering a one-way door.</summary>
/// <remarks>Sent as the Flash <c>EnterOneWayDoor</c> message.</remarks>
/// <param name="ItemId">The id of the one-way door item. It must fit in a 32 bit integer.</param>
public sealed record EnterOneWayDoorRequest(Id ItemId)
    : IParserComposer<EnterOneWayDoorRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static EnterOneWayDoorRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static EnterOneWayDoorRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(EnterOneWayDoorRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));
}

/// <summary>Sent when clicking a floor or wall item in the room.</summary>
/// <remarks>
/// Sent as the Flash <c>ClickFurni</c> message. A wall item id is written negated and the id is
/// followed by an integer 0. Parsing treats a negative id as a wall item.
/// </remarks>
/// <param name="ItemId">The id of the clicked item. It must be positive and fit in a 32 bit integer.</param>
/// <param name="Type">The kind of item, <see cref="ItemType.Floor"/> or <see cref="ItemType.Wall"/>.</param>
public sealed record ClickRoomItemRequest(Id ItemId, ItemType Type)
    : IParserComposer<ClickRoomItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ClickRoomItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ClickRoomItemRequest ParseFlash(in PacketReader p)
    {
        long id = p.ReadInt();
        p.ReadInt();
        return id < 0 ? new(-id, ItemType.Wall) : new(id, ItemType.Floor);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClickRoomItemRequest value, in PacketWriter p)
    {
        int id = checked((int)value.ItemId);
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(ItemId), value.ItemId, "A clicked item id must be positive.");
        p.WriteInt(value.Type is ItemType.Wall ? -id : id);
        p.WriteInt(0);
    }
}

/// <summary>Sent when throwing a dice.</summary>
/// <remarks>Sent as the Flash <c>ThrowDice</c> message.</remarks>
/// <param name="ItemId">The id of the dice. It must fit in a 32 bit integer.</param>
public sealed record ThrowDiceRequest(Id ItemId)
    : IParserComposer<ThrowDiceRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ThrowDiceRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ThrowDiceRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ThrowDiceRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));
}

/// <summary>Sent when turning a dice off.</summary>
/// <remarks>Sent as the Flash <c>DiceOff</c> message.</remarks>
/// <param name="ItemId">The id of the dice. It must fit in a 32 bit integer.</param>
public sealed record DiceOffRequest(Id ItemId)
    : IParserComposer<DiceOffRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DiceOffRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DiceOffRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DiceOffRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));
}

/// <summary>Sent when deleting a wall item, such as a post-it note, from the room.</summary>
/// <remarks>Sent as the Flash <c>RemoveItem</c> message. A post-it note removed this way is deleted.</remarks>
/// <param name="ItemId">The id of the wall item. It must fit in a 32 bit integer.</param>
public sealed record RemoveWallItemRequest(Id ItemId)
    : IParserComposer<RemoveWallItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RemoveWallItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RemoveWallItemRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RemoveWallItemRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.ItemId));
}

/// <summary>Sent when changing the color and text of a post-it note.</summary>
/// <remarks>
/// Sent as the Flash <c>SetItemData</c> message. Composing throws when a string is
/// <see langword="null"/> or longer than 65535 encoded bytes.
/// </remarks>
/// <param name="ItemId">The id of the post-it note. It must fit in a 32 bit integer.</param>
/// <param name="Color">The note color.</param>
/// <param name="Text">The note text.</param>
public sealed record SetStickyDataRequest(Id ItemId, string Color, string Text)
    : IParserComposer<SetStickyDataRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SetStickyDataRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SetStickyDataRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SetStickyDataRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        int item_id = checked((int)value.ItemId);
        p.WriteInt(item_id);
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

/// <summary>Requests the color and text of a post-it note.</summary>
/// <remarks>Sent as the Flash <c>GetItemData</c> message.</remarks>
/// <param name="ItemId">The id of the post-it note. It must fit in a 32 bit integer.</param>
public sealed record GetStickyDataRequest(Id ItemId) : IParserComposer<GetStickyDataRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetStickyDataRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseMessage);

    private static GetStickyDataRequest ParseMessage(in PacketReader p) =>
        new(RoomObjectReadWire.ReadRootId(in p, nameof(GetStickyDataRequest)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeMessage);

    private static void ComposeMessage(GetStickyDataRequest value, in PacketWriter p) =>
        RoomObjectReadWire.WriteRootId(value, value.ItemId, in p);
}

/// <summary>Requests the details of a pet.</summary>
/// <remarks>Sent as the Flash <c>GetPetInfo</c> message.</remarks>
/// <param name="PetId">The id of the pet. It must fit in a 32 bit integer.</param>
public sealed record GetPetInfoRequest(Id PetId) : IParserComposer<GetPetInfoRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetPetInfoRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseMessage);

    private static GetPetInfoRequest ParseMessage(in PacketReader p) =>
        new(RoomObjectReadWire.ReadRootId(in p, nameof(GetPetInfoRequest)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeMessage);

    private static void ComposeMessage(GetPetInfoRequest value, in PacketWriter p) =>
        RoomObjectReadWire.WriteRootId(value, value.PetId, in p);
}

/// <summary>Sent when placing a post-it note on a wall.</summary>
/// <remarks>Sent as the Flash <c>PlacePostIt</c> message.</remarks>
/// <param name="ItemId">The id of the post-it note item. It must fit in a 32 bit integer.</param>
/// <param name="WallLocation">The target position as a wall location string, at most 65535 encoded bytes.</param>
public sealed record PlacePostItRequest(Id ItemId, string WallLocation)
    : IParserComposer<PlacePostItRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PlacePostItRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PlacePostItRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PlacePostItRequest value, in PacketWriter p)
    {
        ValidateWallLocation(value.WallLocation, in p);
        int item_id = checked((int)value.ItemId);
        p.WriteInt(item_id);
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

/// <summary>Sent when placing a post-it note on a wall with its color and text in one message.</summary>
/// <remarks>
/// Sent as the Flash <c>AddSpamWallPostIt</c> message. Composing throws when a string is
/// <see langword="null"/> or longer than 65535 encoded bytes.
/// </remarks>
/// <param name="ItemId">The id of the post-it note item. It must fit in a 32 bit integer.</param>
/// <param name="WallLocation">The target position as a wall location string.</param>
/// <param name="Color">The note color.</param>
/// <param name="Text">The note text.</param>
public sealed record AddSpamWallPostItRequest(
    Id ItemId,
    string WallLocation,
    string Color,
    string Text) : IParserComposer<AddSpamWallPostItRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AddSpamWallPostItRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AddSpamWallPostItRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AddSpamWallPostItRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        int item_id = checked((int)value.ItemId);
        p.WriteInt(item_id);
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

/// <summary>Sent when using a floor item.</summary>
/// <remarks>Sent as the Flash <c>UseFurniture</c> message.</remarks>
/// <param name="ItemId">The id of the floor item.</param>
/// <param name="State">The state value sent with the use, 0 for a plain use.</param>
public sealed record UseFloorItemRequest(Id ItemId, int State)
    : IParserComposer<UseFloorItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UseFloorItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UseFloorItemRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UseFloorItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.State);
    }
}

/// <summary>Sent when using a wall item.</summary>
/// <remarks>Sent as the Flash <c>UseWallItem</c> message.</remarks>
/// <param name="ItemId">The id of the wall item.</param>
/// <param name="State">The state value sent with the use, 0 for a plain use.</param>
public sealed record UseWallItemRequest(Id ItemId, int State)
    : IParserComposer<UseWallItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UseWallItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UseWallItemRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UseWallItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.State);
    }
}

/// <summary>Specifies whether a <see cref="PlaceRoomItemRequest"/> places a floor item or a wall item.</summary>
public enum RoomItemPlacementKind
{
    /// <summary>A floor item placed on a tile with a direction.</summary>
    Floor,
    /// <summary>A wall item placed at a wall location.</summary>
    Wall
}

/// <summary>Sent when placing an item from the inventory in the room.</summary>
/// <remarks>
/// Sent as the Flash <c>PlaceObject</c> message, whose single string holds the item id followed by
/// <c>x y direction</c> for a floor item or by the wall location string for a wall item. Create
/// instances with <see cref="PlaceRoomItemRequest.Floor"/> or <see cref="PlaceRoomItemRequest.Wall"/>.
/// </remarks>
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

    /// <summary>Gets whether the request places a floor item or a wall item.</summary>
    public RoomItemPlacementKind Kind { get; }
    /// <summary>Gets the id of the inventory item to place.</summary>
    public Id ItemId { get; }
    /// <summary>Gets the tile x coordinate, 0 for a wall item.</summary>
    public int X { get; }
    /// <summary>Gets the tile y coordinate, 0 for a wall item.</summary>
    public int Y { get; }
    /// <summary>Gets the direction the floor item faces, 0 for a wall item.</summary>
    public int Direction { get; }
    /// <summary>Gets the wall location, or <see langword="null"/> for a floor item.</summary>
    public WallLocation? WallLocation { get; }

    /// <summary>Creates a request that places a floor item on a tile.</summary>
    /// <param name="itemId">The id of the inventory item to place.</param>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    /// <param name="direction">The direction the item faces, as a <see cref="Qx.Model.Direction"/> value.</param>
    /// <returns>The floor placement request.</returns>
    public static PlaceRoomItemRequest Floor(Id itemId, int x, int y, int direction) =>
        new(RoomItemPlacementKind.Floor, itemId, x, y, direction, null);

    /// <summary>Creates a request that places a wall item at a wall location.</summary>
    /// <param name="itemId">The id of the inventory item to place.</param>
    /// <param name="wallLocation">The wall location to place the item at.</param>
    /// <returns>The wall placement request.</returns>
    public static PlaceRoomItemRequest Wall(Id itemId, WallLocation wallLocation) =>
        new(RoomItemPlacementKind.Wall, itemId, 0, 0, 0, wallLocation);

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PlaceRoomItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Parses the Flash <c>PlaceObject</c> payload from a packet.</summary>
    /// <remarks>A placement that starts with <c>:w=</c> is read as a wall item, anything else as <c>x y direction</c>.</remarks>
    /// <param name="p">The packet reader.</param>
    /// <returns>The parsed placement request.</returns>
    /// <exception cref="InvalidDataException">Thrown when the packet is too short, has trailing bytes, or holds an invalid item id or floor location.</exception>
    /// <exception cref="FormatException">Thrown when the wall location string is invalid.</exception>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    /// <summary>Composes the Flash <c>PlaceObject</c> payload of a placement request into a packet.</summary>
    /// <param name="value">The placement request to compose.</param>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when a wall placement has no wall location or an invalid orientation, or the kind is unknown.</exception>
    /// <exception cref="OverflowException">Thrown when the item id does not fit in a 32 bit integer.</exception>
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

    private static WallLocation RequireWallLocation(PlaceRoomItemRequest value) =>
        RoomPlacementWire.RequireWallLocation(
            value.WallLocation ?? throw new InvalidDataException(
                "Wall-item placement requires a wall location."),
            nameof(PlaceRoomItemRequest));

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
}

/// <summary>Sent when moving a floor item to a tile and direction.</summary>
/// <remarks>Sent as the Flash <c>MoveObject</c> message.</remarks>
/// <param name="ItemId">The id of the floor item.</param>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
/// <param name="Direction">The direction the item faces, as a <see cref="Qx.Model.Direction"/> value.</param>
public sealed record MoveFloorItemRequest(Id ItemId, int X, int Y, int Direction)
    : IParserComposer<MoveFloorItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MoveFloorItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MoveFloorItemRequest ParseFlash(in PacketReader p) => ParseItem(in p, 16);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MoveFloorItemRequest value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
        p.WriteInt(value.Direction);
    }
}

/// <summary>Sent when moving a wall item to another wall location.</summary>
/// <remarks>Sent as the Flash <c>MoveWallItem</c> message. The location is written as its string form.</remarks>
/// <param name="ItemId">The id of the wall item.</param>
/// <param name="Location">The target wall location. Its orientation must be left or right.</param>
public sealed record MoveWallItemRequest(Id ItemId, WallLocation Location)
    : IParserComposer<MoveWallItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MoveWallItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MoveWallItemRequest ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 8, nameof(MoveWallItemRequest));
        var result = new MoveWallItemRequest(p.ReadId(), WallLocation.Parse(in p));
        RoomPlacementWire.RequireEmpty(in p, nameof(MoveWallItemRequest));
        return result;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Sent when picking up an item from the room.</summary>
/// <remarks>Sent as the Flash <c>PickupObject</c> message. Categories other than 1 and 2 are rejected.</remarks>
/// <param name="Category">The item category, 2 for a floor item and 1 for a wall item.</param>
/// <param name="ItemId">The id of the item to pick up. It must fit in a 32 bit integer.</param>
/// <param name="Confirmed">Whether the pickup answers the client's pickup confirmation.</param>
public sealed record PickupRoomItemRequest(int Category, Id ItemId, bool Confirmed)
    : IParserComposer<PickupRoomItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PickupRoomItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PickupRoomItemRequest value, in PacketWriter p)
    {
        _ = checked((int)(long)value.ItemId);
        p.WriteInt(RoomPlacementWire.RequireCategory(
            value.Category,
            nameof(PickupRoomItemRequest)));
        p.WriteId(value.ItemId);
        p.WriteBool(value.Confirmed);
    }
}

/// <summary>Sent when dropping the item the user's avatar is holding.</summary>
/// <remarks>Sent as the Flash <c>DropCarryItem</c> message, which carries no fields.</remarks>
public sealed record DropHandItemRequest : IParserComposer<DropHandItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DropHandItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DropHandItemRequest ParseFlash(in PacketReader p) => new();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DropHandItemRequest value, in PacketWriter p)
    {
    }
}

/// <summary>Sent when giving the item the user's avatar is holding to another user.</summary>
/// <remarks>Sent as the Flash <c>PassCarryItem</c> message.</remarks>
/// <param name="RecipientId">The id of the user who receives the item. It must fit in a 32 bit integer.</param>
public sealed record PassHandItemRequest(Id RecipientId) : IParserComposer<PassHandItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PassHandItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PassHandItemRequest ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PassHandItemRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.RecipientId));
}

/// <summary>Requests the users banned from a room.</summary>
/// <remarks>Sent as the Flash <c>GetBannedUsersFromRoom</c> message.</remarks>
/// <param name="RoomId">The id of the room. It must fit in a 32 bit integer.</param>
public sealed record GetRoomBansRequest(Id RoomId)
    : IParserComposer<GetRoomBansRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetRoomBansRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetRoomBansRequest ParseFlash(in PacketReader p)
    {
        var value = new GetRoomBansRequest(p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(GetRoomBansRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetRoomBansRequest value, in PacketWriter p)
    {
        int room_id = RoomModerationRequestWire.RequireFlashId(
            value.RoomId,
            nameof(RoomId));
        p.WriteInt(room_id);
    }
}

/// <summary>Requests the users who have rights in a room.</summary>
/// <remarks>Sent as the Flash <c>GetFlatControllers</c> message.</remarks>
/// <param name="RoomId">The id of the room.</param>
public sealed record GetFlatControllersRequest(Id RoomId)
    : IParserComposer<GetFlatControllersRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetFlatControllersRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetFlatControllersRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetFlatControllersRequest value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

/// <summary>Sent when muting a user in a room.</summary>
/// <remarks>Sent as the Flash <c>MuteUser</c> message.</remarks>
/// <param name="UserId">The id of the user to mute. It must fit in a 32 bit integer.</param>
/// <param name="RoomId">The id of the room. It must fit in a 32 bit integer.</param>
/// <param name="Minutes">The mute duration in minutes.</param>
public sealed record MuteRoomUserRequest(Id UserId, Id RoomId, int Minutes)
    : IParserComposer<MuteRoomUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MuteRoomUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MuteRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new MuteRoomUserRequest(p.ReadInt(), p.ReadInt(), p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(MuteRoomUserRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Sent when kicking a user from the current room.</summary>
/// <remarks>Sent as the Flash <c>KickUser</c> message.</remarks>
/// <param name="UserId">The id of the user to kick. It must fit in a 32 bit integer.</param>
public sealed record KickRoomUserRequest(Id UserId)
    : IParserComposer<KickRoomUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static KickRoomUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static KickRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new KickRoomUserRequest(p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(KickRoomUserRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(KickRoomUserRequest value, in PacketWriter p)
    {
        int user_id = RoomModerationRequestWire.RequireFlashId(
            value.UserId,
            nameof(UserId));
        p.WriteInt(user_id);
    }
}

/// <summary>Sent when banning a user from a room.</summary>
/// <remarks>Sent as the Flash <c>BanUserWithDuration</c> message.</remarks>
/// <param name="UserId">The id of the user to ban. It must fit in a 32 bit integer.</param>
/// <param name="RoomId">The id of the room. It must fit in a 32 bit integer.</param>
/// <param name="Duration">The ban length key, such as <c>RWUAM_BAN_USER_HOUR</c>, <c>RWUAM_BAN_USER_DAY</c> or <c>RWUAM_BAN_USER_PERM</c>.</param>
public sealed record BanRoomUserRequest(Id UserId, Id RoomId, string Duration)
    : IParserComposer<BanRoomUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BanRoomUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BanRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new BanRoomUserRequest(p.ReadInt(), p.ReadInt(), p.ReadString());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(BanRoomUserRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Sent when lifting a user's ban from a room.</summary>
/// <remarks>Sent as the Flash <c>UnbanUserFromRoom</c> message.</remarks>
/// <param name="UserId">The id of the banned user. It must fit in a 32 bit integer.</param>
/// <param name="RoomId">The id of the room. It must fit in a 32 bit integer.</param>
public sealed record UnbanRoomUserRequest(Id UserId, Id RoomId)
    : IParserComposer<UnbanRoomUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UnbanRoomUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UnbanRoomUserRequest ParseFlash(in PacketReader p)
    {
        var value = new UnbanRoomUserRequest(p.ReadInt(), p.ReadInt());
        RoomModerationRequestWire.RequireEmpty(in p, nameof(UnbanRoomUserRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Sent when the user's avatar performs an expression, such as a wave.</summary>
/// <remarks>Sent as the Flash <c>AvatarExpression</c> message.</remarks>
/// <param name="Expression">The expression id.</param>
public sealed record AvatarExpressionRequest(int Expression)
    : IParserComposer<AvatarExpressionRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarExpressionRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarExpressionRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarExpressionRequest value, in PacketWriter p) =>
        p.WriteInt(value.Expression);
}

/// <summary>Sent when the user's avatar starts or stops dancing.</summary>
/// <remarks>Sent as the Flash <c>Dance</c> message.</remarks>
/// <param name="Style">The dance style as a <see cref="Dances"/> value, 0 to stop dancing.</param>
public sealed record AvatarDanceRequest(int Style)
    : IParserComposer<AvatarDanceRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarDanceRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarDanceRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarDanceRequest value, in PacketWriter p) =>
        p.WriteInt(value.Style);
}

/// <summary>Sent when the user's avatar holds up a sign.</summary>
/// <remarks>Sent as the Flash <c>Sign</c> message.</remarks>
/// <param name="Sign">The sign id.</param>
public sealed record AvatarSignRequest(int Sign)
    : IParserComposer<AvatarSignRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarSignRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarSignRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarSignRequest value, in PacketWriter p) =>
        p.WriteInt(value.Sign);
}

/// <summary>Sent when selecting the effect the user's avatar wears.</summary>
/// <remarks>Sent as the Flash <c>AvatarEffectSelected</c> message.</remarks>
/// <param name="Effect">The effect id.</param>
public sealed record AvatarEffectSelectionRequest(int Effect)
    : IParserComposer<AvatarEffectSelectionRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarEffectSelectionRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarEffectSelectionRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarEffectSelectionRequest value, in PacketWriter p) =>
        p.WriteInt(value.Effect);
}

/// <summary>Sent when changing the posture of the user's avatar.</summary>
/// <remarks>Sent as the Flash <c>ChangePosture</c> message.</remarks>
/// <param name="Posture">The posture id.</param>
public sealed record AvatarPostureRequest(int Posture)
    : IParserComposer<AvatarPostureRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarPostureRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarPostureRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarPostureRequest value, in PacketWriter p) =>
        p.WriteInt(value.Posture);
}

/// <summary>Sent when walking the user's avatar to a tile.</summary>
/// <remarks>Sent as the Flash <c>MoveAvatar</c> message.</remarks>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
public sealed record WalkRequest(int X, int Y) : IParserComposer<WalkRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WalkRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WalkRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WalkRequest value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
    }
}

/// <summary>Sent when turning the user's avatar to face a tile.</summary>
/// <remarks>Sent as the Flash <c>LookTo</c> message.</remarks>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
public sealed record LookToRequest(int X, int Y) : IParserComposer<LookToRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static LookToRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static LookToRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(LookToRequest value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
    }
}

/// <summary>Sent when the user talks in the room.</summary>
/// <remarks>Sent as the Flash <c>Chat</c> message.</remarks>
/// <param name="Text">The message text.</param>
/// <param name="BubbleStyle">The chat bubble style.</param>
/// <param name="TrackingId">The tracking id sent with the message, or -1 for none.</param>
public sealed record TalkRequest(string Text, int BubbleStyle, int TrackingId)
    : IParserComposer<TalkRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TalkRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TalkRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TalkRequest value, in PacketWriter p)
    {
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
        p.WriteInt(value.TrackingId);
    }
}

/// <summary>Sent when the user shouts in the room.</summary>
/// <remarks>Sent as the Flash <c>Shout</c> message.</remarks>
/// <param name="Text">The message text.</param>
/// <param name="BubbleStyle">The chat bubble style.</param>
public sealed record ShoutRequest(string Text, int BubbleStyle)
    : IParserComposer<ShoutRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ShoutRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ShoutRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ShoutRequest value, in PacketWriter p)
    {
        p.WriteString(value.Text);
        p.WriteInt(value.BubbleStyle);
    }
}

/// <summary>Sent when the user whispers to another user in the room.</summary>
/// <remarks>
/// Sent as the Flash <c>Whisper</c> message. The recipient and the text travel as one string
/// joined by a space, and parsing splits that string at the first space.
/// </remarks>
/// <param name="Recipient">The name of the user to whisper to.</param>
/// <param name="Text">The message text.</param>
/// <param name="BubbleStyle">The chat bubble style.</param>
public sealed record WhisperRequest(string Recipient, string Text, int BubbleStyle)
    : IParserComposer<WhisperRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WhisperRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WhisperRequest ParseFlash(in PacketReader p)
    {
        string combined = p.ReadString();
        int separator = combined.IndexOf(' ');
        if (separator < 0)
            throw new InvalidDataException("A Flash whisper requires a recipient separator.");
        return new(combined[..separator], combined[(separator + 1)..], p.ReadInt());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WhisperRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Recipient, nameof(Recipient));
        ArgumentNullException.ThrowIfNull(value.Text, nameof(Text));
        string combined = $"{value.Recipient} {value.Text}";
        ValidateString(combined, nameof(Text), in p);
        p.WriteString(combined);
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

/// <summary>Sent when the user starts typing a chat message.</summary>
/// <remarks>Sent as the Flash <c>StartTyping</c> message, which carries no fields.</remarks>
public sealed record StartTypingRequest : IParserComposer<StartTypingRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static StartTypingRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static StartTypingRequest ParseFlash(in PacketReader p) => new();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(StartTypingRequest value, in PacketWriter p)
    {
    }
}

/// <summary>Sent when the user stops typing a chat message.</summary>
/// <remarks>Sent as the Flash <c>CancelTyping</c> message, which carries no fields.</remarks>
public sealed record CancelTypingRequest : IParserComposer<CancelTypingRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CancelTypingRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CancelTypingRequest ParseFlash(in PacketReader p) => new();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CancelTypingRequest value, in PacketWriter p)
    {
    }
}

/// <summary>Sent when leaving the current room.</summary>
/// <remarks>Sent as the Flash <c>Quit</c> message, which carries no fields.</remarks>
public sealed record QuitRoomRequest : IParserComposer<QuitRoomRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static QuitRoomRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static QuitRoomRequest ParseFlash(in PacketReader p) => new();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(QuitRoomRequest value, in PacketWriter p)
    {
    }
}

/// <summary>Requests the data of a room.</summary>
/// <remarks>
/// Sent as the Flash <c>GetGuestRoom</c> message. Both flags are written as integers, 1 for
/// <see langword="true"/> and 0 for <see langword="false"/>.
/// </remarks>
/// <param name="RoomId">The id of the room.</param>
/// <param name="EnterRoom">Whether the client is entering the room. The server echoes it in its reply.</param>
/// <param name="RoomForward">Whether the request follows a room forward.</param>
public sealed record GetGuestRoomRequest(Id RoomId, bool EnterRoom, bool RoomForward)
    : IParserComposer<GetGuestRoomRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetGuestRoomRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetGuestRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt() != 0, p.ReadInt() != 0);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetGuestRoomRequest value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteInt(value.EnterRoom ? 1 : 0);
        p.WriteInt(value.RoomForward ? 1 : 0);
    }
}

/// <summary>Requests the settings of a room.</summary>
/// <remarks>Sent as the Flash <c>GetRoomSettings</c> message.</remarks>
/// <param name="RoomId">The id of the room. It must fit in a 32 bit integer.</param>
public sealed record GetRoomSettingsRequest(Id RoomId)
    : IParserComposer<GetRoomSettingsRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetRoomSettingsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetRoomSettingsRequest ParseFlash(in PacketReader p)
    {
        var value = new GetRoomSettingsRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetRoomSettingsRequest value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        p.WriteInt(room_id);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(GetRoomSettingsRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Sent when saving the settings of a room.</summary>
/// <remarks>
/// Sent as the Flash <c>SaveRoomSettings</c> message. <see cref="SaveRoomSettingsRequest.NftGroupIds"/> is not written to
/// the Flash message and is empty after parsing. Composing throws when a string is
/// <see langword="null"/> or longer than 65535 encoded bytes.
/// </remarks>
public sealed record SaveRoomSettingsRequest : IParserComposer<SaveRoomSettingsRequest>
{
    private const int FlashFixedTailBytes = 44;

    private IReadOnlyList<string> _tags = Array.AsReadOnly(Array.Empty<string>());
    private IReadOnlyList<Id> _nft_group_ids = Array.AsReadOnly(Array.Empty<Id>());

    /// <summary>Gets the id of the room.</summary>
    /// <remarks>The id must fit in a 32 bit integer.</remarks>
    public Id RoomId { get; init; }
    /// <summary>Gets the room name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Gets the room description.</summary>
    public string Description { get; init; } = "";
    /// <summary>Gets the door mode that decides who is let into the room.</summary>
    public RoomDoorMode DoorMode { get; init; }
    /// <summary>Gets the room password, empty by default.</summary>
    public string Password { get; init; } = "";
    /// <summary>Gets the maximum number of visitors.</summary>
    public int MaximumVisitors { get; init; }
    /// <summary>Gets the id of the navigator category the room is listed in.</summary>
    public int CategoryId { get; init; }
    /// <summary>Gets the room tags.</summary>
    /// <remarks>The setter stores a read-only copy and throws when the value is <see langword="null"/>.</remarks>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        init => _tags = Freeze(value, nameof(Tags));
    }
    /// <summary>Gets who may trade in the room.</summary>
    public RoomTradeMode TradeMode { get; init; }
    /// <summary>Gets whether pets are allowed in the room.</summary>
    public bool AllowPets { get; init; }
    /// <summary>Gets whether pets may eat food in the room.</summary>
    public bool AllowFoodConsume { get; init; }
    /// <summary>Gets whether avatars can walk through each other.</summary>
    public bool AllowWalkThrough { get; init; }
    /// <summary>Gets whether the room walls are hidden.</summary>
    public bool HideWalls { get; init; }
    /// <summary>Gets the wall thickness.</summary>
    public RoomThickness WallThickness { get; init; }
    /// <summary>Gets the floor thickness.</summary>
    public RoomThickness FloorThickness { get; init; }
    /// <summary>Gets who may mute users in the room.</summary>
    public RoomModerationPermission WhoCanMute { get; init; }
    /// <summary>Gets who may kick users from the room.</summary>
    public RoomModerationPermission WhoCanKick { get; init; }
    /// <summary>Gets who may ban users from the room.</summary>
    public RoomModerationPermission WhoCanBan { get; init; }
    /// <summary>Gets how strictly the room silences repeated or rapid chat.</summary>
    public RoomChatFloodSensitivity ChatFloodSensitivity { get; init; }
    /// <summary>Gets whether avatars leave the room when they step on the door tile.</summary>
    public bool LeaveOnDoorTile { get; init; }
    /// <summary>Gets whether idle avatars fall asleep.</summary>
    public bool IdleSleepEnabled { get; init; }
    /// <summary>Gets the idle time in seconds before an avatar falls asleep.</summary>
    public int IdleSleepTimeoutSeconds { get; init; }
    /// <summary>Gets whether idle avatars are kicked from the room.</summary>
    public bool IdleAutokickEnabled { get; init; }
    /// <summary>Gets the idle time in seconds before an avatar is kicked.</summary>
    public int IdleAutokickTimeoutSeconds { get; init; }
    /// <summary>Gets whether every pet in the room is muted.</summary>
    public bool MuteAllPets { get; init; }
    /// <summary>Gets the NFT group ids carried with the settings.</summary>
    /// <remarks>Not written to the Flash message. The setter stores a read-only copy and throws when the value is <see langword="null"/>.</remarks>
    public IReadOnlyList<Id> NftGroupIds
    {
        get => _nft_group_ids;
        init => _nft_group_ids = Freeze(value, nameof(NftGroupIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SaveRoomSettingsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
