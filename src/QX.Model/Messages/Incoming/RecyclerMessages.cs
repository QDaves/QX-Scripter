using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// How the recycler stands: whether one is running and how long it has left.
/// </summary>
/// <param name="Status">The recycler's state as the hotel numbers it.</param>
/// <param name="TimeoutSeconds">Seconds until the running session ends.</param>
public sealed record RecyclerStatus(int Status, int TimeoutSeconds) : IParserComposer<RecyclerStatus>
{
    public static RecyclerStatus Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static RecyclerStatus ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(RecyclerStatus value, in PacketWriter p)
    {
        p.WriteInt(value.Status);
        p.WriteInt(value.TimeoutSeconds);
    }
}

/// <summary>
/// A recycler session ended, and what it produced.
/// </summary>
/// <param name="Status">How it ended as the hotel numbers it.</param>
/// <param name="PrizeId">What was won, when the session produced anything.</param>
public sealed record RecyclerFinished(int Status, int PrizeId) : IParserComposer<RecyclerFinished>
{
    public static RecyclerFinished Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static RecyclerFinished ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(RecyclerFinished value, in PacketWriter p)
    {
        p.WriteInt(value.Status);
        p.WriteInt(value.PrizeId);
    }
}
