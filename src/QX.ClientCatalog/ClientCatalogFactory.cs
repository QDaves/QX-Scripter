using Qx;
using Qx.Messages;
using Qx.Protocol;
using Qx.Headers.Flash;
using Qx.Unity;

namespace Qx.ClientCatalog;

public static class ClientCatalogFactory
{
    const long UnityWireFamilyRelease = 2415;

    public static MessageCatalog Create(PreparedHeaderCatalog prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        Validate(prepared);
        var catalog = new MessageCatalog();
        foreach (HeaderCatalogEntry entry in prepared.Catalog.Entries)
        {
            string? name = entry.Name ?? entry.Aliases.FirstOrDefault();
            if (name is null)
                continue;
            Add(catalog, entry.Direction, entry.HeaderId, [name, .. entry.Aliases]);
        }
        if (catalog.HeaderCount == 0)
            throw new InvalidDataException("The prepared header catalog contains no named headers.");
        catalog.SetBuildFingerprint(prepared.Key.SourceSha256);
        ApplyFlashCompatibilityProfile(catalog, prepared);
        ApplyUnityCompatibilityProfile(catalog, prepared.Key.Client, prepared.Candidate.Version);
        return catalog;
    }

    static void Validate(PreparedHeaderCatalog prepared)
    {
        ClientType client = ClientCatalogClients.FromFamily(prepared.Candidate.Family);
        var provenance = new HeaderCatalogProvenance(
            prepared.Candidate.Version,
            prepared.Candidate.Source,
            prepared.Candidate.ContentRevision);
        StringComparer paths = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        bool source_matches = prepared.Candidate.Files
            .Select(Path.GetFullPath)
            .Contains(prepared.SourcePath, paths);
        if (prepared.Key.Client != client ||
            prepared.Key.Provenance != provenance ||
            prepared.Catalog.Provenance != prepared.Key.Provenance ||
            !paths.Equals(prepared.NormalizedPath, Path.GetFullPath(prepared.Candidate.Path)) ||
            !source_matches)
        {
            throw new InvalidDataException("The prepared header catalog has inconsistent source provenance.");
        }
    }

    public static MessageCatalog CreateUnityReference()
    {
        UnityHeaderNameDatabase names = UnityHeaderNameDatabase.LoadDefault();
        var catalog = new MessageCatalog();
        foreach ((short id, UnityHeaderNames entry) in names.Incoming)
            Add(catalog, Direction.In, id, entry.Name, entry.FlashName);
        foreach ((short id, UnityHeaderNames entry) in names.Outgoing)
            Add(catalog, Direction.Out, id, entry.Name, entry.FlashName);
        return catalog;
    }

    public static MessageCatalog Create(UnityMessageMap messages) => Create(messages, null);

    public static MessageCatalog Create(UnityMessageMap messages, string? client_version)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var catalog = new MessageCatalog();
        foreach (UnityHeaderDefinition message in messages.Incoming)
            Add(catalog, Direction.In, message.Id, message.Name, message.FlashName, message.SourceName);
        foreach (UnityHeaderDefinition message in messages.Outgoing)
            Add(catalog, Direction.Out, message.Id, message.Name, message.FlashName, message.SourceName);
        catalog.SetBuildFingerprint(messages.MetadataSha256);
        ApplyUnityCompatibilityProfile(catalog, ClientCatalogClients.Unity, client_version);
        return catalog;
    }

    public static MessageCatalog Create(FlashHeaderMap messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var catalog = new MessageCatalog();
        foreach (FlashHeaderDefinition message in messages.Incoming)
            Add(catalog, Direction.In, message.Id, message.Name, message.Class, message.Qualified);
        foreach (FlashHeaderDefinition message in messages.Outgoing)
            Add(catalog, Direction.Out, message.Id, message.Name, message.Class, message.Qualified);
        if (messages.SourceSha256.Length != 0)
            catalog.SetBuildFingerprint(messages.SourceSha256);
        catalog.SetWireProfile(new MessageWireProfile(
            MessageWiredContextLayout.Unknown,
            null,
            FlashMarketplaceLayout: FlashMarketplaceLayoutDetector.Detect(messages)));
        return catalog;
    }

    static void Add(MessageCatalog catalog, Direction direction, int id, params string?[] names)
    {
        string[] aliases = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (aliases.Length == 0)
            return;
        catalog.Add(direction, id, aliases[0]);
        foreach (string alias in aliases.Skip(1))
            catalog.AddAlias(direction, id, alias);
    }

    static void ApplyUnityCompatibilityProfile(
        MessageCatalog catalog,
        ClientType client,
        string? client_version)
    {
        if (client != ClientCatalogClients.Unity ||
            !long.TryParse(client_version, out long release) ||
            release < UnityWireFamilyRelease)
        {
            return;
        }

        short? marketplace_header = catalog.TryGetId(
            Direction.Out,
            "MarketplaceBuyOffer",
            out short header)
                ? header
                : null;
        catalog.SetWireProfile(new MessageWireProfile(
            MessageWiredContextLayout.Full,
            true,
            UnityAvatarStatusHasTargetId: true,
            UnityUpdateAvatarHasBadgeRank: true,
            UnityInventoryItemHasExtendedMetadata: true,
            UnityGuestRoomResultHasExtendedData: true,
            UnityCraftingProductHasProductCode: true,
            UnityMarketplaceBuyLayout: marketplace_header is null
                ? MarketplaceBuyWireLayout.Unknown
                : MarketplaceBuyWireLayout.OfferId,
            UnityMarketplaceBuyHeaderId: marketplace_header,
            UnityConsoleMessageLayout: ConsoleMessageWireLayout.TaggedHabbicon,
            UnityRoomSettingsLayout: UnityRoomSettingsWireLayout.Modern));
    }

    static void ApplyFlashCompatibilityProfile(
        MessageCatalog catalog,
        PreparedHeaderCatalog prepared)
    {
        if (prepared.Key.Client != ClientCatalogClients.Flash)
            return;
        catalog.SetWireProfile(new MessageWireProfile(
            MessageWiredContextLayout.Unknown,
            null,
            FlashMarketplaceLayout: prepared.Catalog.FlashMarketplaceLayout));
    }
}
