using Qx.Game;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// The hotel's configuration file. Holds the feature switches, limits and prices the client
    /// reads at start-up, none of which ever appear on the wire.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> until the game data has downloaded. The typed helpers on this class
    /// tolerate that; direct use does not.
    /// </remarks>
    public ExternalVariables? Variables => Game.GameData.Variables;

    /// <summary>
    /// A configuration value, resolved the way the client resolves it.
    /// </summary>
    /// <param name="key">A key such as <c>wired.timezones</c>.</param>
    /// <returns>The value, or an empty string when unset or not downloaded yet.</returns>
    public string Config(string key) => Variables?.Get(key) ?? "";

    /// <summary>
    /// A configuration switch, such as <c>wired.menu.enabled</c> or <c>catalog.pets.enabled</c>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <returns><see langword="false"/> when unset or not downloaded yet.</returns>
    public bool ConfigFlag(string key) => Variables?.Flag(key) ?? false;

    /// <summary>
    /// A numeric configuration value, such as <c>marketplace.bulkOfferLimit</c>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <param name="fallback">Used when the key is unset or the data has not downloaded.</param>
    public int ConfigNumber(string key, int fallback = 0) => Variables?.Number(key, fallback) ?? fallback;

    /// <summary>
    /// A comma separated configuration value split into its entries, such as
    /// <c>wired.timezones</c>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    public IReadOnlyList<string> ConfigList(string key) => Variables?.List(key) ?? [];

    /// <summary>
    /// Every configuration key starting with a prefix, with the values resolved.
    /// </summary>
    /// <remarks>
    /// Useful for surveying an area of the configuration, for example <c>ConfigGroup("wired.")</c>
    /// to see every wired limit the hotel currently publishes.
    /// </remarks>
    /// <param name="prefix">The key prefix, matched case sensitively as the hotel writes it.</param>
    public IReadOnlyDictionary<string, string> ConfigGroup(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (Variables is not { } variables)
            return new Dictionary<string, string>();

        var matches = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in variables.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                matches[key] = variables.Get(key);
        }
        return matches;
    }

    /// <summary>Whether the hotel currently has the wired menu switched on.</summary>
    public bool WiredEnabled => ConfigFlag("wired.menu.enabled");

    /// <summary>The time zones the wired timer effects accept.</summary>
    public IReadOnlyList<string> WiredTimezones => ConfigList("wired.timezones");

    /// <summary>
    /// The number of log entries a wired chest keeps before the oldest are dropped.
    /// </summary>
    public int WiredChestMaxLogs => ConfigNumber("wired.chests_max_logs");

    /// <summary>
    /// How much a wired chest holds after a given number of capacity upgrades.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>WiredChestUpgradeConfirmationView</c>: capacity is the initial size plus one
    /// upgrade step per purchased upgrade. Both figures come from the hotel configuration, so a
    /// hotel that retunes them changes the answer without any client change.
    /// </remarks>
    /// <param name="coins">
    /// <see langword="true"/> for a coin chest, <see langword="false"/> for a furni chest.
    /// </param>
    /// <param name="upgrades">How many capacity upgrades the chest has had.</param>
    public int WiredChestCapacity(bool coins, int upgrades = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upgrades);
        string prefix = coins ? "wired.coins_chest." : "wired.furni_chest.";
        return ConfigNumber(prefix + "initial_capacity") + upgrades * ConfigNumber(prefix + "upgrade_capacity");
    }

    /// <summary>The highest number of capacity upgrades a wired chest accepts.</summary>
    /// <param name="coins">
    /// <see langword="true"/> for a coin chest, <see langword="false"/> for a furni chest.
    /// </param>
    public int WiredChestMaxUpgrades(bool coins) =>
        ConfigNumber(coins ? "wired.coins_chest.max_upgrades" : "wired.furni_chest.max_upgrades");

    /// <summary>
    /// What a run of wired chest capacity upgrades costs, in credits and in diamonds.
    /// </summary>
    /// <remarks>
    /// The client defaults both prices to 999 when the hotel has not published them, so an
    /// unconfigured hotel reads as prohibitively expensive rather than free. A price of zero means
    /// that currency is not charged at all, which is how the client decides whether to show it.
    /// </remarks>
    /// <param name="upgrades">How many upgrades to buy at once.</param>
    public (int Credits, int Diamonds) WiredChestUpgradeCost(int upgrades = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upgrades);
        return (ConfigNumber("wired.chests.upgrade_cost_credits", 999) * upgrades,
            ConfigNumber("wired.chests.upgrade_cost_diamonds", 999) * upgrades);
    }

    /// <summary>
    /// Whether a furni is a starter wired chest, which is the small variant that cannot be
    /// upgraded to the full capacity range.
    /// </summary>
    /// <remarks>
    /// The client decides this from the furni's class identifier containing a configured infix, and
    /// treats an empty infix as "no starter chests exist" rather than as a match on everything.
    /// </remarks>
    /// <param name="furniClassName">The furni class identifier, for example <c>wired_chest_starter</c>.</param>
    public bool IsStarterWiredChest(string furniClassName)
    {
        ArgumentNullException.ThrowIfNull(furniClassName);
        string infix = Config("wired.chests_starter_infix");
        return infix.Length > 0 && furniClassName.Contains(infix, StringComparison.Ordinal);
    }

    /// <summary>The largest number of marketplace offers the hotel returns in one page.</summary>
    public int MarketplaceBulkOfferLimit => ConfigNumber("marketplace.bulkOfferLimit");
}
