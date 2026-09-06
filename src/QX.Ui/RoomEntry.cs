using System.ComponentModel;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;

namespace Qx.Ui;

/// <summary>Which part of the room's contents is being looked at.</summary>
public enum RoomSection
{
    Info,
    Users,
    Visitors,
    Bans,
    Furni
}

/// <summary>
/// Anything that draws a picture fetched from the hotel.
/// </summary>
/// <remarks>
/// The fetch is started by the first read of <see cref="Image"/> and the row is told when it lands,
/// because a binding cannot await. It goes through <see cref="HabboImages"/> rather than straight
/// off the url: the imaging host refuses a request carrying no user agent, and these records are
/// rebuilt on every room event, so a cache living here would be thrown away on each one.
/// </remarks>
public abstract class RemoteImage : INotifyPropertyChanged
{
    private static UiTaskScope? _image_tasks;
    private ImageSource? _image;
    private bool _requested;

    public event PropertyChangedEventHandler? PropertyChanged;

    public abstract string? ImageUrl { get; }

    public ImageSource? Image
    {
        get
        {
            if (_requested || ImageUrl is null)
                return _image;

            _requested = true;
            ImageTasks.OnUi(LoadAsync);
            return _image;
        }
    }

    public bool HasImage => Image is not null;

    private static UiTaskScope ImageTasks => _image_tasks ??= new UiTaskScope(
        System.Windows.Application.Current?.Dispatcher ??
            throw new InvalidOperationException("Image loading requires an active WPF application."),
        "images");

    protected static void Observe(Func<Task> task_factory) => ImageTasks.Observe(task_factory);

    private async Task LoadAsync()
    {
        ImageSource? loaded = await HabboImages.LoadAsync(ImageUrl).ConfigureAwait(true);
        if (loaded is null)
            return;

        _image = loaded;
        Raise(nameof(Image));
        Raise(nameof(HasImage));
    }

    /// <summary>
    /// Asks again, for a row whose picture is not fixed.
    /// </summary>
    /// <remarks>
    /// Most rows show one picture for their whole life. A wardrobe tile does not: turning the
    /// figure is a different render at a different address, so the tile has to be able to forget
    /// what it fetched.
    /// </remarks>
    /// <summary>
    /// Puts a picture in place that was already fetched.
    /// </summary>
    /// <remarks>
    /// <see cref="Reload"/> clears what is shown and asks again, so the tile goes blank for as long
    /// as the fetch takes. For anything that changes while somebody is looking at it, that blink is
    /// the whole problem: fetch first, then swap, and nothing empty is ever on screen.
    /// </remarks>
    protected void Adopt(ImageSource picture)
    {
        _image = picture;
        _requested = true;
        Raise(nameof(Image));
        Raise(nameof(HasImage));
    }

    protected void Reload()
    {
        _requested = false;
        _image = null;
        Raise(nameof(Image));
        Raise(nameof(HasImage));
        Raise(nameof(ImageUrl));
    }

    protected void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

/// <summary>
/// One line in the room browser: a person, a bot, a pet or a piece of furni.
/// </summary>
/// <remarks>
/// One shape for all of them rather than a type each. What a row shows is a picture, a name, a
/// short line under it and a position, and every one of them has those; a separate record each
/// would be four templates saying the same thing.
/// </remarks>
public sealed class RoomEntry(string? imageUrl = null) : RemoteImage
{
    public required string Name { get; init; }
    public string Detail { get; init; } = "";
    public string Position { get; init; } = "";
    public string Tag { get; init; } = "";
    public PackIconKind Fallback { get; init; } = PackIconKind.AccountCircle;
    public bool IsIdle { get; init; }
    public bool IsTrading { get; init; }

    /// <summary>The identity behind the row, for anything that acts on it.</summary>
    public Id EntityId { get; init; }

    public int Index { get; init; } = -1;
    public long RoomGeneration { get; init; }

    /// <summary>
    /// The avatar the row stands for, when it is one.
    /// </summary>
    /// <remarks>
    /// Carried rather than looked up again: acting on somebody needs their room index, their figure
    /// and their motto, and finding those from a name means walking the room once per row.
    /// </remarks>
    public Avatar? Person { get; init; }

