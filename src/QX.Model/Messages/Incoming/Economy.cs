using System.Globalization;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>CreditBalance</c> message, received with the user's credit balance.</summary>
/// <param name="Balance">The credit balance as the decimal string sent by the server.</param>
public sealed record CreditBalance(string Balance) : IParserComposer<CreditBalance>
{
    /// <summary>Gets the credit balance as a whole number, with any decimal part truncated.</summary>
    /// <exception cref="InvalidDataException">
    /// Thrown when <see cref="Balance"/> is not a number or does not fit in an <see cref="int"/>.
    /// </exception>
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

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CreditBalance Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CreditBalance ParseFlash(in PacketReader p)
    {
        var value = new CreditBalance(p.ReadString());
        EconomyWire.RequireEmpty(in p, nameof(CreditBalance));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CreditBalance value, in PacketWriter p)
    {
        EconomyWire.RequireString(value.Balance, nameof(Balance), in p);
        p.WriteString(value.Balance);
    }
}

/// <summary>
/// Represents the <c>HabboActivityPointNotification</c> message, received when the balance of an activity point
/// currency changes.
/// </summary>
/// <param name="Amount">The new balance of the currency.</param>
/// <param name="Change">The amount the balance changed by.</param>
/// <param name="Type">The activity point type of the currency.</param>
public sealed record ActivityPointNotification(int Amount, int Change, int Type)
    : IParserComposer<ActivityPointNotification>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ActivityPointNotification Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ActivityPointNotification ParseFlash(in PacketReader p)
    {
        var value = new ActivityPointNotification(p.ReadInt(), p.ReadInt(), p.ReadInt());
        EconomyWire.RequireEmpty(in p, nameof(ActivityPointNotification));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ActivityPointNotification value, in PacketWriter p)
    {
        p.WriteInt(value.Amount);
        p.WriteInt(value.Change);
        p.WriteInt(value.Type);
    }
}

/// <summary>Represents the balance of one activity point currency.</summary>
/// <param name="Type">The activity point type of the currency.</param>
/// <param name="Amount">The balance of the currency.</param>
public readonly record struct ActivityPoint(int Type, int Amount);

/// <summary>Represents the <c>ActivityPoints</c> message, received with the user's activity point balances.</summary>
public sealed record ActivityPoints : IParserComposer<ActivityPoints>
{
    private IReadOnlyList<ActivityPoint> points = Array.Empty<ActivityPoint>();

    /// <summary>Initializes a new instance of the <see cref="ActivityPoints"/> record.</summary>
    /// <param name="points">The balances, copied into a read only list.</param>
    public ActivityPoints(IReadOnlyList<ActivityPoint> points)
    {
        Points = points;
    }

    /// <summary>Gets the balance of each activity point currency, as a read only copy.</summary>
    public IReadOnlyList<ActivityPoint> Points
    {
        get => points;
        init => points = EconomyWire.FreezePoints(value, nameof(Points));
    }

    /// <summary>Deconstructs the message into its balances.</summary>
    /// <param name="points">The balance of each activity point currency.</param>
    public void Deconstruct(out IReadOnlyList<ActivityPoint> points) => points = Points;

    /// <summary>Gets the balance of an activity point currency.</summary>
    /// <param name="type">The activity point type of the currency.</param>
    /// <returns>The balance of the first entry with the type, or 0 when there is none.</returns>
    public int Get(int type) => Points.Where(point => point.Type == type).Select(point => point.Amount).FirstOrDefault();

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ActivityPoints Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ActivityPoints value, in PacketWriter p)
    {
        ActivityPoint[] points = EconomyWire.PreparePoints(value.Points);
        p.WriteInt(points.Length);
        foreach (ActivityPoint point in points)
        {
            p.WriteInt(point.Type);
            p.WriteInt(point.Amount);
        }
    }
}
