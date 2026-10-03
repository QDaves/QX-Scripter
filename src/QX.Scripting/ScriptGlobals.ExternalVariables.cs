using Qx.Game;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the hotel's configuration file, or <see langword="null"/> until the game data has
    /// downloaded.
    /// </summary>
    /// <remarks>
    /// It holds the feature switches, limits and prices the client reads at start-up, none of
    /// which ever appear on the wire. The typed helpers on this class tolerate a missing file;
    /// direct use does not.
    /// </remarks>
    public ExternalVariables? ExternalVariables => Game.GameData.Variables;

    /// <summary>
    /// Gets a configuration value, resolved the way the client resolves it.
    /// </summary>
    /// <remarks>
    /// <c>${...}</c> references to other keys are resolved and URLs are normalized, as
    /// <see cref="ExternalVariables.Get(string)"/> does.
    /// </remarks>
    /// <param name="key">The key to read, such as <c>wired.timezones</c>.</param>
    /// <returns>The value, or an empty string when unset or not downloaded yet.</returns>
    public string Config(string key) => ExternalVariables?.Get(key) ?? "";

    /// <summary>
    /// Gets whether a configuration switch, such as <c>wired.menu.enabled</c> or
    /// <c>catalog.pets.enabled</c>, is on.
    /// </summary>
    /// <remarks>
    /// Only <c>1</c> and <c>true</c> in any casing count as on. The raw entry is read, so
    /// <c>${...}</c> references are not resolved.
    /// </remarks>
    /// <param name="key">The key to read.</param>
    /// <returns>
    /// <see langword="true"/> when the switch is on; <see langword="false"/> when it is off, unset
    /// or not downloaded yet.
    /// </returns>
    public bool ConfigFlag(string key) => ExternalVariables?.Flag(key) ?? false;

    /// <summary>
    /// Gets a numeric configuration value, such as <c>marketplace.bulkOfferLimit</c>.
    /// </summary>
    /// <remarks>
    /// A value that is present but cannot be parsed reads as 0, not as <paramref name="fallback"/>,
    /// because the client does the same. The raw entry is read without resolving <c>${...}</c>
    /// references.
    /// </remarks>
    /// <param name="key">The key to read.</param>
    /// <param name="fallback">The value to use when the key is unset or the data has not downloaded.</param>
    /// <returns>The configured number, 0 when it cannot be parsed, or <paramref name="fallback"/>.</returns>
    public int ConfigNumber(string key, int fallback = 0) => ExternalVariables?.Number(key, fallback) ?? fallback;

    /// <summary>
    /// Gets a comma separated configuration value, such as <c>wired.timezones</c>, split into its
    /// entries.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <returns>
    /// The trimmed, non-empty entries, or an empty list when the key is unset or the data has not
    /// downloaded.
    /// </returns>
    public IReadOnlyList<string> ConfigList(string key) => ExternalVariables?.List(key) ?? [];

    /// <summary>
    /// Gets every configuration key that starts with a prefix, with the values resolved.
    /// </summary>
    /// <remarks>
    /// Useful for surveying an area of the configuration, for example <c>ConfigGroup("wired.")</c>
    /// to see every wired limit the hotel currently publishes.
    /// </remarks>
    /// <param name="prefix">The key prefix, matched case sensitively as the hotel writes it.</param>
    /// <returns>
    /// The matching keys and their resolved values, or an empty dictionary when the data has not
    /// downloaded.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <see langword="null"/>.</exception>
    public IReadOnlyDictionary<string, string> ConfigGroup(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (ExternalVariables is not { } variables)
            return new Dictionary<string, string>();

        var matches = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in variables.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                matches[key] = variables.Get(key);
        }
        return matches;
    }

    /// <summary>Gets whether the hotel currently has the wired menu switched on.</summary>
    /// <remarks>Reads the <c>wired.menu.enabled</c> switch.</remarks>
    public bool WiredEnabled => ConfigFlag("wired.menu.enabled");

    /// <summary>Gets the time zones the wired timer effects accept.</summary>
    /// <remarks>Reads the <c>wired.timezones</c> list; empty until the game data has downloaded.</remarks>
    public IReadOnlyList<string> WiredTimezones => ConfigList("wired.timezones");

    /// <summary>
    /// Gets the number of log entries a wired chest keeps before the oldest are dropped.
    /// </summary>
    /// <remarks>Reads <c>wired.chests_max_logs</c>; 0 when unset or not downloaded yet.</remarks>
    public int WiredChestMaxLogs => ConfigNumber("wired.chests_max_logs");

    /// <summary>
    /// Gets how much a wired chest holds after a given number of capacity upgrades.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>WiredChestUpgradeConfirmationView</c>: capacity is the initial size plus one
    /// upgrade step per purchased upgrade. Both figures come from the hotel configuration, so a
    /// hotel that retunes them changes the answer without any client change. Starter chests are
    /// not considered; use <see cref="ChestCapacityOf(Qx.Model.FloorItem, bool)"/> for a chest in the room.
    /// </remarks>
    /// <param name="coins">
    /// <see langword="true"/> for a coin chest; <see langword="false"/> for a furni chest.
    /// </param>
    /// <param name="upgrades">The number of capacity upgrades the chest has had.</param>
    /// <returns>The capacity, or 0 when the game data has not downloaded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="upgrades"/> is negative.</exception>
    public int WiredChestCapacity(bool coins, int upgrades = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upgrades);
        string prefix = coins ? "wired.coins_chest." : "wired.furni_chest.";
        return ConfigNumber(prefix + "initial_capacity") + upgrades * ConfigNumber(prefix + "upgrade_capacity");
    }

    /// <summary>Gets the highest number of capacity upgrades a wired chest accepts.</summary>
    /// <param name="coins">
    /// <see langword="true"/> for a coin chest; <see langword="false"/> for a furni chest.
    /// </param>
    /// <returns>The configured maximum, or 0 when unset or not downloaded yet.</returns>
    public int WiredChestMaxUpgrades(bool coins) =>
        ConfigNumber(coins ? "wired.coins_chest.max_upgrades" : "wired.furni_chest.max_upgrades");

    /// <summary>
    /// Gets what a run of wired chest capacity upgrades costs, in credits and in diamonds.
    /// </summary>
    /// <remarks>
    /// The client defaults both prices to 999 when the hotel has not published them, so an
    /// unconfigured hotel reads as prohibitively expensive rather than free. A price of zero means
    /// that currency is not charged at all, which is how the client decides whether to show it.
    /// </remarks>
    /// <param name="upgrades">The number of upgrades to buy at once.</param>
    /// <returns>The total credit and diamond price of <paramref name="upgrades"/> upgrades.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="upgrades"/> is negative.</exception>
    public (int Credits, int Diamonds) WiredChestUpgradeCost(int upgrades = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upgrades);
        return (ConfigNumber("wired.chests.upgrade_cost_credits", 999) * upgrades,
            ConfigNumber("wired.chests.upgrade_cost_diamonds", 999) * upgrades);
    }

    /// <summary>
    /// Gets whether a furni is a starter wired chest, the small variant that cannot be upgraded.
    /// </summary>
    /// <remarks>
    /// The client decides this from the furni's class identifier containing the infix configured in
    /// <c>wired.chests_starter_infix</c>, and treats an empty infix as "no starter chests exist"
    /// rather than as a match on everything. The match is case sensitive.
    /// </remarks>
    /// <param name="furniClassName">The furni class identifier, for example <c>wired_chest_starter</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the class identifier contains the configured infix; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="furniClassName"/> is <see langword="null"/>.</exception>
    public bool IsStarterWiredChest(string furniClassName)
    {
        ArgumentNullException.ThrowIfNull(furniClassName);
        string infix = Config("wired.chests_starter_infix");
        return infix.Length > 0 && furniClassName.Contains(infix, StringComparison.Ordinal);
    }

    /// <summary>Gets the largest number of marketplace offers the hotel returns in one page.</summary>
    /// <remarks>Reads <c>marketplace.bulkOfferLimit</c>; 0 when unset or not downloaded yet.</remarks>
    public int MarketplaceBulkOfferLimit => ConfigNumber("marketplace.bulkOfferLimit");
}
