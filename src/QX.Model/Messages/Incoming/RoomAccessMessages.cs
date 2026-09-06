using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum RoomConnectionFailureKind
{
    Unknown,
    Full,
    QueueError,
    Banned,
    Blocked
}

public enum RoomQueueTarget
{
    Spectator = 1,
    Visitor = 2
}

public sealed record OpenConnectionConfirmation(Id RoomId) : IParserComposer<OpenConnectionConfirmation>
{
    public static OpenConnectionConfirmation Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OpenConnectionConfirmation ParseFlash(in PacketReader p) => new(p.ReadId());

    private static OpenConnectionConfirmation ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OpenConnectionConfirmation value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(OpenConnectionConfirmation value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

public sealed record FlatAccessible(Id RoomId, string UserName) : IParserComposer<FlatAccessible>
{
    public bool IsSelf => string.IsNullOrEmpty(UserName);

    public static FlatAccessible Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FlatAccessible ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FlatAccessible value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteString(value.UserName);
    }
}

public sealed record FlatAccessDenied(Id RoomId, string? UserName) : IParserComposer<FlatAccessDenied>
{
    public bool IsSelf => string.IsNullOrEmpty(UserName);

    public static FlatAccessDenied Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FlatAccessDenied ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadId();
        return new FlatAccessDenied(room_id, p.Available > 0 ? p.ReadString() : null);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FlatAccessDenied value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        if (value.UserName is not null)
            p.WriteString(value.UserName);
    }
}

public sealed record NoSuchFlat(Id RoomId) : IParserComposer<NoSuchFlat>
{
    public static NoSuchFlat Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NoSuchFlat ParseFlash(in PacketReader p) => new(p.ReadId());

    private static NoSuchFlat ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NoSuchFlat value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(NoSuchFlat value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

public sealed record CanNotConnect(int ReasonCode, string Parameter) : IParserComposer<CanNotConnect>
{
    public RoomConnectionFailureKind Kind => ReasonCode switch
    {
        1 => RoomConnectionFailureKind.Full,
        3 => RoomConnectionFailureKind.QueueError,
        4 => RoomConnectionFailureKind.Banned,
        5 => RoomConnectionFailureKind.Blocked,
        _ => RoomConnectionFailureKind.Unknown
    };

    public static CanNotConnect Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CanNotConnect ParseFlash(in PacketReader p)
    {
        int reason_code = p.ReadInt();
        return new CanNotConnect(reason_code, reason_code == 3 ? p.ReadString() : "");
    }

    private static CanNotConnect ParseUnity(in PacketReader p)
    {
        int reason_code = p.ReadInt();
        return new CanNotConnect(reason_code, reason_code == 3 ? p.ReadString() : "");
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CanNotConnect value, in PacketWriter p)
    {
        p.WriteInt(value.ReasonCode);
        if (value.ReasonCode == 3)
            p.WriteString(value.Parameter);
    }

    private static void ComposeUnity(CanNotConnect value, in PacketWriter p)
    {
        p.WriteInt(value.ReasonCode);
        if (value.ReasonCode == 3)
            p.WriteString(value.Parameter);
    }
}

public sealed record RoomQueueEntry(string Type, int Size) : IParserComposer<RoomQueueEntry>
{
    public static RoomQueueEntry Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomQueueEntry ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    private static RoomQueueEntry ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomQueueEntry value, in PacketWriter p)
    {
        p.WriteString(value.Type);
        p.WriteInt(value.Size);
    }

    private static void ComposeUnity(RoomQueueEntry value, in PacketWriter p)
    {
        p.WriteString(value.Type);
        p.WriteInt(value.Size);
    }
}

public sealed record RoomQueueSet(
    string Name,
    RoomQueueTarget Target,
    IReadOnlyList<RoomQueueEntry> Queues) : IParserComposer<RoomQueueSet>
{
    public int? Position => Queues.Count == 0 || Queues[0].Size < 0
        ? null
        : Queues[0].Size == int.MaxValue ? int.MaxValue : Queues[0].Size + 1;

    public static RoomQueueSet Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomQueueSet ParseFlash(in PacketReader p)
    {
        string name = p.ReadString();
        var target = (RoomQueueTarget)p.ReadInt();
        int count = p.ReadLength();
        var queues = new RoomQueueEntry[count];
        for (int index = 0; index < count; index++)
            queues[index] = p.Parse<RoomQueueEntry>();
        return new RoomQueueSet(name, target, queues);
    }

    private static RoomQueueSet ParseUnity(in PacketReader p)
    {
        string name = p.ReadString();
        var target = (RoomQueueTarget)p.ReadInt();
        int count = p.ReadLength();
        var queues = new RoomQueueEntry[count];
        for (int index = 0; index < count; index++)
            queues[index] = p.Parse<RoomQueueEntry>();
        return new RoomQueueSet(name, target, queues);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomQueueSet value, in PacketWriter p)
    {
        p.WriteString(value.Name);
        p.WriteInt((int)value.Target);
        p.WriteLength((Length)value.Queues.Count);
        foreach (RoomQueueEntry queue in value.Queues)
            p.Compose(queue);
    }

    private static void ComposeUnity(RoomQueueSet value, in PacketWriter p)
    {
        p.WriteString(value.Name);
        p.WriteInt((int)value.Target);
        p.WriteLength((Length)value.Queues.Count);
        foreach (RoomQueueEntry queue in value.Queues)
            p.Compose(queue);
    }
}

public sealed record RoomQueueStatus(Id RoomId, IReadOnlyList<RoomQueueSet> Sets)
    : IParserComposer<RoomQueueStatus>
{
    public RoomQueueTarget? ActiveTarget => Sets.Count == 0 ? null : Sets[0].Target;
    public RoomQueueSet? ActiveSet => ActiveTarget is { } target
        ? Sets.FirstOrDefault(set => set.Target == target)
        : null;
    public int? Position => ActiveSet?.Position;

    public static RoomQueueStatus Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomQueueStatus ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadId();
        int count = p.ReadLength();
        var sets = new RoomQueueSet[count];
        for (int index = 0; index < count; index++)
            sets[index] = p.Parse<RoomQueueSet>();
        return new RoomQueueStatus(room_id, sets);
    }

    private static RoomQueueStatus ParseUnity(in PacketReader p)
    {
        Id room_id = p.ReadId();
        int count = p.ReadLength();
        var sets = new RoomQueueSet[count];
        for (int index = 0; index < count; index++)
            sets[index] = p.Parse<RoomQueueSet>();
        return new RoomQueueStatus(room_id, sets);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomQueueStatus value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteLength((Length)value.Sets.Count);
        foreach (RoomQueueSet set in value.Sets)
            p.Compose(set);
    }

    private static void ComposeUnity(RoomQueueStatus value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteLength((Length)value.Sets.Count);
        foreach (RoomQueueSet set in value.Sets)
            p.Compose(set);
    }
}
