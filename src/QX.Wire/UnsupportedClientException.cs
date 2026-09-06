namespace Qx;

public sealed class UnsupportedClientException(ClientType client)
    : Exception($"This operation is not supported for the {client} client.")
{
    public ClientType Client { get; } = client;
}
