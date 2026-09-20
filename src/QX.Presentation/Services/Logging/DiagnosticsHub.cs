using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using Qx.Diagnostics;
using Qx.Interception;
using Qx.Messages;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Logging;

public sealed class DiagnosticsHub : IApplicationLog, IDisposable
{
    public const int Capacity = 5000;
    public const long RotationBytes = 4 * 1024 * 1024;

    readonly string _log_file;
    readonly BlockingCollection<string> _lines = new(new ConcurrentQueue<string>());
    readonly ConcurrentQueue<Action> _flushes = new();
    readonly ConcurrentQueue<LogEntry> _pending = new();
    readonly ObservableCollection<LogEntry> _entries = [];
    readonly InterceptFailureLog _intercepts = new();
    readonly Thread _writer;
    CoalescingSignal? _drain;
    DesktopRuntime? _runtime;
    long _sequence;
    int _disposed;

    DiagnosticsHub(string log_file)
    {
        _log_file = log_file;
        Entries = new ReadOnlyObservableCollection<LogEntry>(_entries);
        _writer = new Thread(WriteLines) { IsBackground = true, Name = "QX log writer" };
    }

    public ReadOnlyObservableCollection<LogEntry> Entries { get; }

    public event Action<IReadOnlyList<LogEntry>>? Appended;

    public event Action<int>? Trimmed;

    public event Action? Cleared;

    public static DiagnosticsHub Start(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Diag.Enabled = true;
        Diag.MinLevel = DiagLevel.Info;
        var hub = new DiagnosticsHub(paths.LogFile);
        hub.Rotate();
        hub._writer.Start();
        Diag.Emitted += hub.Record;
        return hub;
    }

    public void AttachDispatcher(IUiDispatcher dispatcher)
    {
        _drain = new CoalescingSignal(dispatcher, Drain);
        _drain.Raise();
    }

    public void AttachRuntime(DesktopRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (Interlocked.CompareExchange(ref _runtime, runtime, null) is not null)
            throw new InvalidOperationException("A runtime is already attached.");
        runtime.Extension.InterceptFailed += OnInterceptFailed;
    }

    public void DetachRuntime()
    {
        if (Interlocked.Exchange(ref _runtime, null) is { } runtime)
            runtime.Extension.InterceptFailed -= OnInterceptFailed;
    }

    public void ReportInterceptFailure(Header packet_header, Exception error, IMessageManager? messages)
    {
        if (_intercepts.ShouldReport(packet_header, error))
            Diag.Error(InterceptFailureLog.Format(InterceptFailureLog.Describe(packet_header, messages), error), "intercept");
    }

    public bool FlushNow(TimeSpan budget)
    {
        var flushed = new ManualResetEventSlim();
        _flushes.Enqueue(flushed.Set);
        if (!Offer(""))
            return false;
        return flushed.Wait(budget);
    }

    public void Clear()
    {
        _entries.Clear();
        Cleared?.Invoke();
    }

    public Task FlushAsync(CancellationToken cancellation_token)
    {
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _flushes.Enqueue(() => flushed.TrySetResult());
        if (!Offer(""))
            flushed.TrySetResult();
        return flushed.Task.WaitAsync(cancellation_token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Diag.Emitted -= Record;
        _lines.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(1));
    }

    void Record(DiagLevel level, string message, string? category)
    {
        DateTime now = DateTime.Now;
        string tag = category is null ? "" : category + " ";
        Offer($"[{now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}] {level} {tag}{message}{Environment.NewLine}");
        _pending.Enqueue(new LogEntry(Interlocked.Increment(ref _sequence), now, level, category, message));
        _drain?.Raise();
    }

    bool Offer(string line)
    {
        try
        {
            return !_lines.IsAddingCompleted && _lines.TryAdd(line);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    void Drain()
    {
        List<LogEntry> batch = [];
        while (_pending.TryDequeue(out LogEntry? entry))
            batch.Add(entry);
        if (batch.Count == 0)
            return;
        foreach (LogEntry entry in batch)
            _entries.Add(entry);
        int removed = 0;
        while (_entries.Count > Capacity)
        {
            _entries.RemoveAt(0);
            removed++;
        }
        Appended?.Invoke(batch);
        if (removed > 0)
            Trimmed?.Invoke(removed);
    }

    void Rotate()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_log_file)!);
            if (File.Exists(_log_file) && new FileInfo(_log_file).Length > RotationBytes)
                File.Move(_log_file, _log_file + ".1", overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    void WriteLines()
    {
        StreamWriter? writer = null;
        try
        {
            foreach (string line in _lines.GetConsumingEnumerable())
            {
                writer ??= Open();
                if (line.Length > 0)
                    writer?.Write(line);
                if (_lines.Count == 0)
                {
                    writer?.Flush();
                    CompleteFlushes();
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
        }
        finally
        {
            writer?.Dispose();
            CompleteFlushes();
        }
    }

    StreamWriter? Open()
    {
        try
        {
            var stream = new FileStream(_log_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return new StreamWriter(stream);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    void CompleteFlushes()
    {
        while (_flushes.TryDequeue(out Action? flushed))
            flushed();
    }

    void OnInterceptFailed(Intercept intercept, Exception error) =>
        ReportInterceptFailure(intercept.Packet.Header, error, Volatile.Read(ref _runtime)?.Messages);
}
