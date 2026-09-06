namespace Qx.Messages;

public interface IPacket : IDisposable
{
    Header Header { get; set; }
    ClientType Client { get; }
    IParserContext? Context { get; }
    PacketBuffer Buffer { get; }
    ref int Position { get; }
    int Length { get; }

    PacketReader Reader();
    PacketReader ReaderAt(ref int pos);
    PacketWriter Writer();
    PacketWriter WriterAt(ref int pos);

    Span<byte> Allocate(int n);
    ReadOnlySpan<byte> ReadSpan(int n);
    void WriteSpan(ReadOnlySpan<byte> bytes);

    void Clear();
    IPacket Copy();
}
