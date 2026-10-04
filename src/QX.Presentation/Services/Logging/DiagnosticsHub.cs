using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

    const int BatchChars = 64 * 1024;
    static readonly TimeSpan FileLockTimeout = TimeSpan.FromSeconds(2);

    readonly string _log_file;
    readonly Mutex? _file_lock;
    readonly BlockingCollection<string> _lines = new(new ConcurrentQueue<string>());
    readonly ConcurrentQueue<Action> _flushes = new();
    readonly ConcurrentQueue<LogEntry> _pending = new();
    readonly ObservableCollection<LogEntry> _entries = [];
    readonly InterceptFailureLog _intercepts = new();
    readonly Thread _writer;
    FileStream? _file;
    CoalescingSignal? _drain;
    DesktopRuntime? _runtime;
    long _sequence;
    int _disposed;

    DiagnosticsHub(string log_file)
    {
        _log_file = log_file;
        _file_lock = CreateFileLock(log_file);
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

    public void ReportInterceptFailure(Header packetHeader, Exception error, IMessageManager? messages)
    {
        if (_intercepts.ShouldReport(packetHeader, error))
            Diag.Error(InterceptFailureLog.Format(InterceptFailureLog.Describe(packetHeader, messages), error), "intercept");
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

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _flushes.Enqueue(() => flushed.TrySetResult());
        if (!Offer(""))
            flushed.TrySetResult();
        return flushed.Task.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Diag.Emitted -= Record;
        _lines.CompleteAdding();
        if (_writer.Join(TimeSpan.FromSeconds(1)))
            _file_lock?.Dispose();
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

    void Rotate() => UnderFileLock(() =>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_log_file)!);
        if (File.Exists(_log_file) && new FileInfo(_log_file).Length > RotationBytes && !IsOpenElsewhere(_log_file))
            File.Move(_log_file, _log_file + ".1", overwrite: true);
    });

    static bool IsOpenElsewhere(string path)
    {
        try
        {
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None).Dispose();
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    void WriteLines()
    {
        var batch = new StringBuilder();
        try
        {
            foreach (string line in _lines.GetConsumingEnumerable())
            {
                batch.Append(line);
                if (_lines.Count > 0 && batch.Length < BatchChars)
                    continue;
                Append(batch);
                if (_lines.Count == 0)
                    CompleteFlushes();
            }
        }
        finally
        {
            _file?.Dispose();
            _file = null;
            CompleteFlushes();
        }
    }

    void Append(StringBuilder batch)
    {
        if (batch.Length == 0)
            return;
        byte[] bytes = Encoding.UTF8.GetBytes(batch.ToString());
        batch.Clear();
        bool written = UnderFileLock(() =>
        {
            _file ??= new FileStream(_log_file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 0);
            _file.Seek(0, SeekOrigin.End);
            _file.Write(bytes);
        });
        if (written)
            return;
        _file?.Dispose();
        _file = null;
    }

    bool UnderFileLock(Action write)
    {
        bool owned = AcquireFileLock();
        try
        {
            write();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (owned)
                _file_lock!.ReleaseMutex();
        }
    }

    bool AcquireFileLock()
    {
        if (_file_lock is null)
            return false;
        try
        {
            return _file_lock.WaitOne(FileLockTimeout);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    static Mutex? CreateFileLock(string log_file)
    {
        byte[] path = Encoding.UTF8.GetBytes(Path.GetFullPath(log_file).ToUpperInvariant());
        try
        {
            return new Mutex(
                false,
                "qx-log-" + Convert.ToHexString(SHA256.HashData(path), 0, 16),
                new NamedWaitHandleOptions { CurrentSessionOnly = false });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
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
