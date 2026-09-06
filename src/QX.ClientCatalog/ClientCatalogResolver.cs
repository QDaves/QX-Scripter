using Qx;
using Qx.Protocol;
using Qx.Headers.Flash;
using Qx.Unity;

namespace Qx.ClientCatalog;

public sealed record ClientCatalogResolution(
    ClientType Client,
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

    public ClientCatalogResolver(HttpClient http, string? launcher_data = null, string? cache_root = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _launcher_data = launcher_data;
        _cache_root = cache_root;
    }

    internal HttpClient Http => _http;
    internal string? LauncherData => _launcher_data;
    internal string? CacheRoot => _cache_root;

    public async Task<ClientCatalogResolution?> ResolveHeadersAsync(
        ClientType client,
        bool installed_only = false,
        CancellationToken cancellation_token = default) => client switch
    {
        ClientCatalogClients.Unity => await ResolveUnityAsync(installed_only, cancellation_token).ConfigureAwait(false),
        ClientCatalogClients.Flash => await ResolveFlashAsync(installed_only, cancellation_token).ConfigureAwait(false),
        _ => null
    };

    async Task<ClientCatalogResolution?> ResolveUnityAsync(
        bool installed_only,
        CancellationToken cancellation_token)
    {
        var resolver = new HabboUnityClientResolver(_http, _launcher_data, UnityCache());
        HabboUnityRelease? release = installed_only
            ? resolver.FindInstalled()
            : await resolver.ResolveLatestAsync(cancellation_token: cancellation_token).ConfigureAwait(false);
        if (release is null)
            return null;

        UnityMessageMap messages = await Task.Run(
            () => new UnityHeaderExtractor().ExtractMetadata(release.Client.MetadataPath),
            cancellation_token).ConfigureAwait(false);
        MessageCatalog catalog = ClientCatalogFactory.Create(messages, release.Version);
        return new ClientCatalogResolution(
            ClientCatalogClients.Unity,
            release.Version,
            catalog,
            release.Source.ToString(),
            release.IsCurrent);
    }

    async Task<ClientCatalogResolution?> ResolveFlashAsync(
        bool installed_only,
        CancellationToken cancellation_token)
    {
        var resolver = new HabboAirClientResolver(_http, _launcher_data, FlashCache());
        HabboAirRelease? release = installed_only
            ? resolver.FindInstalled()
            : await resolver.ResolveLatestAsync(cancellation_token: cancellation_token).ConfigureAwait(false);
        if (release is null)
            return null;

        MessageCatalog catalog = await Task.Run(() =>
        {
            using FlashHeaderMap extracted = FlashHeaderExtractor.Extract(
                release.SwfPath,
                SignatureDatabase.LoadDefault());
            return ClientCatalogFactory.Create(extracted);
        }, cancellation_token).ConfigureAwait(false);
        return new ClientCatalogResolution(
            ClientCatalogClients.Flash,
            release.Version,
            catalog,
            release.Source.ToString(),
            release.IsCurrent);
    }

    string? UnityCache() => _cache_root is null ? null : Path.Combine(_cache_root, "unity");
    string? FlashCache() => _cache_root is null ? null : Path.Combine(_cache_root, "swf");
}
