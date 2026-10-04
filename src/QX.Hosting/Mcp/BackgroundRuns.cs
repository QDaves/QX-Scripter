using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Qx.Scripting.Hosting;

namespace Qx.Hosting;

/// <summary>What a client reads of a background run.</summary>
/// <param name="Id">The run's id.</param>
/// <param name="Source">The script it runs, or <c>run_code</c> for code that has no file.</param>
/// <param name="State">Where the run stands.</param>
/// <param name="RuntimeMs">How long it has run, or ran.</param>
/// <param name="Next">The line to pass as <c>since</c> to read only what comes after.</param>
/// <param name="Output">The output lines from the requested line on, as far as they are kept.</param>
/// <param name="Errors">Everything that went wrong, in order.</param>
internal sealed record BackgroundRunSnapshot(
    long Id,
    string Source,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ScriptRunState>))] ScriptRunState State,
    double RuntimeMs,
    int Next,
    IReadOnlyList<string> Output,
    IReadOnlyList<ScriptExecutionError> Errors);

/// <summary>
/// Script runs an MCP client started without waiting for them. A run is read while it goes on
/// and stopped on request; it opens no tab and writes nothing to the library. Running runs are
/// always kept, finished ones only up to <see cref="Kept"/>.
/// </summary>
internal sealed class BackgroundRuns(ScriptExecutionService scripts)
{
    public const int Kept = 20;

    readonly ConcurrentDictionary<long, BackgroundRun> _runs = new();
    long _last_id;

    public BackgroundRun Start(string code, string source, string? source_identity, string file_name, TimeSpan? timeout)
    {
        long id = Interlocked.Increment(ref _last_id);
        var run = new BackgroundRun(id, source);
        _runs[id] = run;
        Forget();
        run.Follow(scripts.RunAsync(new ScriptExecutionRequest
        {
            Code = code,
            SourceIdentity = source_identity ?? $"mcp:run:{id}",
            FileName = file_name,
            Timeout = timeout,
            OutputWritten = run.Write,
            ErrorReported = run.Report,
            StateChanged = run.Change
        }, run.Token));
        return run;
    }

    public BackgroundRun? Find(long id) => _runs.GetValueOrDefault(id);

    public IReadOnlyList<BackgroundRun> All => [.. _runs.Values.OrderBy(run => run.Id)];

    void Forget()
    {
        foreach (BackgroundRun run in _runs.Values.Where(run => run.IsDone).OrderByDescending(run => run.Id).Skip(Kept))
            _runs.TryRemove(run.Id, out _);
    }
}

internal sealed class BackgroundRun(long id, string source)
{
    public const int KeptLines = 5000;

    readonly CancellationTokenSource _stop = new();
    readonly Lock _gate = new();
    readonly Queue<string> _lines = new();
    readonly List<ScriptExecutionError> _errors = [];
    readonly long _started = Stopwatch.GetTimestamp();
    int _written;
    ScriptRunState _state = ScriptRunState.Compiling;
    double? _runtime_ms;

    public long Id => id;

    public string Source => source;

    public CancellationToken Token => _stop.Token;

    public Task Completion { get; private set; } = Task.CompletedTask;

    public bool IsDone
    {
        get
        {
            lock (_gate)
                return _runtime_ms is not null;
        }
    }

    public void Write(string line)
    {
        lock (_gate)
        {
            _lines.Enqueue(line);
            _written++;
            if (_lines.Count > KeptLines)
                _lines.Dequeue();
        }
    }

    public void Report(ScriptExecutionError error)
    {
        lock (_gate)
            _errors.Add(error);
    }

    public void Change(ScriptRunState state)
    {
        lock (_gate)
        {
            if (_runtime_ms is null)
                _state = state;
        }
    }

    /// <summary>Asks the run to stop, and says whether it was still going.</summary>
    public bool Stop()
    {
        if (IsDone)
            return false;
        _stop.Cancel();
        return true;
    }

    public void Follow(Task<ScriptExecutionResult> run) => Completion = FinishAsync(run);

    public BackgroundRunSnapshot Read(int since)
    {
        lock (_gate)
        {
            int first = _written - _lines.Count;
            string[] output = [.. _lines.Skip(Math.Max(since - first, 0))];
            double runtime_ms = _runtime_ms ?? Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            return new BackgroundRunSnapshot(id, source, _state, runtime_ms, _written, output, [.. _errors]);
        }
    }

    async Task FinishAsync(Task<ScriptExecutionResult> run)
    {
        ScriptExecutionResult result;
        try
        {
            result = await run.ConfigureAwait(false);
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                _errors.Add(ScriptExecutionError.FromException(error, "host", source));
                _state = ScriptRunState.Faulted;
                _runtime_ms = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            }
            return;
        }
        lock (_gate)
        {
            _errors.Clear();
            _errors.AddRange(result.Errors);
            _state = result.State;
            _runtime_ms = result.RuntimeMs;
        }
    }
}
