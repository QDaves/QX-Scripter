using Qx.Game;
using Qx.Game.Application;
using Qx.Model;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the downloaded game data, such as furni definitions, catalog products and hotel texts.
    /// </summary>
    /// <remarks>
    /// Check <see cref="Qx.Game.GameData.IsLoaded"/> before relying on it; each data property is
    /// <see langword="null"/> until it has been loaded for the current hotel.
    /// </remarks>
    public GameData GameData => Game.GameData;

    /// <summary>
    /// Gets the furni definition behind a room item.
    /// </summary>
    /// <remarks>
    /// The definition holds the class identifier, display name, category, stacking and sit and
    /// walk flags. Matching prefers the item's own class identifier and falls back to its type
    /// and kind.
    /// </remarks>
    /// <param name="item">Any floor or wall item in a room.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// item's kind is not in it.
    /// </returns>
    public FurniInfo? FurniOf(Furni item) => Game.GameData.Furni?.GetInfo(item);

    /// <summary>
    /// Gets the furni definition behind an inventory item.
    /// </summary>
    /// <remarks>
    /// The item is matched by its type and kind, as <see cref="FurniOf(ItemType, int)"/> does.
    /// The definition's <see cref="FurniInfo.Identifier"/> is the item's class identifier.
    /// </remarks>
    /// <param name="item">An item in the local user's inventory.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// item's kind is not in it.
    /// </returns>
    public FurniInfo? FurniOf(InventoryItem item) => FurniOf(item.Type, item.Kind);

    /// <summary>
    /// Gets the furni definition behind an item offered in a trade.
    /// </summary>
    /// <remarks>
    /// The item is matched by its type and kind, as <see cref="FurniOf(ItemType, int)"/> does.
    /// The definition's <see cref="FurniInfo.Identifier"/> is the item's class identifier.
    /// </remarks>
    /// <param name="item">An item from a trade offer message, as <c>TradeOffers</c> lists it.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// item's kind is not in it.
    /// </returns>
    public FurniInfo? FurniOf(TradeItem item) => FurniOf(item.Type, item.Kind);

    /// <summary>
    /// Gets the furni definition behind an item of the open trade.
    /// </summary>
    /// <remarks>
    /// The item is matched by its type and kind, as <see cref="FurniOf(ItemType, int)"/> does.
    /// The definition's <see cref="FurniInfo.Identifier"/> is the item's class identifier.
    /// </remarks>
    /// <param name="item">An item of either side of the open trade, as <see cref="TradeOfferView.Items"/> lists it.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// item's kind is not in it.
    /// </returns>
    public FurniInfo? FurniOf(TradeItemView item) => FurniOf(item.Type, item.Kind);

    /// <summary>
    /// Gets the furni definition for a type and kind.
    /// </summary>
    /// <remarks>
    /// Use it for items that carry only a type and kind, such as marketplace offers and catalog
    /// entries.
    /// </remarks>
    /// <param name="type">Whether the kind is a floor or a wall item.</param>
    /// <param name="kind">The numeric kind, which differs between hotels.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// kind is not in it.
    /// </returns>
    public FurniInfo? FurniOf(ItemType type, int kind) => Game.GameData.Furni?.GetInfo(type, kind);

    /// <summary>
    /// Gets the display name of a room item, as shown in the client.
    /// </summary>
    /// <param name="item">Any floor or wall item in a room.</param>
    /// <returns>
    /// The localized name, or <c>"#"</c> followed by the numeric kind when the furni data has not
    /// downloaded, the kind is unknown or the name is empty, so the result is never empty.
    /// </returns>
    public string FurniName(Furni item) =>
        Game.GameData.Furni?.GetInfo(item)?.Name is { Length: > 0 } name ? name : "#" + item.Kind;

    /// <summary>
    /// Gets the display name of an inventory item, as shown in the client.
    /// </summary>
    /// <param name="item">An item in the local user's inventory.</param>
    /// <returns>
    /// The localized name, or <c>"#"</c> followed by the numeric kind when the furni data has not
    /// downloaded, the kind is unknown or the name is empty, so the result is never empty.
    /// </returns>
    public string FurniName(InventoryItem item) => FurniName(item.Type, item.Kind);

    /// <summary>
    /// Gets the display name of an item offered in a trade, as shown in the client.
    /// </summary>
    /// <param name="item">An item from a trade offer message, as <c>TradeOffers</c> lists it.</param>
    /// <returns>
    /// The localized name, or <c>"#"</c> followed by the numeric kind when the furni data has not
    /// downloaded, the kind is unknown or the name is empty, so the result is never empty.
    /// </returns>
    public string FurniName(TradeItem item) => FurniName(item.Type, item.Kind);

    /// <summary>
    /// Gets the display name of an item of the open trade, as shown in the client.
    /// </summary>
    /// <param name="item">An item of either side of the open trade, as <see cref="TradeOfferView.Items"/> lists it.</param>
    /// <returns>
    /// The localized name, or <c>"#"</c> followed by the numeric kind when the furni data has not
    /// downloaded, the kind is unknown or the name is empty, so the result is never empty.
    /// </returns>
    public string FurniName(TradeItemView item) => FurniName(item.Type, item.Kind);

    /// <summary>
    /// Gets the display name for a type and kind, as shown in the client.
    /// </summary>
    /// <param name="type">Whether the kind is a floor or a wall item.</param>
    /// <param name="kind">The numeric kind, which differs between hotels.</param>
    /// <returns>
    /// The localized name, or <c>"#"</c> followed by the kind when the furni data has not
    /// downloaded, the kind is unknown or the name is empty, so the result is never empty.
    /// </returns>
    public string FurniName(ItemType type, int kind) =>
        Game.GameData.Furni?.GetInfo(type, kind)?.Name is { Length: > 0 } name ? name : "#" + kind;

    /// <summary>
    /// Gets whether a furni is of the given class, comparing class identifiers and ignoring case.
    /// </summary>
    /// <remarks>
    /// Comparing identifiers recognizes a furni across hotels, since kind numbers differ between
    /// hotels. The comparison uses the full identifier from the furni data, including any
    /// <c>*</c> color suffix.
    /// </remarks>
    /// <param name="item">The furni to test.</param>
    /// <param name="identifier">The class identifier, for example <c>"rare_dragonlamp"</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the identifiers match; otherwise, <see langword="false"/>,
    /// which is also the result when the furni data has not downloaded yet.
    /// </returns>
    public bool IsIdentifier(Furni item, string identifier) =>
        HasIdentifier(FurniOf(item), identifier);

    /// <summary>
    /// Gets whether an inventory item is of the given class, comparing class identifiers and
    /// ignoring case.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="IsIdentifier(Furni, string)"/>.
    /// </remarks>
    /// <param name="item">An item in the local user's inventory.</param>
    /// <param name="identifier">The class identifier, for example <c>"rare_dragonlamp"</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the identifiers match; otherwise, <see langword="false"/>,
    /// which is also the result when the furni data has not downloaded yet.
    /// </returns>
    public bool IsIdentifier(InventoryItem item, string identifier) =>
        HasIdentifier(FurniOf(item), identifier);

    /// <summary>
    /// Gets whether an item offered in a trade is of the given class, comparing class identifiers
    /// and ignoring case.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="IsIdentifier(Furni, string)"/>.
    /// </remarks>
    /// <param name="item">An item from a trade offer message, as <c>TradeOffers</c> lists it.</param>
    /// <param name="identifier">The class identifier, for example <c>"rare_dragonlamp"</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the identifiers match; otherwise, <see langword="false"/>,
    /// which is also the result when the furni data has not downloaded yet.
    /// </returns>
    public bool IsIdentifier(TradeItem item, string identifier) =>
        HasIdentifier(FurniOf(item), identifier);

    /// <summary>
    /// Gets whether an item of the open trade is of the given class, comparing class identifiers
    /// and ignoring case.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="IsIdentifier(Furni, string)"/>.
    /// </remarks>
    /// <param name="item">An item of either side of the open trade, as <see cref="TradeOfferView.Items"/> lists it.</param>
    /// <param name="identifier">The class identifier, for example <c>"rare_dragonlamp"</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the identifiers match; otherwise, <see langword="false"/>,
    /// which is also the result when the furni data has not downloaded yet.
    /// </returns>
    public bool IsIdentifier(TradeItemView item, string identifier) =>
        HasIdentifier(FurniOf(item), identifier);

    private static bool HasIdentifier(FurniInfo? info, string identifier) =>
        string.Equals(info?.Identifier, identifier, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the catalog product definition for a product code.
    /// </summary>
    /// <param name="code">The product code as used by the catalog, matched case-sensitively.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the product data has not downloaded or the
    /// code is unknown.
    /// </returns>
    public ProductInfo? ProductOf(string code) => Game.GameData.Products?.GetInfo(code);

    /// <summary>
    /// Gets the display name of a catalog product.
    /// </summary>
    /// <param name="code">The product code as used by the catalog.</param>
    /// <returns>The localized name, or the code itself when it cannot be resolved.</returns>
    public string ProductName(string code) =>
        ProductOf(code)?.Name is { Length: > 0 } name ? name : code;

    /// <summary>
    /// Gets the description text of a catalog product.
    /// </summary>
    /// <param name="code">The product code as used by the catalog.</param>
    /// <returns>The description, or an empty string when it cannot be resolved.</returns>
    public string ProductDescription(string code) =>
        ProductOf(code)?.Description ?? "";

    /// <summary>
    /// Gets the display name of a badge.
    /// </summary>
    /// <param name="code">The badge code, for example <c>"ACH_BasicClub1"</c>.</param>
    /// <returns>The localized name, or the code itself when the texts have not downloaded or have no entry.</returns>
    public string BadgeName(string code) => Game.GameData.Texts?.BadgeName(code) ?? code;

    /// <summary>
    /// Gets the display name of an avatar effect.
    /// </summary>
    /// <param name="id">The effect id, as reported by <see cref="OnAvatarEffectChanged"/>.</param>
    /// <returns>The localized name, or an empty string when it cannot be resolved.</returns>
    public string EffectName(int id) => Game.GameData.Texts?.EffectName(id) ?? "";

    /// <summary>
    /// Gets the display name of a hand item, which is the drink or object an avatar holds.
    /// </summary>
    /// <param name="id">The hand item id, as reported by <see cref="OnAvatarHandItemChanged"/>.</param>
    /// <returns>The localized name, or an empty string when it cannot be resolved.</returns>
    public string HandItemName(int id) => Game.GameData.Texts?.HandItemName(id) ?? "";

    /// <summary>
    /// Looks up a badge's localized name in the external texts, under the key
    /// <c>badge_name_&lt;code&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="BadgeName"/>, it tells a missing entry apart from a name.
    /// </remarks>
    /// <param name="code">The badge code.</param>
    /// <param name="name">Receives the name, or <see langword="null"/> when the key is absent.</param>
    /// <returns><see langword="true"/> when the text table has that key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public bool TryGetBadgeName(string code, out string? name) =>
        TryGetText($"badge_name_{code}", out name);

    /// <summary>
    /// Looks up a badge's localized description under the key <c>badge_desc_&lt;code&gt;</c>.
    /// </summary>
    /// <param name="code">The badge code.</param>
    /// <param name="description">Receives the description, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the text table has that key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public bool TryGetBadgeDescription(string code, out string? description) =>
        TryGetText($"badge_desc_{code}", out description);

    /// <summary>Gets a badge's localized description.</summary>
    /// <param name="code">The badge code.</param>
    /// <returns>The description, or <see langword="null"/> when absent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public string? GetBadgeDescription(string code) =>
        TryGetBadgeDescription(code, out string? description)
            ? description
            : null;

    /// <summary>
    /// Looks up an avatar effect's localized name under the key <c>fx_&lt;id&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="EffectName"/>, it tells a missing entry apart from a name.
    /// </remarks>
    /// <param name="id">The effect id.</param>
    /// <param name="name">Receives the name, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the text table has that key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public bool TryGetEffectName(int id, out string? name) =>
        TryGetText($"fx_{id}", out name);

    /// <summary>
    /// Looks up an avatar effect's localized description under the key <c>fx_&lt;id&gt;_desc</c>.
    /// </summary>
    /// <param name="id">The effect id.</param>
    /// <param name="description">Receives the description, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the text table has that key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public bool TryGetEffectDescription(int id, out string? description) =>
        TryGetText($"fx_{id}_desc", out description);

    /// <summary>Gets an avatar effect's localized description.</summary>
    /// <param name="id">The effect id.</param>
    /// <returns>The description, or <see langword="null"/> when absent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public string? GetEffectDescription(int id) =>
        TryGetEffectDescription(id, out string? description)
            ? description
            : null;

    /// <summary>
    /// Looks up a hand item's localized name under the key <c>handitem&lt;id&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="HandItemName"/>, it tells a missing entry apart from a name.
    /// </remarks>
    /// <param name="id">The hand item id.</param>
    /// <param name="name">Receives the name, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the text table has that key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public bool TryGetHandItemName(int id, out string? name) =>
        TryGetText($"handitem{id}", out name);

    /// <summary>
    /// Gets every hand item id whose localized name matches the given name, compared
    /// case-insensitively.
    /// </summary>
    /// <remarks>
    /// The reverse of <see cref="HandItemName"/>. Several ids can share one name, which is why
    /// the result is a sequence. Argument checks and the text lookup are deferred: both exceptions
    /// are thrown when the sequence is first enumerated, not when the method is called.
    /// </remarks>
    /// <param name="name">The hand item name to look for.</param>
    /// <returns>
    /// The matching ids, produced lazily by scanning the whole external text table on enumeration.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the external texts have not been loaded.</exception>
    public IEnumerable<int> GetHandItemIds(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach ((string key, string value) in LoadedTexts)
        {
            if (!value.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                !key.StartsWith("handitem", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(
                    key.AsSpan("handitem".Length),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int id))
            {
                continue;
            }
            yield return id;
        }
    }

    private ExternalTexts LoadedTexts =>
        Game.GameData.Texts ?? throw new InvalidOperationException("External texts have not been loaded.");

    private bool TryGetText(string key, out string? value)
    {
        value = LoadedTexts[key];
        return value is not null;
    }
}
