using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Model;
using Qx.Presentation.Services.Inventory;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Inventory;

public sealed record InventoryRowFacts(string Name, string Detail, string Group, ItemType? Type, int Count, bool Tradeable, int? Price);

public sealed partial class InventoryRowViewModel : ObservableObject
{
    public InventoryRowViewModel(InventoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Key = entry.Key;
        Adopt(entry);
    }

    public InventoryItemKind Key { get; }

    public MarketplaceKind Kind => new(Type ?? ItemType.Floor, Identifier);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    public partial string Identifier { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    public partial string Detail { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupText))]
    public partial string Group { get; private set; } = "";

    [ObservableProperty]
    public partial ItemType? Type { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip), nameof(HasMany), nameof(OwnedText))]
    public partial int Count { get; private set; }

    [ObservableProperty]
    public partial bool Tradeable { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<Id> ItemIds { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyText), nameof(KeySort))]
    public partial Id ItemId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial ImageRequest? Image { get; private set; }

    [ObservableProperty]
    public partial IconKind Fallback { get; private set; } = IconKind.Furni;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarketText), nameof(MarketValue), nameof(HasMarket))]
    public partial MarketplacePrice? Market { get; private set; }

    public InventoryRowFacts Facts { get; private set; } = new("", "", "", null, 0, false, null);

    public bool CanSell => Tradeable && Type is not null && Identifier.Length > 0;

    public bool HasImage => Image is not null;

    public bool HasMany => Count > 1;

    public string GroupText => Group switch
    {
        "floor" => "Floor",
        "wall" => "Wall",
        "pet" => "Pet",
        _ => Group
    };

    public string OwnedText => Count.ToString("N0", CultureInfo.CurrentCulture);

    public string KeyText => Math.Abs((long)ItemId).ToString(CultureInfo.InvariantCulture);

    public long KeySort => ItemId;

    public bool HasMarket => Market?.IsKnown == true;

    public int MarketValue => Market?.Suggested ?? -1;

    public string MarketText => !CanSell
        ? "—"
        : Market is null
            ? ""
            : Market.ShortText;

    public string Tooltip => Detail.Length > 0
        ? $"{Name}\n{Detail}" + (HasMany ? $"\n{Count} owned" : "")
        : Name + (HasMany ? $"\n{Count} owned" : "");

    public void Adopt(InventoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Name = entry.Name;
        Identifier = entry.Identifier;
        Detail = entry.Detail;
        Group = entry.Group;
        Type = entry.Type;
        Count = entry.Count;
        Tradeable = entry.Tradeable;
        ItemIds = entry.ItemIds;
        ItemId = entry.ItemId;
        Fallback = entry.Type is null ? IconKind.Pet : IconKind.Furni;
        Image = entry.ImageUrl is { Length: > 0 } url ? new ImageRequest(url, true, Fallback) : null;
        if (!CanSell)
            Market = null;
        Remember();
    }

    public bool ShowPrice(MarketplacePrice price)
    {
        ArgumentNullException.ThrowIfNull(price);
        if (Market is { } held && held.Suggested == price.Suggested && held.IsCurrent == price.IsCurrent)
            return false;
        Market = price;
        Remember();
        return true;
    }

    void Remember() => Facts = new InventoryRowFacts(Name, Detail, Group, Type, Count, Tradeable, Market?.Suggested);
}
