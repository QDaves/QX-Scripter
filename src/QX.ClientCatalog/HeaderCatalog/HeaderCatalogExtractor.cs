using Qx.ClientCatalog.InstalledClients;
using Qx.Headers.Flash;
using Qx.Messages;

namespace Qx.ClientCatalog;

internal sealed record HeaderCatalogExtractionTarget(
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
        HeaderCatalogExtractionTarget target,
        HeaderCatalogProvenance provenance,
        CancellationToken cancellation_token);
}

internal sealed class HeaderCatalogExtractor : IHeaderCatalogExtractor
{
    const string FlashExtractorRevision = "flash-fast-header-v7";

    readonly SignatureDatabase _flash_names;

    public HeaderCatalogExtractor()
    {
        _flash_names = SignatureDatabase.LoadDefault();
    }

    public HeaderCatalogExtractionTarget Resolve(InstalledClientCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new HeaderCatalogExtractionTarget(
            FlashSource(candidate),
            _flash_names.CatalogSha256,
            FlashExtractorRevision);
    }

    public Task<HeaderCatalogExtractionResult> ExtractAsync(
        HeaderCatalogExtractionTarget target,
        HeaderCatalogProvenance provenance,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(provenance);
        return Task.Run(
            () => ExtractFlash(target.SourcePath, provenance, cancellation_token),
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
                messages.Incoming.Select(message => FlashEntry(MessageDirection.In, message))
                    .Concat(messages.Outgoing.Select(message => FlashEntry(MessageDirection.Out, message))),
                messages.BuildIds,
                marketplace_layout),
            messages.SourceSha256);
    }

    static HeaderCatalogEntry FlashEntry(MessageDirection direction, FlashHeaderDefinition message)
    {
        if ((uint)message.Id > ushort.MaxValue)
            throw new InvalidDataException($"Flash header ID {message.Id} is outside the wire range.");
        return Entry(
            direction,
            checked((ushort)message.Id),
            [message.Name, .. message.SemanticAliases, message.Class, message.Qualified]);
    }

    static HeaderCatalogEntry Entry(
        MessageDirection direction,
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

    static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
