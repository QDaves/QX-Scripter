namespace Qx.Game;

/// <summary>
/// Thrown when a request receives no matching response within its timeout.
/// </summary>
/// <param name="outgoingName">The name of the outgoing message that was sent.</param>
/// <param name="incomingName">The name of the incoming message that was awaited.</param>
/// <param name="timeoutMs">The timeout that elapsed, in milliseconds.</param>
public sealed class RequestTimeoutException(
    string outgoingName,
    string incomingName,
    int timeoutMs)
    : TimeoutException(
        $"Request '{outgoingName}' timed out after {timeoutMs} ms while waiting for '{incomingName}'.")
{
    /// <summary>Gets the name of the outgoing message that was sent.</summary>
    public string OutgoingName { get; } = outgoingName;
    /// <summary>Gets the name of the incoming message that was awaited.</summary>
    public string IncomingName { get; } = incomingName;
    /// <summary>Gets the timeout that elapsed, in milliseconds.</summary>
    public int TimeoutMs { get; } = timeoutMs;
}

/// <summary>
/// Thrown when the hotel connection is unavailable or closes before a request receives its response.
/// </summary>
/// <remarks>
/// Also thrown when the hotel session changes while a request is waiting.
/// </remarks>
/// <param name="outgoingName">The name of the outgoing message of the request.</param>
/// <param name="incomingName">The name of the incoming message that was awaited.</param>
public sealed class RequestDisconnectedException(string outgoingName, string incomingName)
    : InvalidOperationException(
        $"The connection closed while request '{outgoingName}' was waiting for '{incomingName}'.")
{
    /// <summary>Gets the name of the outgoing message of the request.</summary>
    public string OutgoingName { get; } = outgoingName;
    /// <summary>Gets the name of the incoming message that was awaited.</summary>
    public string IncomingName { get; } = incomingName;
}

/// <summary>
/// Thrown when a response message cannot be parsed as the expected model.
/// </summary>
/// <remarks>
/// A message that leaves unread bytes after parsing is treated as a parse failure.
/// </remarks>
/// <param name="incomingName">The name of the incoming message.</param>
/// <param name="responseType">The name of the model type the message was parsed as.</param>
/// <param name="detail">A description of the failure.</param>
/// <param name="innerException">The exception that caused the failure, or <see langword="null"/>.</param>
public sealed class ResponseParseException(
    string incomingName,
    string responseType,
    string detail,
    Exception? innerException = null)
    : Exception(
        $"Response '{incomingName}' could not be parsed as '{responseType}': {detail}",
        innerException)
{
    /// <summary>Gets the name of the incoming message.</summary>
    public string IncomingName { get; } = incomingName;
    /// <summary>Gets the name of the model type the message was parsed as.</summary>
    public string ResponseType { get; } = responseType;
}

/// <summary>
/// Thrown when the predicate that matches a response to its request throws an exception.
/// </summary>
/// <param name="incomingName">The name of the incoming message.</param>
/// <param name="responseType">The name of the model type the message was parsed as.</param>
/// <param name="innerException">The exception thrown by the predicate.</param>
public sealed class ResponseMatchException(
    string incomingName,
    string responseType,
    Exception innerException)
    : InvalidOperationException(
        $"The correlation predicate for response '{incomingName}' and model '{responseType}' failed.",
        innerException)
{
    /// <summary>Gets the name of the incoming message.</summary>
    public string IncomingName { get; } = incomingName;
    /// <summary>Gets the name of the model type the message was parsed as.</summary>
    public string ResponseType { get; } = responseType;
}

/// <summary>
/// Thrown when the fragments of a multi-part load can no longer be matched to the request that started it.
/// </summary>
/// <remarks>
/// Raised by fragmented loads such as the inventory, the badge inventory and the friend list after the
/// request that owned a load expired before all fragments arrived. The data recovers when a later
/// complete load arrives or after reconnecting.
/// </remarks>
/// <param name="resourceName">The name of the resource being loaded, such as <c>inventory</c>.</param>
/// <param name="retiredRequestEpoch">The epoch of the request that expired.</param>
/// <param name="activeRequestEpoch">The epoch of the request that was active and not completed.</param>
public sealed class FragmentedLoadCorrelationException(
    string resourceName,
    long retiredRequestEpoch,
    long activeRequestEpoch)
    : InvalidOperationException(
        $"The '{resourceName}' baseline cannot be correlated after request epoch {retiredRequestEpoch} expired. Request epoch {activeRequestEpoch} was not completed; wait for the successor baseline or reconnect.")
{
    /// <summary>Gets the name of the resource being loaded.</summary>
    public string ResourceName { get; } = resourceName;
    /// <summary>Gets the epoch of the request that expired.</summary>
    public long RetiredRequestEpoch { get; } = retiredRequestEpoch;
    /// <summary>Gets the epoch of the request that was active and not completed.</summary>
    public long ActiveRequestEpoch { get; } = activeRequestEpoch;
}
