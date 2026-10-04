using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents navigator metadata sent for a room.</summary>
/// <param name="RoomId">The ID of the room, sent as a 32 bit integer.</param>
/// <param name="FirstValue">The first metadata value.</param>
/// <param name="SecondValue">The second metadata value.</param>
public sealed record NavigatorRoomMetadata(Id RoomId, string FirstValue, string SecondValue)
    : IParserComposer<NavigatorRoomMetadata>
{
    /// <summary>Parses the metadata from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NavigatorRoomMetadata Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NavigatorRoomMetadata ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    /// <summary>Composes the metadata into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NavigatorRoomMetadata value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.RoomId));
        p.WriteString(value.FirstValue);
        p.WriteString(value.SecondValue);
    }
}

/// <summary>Represents one block of rooms in a navigator search result.</summary>
/// <param name="SearchCode">The search code of the block.</param>
/// <param name="Text">The title text of the block.</param>
/// <param name="ActionAllowed">The action code the hotel allows for the block.</param>
/// <param name="ForceClosed">Whether the block is shown collapsed.</param>
/// <param name="ViewMode">The view mode the block is shown in.</param>
/// <param name="Rooms">The rooms in the block.</param>
public sealed record NavigatorSearchBlock(
    string SearchCode,
    string Text,
    int ActionAllowed,
    bool ForceClosed,
    int ViewMode,
    IReadOnlyList<RoomData> Rooms) : IParserComposer<NavigatorSearchBlock>
{
    /// <summary>Parses the block from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NavigatorSearchBlock Parse(in PacketReader p)
        => FlashWire.Parse(in p, ParseFlash);

    private static NavigatorSearchBlock ParseFlash(in PacketReader p)
    {
        string searchCode = p.ReadString();
        string text = p.ReadString();
        int actionAllowed = p.ReadInt();
        bool forceClosed = p.ReadBool();
        int viewMode = p.ReadInt();

        int count = p.ReadInt();
        var rooms = new RoomData[count];
        for (int i = 0; i < count; i++)
            rooms[i] = p.Parse<RoomData>();

        return new NavigatorSearchBlock(
            searchCode,
            text,
            actionAllowed,
            forceClosed,
            viewMode,
            rooms);
    }

    /// <summary>Composes the block into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NavigatorSearchBlock value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteString(value.Text);
        p.WriteInt(value.ActionAllowed);
        p.WriteBool(value.ForceClosed);
        p.WriteInt(value.ViewMode);

        p.WriteInt(value.Rooms.Count);
        foreach (RoomData room in value.Rooms)
            p.Compose(room);
    }
}

/// <summary>Represents the <c>NavigatorSearchResultBlocks</c> message, received with the results of a navigator search.</summary>
/// <remarks>
/// The legacy <c>GuestRoomSearchResult</c> message is mapped onto this type as well, with all of its
/// rooms in a single block.
/// </remarks>
/// <param name="SearchCode">The search code of the search, such as the navigator view it belongs to.</param>
/// <param name="Filter">The filter text of the search.</param>
/// <param name="Blocks">The blocks of rooms in the result.</param>
public sealed record NavigatorSearchResult(
    string SearchCode,
    string Filter,
    IReadOnlyList<NavigatorSearchBlock> Blocks) : IParserComposer<NavigatorSearchResult>
{
    /// <summary>Gets the rooms of every block, in block order.</summary>
    public IEnumerable<RoomData> Rooms => Blocks.SelectMany(b => b.Rooms);

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NavigatorSearchResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NavigatorSearchResult ParseFlash(in PacketReader p)
    {
        string searchCode = p.ReadString();
        string filter = p.ReadString();

        int count = p.ReadInt();
        var blocks = new NavigatorSearchBlock[count];
        for (int i = 0; i < count; i++)
            blocks[i] = p.Parse<NavigatorSearchBlock>();

        return new NavigatorSearchResult(searchCode, filter, blocks);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NavigatorSearchResult value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);

        p.WriteInt(value.Blocks.Count);
        foreach (NavigatorSearchBlock block in value.Blocks)
            p.Compose(block);
    }
}
