using System.Globalization;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record WalletBalanceRequest : IParserComposer<WalletBalanceRequest>
{
    public static WalletBalanceRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WalletBalanceRequest ParseFlash(in PacketReader p)
    {
        EconomyWire.RequireEmpty(in p, nameof(WalletBalanceRequest));
        return new WalletBalanceRequest();
    }

    private static WalletBalanceRequest ParseUnity(in PacketReader p)
    {
        EconomyWire.RequireEmpty(in p, nameof(WalletBalanceRequest));
        return new WalletBalanceRequest();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WalletBalanceRequest value, in PacketWriter p)
    {
    }

    private static void ComposeUnity(WalletBalanceRequest value, in PacketWriter p)
    {
    }
}

public sealed record CreditBalance(string Balance) : IParserComposer<CreditBalance>
{
    public int Credits
    {
        get
        {
            const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (!decimal.TryParse(Balance, style, CultureInfo.InvariantCulture, out decimal value) ||
                value < int.MinValue ||
                value > int.MaxValue)
            {
                throw new InvalidDataException($"Invalid credit balance '{Balance}'.");
            }
            return decimal.ToInt32(decimal.Truncate(value));
        }
    }

    public static CreditBalance Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CreditBalance ParseFlash(in PacketReader p)
    {
        var value = new CreditBalance(p.ReadString());
        EconomyWire.RequireEmpty(in p, nameof(CreditBalance));
        return value;
    }

    private static CreditBalance ParseUnity(in PacketReader p)
    {
        var value = new CreditBalance(p.ReadString());
        EconomyWire.RequireEmpty(in p, nameof(CreditBalance));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CreditBalance value, in PacketWriter p)
    {
        EconomyWire.RequireString(value.Balance, nameof(Balance), in p);
        p.WriteString(value.Balance);
    }

    private static void ComposeUnity(CreditBalance value, in PacketWriter p)
    {
        EconomyWire.RequireString(value.Balance, nameof(Balance), in p);
        p.WriteString(value.Balance);
    }
}

public sealed record ActivityPointNotification(int Amount, int Change, int Type)
    : IParserComposer<ActivityPointNotification>
{
    public static ActivityPointNotification Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ActivityPointNotification ParseFlash(in PacketReader p)
    {
        var value = new ActivityPointNotification(p.ReadInt(), p.ReadInt(), p.ReadInt());
        EconomyWire.RequireEmpty(in p, nameof(ActivityPointNotification));
        return value;
    }

    private static ActivityPointNotification ParseUnity(in PacketReader p)
    {
        var value = new ActivityPointNotification(p.ReadInt(), p.ReadInt(), p.ReadInt());
        EconomyWire.RequireEmpty(in p, nameof(ActivityPointNotification));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ActivityPointNotification value, in PacketWriter p)
    {
        p.WriteInt(value.Amount);
        p.WriteInt(value.Change);
        p.WriteInt(value.Type);
    }

    private static void ComposeUnity(ActivityPointNotification value, in PacketWriter p)
    {
        p.WriteInt(value.Amount);
        p.WriteInt(value.Change);
        p.WriteInt(value.Type);
    }
}

public readonly record struct ActivityPoint(int Type, int Amount);

public sealed record ActivityPoints : IParserComposer<ActivityPoints>
{
    private IReadOnlyList<ActivityPoint> points = Array.Empty<ActivityPoint>();

    public ActivityPoints(IReadOnlyList<ActivityPoint> Points)
    {
        this.Points = Points;
    }

    public IReadOnlyList<ActivityPoint> Points
    {
        get => points;
        init => points = EconomyWire.FreezePoints(value, nameof(Points));
    }

    public void Deconstruct(out IReadOnlyList<ActivityPoint> Points) => Points = this.Points;

    public int Get(int type) => Points.Where(point => point.Type == type).Select(point => point.Amount).FirstOrDefault();

    public static ActivityPoints Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ActivityPoints ParseFlash(in PacketReader p)
    {
        int count = EconomyWire.RequireCount(
            p.ReadInt(),
            p.Available,
            EconomyWire.ActivityPointBytes,
            nameof(Points));
        var points = new ActivityPoint[count];
        for (int index = 0; index < points.Length; index++)
            points[index] = new ActivityPoint(p.ReadInt(), p.ReadInt());
        var value = new ActivityPoints(points);
        EconomyWire.RequireEmpty(in p, nameof(ActivityPoints));
        return value;
    }

    private static ActivityPoints ParseUnity(in PacketReader p)
    {
        int count = EconomyWire.RequireCount(
            unchecked((ushort)p.ReadShort()),
            p.Available,
            EconomyWire.ActivityPointBytes,
            nameof(Points));
        var points = new ActivityPoint[count];
        for (int index = 0; index < points.Length; index++)
            points[index] = new ActivityPoint(p.ReadInt(), p.ReadInt());
        var value = new ActivityPoints(points);
        EconomyWire.RequireEmpty(in p, nameof(ActivityPoints));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ActivityPoints value, in PacketWriter p)
    {
        ActivityPoint[] points = EconomyWire.PreparePoints(value.Points, false);
        p.WriteInt(points.Length);
        foreach (ActivityPoint point in points)
        {
            p.WriteInt(point.Type);
            p.WriteInt(point.Amount);
        }
    }

    private static void ComposeUnity(ActivityPoints value, in PacketWriter p)
    {
        ActivityPoint[] points = EconomyWire.PreparePoints(value.Points, true);
        p.WriteShort(unchecked((short)(ushort)points.Length));
        foreach (ActivityPoint point in points)
        {
            p.WriteInt(point.Type);
            p.WriteInt(point.Amount);
        }
    }
}

internal static class EconomyWire
{
    internal const int ActivityPointBytes = sizeof(int) * 2;

    internal static int RequireCount(int count, int available, int minimum_bytes, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (available < 0 || minimum_bytes <= 0 || count > available / minimum_bytes)
            throw new InvalidDataException($"{name} count {count} exceeds the remaining payload capacity.");
        return count;
    }

    internal static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    internal static IReadOnlyList<ActivityPoint> FreezePoints(
        IReadOnlyList<ActivityPoint> values,
        string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    internal static ActivityPoint[] PreparePoints(
        IReadOnlyList<ActivityPoint> values,
        bool unity)
    {
        ArgumentNullException.ThrowIfNull(values);
        ActivityPoint[] snapshot = values.ToArray();
        if (unity && snapshot.Length > ushort.MaxValue)
            throw new InvalidDataException($"{nameof(ActivityPoints.Points)} count {snapshot.Length} exceeds the Unity wire limit.");
        return snapshot;
    }

    internal static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
