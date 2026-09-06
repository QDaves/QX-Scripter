namespace Qx.Game;

public sealed class RequestTimeoutException(
    string outgoing_name,
    string incoming_name,
    int timeout_ms)
    : TimeoutException(
        $"Request '{outgoing_name}' timed out after {timeout_ms} ms while waiting for '{incoming_name}'.")
{
    public string OutgoingName { get; } = outgoing_name;
    public string IncomingName { get; } = incoming_name;
    public int TimeoutMs { get; } = timeout_ms;
}

public sealed class RequestDisconnectedException(string outgoing_name, string incoming_name)
    : InvalidOperationException(
        $"The connection closed while request '{outgoing_name}' was waiting for '{incoming_name}'.")
{
    public string OutgoingName { get; } = outgoing_name;
    public string IncomingName { get; } = incoming_name;
}

public sealed class ResponseParseException(
    string incoming_name,
    string response_type,
    string detail,
    Exception? inner_exception = null)
    : Exception(
        $"Response '{incoming_name}' could not be parsed as '{response_type}': {detail}",
        inner_exception)
{
    public string IncomingName { get; } = incoming_name;
    public string ResponseType { get; } = response_type;
}

public sealed class ResponseMatchException(
    string incoming_name,
    string response_type,
    Exception inner_exception)
    : InvalidOperationException(
        $"The correlation predicate for response '{incoming_name}' and model '{response_type}' failed.",
        inner_exception)
{
    public string IncomingName { get; } = incoming_name;
    public string ResponseType { get; } = response_type;
}

public sealed class FragmentedLoadCorrelationException(
    string resource_name,
    long retired_request_epoch,
    long active_request_epoch)
    : InvalidOperationException(
        $"The '{resource_name}' baseline cannot be correlated after request epoch {retired_request_epoch} expired. Request epoch {active_request_epoch} was not completed; wait for the successor baseline or reconnect.")
{
    public string ResourceName { get; } = resource_name;
    public long RetiredRequestEpoch { get; } = retired_request_epoch;
    public long ActiveRequestEpoch { get; } = active_request_epoch;
}
