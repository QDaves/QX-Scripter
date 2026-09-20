using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Model;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed record PersonSearch(string Name, string Detail);

public sealed partial class PersonRowViewModel(string key) : ObservableObject
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string Detail { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPosition))]
    public partial string Position { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTag))]
    public partial string Tag { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsIdle { get; private set; }

    [ObservableProperty]
    public partial bool IsTrading { get; private set; }

    [ObservableProperty]
    public partial RoomPersonKind Kind { get; private set; }

    [ObservableProperty]
    public partial ImageRequest? Head { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdText))]
    public partial Id UserId { get; private set; }

    [ObservableProperty]
    public partial int Index { get; private set; }

    [ObservableProperty]
    public partial int TileOrder { get; private set; }

    public long RoomGeneration { get; private set; }

    public Avatar? Person { get; private set; }

    public string Figure { get; private set; } = "";

    public string Motto { get; private set; } = "";

    public PersonSearch Search { get; private set; } = new("", "");

    public string IdText => UserId.ToString();

    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    public bool HasDetail => Detail.Length > 0;

    public bool HasPosition => Position.Length > 0;

    public bool HasTag => Tag.Length > 0;

    public static PersonRowViewModel FromPerson(RoomPerson person, string web_host)
    {
        ArgumentNullException.ThrowIfNull(person);
        var row = new PersonRowViewModel(PersonKey(person));
        row.Take(person, web_host);
        return row;
    }

    public static PersonRowViewModel FromVisit(RoomVisit visit, string web_host)
    {
        ArgumentNullException.ThrowIfNull(visit);
        var row = new PersonRowViewModel(VisitKey(visit));
        row.Take(visit, web_host);
        return row;
    }

    public static PersonRowViewModel FromBan(RoomBanEntry ban, string web_host)
    {
        ArgumentNullException.ThrowIfNull(ban);
        var row = new PersonRowViewModel(ban.UserId.ToString());
        row.Take(ban, web_host);
        return row;
    }

    public static string PersonKey(RoomPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return $"{person.UserId}/{person.Index}";
    }

    public static string VisitKey(RoomVisit visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return visit.Name;
    }

    public void Take(RoomPerson person, string web_host)
    {
        ArgumentNullException.ThrowIfNull(person);
        Name = person.Name;
        Detail = person.Detail;
        Position = person.Position;
        Tag = person.Kind switch
        {
            RoomPersonKind.Bot => "bot",
            RoomPersonKind.Pet => "pet",
            _ => person.IsStaff ? "staff" : ""
        };
        Kind = person.Kind;
        IsIdle = person.IsIdle;
        IsTrading = person.IsTrading;
        UserId = person.UserId;
        Index = person.Index;
        TileOrder = (person.Tile.X * 1024) + person.Tile.Y;
        RoomGeneration = person.RoomGeneration;
        Person = person.Person;
        Figure = person.Figure;
        Motto = person.Motto;
        Head = person.Kind is RoomPersonKind.Pet
            ? null
            : Picture(HabboUrls.Head(person.Figure, web_host), person.Kind);
        Search = new PersonSearch(Name, Detail);
    }

    public void Take(RoomVisit visit, string web_host)
    {
        ArgumentNullException.ThrowIfNull(visit);
        Name = visit.Name;
        Detail = visit.Window;
        Position = visit.Visits > 1 ? $"×{visit.Visits}" : "";
        Tag = visit.IsHere ? "here" : "";
        Kind = RoomPersonKind.User;
        IsIdle = false;
        IsTrading = false;
        UserId = visit.UserId;
        Index = visit.Index;
        TileOrder = visit.Visits;
        RoomGeneration = 0;
        Person = null;
        Figure = "";
        Motto = "";
        Head = Picture(HabboUrls.HeadForName(visit.Name, web_host), RoomPersonKind.User);
        Search = new PersonSearch(Name, Detail);
    }

    public void Take(RoomBanEntry ban, string web_host)
    {
        ArgumentNullException.ThrowIfNull(ban);
        Name = ban.Name;
        Detail = $"id {ban.UserId}";
        Position = "";
        Tag = "";
        Kind = RoomPersonKind.User;
        IsIdle = false;
        IsTrading = false;
        UserId = ban.UserId;
        Index = -1;
        TileOrder = 0;
        RoomGeneration = ban.RoomGeneration;
        Person = null;
        Figure = "";
        Motto = "";
        Head = Picture(HabboUrls.HeadForName(ban.Name, web_host), RoomPersonKind.User);
        Search = new PersonSearch(Name, Detail);
    }

    static ImageRequest? Picture(string? url, RoomPersonKind kind) =>
        url is null ? null : new ImageRequest(url, false, kind switch
        {
            RoomPersonKind.Bot => IconKind.Bot,
            RoomPersonKind.Pet => IconKind.Pet,
            _ => IconKind.User
        });
}