    /// <summary>
    /// The piece of furni the row stands for, when it is one.
    /// </summary>
    /// <remarks>
    /// Carried rather than looked up again by id: the acts on the menu need to know whether it is a
    /// floor or a wall item, where it stands and whose it is, and finding all of that from an id
    /// means walking the room once per selected row.
    /// </remarks>
    public Furni? Item { get; init; }

    public override string? ImageUrl { get; } = imageUrl;

    public bool HasPosition => Position.Length > 0;
    public bool HasDetail => Detail.Length > 0;
    public bool HasTag => Tag.Length > 0;
}

/// <summary>
/// One cell of the furni grid: every copy of a single kind in the room, counted.
/// </summary>
/// <remarks>
/// The list answers "where is each one and whose is it", so it keeps every piece apart. The grid
/// answers "what is in this room at all", which is a different question, so it folds copies of one
/// kind into a single cell with a count. Both read the same room; neither replaces the other.
/// </remarks>
public sealed class FurniStack(string? imageUrl = null) : RemoteImage
{
    public required string Name { get; init; }
    public required int Count { get; init; }
    public string Identifier { get; init; } = "";
    public int Kind { get; init; }
    public ItemType Type { get; init; }

    /// <summary>Every copy of the kind, so an act from the grid reaches all of them.</summary>
    public IReadOnlyList<Furni> Items { get; init; } = [];

    public override string? ImageUrl { get; } = imageUrl;

    public bool HasMany => Count > 1;

