using Qx.Game;
using Qx.Model;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// The downloaded game data: furni definitions, catalog products and hotel texts. Check
    /// <see cref="Qx.Game.GameData.IsLoaded"/> before relying on it.
    /// </summary>
    public GameData GameData => Game.GameData;

    /// <summary>
    /// The furni definition behind a room item: its class identifier, display name, category,
    /// stacking and sit/walk flags. Inventory and trade items are not <see cref="Furni"/>; look
    /// those up with <see cref="FurniOf(ItemType, int)"/>.
    /// </summary>
    /// <param name="item">Any floor or wall item in a room.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// item's kind is not in it. Matching prefers the item's own class identifier and falls back
    /// to its type and kind.
    /// </returns>
    public FurniInfo? FurniOf(Furni item) => Game.GameData.Furni?.GetInfo(item);

    /// <summary>
    /// The furni definition for a type and kind, for items that are not room furni - inventory
    /// items, trade offers, marketplace offers and catalog entries.
    /// </summary>
    /// <param name="type">Whether the kind is a floor or a wall item.</param>
    /// <param name="kind">The numeric kind, which differs between hotels.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the furni data has not downloaded or the
    /// kind is not in it.
    /// </returns>
    public FurniInfo? FurniOf(ItemType type, int kind) => Game.GameData.Furni?.GetInfo(type, kind);

    /// <summary>
    /// The display name of a room item, as shown in the client. For inventory and trade items use
    /// <see cref="FurniName(ItemType, int)"/>.
    /// </summary>
    /// <returns>
    /// The localised name, or <c>"#"</c> followed by the numeric kind when the furni data has not
    /// downloaded or the kind is unknown - so this never returns an empty string.
    /// </returns>
    public string FurniName(Furni item) =>
        Game.GameData.Furni?.GetInfo(item)?.Name is { Length: > 0 } name ? name : "#" + item.Kind;

    /// <summary>
    /// The display name for a type and kind, as shown in the client.
    /// </summary>
    /// <param name="type">Whether the kind is a floor or a wall item.</param>
    /// <param name="kind">The numeric kind, which differs between hotels.</param>
    /// <returns>
    /// The localised name, or <c>"#"</c> followed by the kind when the furni data has not
    /// downloaded or the kind is unknown - so this never returns an empty string.
    /// </returns>
    public string FurniName(ItemType type, int kind) =>
        Game.GameData.Furni?.GetInfo(type, kind)?.Name is { Length: > 0 } name ? name : "#" + kind;

    /// <summary>
    /// Whether a furni is of the given class, comparing class identifiers case-insensitively.
    /// This is the robust way to recognise a furni, since kind numbers differ between hotels.
    /// </summary>
    /// <param name="item">The furni to test.</param>
    /// <param name="identifier">The class identifier, for example <c>"rare_dragonlamp"</c>.</param>
    /// <returns><see langword="false"/> when the furni data has not downloaded yet.</returns>
    public bool IsIdentifier(Furni item, string identifier) =>
        string.Equals(FurniOf(item)?.Identifier, identifier, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The catalog product definition for a product code.
    /// </summary>
    /// <param name="code">The product code as used by the catalog.</param>
    /// <returns>
    /// The definition, or <see langword="null"/> when the product data has not downloaded or the
    /// code is unknown.
    /// </returns>
    public ProductInfo? ProductOf(string code) => Game.GameData.Products?.GetInfo(code);

    /// <summary>
    /// The display name of a catalog product.
    /// </summary>
    /// <returns>The localised name, or the code itself when it cannot be resolved.</returns>
    public string ProductName(string code) =>
        ProductOf(code)?.Name is { Length: > 0 } name ? name : code;

    /// <summary>
    /// The description text of a catalog product.
    /// </summary>
    /// <returns>The description, or an empty string when it cannot be resolved.</returns>
    public string ProductDescription(string code) =>
        ProductOf(code)?.Description ?? "";

    /// <summary>
    /// The display name of a badge.
    /// </summary>
    /// <param name="code">The badge code, for example <c>"ACH_BasicClub1"</c>.</param>
    /// <returns>The localised name, or the code itself when the texts have not downloaded or have no entry.</returns>
    public string BadgeName(string code) => Game.GameData.Texts?.BadgeName(code) ?? code;

    /// <summary>
    /// The display name of an avatar effect.
    /// </summary>
    /// <param name="id">The effect id, as reported by <see cref="OnAvatarEffectChanged"/>.</param>
    /// <returns>The localised name, or an empty string when it cannot be resolved.</returns>
    public string EffectName(int id) => Game.GameData.Texts?.EffectName(id) ?? "";

    /// <summary>
    /// The display name of a hand item (the drink or object an avatar holds).
    /// </summary>
    /// <param name="id">The hand-item id, as reported by <see cref="OnAvatarHandItemChanged"/>.</param>
    /// <returns>The localised name, or an empty string when it cannot be resolved.</returns>
    public string HandItemName(int id) => Game.GameData.Texts?.HandItemName(id) ?? "";
}
