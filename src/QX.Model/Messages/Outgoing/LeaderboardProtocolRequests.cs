using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record LeaderboardRequest(
    int GameTypeId,
    int StartRank,
    int Direction,
    int ViewSize,
    int WindowSize) : IParserComposer<LeaderboardRequest>
{
    public static LeaderboardRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static LeaderboardRequest ParseFlash(in PacketReader p)
    {
        LeaderboardWire.RequireRemaining(in p, sizeof(int) * 5, 0, nameof(LeaderboardRequest));
        var value = new LeaderboardRequest(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());
        LeaderboardWire.RequireEmpty(in p, nameof(LeaderboardRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(LeaderboardRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.GameTypeId);
        p.WriteInt(value.StartRank);
        p.WriteInt(value.Direction);
        p.WriteInt(value.ViewSize);
        p.WriteInt(value.WindowSize);
    }
}

public sealed record WeeklyLeaderboardRequest(
    int GameTypeId,
    int WeekOffset,
    int StartRank,
    int Direction,
    int ViewSize,
    int WindowSize) : IParserComposer<WeeklyLeaderboardRequest>
{
    public static WeeklyLeaderboardRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WeeklyLeaderboardRequest ParseFlash(in PacketReader p)
    {
        LeaderboardWire.RequireRemaining(
            in p,
            sizeof(int) * 6,
            0,
            nameof(WeeklyLeaderboardRequest));
        var value = new WeeklyLeaderboardRequest(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());
        LeaderboardWire.RequireEmpty(in p, nameof(WeeklyLeaderboardRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WeeklyLeaderboardRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.GameTypeId);
        p.WriteInt(value.WeekOffset);
        p.WriteInt(value.StartRank);
        p.WriteInt(value.Direction);
        p.WriteInt(value.ViewSize);
        p.WriteInt(value.WindowSize);
    }
}
