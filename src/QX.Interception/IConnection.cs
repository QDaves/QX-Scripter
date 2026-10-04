using Qx.Messages;

namespace Qx.Interception;

/// <summary>Defines a connection to the hotel that sends packets and reports the session lifecycle.</summary>
public interface IConnection
{
    /// <summary>Gets whether a hotel session is active.</summary>
    bool IsConnected { get; }
    /// <summary>Gets the active hotel session, or <see langword="null"/> when there is none.</summary>
    Session? Session { get; }

    /// <summary>Occurs when a hotel session starts.</summary>
    event Action<Session>? Connected;
    /// <summary>Occurs when the hotel session ends.</summary>
    event Action? Disconnected;

    /// <summary>Sends a packet to the client or the server, depending on the direction of its header.</summary>
    /// <param name="packet">The packet to send.</param>
    void Send(IPacket packet);

    /// <summary>Sends a packet only when <paramref name="expectedSession"/> is still the active session.</summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="expectedSession">The session the packet belongs to.</param>
    /// <exception cref="InvalidOperationException">Thrown when the active session is not <paramref name="expectedSession"/>.</exception>
    void Send(IPacket packet, Session? expectedSession)
    {
        if (!ReferenceEquals(Session, expectedSession))
        {
            throw new InvalidOperationException(
                "The connection session changed before the packet could be sent.");
        }
        Send(packet);
    }
}
