using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Model;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed record FurniSearch(string Name, string Detail, string Identifier);

public sealed partial class FurniRowViewModel(string key) : ObservableObject
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string Detail { get; private set; } = "";

    [ObservableProperty]
    public partial string Position { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTag))]
    public partial string Tag { get; private set; } = "";

    [ObservableProperty]
    public partial ImageRequest? Icon { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdText))]
    public partial Id ItemId { get; private set; }

    public int Kind { get; private set; }

    public ItemType Placement { get; private set; }

    public bool IsFloor { get; private set; }

    public Point Tile { get; private set; }

    public Furni? Item { get; private set; }

    public FurniSearch Search { get; private set; } = new("", "", "");

    public string IdText => ItemId.ToString();

    public bool HasDetail => Detail.Length > 0;

    public bool HasTag => Tag.Length > 0;

    public static string KeyOf(RoomFurniPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return $"{piece.Placement}/{piece.ItemId}";
    }

    public static FurniRowViewModel From(RoomFurniPiece piece, string web_host)
    {
        var row = new FurniRowViewModel(KeyOf(piece));
        row.Take(piece, web_host);
        return row;
    }

    public void Take(RoomFurniPiece piece, string web_host)
    {
        ArgumentNullException.ThrowIfNull(piece);
        Name = piece.Name;
        Detail = piece.Owner;
        Position = piece.Position;
        Tag = piece.IsHidden ? "hidden" : "";
        ItemId = piece.ItemId;
        Kind = piece.Kind;
        Placement = piece.Placement;
        IsFloor = piece.IsFloor;
        Tile = piece.Tile;
        Item = piece.Item;
        Icon = HabboUrls.FurniIcon(piece.Revision, piece.Identifier) is { } url
            ? new ImageRequest(url, true, IconKind.Furni)
            : null;
        Search = new FurniSearch(Name, Detail, piece.Identifier);
    }
}
