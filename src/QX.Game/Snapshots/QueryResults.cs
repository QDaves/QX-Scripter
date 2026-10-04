namespace Qx.Game.Snapshots;

/// <summary>Provides factories for the <see cref="QueryEnvelope{T}"/> that every read query returns.</summary>
public static class QueryResults
{
    /// <summary>Creates a successful envelope around a payload.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="query">The query name.</param>
    /// <param name="data">The payload.</param>
    /// <param name="ready">Whether the subsystem is connected and usable.</param>
    /// <param name="loaded">Whether every part of the answer has arrived.</param>
    /// <param name="stale">Whether the payload was retained from an ended session.</param>
    /// <param name="truncated">Whether items were dropped to stay under a cap.</param>
    /// <param name="pending">The names of the pieces that have not arrived yet, or <see langword="null"/> for none.</param>
    /// <param name="capturedAtUtc">The time the state was read, or <see langword="null"/> for the current UTC time.</param>
    /// <returns>The envelope, with no error.</returns>
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

    /// <summary>Creates a failed envelope that describes an exception.</summary>
    /// <remarks>Every metadata flag is <see langword="false"/> and the payload is <see langword="default"/>.</remarks>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="query">The query name.</param>
    /// <param name="error">The exception that failed the query.</param>
    /// <param name="cancellationToken">The token of the query, used to tell a cancellation from a timeout.</param>
    /// <param name="capturedAtUtc">The time of the failure, or <see langword="null"/> for the current UTC time.</param>
    /// <returns>The envelope, with the error described by <see cref="Describe"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
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

    /// <summary>Creates the load state that accompanies a snapshot.</summary>
    /// <param name="ready">Whether the subsystem is connected and usable.</param>
    /// <param name="loaded">Whether every part of the answer has arrived.</param>
    /// <param name="stale">Whether the payload was retained from an ended session.</param>
    /// <param name="truncated">Whether items were dropped to stay under a cap.</param>
    /// <param name="pending">The names of the pieces that have not arrived yet, or <see langword="null"/> for none.</param>
    /// <param name="capturedAtUtc">The time the state was read, or <see langword="null"/> for the current UTC time.</param>
    /// <returns>The metadata, with a copy of <paramref name="pending"/>.</returns>
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

    /// <summary>Classifies an exception into a stable error code with its request details.</summary>
    /// <remarks>
    /// An <see cref="AggregateException"/> is unwrapped to its base exception first. A cancellation counts
    /// as <c>cancelled</c> only when <paramref name="cancellationToken"/> was canceled; otherwise it counts
    /// as <c>timeout</c>.
    /// </remarks>
    /// <param name="error">The exception to describe.</param>
    /// <param name="cancellationToken">The token of the query, used to tell a cancellation from a timeout.</param>
    /// <returns>The error description.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
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
