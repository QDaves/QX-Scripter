namespace Qx.Presentation.ViewModels.GameCatalog;

public enum GameDataTab
{
    Furni,
    Texts,
    Variables,
    Products
}

public enum FurniPlacementFilter
{
    All,
    Floor,
    Wall
}

public enum FurniAvailability
{
    Anywhere,
    Marketplace,
    Shop,
    Both
}

public enum BuySource
{
    Cheapest,
    Marketplace,
    Shop
}

public sealed record PlacementChoice(FurniPlacementFilter Value, string Text)
{
    public static IReadOnlyList<PlacementChoice> All { get; } =
    [
        new(FurniPlacementFilter.All, "All"),
        new(FurniPlacementFilter.Floor, "Floor"),
        new(FurniPlacementFilter.Wall, "Wall")
    ];
}

public sealed record AvailabilityChoice(FurniAvailability Value, string Text)
{
    public static IReadOnlyList<AvailabilityChoice> All { get; } =
    [
        new(FurniAvailability.Anywhere, "Anywhere"),
        new(FurniAvailability.Marketplace, "Marketplace"),
        new(FurniAvailability.Shop, "Shop"),
        new(FurniAvailability.Both, "Both")
    ];
}

public sealed record BuySourceChoice(BuySource Value, string Text)
{
    public static IReadOnlyList<BuySourceChoice> All { get; } =
    [
        new(BuySource.Cheapest, "Cheapest"),
        new(BuySource.Marketplace, "Marketplace"),
        new(BuySource.Shop, "Shop")
    ];
}
