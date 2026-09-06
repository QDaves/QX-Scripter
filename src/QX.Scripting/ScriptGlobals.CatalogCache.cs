using Qx.Game;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Reads the whole catalog once and keeps it, so later searches answer from memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the expensive call: a full catalog is well over a hundred round trips. Everything
    /// after it — <see cref="FindCatalogOffers"/>, <see cref="CachedCatalogPages"/>,
    /// <see cref="GetCatalogPage"/> — answers from what this collected.
    /// </para>
    /// <para>
    /// Safe to call again. Pages already held and still current are skipped, so a second call after
    /// an interrupted walk finishes it rather than repeating it, and the cache is cleared by itself
    /// when the hotel announces a republish.
    /// </para>
    /// </remarks>
    /// <param name="catalogType">The catalog mode, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.</param>
    /// <param name="onlyVisible">Whether to skip pages the client would not show.</param>
    /// <param name="delayMs">A pause between pages, to stay gentle on the hotel.</param>
    /// <param name="maxAgeMinutes">
    /// How old a cached page may be before it is fetched again. Zero forces a full refetch.
    /// </param>
    /// <param name="onProgress">Called with pages done and pages total, if given.</param>
    public Task<CatalogLoadReport> LoadCatalog(
        string catalogType = "NORMAL",
        bool onlyVisible = true,
        int delayMs = 0,
        double maxAgeMinutes = 5,
        Action<int, int>? onProgress = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxAgeMinutes);
        IProgress<(int Loaded, int Total)>? progress = onProgress is null
            ? null
            : new Progress<(int Loaded, int Total)>(step => onProgress(step.Loaded, step.Total));

        return Game.Catalog.LoadAllPagesAsync(
            catalogType,
            onlyVisible,
            delayMs,
            TimeSpan.FromMinutes(maxAgeMinutes),
            15000,
            progress,
            Ct);
    }

    /// <summary>
    /// Searches the cached catalog by text, without asking the hotel.
    /// </summary>
    /// <remarks>
    /// Searches only what is cached, so run <see cref="LoadCatalog"/> first. Matching covers the
    /// offer's localisation key, each product's extra parameter, and the furni name resolved from
    /// the downloaded furni data, which is what makes a search for a display name work.
    /// </remarks>
    /// <param name="text">What to look for; matched case-insensitively.</param>
    /// <param name="catalogType">The catalog mode.</param>
    public IReadOnlyList<CatalogOfferMatch> FindCatalogOffers(
        string text,
        string catalogType = "NORMAL") =>
        Game.Catalog.FindOffers(text, catalogType, ProductDisplayName);

    /// <summary>Every catalog page currently held in the cache.</summary>
    /// <param name="catalogType">The catalog mode.</param>
    public IReadOnlyList<CatalogPage> CachedCatalogPages(string catalogType = "NORMAL") =>
        Game.Catalog.CachedPages(catalogType);

    /// <summary>
    /// What the catalog cache holds and how old it is, for deciding whether to reload.
    /// </summary>
    /// <param name="catalogType">The catalog mode.</param>
    public CatalogCacheState CatalogCache(string catalogType = "NORMAL") =>
        Game.Catalog.CacheState(catalogType);

    /// <summary>Forgets the cached catalog so the next read fetches it again.</summary>
    /// <param name="catalogType">The mode to forget, or <see langword="null"/> for all of them.</param>
    public void ClearCatalogCache(string? catalogType = null) =>
        Game.Catalog.ClearCache(catalogType);

    /// <summary>
    /// The display name of a catalog product, resolved through the downloaded furni data.
    /// </summary>
    /// <remarks>
    /// Falls back to the badge or effect name where the product is one of those, and to
    /// <see langword="null"/> when nothing is known, which keeps it usable as a search input.
    /// </remarks>
    /// <param name="product">The product.</param>
    public string? ProductDisplayName(CatalogProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return product.ProductType switch
        {
            "s" => Game.GameData.Furni?.GetInfo(ItemType.Floor, product.FurniClassId)?.Name,
            "i" => Game.GameData.Furni?.GetInfo(ItemType.Wall, product.FurniClassId)?.Name,
            "b" => Game.GameData.Texts?.BadgeName(product.ExtraParam),
            "e" => Game.GameData.Texts?.EffectName(product.FurniClassId),
            _ => null
        };
    }
}
