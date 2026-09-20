using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct HeightmapDiff(int X, int Y, short Value);

public sealed record HeightmapUpdate(IReadOnlyList<HeightmapDiff> Updates) : IParserComposer<HeightmapUpdate>
{
    public static HeightmapUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static HeightmapUpdate ParseFlash(in PacketReader p) => ParseUpdates(in p);

    private static HeightmapUpdate ParseUpdates(in PacketReader p)
    {
        int count = p.ReadByte();
        var updates = new HeightmapDiff[count];
        for (int i = 0; i < count; i++)
            updates[i] = new HeightmapDiff(p.ReadByte(), p.ReadByte(), p.ReadShort());
        return new HeightmapUpdate(updates);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(HeightmapUpdate value, in PacketWriter p) =>
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
