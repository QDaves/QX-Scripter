using Qx;
using Qx.Protocol;

namespace Qx.ClientCatalog;

public sealed record ClientCatalogLoadResult(
    ClientType Client,
    ClientCatalogResolution? Resolution,
    Exception? Error)
{
    public bool Loaded => Resolution is not null;
}

public static class ClientCatalogBootstrapper
{
    public static void LoadEmbeddedReferences(MessageManager messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        messages.LoadFallbackCatalog(ClientCatalogClients.Unity, ClientCatalogFactory.CreateUnityReference());
    }

    public static async Task<IReadOnlyList<ClientCatalogLoadResult>> LoadInstalledAsync(
        MessageManager messages,
        ClientCatalogResolver resolver,
        Action<ClientCatalogResolution>? loaded = null,
        CancellationToken cancellation_token = default)
    {
        LoadEmbeddedReferences(messages);
        Task<ClientCatalogLoadResult>[] tasks = ClientCatalogClients.Supported
            .Select(client => LoadAsync(messages, resolver, client, loaded, cancellation_token))
            .ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    static async Task<ClientCatalogLoadResult> LoadAsync(
        MessageManager messages,
        ClientCatalogResolver resolver,
        ClientType client,
        Action<ClientCatalogResolution>? loaded,
        CancellationToken cancellation_token)
    {
        try
        {
            ClientCatalogResolution? resolution = await resolver
                .ResolveHeadersAsync(client, true, cancellation_token)
                .ConfigureAwait(false);
            if (resolution is not null)
            {
                messages.LoadVerifiedFallbackCatalog(client, resolution.Catalog);
                loaded?.Invoke(resolution);
            }
            return new ClientCatalogLoadResult(client, resolution, null);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new ClientCatalogLoadResult(client, null, error);
        }
    }
}
