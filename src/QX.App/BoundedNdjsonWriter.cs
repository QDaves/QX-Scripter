using System.Threading.Channels;
using Qx.Hosting;

namespace Qx.App;

internal sealed class BoundedNdjsonWriter : IAsyncDisposable
{
    private const int Open = 0;
    private const int Completing = 1;
    private const int Failed = 2;
    private const int Aborted = 3;
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);
    private readonly TextWriter writer;
    private readonly Channel<object> queue;
    private readonly CancellationTokenSource shutdown = new();
    private readonly object shutdown_gate = new();
    private readonly Task pump;
    private readonly TaskCompletionSource<Exception> failure =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? failure_error;
    private Task shutdown_task = Task.CompletedTask;
    private bool cleanup_started;
    private bool shutdown_started;
    private int state;

    public BoundedNdjsonWriter(
        TextWriter writer,
        int capacity = 512)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.writer = writer;
        queue = Channel.CreateBounded<object>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        pump = PumpAsync();
    }

    public Task<Exception> Failure => failure.Task;
    public Exception? FailureException => Volatile.Read(ref failure_error);

    public bool TryWrite(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (Volatile.Read(ref state) != Open)
            return false;
        if (queue.Writer.TryWrite(message))
            return true;
        if (Volatile.Read(ref state) == Open)
            Fail(new IOException("The NDJSON output queue reached its capacity."));
        return false;
    }

    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        SetFailure(error);
    }

    public void Write(object message)
    {
        if (TryWrite(message))
            return;
        throw new IOException(
            "The NDJSON output stream is unavailable.",
            FailureException);
    }

    public void Abort()
    {
        while (true)
        {
            int current = Volatile.Read(ref state);
            if (current is Failed or Aborted)
                return;
            if (Interlocked.CompareExchange(ref state, Aborted, current) != current)
                continue;
            if (current == Open)
                queue.Writer.TryComplete();
            CancelPump();
            return;
        }
    }

    public Task CompleteAsync() => CompleteAsync(DefaultDrainTimeout);

    public async Task CompleteAsync(TimeSpan drain_timeout)
    {
        if (drain_timeout != Timeout.InfiniteTimeSpan && drain_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(drain_timeout));
        if (Interlocked.CompareExchange(ref state, Completing, Open) == Open)
            queue.Writer.TryComplete();
        int current = Volatile.Read(ref state);
        if (current == Aborted)
        {
            ScheduleCleanup();
            return;
        }
        if (current == Failed)
        {
            ScheduleCleanup();
            throw new IOException("The NDJSON output stream failed.", FailureException);
        }

        try
        {
            if (drain_timeout == Timeout.InfiniteTimeSpan)
                await pump.ConfigureAwait(false);
            else
                await pump.WaitAsync(drain_timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Abort();
            ScheduleCleanup();
            if (FailureException is { } timeout_failure)
                throw new IOException("The NDJSON output stream failed.", timeout_failure);
            throw new IOException(
                $"The NDJSON output stream did not drain within {drain_timeout.TotalSeconds:g} seconds.");
        }
        ScheduleCleanup();
        if (FailureException is { } error)
            throw new IOException("The NDJSON output stream failed.", error);
    }

    public async ValueTask DisposeAsync() => await CompleteAsync().ConfigureAwait(false);

    private async Task PumpAsync()
    {
        try
        {
            await foreach (object message in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                string line = ApplicationJson.Serialize(message, indented: false);
                await writer
                    .WriteLineAsync(line.AsMemory(), shutdown.Token)
                    .ConfigureAwait(false);
                await writer.FlushAsync(shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (
            shutdown.IsCancellationRequested && Volatile.Read(ref state) == Aborted)
        {
        }
        catch (Exception) when (Volatile.Read(ref state) == Aborted)
        {
        }
        catch (Exception error)
        {
            SetFailure(error, force: true);
        }
    }

    private void SetFailure(Exception error, bool force = false)
    {
        int previous;
        if (!force)
        {
            previous = Interlocked.CompareExchange(ref state, Failed, Open);
            if (previous != Open)
                return;
        }
        else
        {
            while (true)
            {
                previous = Volatile.Read(ref state);
                if (previous is Failed or Aborted)
                    return;
                if (Interlocked.CompareExchange(ref state, Failed, previous) == previous)
                    break;
            }
        }

        Volatile.Write(ref failure_error, error);
        failure.TrySetResult(error);
        queue.Writer.TryComplete(error);
        CancelPump();
    }

    private void CancelPump()
    {
        lock (shutdown_gate)
        {
            if (shutdown_started)
                return;
            shutdown_started = true;
            shutdown_task = CancelPumpAsync();
        }
    }

    private async Task CancelPumpAsync()
    {
        try
        {
            await shutdown.CancelAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void ScheduleCleanup()
    {
        lock (shutdown_gate)
        {
            if (cleanup_started)
                return;
            cleanup_started = true;
            _ = CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            await pump.ConfigureAwait(false);
            Task cancellation;
            lock (shutdown_gate)
                cancellation = shutdown_task;
            await cancellation.ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            shutdown.Dispose();
        }
    }
}
