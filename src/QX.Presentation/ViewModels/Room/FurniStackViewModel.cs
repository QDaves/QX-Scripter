using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Model;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class FurniStackViewModel(string key) : ObservableObject
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial), nameof(Tooltip))]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMany), nameof(Tooltip))]
    public partial int Count { get; private set; }

    [ObservableProperty]
    public partial ImageRequest? Icon { get; private set; }

    public string Identifier { get; private set; } = "";

    public int Kind { get; private set; }

    public ItemType Placement { get; private set; }

    public IReadOnlyList<Furni> Items { get; private set; } = [];

    public bool HasMany => Count > 1;

    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    public string Tooltip => Identifier.Length > 0
        ? $"{Name}\n{Identifier}\n{Count} in the room"
        : $"{Name}\n{Count} in the room";

    public static string KeyOf(ItemType placement, int kind) => $"{placement}/{kind}";

    public void Take(FurniRowViewModel first, IReadOnlyList<Furni> items, int count)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(items);
        Name = first.Name;
        Count = count;
        Identifier = first.Search.Identifier;
        Kind = first.Kind;
        Placement = first.Placement;
        Items = items;
        Icon = first.Icon is { } picture
            ? new ImageRequest(picture.Url, true, IconKind.Furni)
            : null;
    }
}
