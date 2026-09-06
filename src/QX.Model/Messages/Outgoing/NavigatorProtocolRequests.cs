using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record NavigatorMetadataRequest : IParserComposer<NavigatorMetadataRequest>
{
    public static NavigatorMetadataRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorMetadataRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static NavigatorMetadataRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorMetadataRequest value, in PacketWriter p) { }

    private static void ComposeUnity(NavigatorMetadataRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(NavigatorMetadataRequest)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record FlatCategoriesRequest : IParserComposer<FlatCategoriesRequest>
{
    public static FlatCategoriesRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FlatCategoriesRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static FlatCategoriesRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FlatCategoriesRequest value, in PacketWriter p) { }

    private static void ComposeUnity(FlatCategoriesRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(FlatCategoriesRequest)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record NavigatorEmptySearchRequest : IParserComposer<NavigatorEmptySearchRequest>
{
    public static NavigatorEmptySearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorEmptySearchRequest ParseFlash(in PacketReader p)
    {
        NavigatorProtocolWire.RequireEmpty(in p, nameof(NavigatorEmptySearchRequest));
        return new();
    }

    private static NavigatorEmptySearchRequest ParseUnity(in PacketReader p)
    {
        NavigatorProtocolWire.RequireEmpty(in p, nameof(NavigatorEmptySearchRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorEmptySearchRequest value, in PacketWriter p) { }

    private static void ComposeUnity(NavigatorEmptySearchRequest value, in PacketWriter p) { }
}

public sealed record NavigatorViewSearchRequest(string SearchCode, string Filter)
    : IParserComposer<NavigatorViewSearchRequest>
{
    public static NavigatorViewSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorViewSearchRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    private static NavigatorViewSearchRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorViewSearchRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.SearchCode, nameof(SearchCode), in p);
        NavigatorProtocolWire.ValidateString(value.Filter, nameof(Filter), in p);
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);
    }

    private static void ComposeUnity(NavigatorViewSearchRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.SearchCode, nameof(SearchCode), in p);
        NavigatorProtocolWire.ValidateString(value.Filter, nameof(Filter), in p);
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);
    }
}

public sealed record NavigatorTextSearchRequest(string Text)
    : IParserComposer<NavigatorTextSearchRequest>
{
    public static NavigatorTextSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorTextSearchRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString());

    private static NavigatorTextSearchRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorTextSearchRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.Text, nameof(Text), in p);
        p.WriteString(value.Text);
    }

    private static void ComposeUnity(NavigatorTextSearchRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.Text, nameof(Text), in p);
        p.WriteString(value.Text);
    }
}

public sealed record NavigatorTagSearchRequest(string Tag, int AdIndex)
    : IParserComposer<NavigatorTagSearchRequest>
{
    public static NavigatorTagSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorTagSearchRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    private static NavigatorTagSearchRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorTagSearchRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.Tag, nameof(Tag), in p);
        p.WriteString(value.Tag);
        p.WriteInt(value.AdIndex);
    }

    private static void ComposeUnity(NavigatorTagSearchRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.Tag, nameof(Tag), in p);
        p.WriteString(value.Tag);
        p.WriteInt(value.AdIndex);
    }
}

public sealed record NavigatorAdSearchRequest(int AdIndex)
    : IParserComposer<NavigatorAdSearchRequest>
{
    public static NavigatorAdSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorAdSearchRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static NavigatorAdSearchRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorAdSearchRequest value, in PacketWriter p) =>
        p.WriteInt(value.AdIndex);

    private static void ComposeUnity(NavigatorAdSearchRequest value, in PacketWriter p) =>
        p.WriteInt(value.AdIndex);
}

