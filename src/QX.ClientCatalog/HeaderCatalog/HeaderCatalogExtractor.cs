using Qx.ClientCatalog.InstalledClients;
using Qx.Headers.Flash;
using Qx.Messages;
using Qx.Unity;

namespace Qx.ClientCatalog;

internal sealed record HeaderCatalogExtractionTarget(
    ClientType Client,
    string SourcePath,
    string NameDatabaseSha256,
    string ExtractorRevision);

internal sealed record HeaderCatalogExtractionResult(
    HeaderCatalogSnapshot Catalog,
    string SourceSha256);

internal interface IHeaderCatalogExtractor
{
    HeaderCatalogExtractionTarget Resolve(InstalledClientCandidate candidate);

    Task<HeaderCatalogExtractionResult> ExtractAsync(
        InstalledClientCandidate candidate,
        HeaderCatalogExtractionTarget target,
        HeaderCatalogProvenance provenance,
        CancellationToken cancellation_token);
}

internal sealed class HeaderCatalogExtractor : IHeaderCatalogExtractor
{
    const string FlashExtractorRevision = "flash-fast-header-v6";
    const string UnityExtractorRevision = "unity-fast-header-v1";

    readonly SignatureDatabase _flash_names;
    readonly UnityHeaderNameDatabase _unity_names;
    readonly UnityHeaderExtractor _unity_extractor;

    public HeaderCatalogExtractor()
    {
        _flash_names = SignatureDatabase.LoadDefault();
        _unity_names = UnityHeaderNameDatabase.LoadDefault();
        _unity_extractor = new UnityHeaderExtractor(_unity_names);
    }

    public HeaderCatalogExtractionTarget Resolve(InstalledClientCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.Family switch
        {
            InstalledClientFamily.Flash => new HeaderCatalogExtractionTarget(
                ClientCatalogClients.FromFamily(candidate.Family),
                FlashSource(candidate),
                _flash_names.CatalogSha256,
                FlashExtractorRevision),
            InstalledClientFamily.Unity => new HeaderCatalogExtractionTarget(
                ClientCatalogClients.FromFamily(candidate.Family),
                UnitySource(candidate),
                _unity_names.CatalogSha256,
                UnityExtractorRevision),
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };
    }

    public Task<HeaderCatalogExtractionResult> ExtractAsync(
        InstalledClientCandidate candidate,
        HeaderCatalogExtractionTarget target,
        HeaderCatalogProvenance provenance,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(provenance);
        return Task.Run(
            () => candidate.Family switch
            {
                InstalledClientFamily.Flash => ExtractFlash(target.SourcePath, provenance, cancellation_token),
                InstalledClientFamily.Unity => ExtractUnity(target.SourcePath, provenance, cancellation_token),
                _ => throw new ArgumentOutOfRangeException(nameof(candidate))
            },
            cancellation_token);
    }

    HeaderCatalogExtractionResult ExtractFlash(
        string source_path,
        HeaderCatalogProvenance provenance,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        using FlashHeaderMap messages = FlashHeaderExtractor.Extract(source_path, _flash_names);
        cancellation_token.ThrowIfCancellationRequested();
        FlashMarketplaceWireLayout marketplace_layout =
            FlashMarketplaceLayoutDetector.Detect(messages);
        return new HeaderCatalogExtractionResult(
            new HeaderCatalogSnapshot(
                provenance,
                messages.Incoming.Select(message => FlashEntry(Direction.In, message))
                    .Concat(messages.Outgoing.Select(message => FlashEntry(Direction.Out, message))),
                messages.BuildIds,
                marketplace_layout),
            messages.SourceSha256);
    }

    HeaderCatalogExtractionResult ExtractUnity(
        string source_path,
        HeaderCatalogProvenance provenance,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        UnityMessageMap messages = _unity_extractor.ExtractMetadata(source_path);
        cancellation_token.ThrowIfCancellationRequested();
        HeaderCatalogSnapshot catalog = CreateUnitySnapshot(messages, provenance);
        return new HeaderCatalogExtractionResult(catalog, messages.MetadataSha256);
    }

    internal static HeaderCatalogSnapshot CreateUnitySnapshot(
        UnityMessageMap messages,
        HeaderCatalogProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(provenance);
        if (!messages.DirectionsVerified)
            throw new InvalidDataException("Unity protocol header directions were not structurally verified.");
        return new HeaderCatalogSnapshot(
            provenance,
            messages.Incoming.Select(message => UnityEntry(Direction.In, message))
                .Concat(messages.Outgoing.Select(message => UnityEntry(Direction.Out, message))));
    }

    static HeaderCatalogEntry FlashEntry(Direction direction, FlashHeaderDefinition message)
    {
        if ((uint)message.Id > ushort.MaxValue)
            throw new InvalidDataException($"Flash header ID {message.Id} is outside the wire range.");
        return Entry(
            direction,
            checked((ushort)message.Id),
            [message.Name, .. message.SemanticAliases, message.Class, message.Qualified]);
    }

    static HeaderCatalogEntry UnityEntry(Direction direction, UnityHeaderDefinition message) => Entry(
        direction,
        unchecked((ushort)message.Id),
        message.Name,
        message.FlashName,
        message.SourceName);

    static HeaderCatalogEntry Entry(
        Direction direction,
        ushort header_id,
        params string?[] names)
    {
        string[] values = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length == 0)
            throw new InvalidDataException($"Header {direction}:{header_id} has no usable name.");
        return new HeaderCatalogEntry(direction, header_id, values[0], values.Skip(1));
    }

    static string FlashSource(InstalledClientCandidate candidate)
    {
        string[] files = candidate.Files
            .Where(path => string.Equals(Path.GetExtension(path), ".swf", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer())
            .ToArray();
        if (files.Length != 1)
            throw new InvalidDataException("The installed Flash candidate does not identify exactly one SWF source.");
        return files[0];
    }

    static string UnitySource(InstalledClientCandidate candidate)
    {
        string[] files = candidate.Files
            .Where(path => string.Equals(Path.GetFileName(path), "global-metadata.dat", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer())
            .ToArray();
        if (files.Length != 1)
            throw new InvalidDataException("The installed Unity candidate does not identify exactly one metadata source.");
        return files[0];
    }

    static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
