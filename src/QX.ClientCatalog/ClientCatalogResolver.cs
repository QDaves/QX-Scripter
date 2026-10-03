using Qx.Protocol;
using Qx.Headers.Flash;

namespace Qx.ClientCatalog;

public sealed record ClientCatalogResolution(
    string Version,
    MessageCatalog Catalog,
    string Source,
    bool IsCurrent,
    Exception? SchemaError = null);

public sealed class ClientCatalogResolver
{
    readonly HttpClient _http;
    readonly string? _launcher_data;
    readonly string? _cache_root;

    public ClientCatalogResolver(HttpClient http, string? launcherData = null, string? cacheRoot = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _launcher_data = launcherData;
        _cache_root = cacheRoot;
    }

    internal HttpClient Http => _http;
    internal string? LauncherData => _launcher_data;
    internal string? CacheRoot => _cache_root;

    public async Task<ClientCatalogResolution?> ResolveHeadersAsync(
        bool installedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var resolver = new HabboAirClientResolver(_http, _launcher_data, FlashCache());
        HabboAirRelease? release = installedOnly
            ? resolver.FindInstalled()
            : await resolver.ResolveLatestAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (release is null)
            return null;

        MessageCatalog catalog = await Task.Run(() =>
        {
            using FlashHeaderMap extracted = FlashHeaderExtractor.Extract(
                release.SwfPath,
                SignatureDatabase.LoadDefault());
            return ClientCatalogFactory.Create(extracted);
        }, cancellationToken).ConfigureAwait(false);
        return new ClientCatalogResolution(
            release.Version,
            catalog,
            release.Source.ToString(),
            release.IsCurrent);
    }
    string? FlashCache() => _cache_root is null ? null : Path.Combine(_cache_root, "swf");
}
