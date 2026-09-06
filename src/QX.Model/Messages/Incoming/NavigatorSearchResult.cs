using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NavigatorRoomMetadata(Id RoomId, string FirstValue, string SecondValue)
    : IParserComposer<NavigatorRoomMetadata>
{
    public static NavigatorRoomMetadata Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorRoomMetadata ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    private static NavigatorRoomMetadata ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorRoomMetadata value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.RoomId));
        p.WriteString(value.FirstValue);
        p.WriteString(value.SecondValue);
    }

    private static void ComposeUnity(NavigatorRoomMetadata value, in PacketWriter p)
    {
        p.WriteLong(value.RoomId);
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
    IReadOnlyList<RoomData> Rooms,
    IReadOnlyList<NavigatorRoomMetadata> UnityMetadata) : IParserComposer<NavigatorSearchBlock>
{
    public NavigatorSearchBlock(
        string SearchCode,
        string Text,
        int ActionAllowed,
        bool ForceClosed,
        int ViewMode,
        IReadOnlyList<RoomData> Rooms)
        : this(SearchCode, Text, ActionAllowed, ForceClosed, ViewMode, Rooms, [])
    {
    }

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
        => ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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
            rooms,
            []);
    }

    private static NavigatorSearchBlock ParseUnity(in PacketReader p)
    {
        string searchCode = p.ReadString();
        string text = p.ReadString();
        int actionAllowed = p.ReadInt();
        bool forceClosed = p.ReadBool();
        int viewMode = p.ReadInt();

        int count = p.ReadLength();
        var rooms = new RoomData[count];
        for (int i = 0; i < count; i++)
            rooms[i] = p.Parse<RoomData>();

        IReadOnlyList<NavigatorRoomMetadata> unity_metadata =
            p.ParseArray<NavigatorRoomMetadata>();

        return new NavigatorSearchBlock(
            searchCode,
            text,
            actionAllowed,
            forceClosed,
            viewMode,
            rooms,
            unity_metadata);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(NavigatorSearchBlock value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteString(value.Text);
        p.WriteInt(value.ActionAllowed);
        p.WriteBool(value.ForceClosed);
        p.WriteInt(value.ViewMode);

        p.WriteLength((Length)value.Rooms.Count);
        foreach (RoomData room in value.Rooms)
            p.Compose(room);

        p.ComposeArray(value.UnityMetadata);
    }
}

public sealed record NavigatorSearchResult(
    string SearchCode,
    string Filter,
    IReadOnlyList<NavigatorSearchBlock> Blocks) : IParserComposer<NavigatorSearchResult>
{
    public IEnumerable<RoomData> Rooms => Blocks.SelectMany(b => b.Rooms);

    public static NavigatorSearchResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static NavigatorSearchResult ParseUnity(in PacketReader p)
    {
        string searchCode = p.ReadString();
        string filter = p.ReadString();

        int count = p.ReadLength();
        var blocks = new NavigatorSearchBlock[count];
        for (int i = 0; i < count; i++)
            blocks[i] = p.Parse<NavigatorSearchBlock>();

        return new NavigatorSearchResult(searchCode, filter, blocks);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorSearchResult value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);

        p.WriteInt(value.Blocks.Count);
        foreach (NavigatorSearchBlock block in value.Blocks)
            p.Compose(block);
    }

    private static void ComposeUnity(NavigatorSearchResult value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);

        p.WriteLength((Length)value.Blocks.Count);
        foreach (NavigatorSearchBlock block in value.Blocks)
            p.Compose(block);
    }
}
