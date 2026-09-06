using System.Buffers;

namespace Qx.Messages;

public sealed class PacketBuffer(int minimumCapacity = PacketBuffer.InitialCapacity) : IDisposable
{
    const int InitialCapacity = 32;

    private volatile bool _disposed;
    private IMemoryOwner<byte> _owner = MemoryPool<byte>.Shared.Rent(minimumCapacity);

    public int Length { get; private set; }

    public Span<byte> Span => _owner.Memory.Span[..Length];

    public PacketBuffer(ReadOnlySpan<byte> data)
        : this(data.Length)
    {
        Length = data.Length;
        data.CopyTo(Span);
    }

    public PacketBuffer(in ReadOnlySequence<byte> data)
        : this((int)data.Length)
    {
        Length = (int)data.Length;
        data.CopyTo(Span);
    }

    public void Grow(int min)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(min);

        if (_owner.Memory.Length < min)
        {
            int capacity = _owner.Memory.Length;
            while (capacity < min)
                capacity <<= 1;

            using IMemoryOwner<byte> old = _owner;
            _owner = MemoryPool<byte>.Shared.Rent(capacity);
            old.Memory.CopyTo(_owner.Memory);
        }

        if (Length < min)
            Length = min;
    }

    public Span<byte> Allocate(int start, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);

        Grow(start + length);
        return Span[start..(start + length)];
    }

    public Span<byte> Resize(Range range, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var (start, preLen) = range.GetOffsetAndLength(Length);
        int diff = length - preLen;

        if (diff > 0)
        {
            Grow(Length + diff);
            Span[(start + preLen)..^diff].CopyTo(Span[(start + length)..]);
        }
        else if (diff < 0)
        {
            Span[(start + preLen)..].CopyTo(Span[(start + length)..^-diff]);
            Length += diff;
        }

        return Span[start..(start + length)];
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Length = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _owner.Dispose();
    }

    public PacketBuffer Copy() => new(Span);
}
