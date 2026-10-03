namespace Qx;

/// <summary>Specifies the direction of a message.</summary>
public enum MessageDirection
{
    /// <summary>No direction.</summary>
    None = 0,
    /// <summary>Incoming, from the server to the client.</summary>
    In = 1,
    /// <summary>Outgoing, from the client to the server.</summary>
    Out = 2,
    /// <summary>Both incoming and outgoing.</summary>
    Both = 3
}
