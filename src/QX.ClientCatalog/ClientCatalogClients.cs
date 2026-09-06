using Qx.ClientCatalog.InstalledClients;

namespace Qx.ClientCatalog;

internal static class ClientCatalogClients
{
    public const ClientType Flash = ClientType.Flash;
    public const ClientType Unity = ClientType.Unity;

    public static IReadOnlyList<ClientType> Supported { get; } = [Unity, Flash];

    public static ClientType FromFamily(InstalledClientFamily family) => family switch
    {
        InstalledClientFamily.Flash => Flash,
        InstalledClientFamily.Unity => Unity,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };
}
