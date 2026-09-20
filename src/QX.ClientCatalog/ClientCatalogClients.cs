using Qx.ClientCatalog.InstalledClients;

namespace Qx.ClientCatalog;

internal static class ClientCatalogClients
{
    public const ClientType Flash = ClientType.Flash;

    public static IReadOnlyList<ClientType> Supported { get; } = [Flash];

    public static ClientType FromFamily(InstalledClientFamily family) => family switch
    {
        InstalledClientFamily.Flash => Flash,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };
}
