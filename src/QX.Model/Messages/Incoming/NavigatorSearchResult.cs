using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NavigatorRoomMetadata(Id RoomId, string FirstValue, string SecondValue)
    : IParserComposer<NavigatorRoomMetadata>
{
    public static NavigatorRoomMetadata Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NavigatorRoomMetadata ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NavigatorRoomMetadata value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.RoomId));
        p.WriteString(value.FirstValue);
        p.WriteString(value.SecondValue);
    }
}

public sealed record NavigatorSearchBlock(
    string SearchCode,
    string Text,
    int ActionAllowed,
    bool ForceClosed,
    int ViewMode,
    IReadOnlyList<RoomData> Rooms) : IParserComposer<NavigatorSearchBlock>
{
    public void Deconstruct(
        out string SearchCode,
        out string Text,
        out int ActionAllowed,
        out bool ForceClosed,
        out int ViewMode,
        out IReadOnlyList<RoomData> Rooms)
    {
        SearchCode = this.SearchCode;
        Text = this.Text;
        ActionAllowed = this.ActionAllowed;
        ForceClosed = this.ForceClosed;
        ViewMode = this.ViewMode;
        Rooms = this.Rooms;
    }

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

public sealed record NavigatorSearchResult(
    string SearchCode,
    string Filter,
    IReadOnlyList<NavigatorSearchBlock> Blocks) : IParserComposer<NavigatorSearchResult>
{
    public IEnumerable<RoomData> Rooms => Blocks.SelectMany(b => b.Rooms);

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
