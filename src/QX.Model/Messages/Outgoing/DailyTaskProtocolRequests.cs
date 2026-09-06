using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record DailyTaskListRequest : IParserComposer<DailyTaskListRequest>
{
    public static DailyTaskListRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static DailyTaskListRequest ParseFlash(in PacketReader p)
    {
        DailyTaskWire.RequireEmpty(in p, nameof(DailyTaskListRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTaskListRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);
}

public sealed record DailyTaskClaimRequest(long TaskId)
    : IParserComposer<DailyTaskClaimRequest>
{
    public static DailyTaskClaimRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static DailyTaskClaimRequest ParseFlash(in PacketReader p)
    {
        DailyTaskWire.RequireRemaining(in p, sizeof(int), 0, nameof(DailyTaskClaimRequest));
        var value = new DailyTaskClaimRequest(p.ReadInt());
        DailyTaskWire.RequireEmpty(in p, nameof(DailyTaskClaimRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTaskClaimRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(unchecked((int)value.TaskId));
    }
}