    /// <summary>Stands in for an icon that never arrives, so a cell is never blank.</summary>
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    public string Tooltip => Identifier.Length > 0
        ? $"{Name}\n{Count} in the room\n{Identifier}"
        : $"{Name}\n{Count} in the room";
}

/// <summary>Reads the mirrored room state into rows the browser can draw.</summary>
public static class RoomContents
{
    /// <summary>
    /// Every person, bot and pet in the room, people first.
    /// </summary>
    /// <remarks>
    /// One list rather than three tabs. Who is in a room is a single question, and splitting it by
    /// what kind of thing each one is answers it three times over. The kind rides along as a tag on
    /// the row instead.
    /// </remarks>
    public static IReadOnlyList<RoomEntry> Avatars(GameState game) =>
        game.Room.Capture(room =>
            room.Avatars
                .OrderBy(Rank)
                .ThenBy(avatar => avatar.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(avatar => Row(avatar, room.Generation))
                .ToList());

    private static int Rank(Avatar avatar) => avatar switch
    {
        User => 0,
        Bot => 1,
        _ => 2
    };

    private static RoomEntry Row(Avatar avatar, long room_generation)
    {
        // A pet's figure needs the pet imager and a breed split out of its own figure string, which
        // the avatar imager cannot render. A glyph beats a broken picture.
        string? head = Head(avatar);

        return new RoomEntry(head)
        {
            Name = avatar.Name,
            Detail = Detail(avatar),
            Position = $"{avatar.X}, {avatar.Y}",
            Tag = Tag(avatar),
            Fallback = avatar switch
            {
                Bot => PackIconKind.Robot,
                Pet => PackIconKind.Paw,
                _ => PackIconKind.AccountCircle
            },
            IsIdle = avatar.IsIdle,
            EntityId = avatar.Id,
            Index = avatar.Index,
            RoomGeneration = room_generation,
            Person = avatar
        };
    }

    internal static string? Head(Avatar avatar) =>
        avatar is User or Bot ? HabboImages.HeadUrl(avatar.Figure) : null;

    private static string Detail(Avatar avatar) => avatar switch
    {
        Pet pet => pet.OwnerName.Length > 0 ? $"owned by {pet.OwnerName}" : "",
        Bot bot => bot.OwnerName.Length > 0 ? $"owned by {bot.OwnerName}" : "",
        _ => avatar.Motto
    };

    private static string Tag(Avatar avatar) => avatar switch
    {
        User { IsStaff: true } => "staff",
        Bot => "bot",
        Pet => "pet",
        _ => ""
    };

    /// <summary>
    /// Every piece of furni in the room, floor and wall together, one row each.
    /// </summary>
    /// <remarks>
    /// Not collapsed by kind — see <see cref="FurniByKind"/> for the view that is.
    /// </remarks>
    public static IReadOnlyList<RoomEntry> Furni(GameState game)
    {
        FurniData? data = game.GameData.Furni;

        return All(game)
            .Select(item =>
            {
                FurniInfo? info = data?.GetInfo(item);
                return new RoomEntry(HabboImages.FurniIconUrl(info?.Revision ?? 0, info?.Identifier))
                {
                    Name = Named(info, item),
                    Detail = item.OwnerName.Length > 0 ? $"owned by {item.OwnerName}" : "",
                    Position = item is FloorItem floor ? $"{floor.X}, {floor.Y}" : "wall",
                    Tag = item.IsHidden ? "hidden" : "",
                    Fallback = PackIconKind.SofaSingleOutline,
                    EntityId = item.Id,
                    Item = item
                };
            })
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The furni standing inside a rectangle of floor.
    /// </summary>
    /// <remarks>
    /// Wall items are left out. An area is two tiles on the floor, and a picture hanging on the
    /// wall is not on either of them.
    /// </remarks>
    public static IReadOnlyList<RoomEntry> Within(IReadOnlyList<RoomEntry> rows, Area area) =>
        [.. rows.Where(row => row.Item is FloorItem floor && area.Contains(floor.Location.XY))];

    /// <summary>Everyone who has been in the room, most recently arrived first.</summary>
    public static IReadOnlyList<RoomEntry> Visitors(GameState game) =>
        game.Visitors.Visitors
            .Select(visitor => new RoomEntry(HabboImages.HeadUrlForName(visitor.Name))
            {
                Name = visitor.Name,
                Detail = VisitDetail(visitor),
                Tag = visitor.IsHere ? "here" : "",
                Position = visitor.Visits > 1 ? $"×{visitor.Visits}" : "",
                Fallback = PackIconKind.AccountCircle,
                EntityId = visitor.UserId,
                Index = visitor.Index
            })
            .ToList();

    private static string VisitDetail(RoomVisitor visitor) => (visitor.Entered, visitor.Left) switch
    {
        (null, null) => "was already here",
        (null, { } left) => $"was already here, left {Clock(left)}",
        ({ } entered, null) => $"came in {Clock(entered)}",
        ({ } entered, { } left) => $"{Clock(entered)} – {Clock(left)}"
    };

    private static string Clock(DateTime moment) => moment.ToString("HH:mm:ss");

    /// <summary>Everyone barred from the room.</summary>
    public static IReadOnlyList<RoomEntry> Bans(RoomModerationStateView state) =>
        state.BanList.Bans
            .Select(ban => new RoomEntry(HabboImages.HeadUrlForName(ban.Name))
            {
                Name = ban.Name,
                Detail = $"id {ban.UserId}",
                Fallback = PackIconKind.AccountCancelOutline,
                EntityId = ban.UserId,
                RoomGeneration = state.RoomGeneration
            })
            .ToList();

    /// <summary>The same room folded to one cell per kind, for the grid.</summary>
    public static IReadOnlyList<FurniStack> FurniByKind(GameState game)
    {
        FurniData? data = game.GameData.Furni;

        return All(game)
            .GroupBy(item => (item.Type, item.Kind))
            .Select(group =>
            {
                Qx.Model.Furni first = group.First();
                FurniInfo? info = data?.GetInfo(first);
                return new FurniStack(HabboImages.FurniIconUrl(info?.Revision ?? 0, info?.Identifier))
                {
                    Name = Named(info, first),
                    Count = group.Count(),
                    Identifier = info?.Identifier ?? "",
                    Kind = first.Kind,
                    Type = first.Type,
                    Items = [.. group]
                };
            })
            .OrderBy(stack => stack.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IEnumerable<Qx.Model.Furni> All(GameState game) =>
        game.Room.FloorItems.Cast<Qx.Model.Furni>().Concat(game.Room.WallItems);

    private static string Named(FurniInfo? info, Qx.Model.Furni item) =>
        info?.Name is { Length: > 0 } name ? name : $"Furni {item.Kind}";
}
