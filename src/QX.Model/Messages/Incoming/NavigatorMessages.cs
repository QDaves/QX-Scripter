using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// One entry in the navigator's left pane: a search the hotel offers by name.
/// </summary>
/// <remarks>
/// The same shape serves both the quick links under a category and the searches the user saved,
/// which is why the identifier is only meaningful for the saved ones.
/// </remarks>
/// <param name="Id">The saved search's identifier; zero for a quick link.</param>
/// <param name="SearchCode">Which view the search belongs to, for example <c>hotel_view</c>.</param>
/// <param name="Filter">The filter text, in the navigator's own prefix syntax.</param>
/// <param name="Localization">The text key for the label the client shows.</param>
public sealed record NavigatorSearch(
    int Id,
    string SearchCode,
    string Filter,
    string Localization) : IParserComposer<NavigatorSearch>
{
    public static NavigatorSearch Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorSearch ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString(), p.ReadString());

    private static NavigatorSearch ParseUnity(in PacketReader p) =>
        new(checked((int)p.ReadLong()), p.ReadString(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorSearch value, in PacketWriter p)
    {
        p.WriteInt(value.Id);
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);
        p.WriteString(value.Localization);
    }

    private static void ComposeUnity(NavigatorSearch value, in PacketWriter p)
    {
        p.WriteLong(value.Id);
        p.WriteString(value.SearchCode);
        p.WriteString(value.Filter);
        p.WriteString(value.Localization);
    }
}

/// <summary>A navigator category, with the searches offered under it.</summary>
/// <param name="SearchCode">The category's code.</param>
/// <param name="QuickLinks">The searches the hotel offers under it.</param>
public sealed record NavigatorCategory(string SearchCode, IReadOnlyList<NavigatorSearch> QuickLinks)
    : IParserComposer<NavigatorCategory>
{
    public int ViewMode { get; init; }

    public static NavigatorCategory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorCategory ParseFlash(in PacketReader p)
    {
        string search_code = p.ReadString();
        return ParseLinks(in p, search_code, p.ReadInt());
    }

    private static NavigatorCategory ParseUnity(in PacketReader p)
    {
        string search_code = p.ReadString();
        return new NavigatorCategory(search_code, [])
        {
            ViewMode = p.ReadInt()
        };
    }

    private static NavigatorCategory ParseLinks(in PacketReader p, string search_code, int count)
    {
        var links = new NavigatorSearch[count];
        for (int i = 0; i < count; i++)
            links[i] = p.Parse<NavigatorSearch>();
        return new NavigatorCategory(search_code, links);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorCategory value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteInt(value.QuickLinks.Count);
        foreach (NavigatorSearch link in value.QuickLinks)
            p.Compose(link);
    }

    private static void ComposeUnity(NavigatorCategory value, in PacketWriter p)
    {
        p.WriteString(value.SearchCode);
        p.WriteInt(value.ViewMode);
    }
}

/// <summary>
/// The navigator's structure: every category the hotel publishes and what sits under each.
/// </summary>
/// <remarks>
/// This is what makes a search code valid. A search sent with a code the hotel does not list here
/// comes back empty rather than refused, so the categories are worth reading before searching.
/// </remarks>
/// <param name="Categories">The categories.</param>
public sealed record NavigatorMetaData(IReadOnlyList<NavigatorCategory> Categories)
    : IParserComposer<NavigatorMetaData>
{
    public static NavigatorMetaData Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorMetaData ParseFlash(in PacketReader p) =>
        ParseCategories(in p, p.ReadInt());

    private static NavigatorMetaData ParseUnity(in PacketReader p) =>
        ParseCategories(in p, p.ReadLength());

    private static NavigatorMetaData ParseCategories(in PacketReader p, int count)
    {
        var categories = new NavigatorCategory[count];
        for (int i = 0; i < count; i++)
            categories[i] = p.Parse<NavigatorCategory>();
        return new NavigatorMetaData(categories);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorMetaData value, in PacketWriter p)
    {
        p.WriteInt(value.Categories.Count);
        foreach (NavigatorCategory category in value.Categories)
            p.Compose(category);
    }

    private static void ComposeUnity(NavigatorMetaData value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Categories.Count);
        foreach (NavigatorCategory category in value.Categories)
            p.Compose(category);
    }
}

/// <summary>A room the hotel is promoting, shown as a tile rather than a list row.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="AreaId">Which promoted area it belongs to.</param>
/// <param name="Image">The tile's image reference.</param>
/// <param name="Caption">The tile's caption.</param>
public sealed record NavigatorLiftedRoom(int RoomId, int AreaId, string Image, string Caption)
    : IParserComposer<NavigatorLiftedRoom>
{
    public static NavigatorLiftedRoom Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorLiftedRoom ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadString());

    private static NavigatorLiftedRoom ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorLiftedRoom value, in PacketWriter p)
    {
        p.WriteInt(value.RoomId);
        p.WriteInt(value.AreaId);
        p.WriteString(value.Image);
        p.WriteString(value.Caption);
    }

    private static void ComposeUnity(NavigatorLiftedRoom value, in PacketWriter p)
    {
        p.WriteInt(value.RoomId);
        p.WriteInt(value.AreaId);
        p.WriteString(value.Image);
        p.WriteString(value.Caption);
    }
}

