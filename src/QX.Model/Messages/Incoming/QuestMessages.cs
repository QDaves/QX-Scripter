using Qx.Messages;
using Qx.Model.Quests;

namespace Qx.Model.Messages.Incoming;

public sealed record Quest : IParserComposer<Quest>
{
    private QuestData data = null!;

    public Quest(QuestData Data)
    {
        this.Data = Data;
    }

    public QuestData Data
    {
        get => data;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Data));
            data = value;
        }
    }

    public void Deconstruct(out QuestData Data)
    {
        Data = this.Data;
    }

    public static Quest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Quest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static Quest ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static Quest ParseRoot(in PacketReader p)
    {
        var strings = QuestWire.NewStringBudget();
        QuestData data = QuestData.ParseWire(in p, 0, ref strings);
        QuestWire.RequireEmpty(in p, nameof(Quest));
        return new Quest(data);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Quest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(Quest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(Quest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = QuestWire.NewStringBudget();
        QuestDataWireSnapshot data = QuestData.PrepareWire(value.Data, ref strings, in p);
        QuestData.WriteWire(data, in p);
    }
}

public sealed record Quests : IParserComposer<Quests>
{
    private IReadOnlyList<QuestData> items =
        Array.AsReadOnly(Array.Empty<QuestData>());

    public Quests(IReadOnlyList<QuestData> Items, bool OpenWindow)
    {
        this.Items = Items;
        this.OpenWindow = OpenWindow;
    }

    public IReadOnlyList<QuestData> Items
    {
        get => items;
        init => items = QuestWire.FreezeReferences(value, nameof(Items));
    }

    public bool OpenWindow { get; init; }

    public void Deconstruct(out IReadOnlyList<QuestData> Items, out bool OpenWindow)
    {
        Items = this.Items;
        OpenWindow = this.OpenWindow;
    }

    public static Quests Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Quests ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static Quests ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static Quests ParseRoot(in PacketReader p)
    {
        IReadOnlyList<QuestData> items = QuestListWire.Parse(in p, sizeof(byte));
        bool open_window = p.ReadBool();
        QuestWire.RequireEmpty(in p, nameof(Quests));
        return new Quests(items, open_window);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Quests value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(Quests value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(Quests value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestDataWireSnapshot[] items = QuestListWire.Prepare(value.Items, in p);
        QuestListWire.Write(items, in p);
        p.WriteBool(value.OpenWindow);
    }
}

public sealed record QuestsSeasonal : IParserComposer<QuestsSeasonal>
{
    private IReadOnlyList<QuestData> items =
        Array.AsReadOnly(Array.Empty<QuestData>());

    public QuestsSeasonal(IReadOnlyList<QuestData> Items)
    {
        this.Items = Items;
    }

    public IReadOnlyList<QuestData> Items
    {
        get => items;
        init => items = QuestWire.FreezeReferences(value, nameof(Items));
    }

    public void Deconstruct(out IReadOnlyList<QuestData> Items)
    {
        Items = this.Items;
    }

    public static QuestsSeasonal Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static QuestsSeasonal ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestsSeasonal ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static QuestsSeasonal ParseRoot(in PacketReader p)
    {
        IReadOnlyList<QuestData> items = QuestListWire.Parse(in p, 0);
        QuestWire.RequireEmpty(in p, nameof(QuestsSeasonal));
        return new QuestsSeasonal(items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(QuestsSeasonal value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(QuestsSeasonal value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(QuestsSeasonal value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestDataWireSnapshot[] items = QuestListWire.Prepare(value.Items, in p);
        QuestListWire.Write(items, in p);
    }
}

public sealed record QuestCompleted : IParserComposer<QuestCompleted>
{
    private QuestData data = null!;

    public QuestCompleted(QuestData Data, bool ShowDialog)
    {
        this.Data = Data;
        this.ShowDialog = ShowDialog;
    }

    public QuestData Data
    {
        get => data;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Data));
            data = value;
        }
    }

    public bool ShowDialog { get; init; }

    public void Deconstruct(out QuestData Data, out bool ShowDialog)
    {
        Data = this.Data;
        ShowDialog = this.ShowDialog;
    }

    public static QuestCompleted Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static QuestCompleted ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestCompleted ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static QuestCompleted ParseRoot(in PacketReader p)
    {
        var strings = QuestWire.NewStringBudget();
        QuestData data = QuestData.ParseWire(in p, sizeof(byte), ref strings);
        bool show_dialog = p.ReadBool();
        QuestWire.RequireEmpty(in p, nameof(QuestCompleted));
        return new QuestCompleted(data, show_dialog);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(QuestCompleted value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(QuestCompleted value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(QuestCompleted value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = QuestWire.NewStringBudget();
        QuestDataWireSnapshot data = QuestData.PrepareWire(value.Data, ref strings, in p);
        QuestData.WriteWire(data, in p);
        p.WriteBool(value.ShowDialog);
    }
}

public sealed record QuestCancelled : IParserComposer<QuestCancelled>
{
    private QuestData data = null!;

    public QuestCancelled(bool IsExpired, QuestData Data)
    {
        this.IsExpired = IsExpired;
        this.Data = Data;
    }

    public bool IsExpired { get; init; }

    public QuestData Data
    {
        get => data;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Data));
            data = value;
        }
    }

    public void Deconstruct(out bool IsExpired, out QuestData Data)
    {
        IsExpired = this.IsExpired;
        Data = this.Data;
    }

    public static QuestCancelled Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static QuestCancelled ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestCancelled ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static QuestCancelled ParseRoot(in PacketReader p)
    {
        QuestWire.RequireRemaining(
            in p,
            sizeof(byte) + QuestWire.QuestMinimumBytes,
            0,
            nameof(QuestCancelled));
        bool is_expired = p.ReadBool();
        var strings = QuestWire.NewStringBudget();
        QuestData data = QuestData.ParseWire(in p, 0, ref strings);
        QuestWire.RequireEmpty(in p, nameof(QuestCancelled));
        return new QuestCancelled(is_expired, data);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(QuestCancelled value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(QuestCancelled value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(QuestCancelled value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = QuestWire.NewStringBudget();
        QuestDataWireSnapshot data = QuestData.PrepareWire(value.Data, ref strings, in p);
        p.WriteBool(value.IsExpired);
        QuestData.WriteWire(data, in p);
    }
}

public sealed record QuestDaily : IParserComposer<QuestDaily>
{
    public QuestDaily(QuestData? Data, int EasyQuestCount, int HardQuestCount)
    {
        this.Data = Data;
        this.EasyQuestCount = EasyQuestCount;
        this.HardQuestCount = HardQuestCount;
    }

    public QuestData? Data { get; init; }
    public int EasyQuestCount { get; init; }
    public int HardQuestCount { get; init; }
    public bool HasQuest => Data is not null;

    public void Deconstruct(
        out QuestData? Data,
        out int EasyQuestCount,
        out int HardQuestCount)
    {
        Data = this.Data;
        EasyQuestCount = this.EasyQuestCount;
        HardQuestCount = this.HardQuestCount;
    }

    public static QuestDaily Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static QuestDaily ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestDaily ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static QuestDaily ParseRoot(in PacketReader p)
    {
        QuestWire.RequireRemaining(in p, sizeof(byte), 0, nameof(QuestDaily));
        if (!p.ReadBool())
        {
            QuestWire.RequireEmpty(in p, nameof(QuestDaily));
            return new QuestDaily(null, 0, 0);
        }

        var strings = QuestWire.NewStringBudget();
        QuestData data = QuestData.ParseWire(
            in p,
            sizeof(int) * 2,
            ref strings);
        int easy_quest_count = p.ReadInt();
        int hard_quest_count = p.ReadInt();
        QuestWire.RequireEmpty(in p, nameof(QuestDaily));
        return new QuestDaily(data, easy_quest_count, hard_quest_count);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(QuestDaily value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(QuestDaily value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(QuestDaily value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Data is null)
        {
            if (value.EasyQuestCount != 0 || value.HardQuestCount != 0)
            {
                throw new InvalidDataException(
                    "A daily quest without data cannot contain quest counts.");
            }
            p.WriteBool(false);
            return;
        }

        var strings = QuestWire.NewStringBudget();
        QuestDataWireSnapshot data = QuestData.PrepareWire(value.Data, ref strings, in p);
        p.WriteBool(true);
        QuestData.WriteWire(data, in p);
        p.WriteInt(value.EasyQuestCount);
        p.WriteInt(value.HardQuestCount);
    }
}

public sealed record AcceptQuest(Id QuestId) : IParserComposer<AcceptQuest>
{
    public static AcceptQuest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AcceptQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static AcceptQuest ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static AcceptQuest ParseRoot(in PacketReader p)
    {
        Id quest_id = QuestWire.ReadId(in p, 0, nameof(QuestId));
        QuestWire.RequireEmpty(in p, nameof(AcceptQuest));
        return new AcceptQuest(quest_id);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AcceptQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(AcceptQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(AcceptQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestWire.RequireId(value.QuestId, p.Client);
        QuestWire.WriteId(value.QuestId, in p);
    }
}

public sealed record ActivateQuest(Id QuestId) : IParserComposer<ActivateQuest>
{
    public static ActivateQuest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ActivateQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static ActivateQuest ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static ActivateQuest ParseRoot(in PacketReader p)
    {
        Id quest_id = QuestWire.ReadId(in p, 0, nameof(QuestId));
        QuestWire.RequireEmpty(in p, nameof(ActivateQuest));
        return new ActivateQuest(quest_id);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ActivateQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(ActivateQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(ActivateQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestWire.RequireId(value.QuestId, p.Client);
        QuestWire.WriteId(value.QuestId, in p);
    }
}

public sealed record RejectQuest(Id QuestId) : IParserComposer<RejectQuest>
{
    public static RejectQuest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RejectQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static RejectQuest ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static RejectQuest ParseRoot(in PacketReader p)
    {
        Id quest_id = QuestWire.ReadId(in p, 0, nameof(QuestId));
        QuestWire.RequireEmpty(in p, nameof(RejectQuest));
        return new RejectQuest(quest_id);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RejectQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(RejectQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(RejectQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestWire.RequireId(value.QuestId, p.Client);
        QuestWire.WriteId(value.QuestId, in p);
    }
}

public sealed record CancelQuest : IParserComposer<CancelQuest>
{
    public static CancelQuest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CancelQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static CancelQuest ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static CancelQuest ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(CancelQuest));
        return new CancelQuest();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CancelQuest value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeUnity(CancelQuest value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(CancelQuest value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

public sealed record GetQuests : IParserComposer<GetQuests>
{
    public static GetQuests Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetQuests ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static GetQuests ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static GetQuests ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(GetQuests));
        return new GetQuests();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetQuests value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeUnity(GetQuests value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(GetQuests value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

public sealed record GetDailyQuest(
    bool IsEasy,
    int Index) : IParserComposer<GetDailyQuest>
{
    public static GetDailyQuest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetDailyQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static GetDailyQuest ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static GetDailyQuest ParseRoot(in PacketReader p)
    {
        QuestWire.RequireRemaining(
            in p,
            sizeof(byte) + sizeof(int),
            0,
            nameof(GetDailyQuest));
        var value = new GetDailyQuest(p.ReadBool(), p.ReadInt());
        QuestWire.RequireEmpty(in p, nameof(GetDailyQuest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetDailyQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(GetDailyQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(GetDailyQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteBool(value.IsEasy);
        p.WriteInt(value.Index);
    }
}

public sealed record GetSeasonalQuests : IParserComposer<GetSeasonalQuests>
{
    public static GetSeasonalQuests Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetSeasonalQuests ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static GetSeasonalQuests ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static GetSeasonalQuests ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(GetSeasonalQuests));
        return new GetSeasonalQuests();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetSeasonalQuests value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeUnity(GetSeasonalQuests value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(GetSeasonalQuests value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

public sealed record OpenQuestTracker : IParserComposer<OpenQuestTracker>
{
    public static OpenQuestTracker Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OpenQuestTracker ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static OpenQuestTracker ParseUnity(in PacketReader p) => ParseRoot(in p);

    private static OpenQuestTracker ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(OpenQuestTracker));
        return new OpenQuestTracker();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OpenQuestTracker value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeUnity(OpenQuestTracker value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(OpenQuestTracker value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

public sealed record FriendRequestQuestComplete : IParserComposer<FriendRequestQuestComplete>
{
    public static FriendRequestQuestComplete Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FriendRequestQuestComplete ParseFlash(in PacketReader p) =>
        ParseRoot(in p);

    private static FriendRequestQuestComplete ParseUnity(in PacketReader p) =>
        ParseRoot(in p);

    private static FriendRequestQuestComplete ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(FriendRequestQuestComplete));
        return new FriendRequestQuestComplete();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FriendRequestQuestComplete value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeUnity(FriendRequestQuestComplete value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(FriendRequestQuestComplete value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

internal static class QuestListWire
{
    public static IReadOnlyList<QuestData> Parse(
        in PacketReader p,
        int trailing_bytes)
    {
        int count = QuestWire.ReadCount(
            in p,
            QuestWire.QuestMinimumBytes,
            trailing_bytes,
            nameof(QuestData));
        var strings = QuestWire.NewStringBudget();
        var items = new QuestData[count];
        for (int index = 0; index < items.Length; index++)
        {
            int sibling_bytes = checked(
                (items.Length - index - 1) * QuestWire.QuestMinimumBytes);
            items[index] = QuestData.ParseWire(
                in p,
                checked(sibling_bytes + trailing_bytes),
                ref strings);
        }
        return Array.AsReadOnly(items);
    }

    public static QuestDataWireSnapshot[] Prepare(
        IReadOnlyList<QuestData> items,
        in PacketWriter p)
    {
        int count = QuestWire.RequireListCount(items, nameof(items));
        var strings = QuestWire.NewStringBudget();
        var snapshots = new QuestDataWireSnapshot[count];
        for (int index = 0; index < snapshots.Length; index++)
        {
            QuestData item = items[index];
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            snapshots[index] = QuestData.PrepareWire(item, ref strings, in p);
        }
        return snapshots;
    }

    public static void Write(QuestDataWireSnapshot[] items, in PacketWriter p)
    {
        QuestWire.WriteCount(items.Length, in p);
        foreach (QuestDataWireSnapshot item in items)
            QuestData.WriteWire(item, in p);
    }
}
