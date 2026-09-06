using Qx.Messages;

namespace Qx.Interception;

public interface IConnection
{
    bool IsConnected { get; }
    Session? Session { get; }

    event Action<Session>? Connected;
    event Action? Disconnected;

    void Send(IPacket packet);

    void Send(IPacket packet, Session? expected_session)
    {
        if (!ReferenceEquals(Session, expected_session))
        {
            throw new InvalidOperationException(
                "The connection session changed before the packet could be sent.");
        }
        Send(packet);
    }
}