/// <summary>The rooms the hotel is currently promoting.</summary>
/// <param name="Rooms">The promoted rooms.</param>
public sealed record NavigatorLiftedRooms(IReadOnlyList<NavigatorLiftedRoom> Rooms)
    : IParserComposer<NavigatorLiftedRooms>
{
    public static NavigatorLiftedRooms Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorLiftedRooms ParseFlash(in PacketReader p) =>
        ParseRooms(in p, p.ReadInt());

    private static NavigatorLiftedRooms ParseUnity(in PacketReader p) =>
        ParseRooms(in p, p.ReadLength());

    private static NavigatorLiftedRooms ParseRooms(in PacketReader p, int count)
    {
        var rooms = new NavigatorLiftedRoom[count];
        for (int i = 0; i < count; i++)
            rooms[i] = p.Parse<NavigatorLiftedRoom>();
        return new NavigatorLiftedRooms(rooms);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorLiftedRooms value, in PacketWriter p)
    {
        p.WriteInt(value.Rooms.Count);
        foreach (NavigatorLiftedRoom room in value.Rooms)
            p.Compose(room);
    }

    private static void ComposeUnity(NavigatorLiftedRooms value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Rooms.Count);
        foreach (NavigatorLiftedRoom room in value.Rooms)
            p.Compose(room);
    }
}

/// <summary>The searches the local user has saved.</summary>
/// <param name="Searches">The saved searches.</param>
public sealed record NavigatorSavedSearches(IReadOnlyList<NavigatorSearch> Searches)
    : IParserComposer<NavigatorSavedSearches>
{
    public static NavigatorSavedSearches Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorSavedSearches ParseFlash(in PacketReader p) =>
        ParseSearches(in p, p.ReadInt());

    private static NavigatorSavedSearches ParseUnity(in PacketReader p) =>
        ParseSearches(in p, p.ReadLength());

    private static NavigatorSavedSearches ParseSearches(in PacketReader p, int count)
    {
        var searches = new NavigatorSearch[count];
        for (int i = 0; i < count; i++)
            searches[i] = p.Parse<NavigatorSearch>();
        return new NavigatorSavedSearches(searches);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorSavedSearches value, in PacketWriter p)
    {
        p.WriteInt(value.Searches.Count);
        foreach (NavigatorSearch search in value.Searches)
            p.Compose(search);
    }

    private static void ComposeUnity(NavigatorSavedSearches value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Searches.Count);
        foreach (NavigatorSearch search in value.Searches)
            p.Compose(search);
    }
}

/// <summary>
/// Which room the account calls home, and which room the hotel wants entered next.
/// </summary>
/// <param name="HomeRoomId">The home room, or zero when none is set.</param>
/// <param name="RoomIdToEnter">
/// A room the hotel is steering the session into, or zero. Non-zero after following an invitation
/// or a link, where the hotel decides the destination rather than the user.
/// </param>
/// <summary>
/// Which room is home and which one to walk into next.
/// </summary>
/// <remarks>
/// Both are room ids, so they are as wide as a room id is: four bytes on Flash and eight on Unity.
/// Read as plain integers this parsed on Flash and silently ran off the end on Unity.
/// </remarks>
public sealed record NavigatorSettings(Id HomeRoomId, Id RoomIdToEnter)
    : IParserComposer<NavigatorSettings>
{
    public static NavigatorSettings Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NavigatorSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static NavigatorSettings ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NavigatorSettings value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.HomeRoomId));
        p.WriteInt(checked((int)value.RoomIdToEnter));
    }

    private static void ComposeUnity(NavigatorSettings value, in PacketWriter p)
    {
        p.WriteLong(value.HomeRoomId);
        p.WriteLong(value.RoomIdToEnter);
    }
}

/// <summary>How the local user has arranged the navigator window.</summary>
/// <param name="WindowX">Window position.</param>
/// <param name="WindowY">Window position.</param>
/// <param name="WindowWidth">Window size.</param>
/// <param name="WindowHeight">Window size.</param>
/// <param name="LeftPaneHidden">Whether the category pane is collapsed away.</param>
/// <param name="ResultsMode">How results are drawn: list, thumbnails and so on.</param>
public sealed record NewNavigatorPreferences(
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    bool LeftPaneHidden,
    int ResultsMode) : IParserComposer<NewNavigatorPreferences>
{
    public static NewNavigatorPreferences Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NewNavigatorPreferences ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadBool(), p.ReadInt());

    private static NewNavigatorPreferences ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NewNavigatorPreferences value, in PacketWriter p)
    {
        p.WriteInt(value.WindowX);
        p.WriteInt(value.WindowY);
        p.WriteInt(value.WindowWidth);
        p.WriteInt(value.WindowHeight);
        p.WriteBool(value.LeftPaneHidden);
        p.WriteInt(value.ResultsMode);
    }

    private static void ComposeUnity(NewNavigatorPreferences value, in PacketWriter p)
    {
        p.WriteInt(value.WindowX);
        p.WriteInt(value.WindowY);
        p.WriteInt(value.WindowWidth);
        p.WriteInt(value.WindowHeight);
        p.WriteBool(value.LeftPaneHidden);
        p.WriteInt(value.ResultsMode);
    }
}

