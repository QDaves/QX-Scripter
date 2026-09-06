using System.Buffers.Binary;

namespace Qx.Interception.GEarth;

public static class GControlFrame
{
    public static int DeclaredLength(ReadOnlySpan<byte> frame) => BinaryPrimitives.ReadInt32BigEndian(frame[..4]);

    public static short Header(ReadOnlySpan<byte> frame) => BinaryPrimitives.ReadInt16BigEndian(frame.Slice(4, 2));

    public static ReadOnlySpan<byte> Body(ReadOnlySpan<byte> frame) => frame[6..];
}
