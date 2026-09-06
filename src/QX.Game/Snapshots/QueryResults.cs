namespace Qx.Game.Snapshots;

public static class QueryResults
{
    public static QueryEnvelope<T> Success<T>(
        string query,
        T data,
        bool ready = true,
        bool loaded = true,
        bool stale = false,
        bool truncated = false,
        IReadOnlyList<string>? pending = null,
        DateTimeOffset? capturedAtUtc = null) =>
        new(
            query,
            Metadata(ready, loaded, stale, truncated, pending, capturedAtUtc),
            data,
            null);

    public static QueryEnvelope<T> Failure<T>(
        string query,
        Exception error,
        CancellationToken cancellationToken = default,
        DateTimeOffset? capturedAtUtc = null) =>
        new(
            query,
            Metadata(false, false, false, false, [], capturedAtUtc),
            default,
            Describe(error, cancellationToken));

    public static QueryMetadataSnapshot Metadata(
        bool ready,
        bool loaded,
        bool stale,
        bool truncated,
        IReadOnlyList<string>? pending = null,
        DateTimeOffset? capturedAtUtc = null) =>
        new(
            ready,
            loaded,
            stale,
            truncated,
            capturedAtUtc ?? DateTimeOffset.UtcNow,
            pending?.ToArray() ?? []);

    public static QueryErrorSnapshot Describe(Exception error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        Exception source = error is AggregateException aggregate
            ? aggregate.GetBaseException()
            : error;
        string code = source switch
        {
            OperationCanceledException when cancellationToken.IsCancellationRequested => "cancelled",
            OperationCanceledException => "timeout",
            RequestTimeoutException => "timeout",
            RequestDisconnectedException => "disconnected",
            ResponseParseException => "invalid_response",
            ResponseMatchException => "correlation_error",
            FragmentedLoadCorrelationException => "correlation_error",
            TimeoutException => "timeout",
            UnsupportedClientException => "unsupported_client",
            NotSupportedException => "unsupported",
            KeyNotFoundException => "not_found",
            ArgumentException => "invalid_request",
            InvalidDataException => "invalid_response",
            IOException => "connection_error",
            InvalidOperationException => "unavailable",
            _ => "request_failed"
        };

        string? outgoing_name = source switch
        {
            RequestTimeoutException timeout => timeout.OutgoingName,
            RequestDisconnectedException disconnected => disconnected.OutgoingName,
            _ => null
        };
        string? incoming_name = source switch
        {
            RequestTimeoutException timeout => timeout.IncomingName,
            RequestDisconnectedException disconnected => disconnected.IncomingName,
            ResponseParseException parse => parse.IncomingName,
            ResponseMatchException match => match.IncomingName,
            _ => null
        };
        string? response_type = source switch
        {
            ResponseParseException parse => parse.ResponseType,
            ResponseMatchException match => match.ResponseType,
            _ => null
        };

        return new QueryErrorSnapshot(
            code,
            source.GetType().FullName ?? source.GetType().Name,
            source.Message,
            outgoing_name,
            incoming_name,
            response_type,
            source is RequestTimeoutException timeout_error ? timeout_error.TimeoutMs : null,
            source is FragmentedLoadCorrelationException correlation_error
                ? correlation_error.ResourceName
                : null,
            source is FragmentedLoadCorrelationException retired_error
                ? retired_error.RetiredRequestEpoch
                : null,
            source is FragmentedLoadCorrelationException active_error
                ? active_error.ActiveRequestEpoch
                : null);
    }
}