/// <summary>
/// One room category a room owner can file their room under.
/// </summary>
/// <param name="NodeId">The category's identifier, which is what a room's category field holds.</param>
/// <param name="Name">The category's name.</param>
/// <param name="Visible">Whether the category is shown at all.</param>
/// <param name="Automatic">
/// Whether the hotel assigns this category itself. An automatic category cannot be chosen by an
/// owner, so it is not a valid target for a room settings save.
/// </param>
/// <param name="AutomaticCategoryKey">The key behind an automatic category.</param>
/// <param name="GlobalCategoryKey">The hotel-wide key this category maps to.</param>
/// <param name="StaffOnly">Whether only staff may file a room here.</param>
public sealed record FlatCategory(
    int NodeId,
    string Name,
    bool Visible,
    bool Automatic,
    string AutomaticCategoryKey,
    string GlobalCategoryKey,
    bool StaffOnly) : IParserComposer<FlatCategory>
{
    /// <summary>Whether a room owner can actually file a room under this category.</summary>
    public bool IsSelectable => Visible && !Automatic && !StaffOnly;

    public static FlatCategory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FlatCategory ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadBool(), p.ReadBool(),
            p.ReadString(), p.ReadString(), p.ReadBool());

    private static FlatCategory ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadBool(), p.ReadBool(),
            p.ReadString(), p.ReadString(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FlatCategory value, in PacketWriter p)
    {
        p.WriteInt(value.NodeId);
        p.WriteString(value.Name);
        p.WriteBool(value.Visible);
        p.WriteBool(value.Automatic);
        p.WriteString(value.AutomaticCategoryKey);
        p.WriteString(value.GlobalCategoryKey);
        p.WriteBool(value.StaffOnly);
    }

    private static void ComposeUnity(FlatCategory value, in PacketWriter p)
    {
        p.WriteInt(value.NodeId);
        p.WriteString(value.Name);
        p.WriteBool(value.Visible);
        p.WriteBool(value.Automatic);
        p.WriteString(value.AutomaticCategoryKey);
        p.WriteString(value.GlobalCategoryKey);
        p.WriteBool(value.StaffOnly);
    }
}

/// <summary>The room categories the hotel publishes.</summary>
/// <param name="Categories">The categories.</param>
public sealed record UserFlatCats(IReadOnlyList<FlatCategory> Categories)
    : IParserComposer<UserFlatCats>
{
    public static UserFlatCats Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserFlatCats ParseFlash(in PacketReader p) =>
        ParseCategories(in p, p.ReadInt());

    private static UserFlatCats ParseUnity(in PacketReader p) =>
        ParseCategories(in p, p.ReadLength());

    private static UserFlatCats ParseCategories(in PacketReader p, int count)
    {
        var categories = new FlatCategory[count];
        for (int i = 0; i < count; i++)
            categories[i] = p.Parse<FlatCategory>();
        return new UserFlatCats(categories);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserFlatCats value, in PacketWriter p)
    {
        p.WriteInt(value.Categories.Count);
        foreach (FlatCategory category in value.Categories)
            p.Compose(category);
    }

    private static void ComposeUnity(UserFlatCats value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Categories.Count);
        foreach (FlatCategory category in value.Categories)
            p.Compose(category);
    }
}

/// <summary>The navigator categories the local user has collapsed.</summary>
/// <param name="Categories">The collapsed category codes.</param>
public sealed record CollapsedCategories(IReadOnlyList<string> Categories)
    : IParserComposer<CollapsedCategories>
{
    public static CollapsedCategories Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CollapsedCategories ParseFlash(in PacketReader p) =>
        ParseCategories(in p, p.ReadInt());

    private static CollapsedCategories ParseUnity(in PacketReader p) =>
        ParseCategories(in p, p.ReadLength());

    private static CollapsedCategories ParseCategories(in PacketReader p, int count)
    {
        var categories = new string[count];
        for (int i = 0; i < count; i++)
            categories[i] = p.ReadString();
        return new CollapsedCategories(categories);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CollapsedCategories value, in PacketWriter p)
    {
        p.WriteInt(value.Categories.Count);
        foreach (string category in value.Categories)
            p.WriteString(category);
    }

    private static void ComposeUnity(CollapsedCategories value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Categories.Count);
        foreach (string category in value.Categories)
            p.WriteString(category);
    }
}
