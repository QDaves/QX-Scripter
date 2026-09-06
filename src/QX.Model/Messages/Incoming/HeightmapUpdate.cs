using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct HeightmapDiff(int X, int Y, short Value);

/// <summary>
/// The tiles whose stacking height changed since the room was drawn.
/// </summary>
/// <remarks>
/// One byte of count on both clients. Unity reaches this through its generic array reader, which
/// is not fixed at two bytes as an array's count usually is — the reader carries the width it was
/// built with and takes a byte, a short or an int accordingly, and this message is one of the ones
/// built for a byte. Each entry is the same on both: two bytes for the tile, a short for its height.
/// </remarks>
public sealed record HeightmapUpdate(IReadOnlyList<HeightmapDiff> Updates) : IParserComposer<HeightmapUpdate>
{
    public static HeightmapUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static HeightmapUpdate ParseFlash(in PacketReader p) => ParseUpdates(in p);

    private static HeightmapUpdate ParseUnity(in PacketReader p) => ParseUpdates(in p);

    private static HeightmapUpdate ParseUpdates(in PacketReader p)
    {
        int count = p.ReadByte();
        var updates = new HeightmapDiff[count];
        for (int i = 0; i < count; i++)
            updates[i] = new HeightmapDiff(p.ReadByte(), p.ReadByte(), p.ReadShort());
        return new HeightmapUpdate(updates);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HeightmapUpdate value, in PacketWriter p) =>
        value.ComposeUpdates(in p);

    private static void ComposeUnity(HeightmapUpdate value, in PacketWriter p) =>
        value.ComposeUpdates(in p);

    private void ComposeUpdates(in PacketWriter p)
    {
        p.WriteByte((byte)Updates.Count);
        foreach (HeightmapDiff update in Updates)
        {
            p.WriteByte((byte)update.X);
            p.WriteByte((byte)update.Y);
            p.WriteShort(update.Value);
        }
    }
}
