using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>Quest</c> message, received when the state of a quest changes.</summary>
public sealed record Quest : IParserComposer<Quest>
{
    private QuestData data = null!;

    /// <summary>Initializes a new instance of the <see cref="Quest"/> class.</summary>
    /// <param name="data">The quest.</param>
    public Quest(QuestData data)
    {
        Data = data;
    }

    /// <summary>Gets the quest.</summary>
    public QuestData Data
    {
        get => data;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Data));
            data = value;
        }
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="data">The quest.</param>
    public void Deconstruct(out QuestData data)
    {
        data = Data;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static Quest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Quest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static Quest ParseRoot(in PacketReader p)
    {
        var strings = QuestWire.NewStringBudget();
        QuestData data = QuestData.ParseWire(in p, 0, ref strings);
        QuestWire.RequireEmpty(in p, nameof(Quest));
        return new Quest(data);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(Quest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(Quest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = QuestWire.NewStringBudget();
        QuestDataWireSnapshot data = QuestData.PrepareWire(value.Data, ref strings, in p);
        QuestData.WriteWire(data, in p);
    }
}

/// <summary>Represents the <c>Quests</c> message, received with the quests available to the user.</summary>
public sealed record Quests : IParserComposer<Quests>
{
    private IReadOnlyList<QuestData> items =
        Array.AsReadOnly(Array.Empty<QuestData>());

    /// <summary>Initializes a new instance of the <see cref="Quests"/> class.</summary>
    /// <param name="items">The quests.</param>
    /// <param name="openWindow">Whether the hotel asks the client to open the quest window.</param>
    public Quests(IReadOnlyList<QuestData> items, bool openWindow)
    {
        Items = items;
        OpenWindow = openWindow;
    }

    /// <summary>Gets the quests.</summary>
    public IReadOnlyList<QuestData> Items
    {
        get => items;
        init => items = QuestWire.FreezeReferences(value, nameof(Items));
    }

    /// <summary>Gets whether the hotel asks the client to open the quest window.</summary>
    public bool OpenWindow { get; init; }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="items">The quests.</param>
    /// <param name="openWindow">Whether the hotel asks the client to open the quest window.</param>
    public void Deconstruct(out IReadOnlyList<QuestData> items, out bool openWindow)
    {
        items = Items;
        openWindow = OpenWindow;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static Quests Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Quests ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static Quests ParseRoot(in PacketReader p)
    {
        IReadOnlyList<QuestData> items = QuestListWire.Parse(in p, sizeof(byte));
        bool open_window = p.ReadBool();
        QuestWire.RequireEmpty(in p, nameof(Quests));
        return new Quests(items, open_window);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(Quests value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(Quests value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestDataWireSnapshot[] items = QuestListWire.Prepare(value.Items, in p);
        QuestListWire.Write(items, in p);
        p.WriteBool(value.OpenWindow);
    }
}

/// <summary>Represents the <c>SeasonalQuests</c> message, received with the seasonal quests available to the user.</summary>
public sealed record QuestsSeasonal : IParserComposer<QuestsSeasonal>
{
    private IReadOnlyList<QuestData> items =
        Array.AsReadOnly(Array.Empty<QuestData>());

    /// <summary>Initializes a new instance of the <see cref="QuestsSeasonal"/> class.</summary>
    /// <param name="items">The seasonal quests.</param>
    public QuestsSeasonal(IReadOnlyList<QuestData> items)
    {
        Items = items;
    }

    /// <summary>Gets the seasonal quests.</summary>
    public IReadOnlyList<QuestData> Items
    {
        get => items;
        init => items = QuestWire.FreezeReferences(value, nameof(Items));
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="items">The seasonal quests.</param>
    public void Deconstruct(out IReadOnlyList<QuestData> items)
    {
        items = Items;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static QuestsSeasonal Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static QuestsSeasonal ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestsSeasonal ParseRoot(in PacketReader p)
    {
        IReadOnlyList<QuestData> items = QuestListWire.Parse(in p, 0);
        QuestWire.RequireEmpty(in p, nameof(QuestsSeasonal));
        return new QuestsSeasonal(items);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(QuestsSeasonal value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(QuestsSeasonal value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestDataWireSnapshot[] items = QuestListWire.Prepare(value.Items, in p);
        QuestListWire.Write(items, in p);
    }
}

/// <summary>Represents the <c>QuestCompleted</c> message, received when the user completes a quest.</summary>
public sealed record QuestCompleted : IParserComposer<QuestCompleted>
{
    private QuestData data = null!;

    /// <summary>Initializes a new instance of the <see cref="QuestCompleted"/> class.</summary>
    /// <param name="data">The completed quest.</param>
    /// <param name="showDialog">Whether the hotel asks the client to show the completion dialog.</param>
    public QuestCompleted(QuestData data, bool showDialog)
    {
        Data = data;
        ShowDialog = showDialog;
    }

    /// <summary>Gets the completed quest.</summary>
    public QuestData Data
    {
        get => data;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Data));
            data = value;
        }
    }

    /// <summary>Gets whether the hotel asks the client to show the completion dialog.</summary>
    public bool ShowDialog { get; init; }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="data">The completed quest.</param>
    /// <param name="showDialog">Whether the hotel asks the client to show the completion dialog.</param>
    public void Deconstruct(out QuestData data, out bool showDialog)
    {
        data = Data;
        showDialog = ShowDialog;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static QuestCompleted Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static QuestCompleted ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestCompleted ParseRoot(in PacketReader p)
    {
        var strings = QuestWire.NewStringBudget();
        QuestData data = QuestData.ParseWire(in p, sizeof(byte), ref strings);
        bool show_dialog = p.ReadBool();
        QuestWire.RequireEmpty(in p, nameof(QuestCompleted));
        return new QuestCompleted(data, show_dialog);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(QuestCompleted value, in PacketWriter p) =>
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

/// <summary>Represents the <c>QuestCancelled</c> message, received when the user's active quest ends without being completed.</summary>
public sealed record QuestCancelled : IParserComposer<QuestCancelled>
{
    private QuestData data = null!;

    /// <summary>Initializes a new instance of the <see cref="QuestCancelled"/> class.</summary>
    /// <param name="isExpired">Whether the quest ended because it expired rather than by request.</param>
    /// <param name="data">The canceled quest.</param>
    public QuestCancelled(bool isExpired, QuestData data)
    {
        IsExpired = isExpired;
        Data = data;
    }

    /// <summary>Gets whether the quest ended because it expired rather than by request.</summary>
    public bool IsExpired { get; init; }

    /// <summary>Gets the canceled quest.</summary>
    public QuestData Data
    {
        get => data;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Data));
            data = value;
        }
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="isExpired">Whether the quest ended because it expired rather than by request.</param>
    /// <param name="data">The canceled quest.</param>
    public void Deconstruct(out bool isExpired, out QuestData data)
    {
        isExpired = IsExpired;
        data = Data;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static QuestCancelled Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static QuestCancelled ParseFlash(in PacketReader p) => ParseRoot(in p);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(QuestCancelled value, in PacketWriter p) =>
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

/// <summary>Represents the <c>QuestDaily</c> message, received with the daily quest offered to the user.</summary>
/// <remarks>
/// The packet starts with a flag that says whether a quest follows. Without a quest the counts are
/// not sent and are 0, and composing throws <see cref="InvalidDataException"/> if they are not.
/// </remarks>
public sealed record QuestDaily : IParserComposer<QuestDaily>
{
    /// <summary>Initializes a new instance of the <see cref="QuestDaily"/> class.</summary>
    /// <param name="data">The daily quest, or <see langword="null"/> when there is none.</param>
    /// <param name="easyQuestCount">The number of quests in the easy daily pool.</param>
    /// <param name="hardQuestCount">The number of quests in the hard daily pool.</param>
    public QuestDaily(QuestData? data, int easyQuestCount, int hardQuestCount)
    {
        Data = data;
        EasyQuestCount = easyQuestCount;
        HardQuestCount = hardQuestCount;
    }

    /// <summary>Gets the daily quest, or <see langword="null"/> when there is none.</summary>
    public QuestData? Data { get; init; }
    /// <summary>Gets the number of quests in the easy daily pool.</summary>
    public int EasyQuestCount { get; init; }
    /// <summary>Gets the number of quests in the hard daily pool.</summary>
    public int HardQuestCount { get; init; }
    /// <summary>Gets whether the message holds a quest.</summary>
    public bool HasQuest => Data is not null;

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="data">The daily quest, or <see langword="null"/> when there is none.</param>
    /// <param name="easyQuestCount">The number of quests in the easy daily pool.</param>
    /// <param name="hardQuestCount">The number of quests in the hard daily pool.</param>
    public void Deconstruct(
        out QuestData? data,
        out int easyQuestCount,
        out int hardQuestCount)
    {
        data = Data;
        easyQuestCount = EasyQuestCount;
        hardQuestCount = HardQuestCount;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static QuestDaily Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static QuestDaily ParseFlash(in PacketReader p) => ParseRoot(in p);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(QuestDaily value, in PacketWriter p) =>
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