public sealed record AddSavedSearchRequest(string SearchCode, string Filter)
    : IParserComposer<AddSavedSearchRequest>
{
    public static AddSavedSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AddSavedSearchRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    private static AddSavedSearchRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AddSavedSearchRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);
    }

    private static void ComposeUnity(AddSavedSearchRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);
    }

    private static void ValidateStrings(AddSavedSearchRequest value, in PacketWriter p)
    {
        ValidateString(value.SearchCode, nameof(SearchCode), in p);
        ValidateString(value.Filter, nameof(Filter), in p);
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

public sealed record DeleteSavedSearchRequest(int SavedSearchId)
    : IParserComposer<DeleteSavedSearchRequest>
{
    public static DeleteSavedSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DeleteSavedSearchRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static DeleteSavedSearchRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DeleteSavedSearchRequest value, in PacketWriter p) =>
        p.WriteInt(value.SavedSearchId);

    private static void ComposeUnity(DeleteSavedSearchRequest value, in PacketWriter p) =>
        p.WriteInt(value.SavedSearchId);
}

public sealed record AddCollapsedCategoryRequest(string SearchCode)
    : IParserComposer<AddCollapsedCategoryRequest>
{
    public static AddCollapsedCategoryRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AddCollapsedCategoryRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString());

    private static AddCollapsedCategoryRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AddCollapsedCategoryRequest value, in PacketWriter p) =>
        p.WriteString(value.SearchCode);

    private static void ComposeUnity(AddCollapsedCategoryRequest value, in PacketWriter p) =>
        p.WriteString(value.SearchCode);
}

public sealed record RemoveCollapsedCategoryRequest(string SearchCode)
    : IParserComposer<RemoveCollapsedCategoryRequest>
{
    public static RemoveCollapsedCategoryRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RemoveCollapsedCategoryRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString());

    private static RemoveCollapsedCategoryRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RemoveCollapsedCategoryRequest value, in PacketWriter p) =>
        p.WriteString(value.SearchCode);

    private static void ComposeUnity(RemoveCollapsedCategoryRequest value, in PacketWriter p) =>
        p.WriteString(value.SearchCode);
}

public sealed record SetHomeRoomRequest(Id RoomId)
    : IParserComposer<SetHomeRoomRequest>
{
    public static SetHomeRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SetHomeRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static SetHomeRoomRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SetHomeRoomRequest value, in PacketWriter p)
    {
        int room_id = checked((int)value.RoomId);
        p.WriteInt(room_id);
    }

    private static void ComposeUnity(SetHomeRoomRequest value, in PacketWriter p) =>
        p.WriteLong(value.RoomId);
}

public sealed record CreateRoomRequest(
    string Name,
    string Description,
    string Model,
    int Category,
    int MaximumVisitors,
    int TradeMode) : IParserComposer<CreateRoomRequest>
{
    public static CreateRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CreateRoomRequest ParseFlash(in PacketReader p) =>
        new(
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    private static CreateRoomRequest ParseUnity(in PacketReader p) =>
        new(
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CreateRoomRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteString(value.Model);
        p.WriteInt(value.Category);
        p.WriteInt(value.MaximumVisitors);
        p.WriteInt(value.TradeMode);
    }

    private static void ComposeUnity(CreateRoomRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteString(value.Model);
        p.WriteInt(value.Category);
        p.WriteInt(value.MaximumVisitors);
        p.WriteInt(value.TradeMode);
    }

    private static void ValidateStrings(CreateRoomRequest value, in PacketWriter p)
    {
        ValidateString(value.Name, nameof(Name), in p);
        ValidateString(value.Description, nameof(Description), in p);
        ValidateString(value.Model, nameof(Model), in p);
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

public sealed record DeleteRoomRequest(Id RoomId)
    : IParserComposer<DeleteRoomRequest>
{
    public static DeleteRoomRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DeleteRoomRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static DeleteRoomRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DeleteRoomRequest value, in PacketWriter p)
    {
        int room_id = checked((int)value.RoomId);
        p.WriteInt(room_id);
    }

    private static void ComposeUnity(DeleteRoomRequest value, in PacketWriter p) =>
        p.WriteLong(value.RoomId);
}

internal static class NavigatorProtocolWire
{
    public static void RequireEmpty(in PacketReader p, string message_name)
    {
        if (p.Available != 0)
        {
            throw new InvalidDataException(
                $"{message_name} contains {p.Available} unexpected bytes.");
        }
    }

    public static void ValidateString(string value, string name, in PacketWriter p)
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
