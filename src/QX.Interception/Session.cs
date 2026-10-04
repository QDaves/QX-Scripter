namespace Qx.Interception;

/// <summary>Represents one hotel connection reported by the interceptor.</summary>
/// <remarks>
/// A new instance is created for every connection, so two sessions are equal only when they are the same
/// connection, even when their host, port and build match.
/// </remarks>
public sealed class Session
{
    /// <summary>Initializes a new instance of the <see cref="Session"/> class.</summary>
    /// <param name="host">The hotel server host.</param>
    /// <param name="port">The hotel server port.</param>
    /// <param name="hotelVersion">The client build version reported for the connection.</param>
    /// <param name="clientIdentifier">The client identifier reported for the connection.</param>
    public Session(
        string host,
        int port,
        string hotelVersion,
        string clientIdentifier)
    {
        Host = host;
        Port = port;
        HotelVersion = hotelVersion;
        ClientIdentifier = clientIdentifier;
    }

    /// <summary>Gets the hotel server host.</summary>
    public string Host { get; }
    /// <summary>Gets the hotel server port.</summary>
    public int Port { get; }
    /// <summary>Gets the client build version reported for the connection.</summary>
    public string HotelVersion { get; }
    /// <summary>Gets the client identifier reported for the connection.</summary>
    public string ClientIdentifier { get; }

    /// <summary>Deconstructs the session into its values.</summary>
    /// <param name="host">The hotel server host.</param>
    /// <param name="port">The hotel server port.</param>
    /// <param name="hotelVersion">The client build version.</param>
    /// <param name="clientIdentifier">The client identifier.</param>
    public void Deconstruct(
        out string host,
        out int port,
        out string hotelVersion,
        out string clientIdentifier)
    {
        host = Host;
        port = Port;
        hotelVersion = HotelVersion;
        clientIdentifier = ClientIdentifier;
    }

    /// <summary>Returns the session values in the form <c>Session { Host = …, Port = …, … }</c>.</summary>
    /// <returns>The host, port, hotel version and client identifier of the session.</returns>
    public override string ToString() =>
        $"Session {{ Host = {Host}, Port = {Port}, HotelVersion = {HotelVersion}, ClientIdentifier = {ClientIdentifier} }}";
}
