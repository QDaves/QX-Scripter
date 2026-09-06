using System.Diagnostics;
using Qx.Game.Snapshots;
using Qx.Scripting;

namespace Qx.Hosting;

internal static class McpReadPipeline
{
    public static async Task<string> ReadAsync<TSource, TResult>(
        string query,
        bool fetch,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<int, CancellationToken, Task> load,
        Func<QueryEnvelope<TSource>> capture,
        Func<QueryEnvelope<TSource>, QueryEnvelope<TResult>> project)
    {
        try
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (fetch && connected)
            {
                if (timeout_ms <= 0)
                    throw new ArgumentOutOfRangeException(nameof(timeout_ms));

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
                timeout.CancelAfter(timeout_ms);
                var elapsed = Stopwatch.StartNew();
                await await_readiness(timeout.Token).ConfigureAwait(false);
                int remaining_timeout = timeout_ms - (int)Math.Ceiling(elapsed.Elapsed.TotalMilliseconds);
                if (remaining_timeout <= 0)
                    throw new OperationCanceledException(timeout.Token);
                await load(remaining_timeout, timeout.Token).ConfigureAwait(false);
            }
            return QueryJson.Serialize(project(capture()));
        }
        catch (Exception error)
        {
            try
            {
                QueryEnvelope<TResult> current = project(capture());
                return QueryJson.Serialize(current with
                {
                    Error = QueryResults.Describe(error, cancellation_token)
                });
            }
            catch
            {
                return QueryJson.SerializeFailure(query, error, cancellation_token);
            }
        }
    }
}
