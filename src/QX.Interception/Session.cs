using Qx;

namespace Qx.Interception;

public sealed record Session
{
    private ClientType _client;

    public Session(
        string host,
        int port,
        string hotel_version,
        string client_identifier,
        ClientType client)
    {
        Host = host;
        Port = port;
        HotelVersion = hotel_version;
        ClientIdentifier = client_identifier;
        Client = client;
    }

    public string Host { get; init; }
    public int Port { get; init; }
    public string HotelVersion { get; init; }
    public string ClientIdentifier { get; init; }
    public ClientType Client
    {
        get => _client;
        init
        {
            if (!ClientTypes.IsSupported(value))
                throw new UnsupportedClientException(value);
            _client = value;
        }
    }

    public void Deconstruct(
        out string host,
        out int port,
        out string hotel_version,
        out string client_identifier,
        out ClientType client)
    {
        host = Host;
        port = Port;
        hotel_version = HotelVersion;
        client_identifier = ClientIdentifier;
        client = Client;
    }
}
