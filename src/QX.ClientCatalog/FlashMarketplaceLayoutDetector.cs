using Qx.Headers.Flash;
using Qx.Messages;

namespace Qx.ClientCatalog;

internal static class FlashMarketplaceLayoutDetector
{
    static readonly string[] LegacySearch = ["int", "int", "String", "int"];
    static readonly string[] ModernSearch = ["int", "int", "String", "int", "Boolean"];
    static readonly string[] LegacyStats = ["int", "int"];
    static readonly string[] ModernStats = ["int", "int", "String"];
    static readonly string[] LegacyOwnOffers = [];
    static readonly string[] ModernOwnOffers = ["int"];

    public static FlashMarketplaceWireLayout Detect(FlashHeaderMap messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        FlashMarketplaceWireLayout[] layouts =
        [
            Classify(messages, "GetMarketplaceOffers", LegacySearch, ModernSearch),
            Classify(messages, "GetMarketplaceItemStats", LegacyStats, ModernStats),
            Classify(messages, "GetMarketplaceOwnOffers", LegacyOwnOffers, ModernOwnOffers)
        ];
        if (layouts.Any(layout => layout is FlashMarketplaceWireLayout.Unknown))
            return FlashMarketplaceWireLayout.Unknown;
        FlashMarketplaceWireLayout layout = layouts[0];
        return layouts.All(candidate => candidate == layout)
            ? layout
            : FlashMarketplaceWireLayout.Unknown;
    }

    static FlashMarketplaceWireLayout Classify(
        FlashHeaderMap messages,
        string name,
        IReadOnlyList<string> legacy,
        IReadOnlyList<string> modern)
    {
        FlashHeaderDefinition[] matches = messages.Outgoing
            .Where(message => HasName(message, name))
            .ToArray();
        if (matches.Length != 1 || !matches[0].ConstructorSignatureResolved)
            return FlashMarketplaceWireLayout.Unknown;
        string[] normalized = matches[0].ConstructorParameterTypes
            .Select(LocalType)
            .ToArray();
        if (normalized.SequenceEqual(legacy, StringComparer.OrdinalIgnoreCase))
            return FlashMarketplaceWireLayout.Legacy;
        if (normalized.SequenceEqual(modern, StringComparer.OrdinalIgnoreCase))
            return FlashMarketplaceWireLayout.Modern;
        return FlashMarketplaceWireLayout.Unknown;
    }

    static bool HasName(FlashHeaderDefinition message, string expected) =>
        expected.Equals(message.Name, StringComparison.OrdinalIgnoreCase) ||
        message.SemanticAliases.Contains(expected, StringComparer.OrdinalIgnoreCase);

    static string LocalType(string value)
    {
        int separator = Math.Max(value.LastIndexOf('.'), value.LastIndexOf(':'));
        return separator < 0 ? value : value[(separator + 1)..];
    }
}
