using System.Globalization;
using Qx.Model;

namespace Qx.Presentation.Services.Marketplace;

public sealed record MarketplacePrice(ItemType Type, string Identifier, int? CurrentPrice, int? AveragePrice, int OpenOffers, int SoldLately)
{
    public int? Suggested => CurrentPrice ?? AveragePrice;

    public bool IsKnown => Suggested is not null;

    public bool IsCurrent => CurrentPrice is not null;

    public string ShortText => IsCurrent
        ? string.Create(CultureInfo.InvariantCulture, $"{CurrentPrice}c")
        : IsKnown ? string.Create(CultureInfo.InvariantCulture, $"~{AveragePrice}c") : "no offers";
}
