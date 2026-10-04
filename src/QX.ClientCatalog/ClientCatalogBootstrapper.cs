using Qx.Protocol;

namespace Qx.ClientCatalog;

public sealed record ClientCatalogLoadResult(
    ClientCatalogResolution? Resolution,
    Exception? Error)
{
    public bool Loaded => Resolution is not null;
}

public static class ClientCatalogBootstrapper
{
    public static async Task<ClientCatalogLoadResult> LoadInstalledAsync(
        MessageManager messages,
        ClientCatalogResolver resolver,
        Action<ClientCatalogResolution>? loaded = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        try
        {
            ClientCatalogResolution? resolution = await resolver
                .ResolveHeadersAsync(true, cancellationToken)
                .ConfigureAwait(false);
            if (resolution is not null)
            {
                messages.LoadVerifiedFallbackCatalog(resolution.Catalog);
                loaded?.Invoke(resolution);
            }
            return new ClientCatalogLoadResult(resolution, null);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new ClientCatalogLoadResult(null, error);
        }
    }
}
