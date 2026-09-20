using Qx;

namespace Qx.Protocol;

internal static class ProtocolClients
{
    public const ClientType Flash = ClientType.Flash;

    public static IReadOnlyList<ClientType> Supported { get; } = [Flash];
}
