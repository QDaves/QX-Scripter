using Qx;

namespace Qx.Protocol;

internal static class ProtocolClients
{
    public const ClientType Flash = ClientType.Flash;
    public const ClientType Unity = ClientType.Unity;

    public static IReadOnlyList<ClientType> Supported { get; } = [Unity, Flash];
}
