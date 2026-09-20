namespace Qx.Presentation.Services.GameCatalog;

public sealed record FurniShopOffer(
    int PageId,
    int OfferId,
    string Page,
    int Amount,
    int PriceInCredits,
    int PriceInActivityPoints,
    int ActivityPointType,
    int PriceInSilver)
{
    public bool IsCreditOnly => PriceInActivityPoints == 0 && PriceInSilver == 0;

    public string PriceText
    {
        get
        {
            List<string> parts = [];
            if (PriceInCredits > 0)
                parts.Add($"{PriceInCredits}c");
            if (PriceInActivityPoints > 0)
                parts.Add($"{PriceInActivityPoints} AP{ActivityPointType}");
            if (PriceInSilver > 0)
                parts.Add($"{PriceInSilver} silver");
            return parts.Count == 0 ? "free" : string.Join(" + ", parts);
        }
    }

    public string Details => Amount > 1
        ? $"{PriceText} · {Amount} items · {Page}"
        : $"{PriceText} · {Page}";

    public bool UsesSameCurrencies(FurniShopOffer other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PriceInCredits > 0 == other.PriceInCredits > 0 &&
            PriceInActivityPoints > 0 == other.PriceInActivityPoints > 0 &&
            PriceInSilver > 0 == other.PriceInSilver > 0 &&
            (PriceInActivityPoints == 0 || ActivityPointType == other.ActivityPointType);
    }

    public bool CostsMoreThan(FurniShopOffer other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PriceInCredits > other.PriceInCredits ||
            PriceInActivityPoints > other.PriceInActivityPoints ||
            PriceInSilver > other.PriceInSilver;
    }
}
