using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Output;

public sealed record OutputLine(long Sequence, string Text, OutputLevel Level, DateTimeOffset At);

public sealed class OutputBuffer
{
    public const int Capacity = 5000;
    public const int TrimBlock = 500;

    readonly ConcurrentQueue<OutputLine> _incoming = new();
    readonly ObservableCollection<OutputLine> _lines = [];
    readonly CoalescingSignal _drain;
    readonly TimeProvider _time;
    long _sequence;
    long _cleared_through;

    public OutputBuffer(IUiDispatcher dispatcher, TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _drain = new CoalescingSignal(dispatcher, Drain, UiPriority.Background);
        Lines = new ReadOnlyObservableCollection<OutputLine>(_lines);
    }

    public ReadOnlyObservableCollection<OutputLine> Lines { get; }

    public event Action<IReadOnlyList<OutputLine>>? Appended;

    public event Action<int>? Trimmed;

    public event Action? Cleared;

    public void Write(string text, OutputLevel level)
    {
        ArgumentNullException.ThrowIfNull(text);
        _incoming.Enqueue(new OutputLine(Interlocked.Increment(ref _sequence), text, level, _time.GetUtcNow()));
        _drain.Raise();
    }

    public void Clear()
    {
        Volatile.Write(ref _cleared_through, Volatile.Read(ref _sequence));
        _lines.Clear();
        Cleared?.Invoke();
    }

    public string Text() => string.Join(Environment.NewLine, _lines.Select(line => line.Text));

    void Drain()
    {
        long cleared = Volatile.Read(ref _cleared_through);
        List<OutputLine> batch = [];
        while (_incoming.TryDequeue(out OutputLine? line))
        {
            if (line.Sequence > cleared)
                batch.Add(line);
        }
        if (batch.Count == 0)
            return;
        foreach (OutputLine line in batch)
            _lines.Add(line);
        int removed = 0;
        while (_lines.Count > Capacity)
        {
            int block = Math.Min(TrimBlock, _lines.Count);
            for (int index = 0; index < block; index++)
                _lines.RemoveAt(0);
            removed += block;
        }
        Appended?.Invoke(batch);
        if (removed > 0)
            Trimmed?.Invoke(removed);
    }
}
