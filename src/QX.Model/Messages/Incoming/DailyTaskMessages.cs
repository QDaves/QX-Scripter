using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Specifies how far a daily task has got.</summary>
public enum DailyTaskStatus
{
    /// <summary>Still being worked on; the repeat counter has not reached the requirement.</summary>
    InProgress = 0,
    /// <summary>Finished and waiting to be claimed.</summary>
    Completed = 1,
    /// <summary>Finished and already claimed.</summary>
    Claimed = 2
}

/// <summary>Represents one item handed out for finishing a daily task.</summary>
public sealed record DailyTaskReward : IParserComposer<DailyTaskReward>
{
    private string _reward_type_id = "";
    private string _extra_params = "";

    /// <summary>Initializes a new instance of the <see cref="DailyTaskReward"/> record.</summary>
    /// <param name="productItemTypeId">The product's item type.</param>
    /// <param name="rewardTypeId">The reward category, which decides how the client draws it.</param>
    /// <param name="extraParams">Reward specific detail, such as a badge code or a furni class.</param>
    /// <param name="amount">How many are given.</param>
    public DailyTaskReward(
        short productItemTypeId,
        string rewardTypeId,
        string extraParams,
        int amount)
    {
        ProductItemTypeId = productItemTypeId;
        RewardTypeId = rewardTypeId;
        ExtraParams = extraParams;
        Amount = amount;
    }

    /// <summary>Gets the product's item type.</summary>
    public short ProductItemTypeId { get; init; }

    /// <summary>Gets the reward category, which decides how the client draws it.</summary>
    public string RewardTypeId
    {
        get => _reward_type_id;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(RewardTypeId));
            _reward_type_id = value;
        }
    }

    /// <summary>Gets reward specific detail, such as a badge code or a furni class.</summary>
    public string ExtraParams
    {
        get => _extra_params;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ExtraParams));
            _extra_params = value;
        }
    }

    /// <summary>Gets how many are given.</summary>
    public int Amount { get; init; }

    /// <summary>Deconstructs the reward into its parts.</summary>
    /// <param name="productItemTypeId">The product's item type.</param>
    /// <param name="rewardTypeId">The reward category.</param>
    /// <param name="extraParams">Reward specific detail, such as a badge code or a furni class.</param>
    /// <param name="amount">How many are given.</param>
    public void Deconstruct(
        out short productItemTypeId,
        out string rewardTypeId,
        out string extraParams,
        out int amount)
    {
        productItemTypeId = ProductItemTypeId;
        rewardTypeId = RewardTypeId;
        extraParams = ExtraParams;
        amount = Amount;
    }

    /// <summary>Parses a daily task reward from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DailyTaskReward Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DailyTaskReward ParseFlash(in PacketReader p)
    {
        var strings = DailyTaskWire.NewStringBudget();
        DailyTaskReward value = ParseWire(in p, 0, ref strings);
        DailyTaskWire.RequireEmpty(in p, nameof(DailyTaskReward));
        return value;
    }

    /// <summary>Composes the daily task reward into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTaskReward value, in PacketWriter p)
    {
        var strings = DailyTaskWire.NewStringBudget();
        DailyTaskRewardWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }

    internal static DailyTaskReward ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref DailyTaskStringBudget strings)
    {
        DailyTaskWire.RequireRemaining(
            in p,
            DailyTaskWire.RewardMinimumBytes,
            trailing_bytes,
            nameof(DailyTaskReward));
        short product_item_type_id = p.ReadShort();
        string reward_type_id = strings.Read(
            in p,
            nameof(RewardTypeId),
            checked(trailing_bytes + DailyTaskWire.StringPrefixBytes + sizeof(int)));
        string extra_params = strings.Read(
            in p,
            nameof(ExtraParams),
            checked(trailing_bytes + sizeof(int)));
        int amount = p.ReadInt();
        return new DailyTaskReward(
            product_item_type_id,
            reward_type_id,
            extra_params,
            amount);
    }

    internal static DailyTaskRewardWireSnapshot PrepareWire(
        DailyTaskReward value,
        ref DailyTaskStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var snapshot = new DailyTaskRewardWireSnapshot(
            value.ProductItemTypeId,
            value.RewardTypeId,
            value.ExtraParams,
            value.Amount);
        strings.Require(snapshot.RewardTypeId, nameof(RewardTypeId), in p);
        strings.Require(snapshot.ExtraParams, nameof(ExtraParams), in p);
        return snapshot;
    }

    internal static void WriteWire(DailyTaskRewardWireSnapshot value, in PacketWriter p)
    {
        p.WriteShort(value.ProductItemTypeId);
        p.WriteString(value.RewardTypeId);
        p.WriteString(value.ExtraParams);
        p.WriteInt(value.Amount);
    }
}

internal readonly record struct DailyTaskRewardWireSnapshot(
    short ProductItemTypeId,
    string RewardTypeId,
    string ExtraParams,
    int Amount);

/// <summary>
/// Represents a single daily task: what to do, how far along it is, and what finishing it pays.
/// </summary>
public sealed record DailyTask : IParserComposer<DailyTask>
{
    private string _task_code = "";
    private string _quest_type_code = "";
    private string _image_version = "";
    private string _catalog_name = "";
    private IReadOnlyList<DailyTaskReward> _rewards =
        Array.AsReadOnly(Array.Empty<DailyTaskReward>());

    /// <summary>Initializes a new instance of the <see cref="DailyTask"/> record.</summary>
    /// <param name="taskId">The task's identifier.</param>
    /// <param name="taskCode">The task's code, which keys its localized name.</param>
    /// <param name="questTypeCode">The underlying quest type, shared with the quest system.</param>
    /// <param name="isBonus">Whether this is the bonus task, which the client styles differently.</param>
    /// <param name="imageVersion">Cache-busting suffix for the task's artwork.</param>
    /// <param name="catalogName">The catalog page the task points at, empty when it points nowhere.</param>
    /// <param name="requiredRepeats">How many repeats finish the task.</param>
    /// <param name="repeats">How many repeats are done.</param>
    /// <param name="status">Whether the task is running, finished or claimed.</param>
    /// <param name="secondsLeftAtArrival">
    /// The lifetime left when the hotel sent this, in seconds. Negative means the hotel considers it
    /// expired. Use <see cref="SecondsLeft"/> rather than this, which does not tick down.
    /// </param>
    /// <param name="receivedAt">When this arrived, used to age <see cref="SecondsLeftAtArrival"/>.</param>
    /// <param name="rewards">What finishing the task pays out.</param>
    public DailyTask(
        long taskId,
        string taskCode,
        string questTypeCode,
        bool isBonus,
        string imageVersion,
        string catalogName,
        int requiredRepeats,
        int repeats,
        DailyTaskStatus status,
        int secondsLeftAtArrival,
        DateTimeOffset receivedAt,
        IReadOnlyList<DailyTaskReward> rewards)
    {
        TaskId = taskId;
        TaskCode = taskCode;
        QuestTypeCode = questTypeCode;
        IsBonus = isBonus;
        ImageVersion = imageVersion;
        CatalogName = catalogName;
        RequiredRepeats = requiredRepeats;
        Repeats = repeats;
        Status = status;
        SecondsLeftAtArrival = secondsLeftAtArrival;
        ReceivedAt = receivedAt;
        Rewards = rewards;
    }

    /// <summary>Gets the task's identifier.</summary>
    public long TaskId { get; init; }

    /// <summary>Gets the task's code, which keys its localized name.</summary>
    public string TaskCode
    {
        get => _task_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(TaskCode));
            _task_code = value;
        }
    }

    /// <summary>Gets the underlying quest type, shared with the quest system.</summary>
    public string QuestTypeCode
    {
        get => _quest_type_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(QuestTypeCode));
            _quest_type_code = value;
        }
    }

    /// <summary>Gets whether this is the bonus task, which the client styles differently.</summary>
    public bool IsBonus { get; init; }

    /// <summary>Gets the cache busting suffix for the task's artwork.</summary>
    public string ImageVersion
    {
        get => _image_version;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ImageVersion));
            _image_version = value;
        }
    }

    /// <summary>Gets the catalog page the task points at, empty when it points nowhere.</summary>
    public string CatalogName
    {
        get => _catalog_name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(CatalogName));
            _catalog_name = value;
        }
    }

    /// <summary>Gets how many repeats finish the task.</summary>
    public int RequiredRepeats { get; init; }

    /// <summary>Gets how many repeats are done.</summary>
    public int Repeats { get; init; }

    /// <summary>Gets whether the task is running, finished or claimed.</summary>
    public DailyTaskStatus Status { get; init; }

    /// <summary>Gets the lifetime left when the hotel sent the task, in seconds.</summary>
    /// <remarks>
    /// Negative means the hotel considers it expired. The value does not tick down, so use
    /// <see cref="SecondsLeft"/> for the current countdown.
    /// </remarks>
    public int SecondsLeftAtArrival { get; init; }

    /// <summary>Gets when the task arrived, used to age <see cref="SecondsLeftAtArrival"/>.</summary>
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>Gets what finishing the task pays out, as a read only copy.</summary>
    public IReadOnlyList<DailyTaskReward> Rewards
    {
        get => _rewards;
        init => _rewards = DailyTaskWire.FreezeReferences(value, nameof(Rewards));
    }

    /// <summary>Deconstructs the task into its parts.</summary>
    /// <param name="taskId">The task's identifier.</param>
    /// <param name="taskCode">The task's code.</param>
    /// <param name="questTypeCode">The underlying quest type.</param>
    /// <param name="isBonus">Whether this is the bonus task.</param>
    /// <param name="imageVersion">The cache busting suffix for the task's artwork.</param>
    /// <param name="catalogName">The catalog page the task points at.</param>
    /// <param name="requiredRepeats">How many repeats finish the task.</param>
    /// <param name="repeats">How many repeats are done.</param>
    /// <param name="status">Whether the task is running, finished or claimed.</param>
    /// <param name="secondsLeftAtArrival">The lifetime left when the hotel sent the task, in seconds.</param>
    /// <param name="receivedAt">When the task arrived.</param>
    /// <param name="rewards">What finishing the task pays out.</param>
    public void Deconstruct(
        out long taskId,
        out string taskCode,
        out string questTypeCode,
        out bool isBonus,
        out string imageVersion,
        out string catalogName,
        out int requiredRepeats,
        out int repeats,
        out DailyTaskStatus status,
        out int secondsLeftAtArrival,
        out DateTimeOffset receivedAt,
        out IReadOnlyList<DailyTaskReward> rewards)
    {
        taskId = TaskId;
        taskCode = TaskCode;
        questTypeCode = QuestTypeCode;
        isBonus = IsBonus;
        imageVersion = ImageVersion;
        catalogName = CatalogName;
        requiredRepeats = RequiredRepeats;
        repeats = Repeats;
        status = Status;
        secondsLeftAtArrival = SecondsLeftAtArrival;
        receivedAt = ReceivedAt;
        rewards = Rewards;
    }

    /// <summary>
    /// How long the task has left, counted down from when it arrived.
    /// </summary>
    /// <remarks>
    /// The hotel sends a lifetime rather than a deadline, so the client stamps the arrival time and
    /// subtracts the elapsed seconds on every read. A task that arrived with no lifetime reports
    /// zero instead of counting into the negative.
    /// </remarks>
    public int SecondsLeft
    {
        get
        {
            if (SecondsLeftAtArrival <= 0)
                return 0;
            long elapsed = (long)(DateTimeOffset.UtcNow - ReceivedAt).TotalSeconds;
            return (int)(SecondsLeftAtArrival - elapsed);
        }
    }

    /// <summary>
    /// Gets whether the hotel already regarded the task as expired when it sent it.
    /// </summary>
    /// <remarks>
    /// This is the arrival-time lifetime, not the ticking one: a task whose countdown has merely
    /// run out on screen is not expired, and an unstarted task never is.
    /// </remarks>
    public bool IsExpired => SecondsLeftAtArrival < 0 && Status != DailyTaskStatus.InProgress;

    /// <summary>Gets whether the task is finished and the reward has not been taken yet.</summary>
    public bool IsClaimable => Status == DailyTaskStatus.Completed;

    /// <summary>Parses a daily task from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>The parser stamps <see cref="ReceivedAt"/> with the current UTC time.</remarks>
    public static DailyTask Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DailyTask ParseFlash(in PacketReader p)
    {
        var strings = DailyTaskWire.NewStringBudget();
        int reward_count = 0;
        DailyTask value = ParseWire(in p, 0, ref strings, ref reward_count);
        DailyTaskWire.RequireEmpty(in p, nameof(DailyTask));
        return value;
    }

    /// <summary>Composes the daily task into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTask value, in PacketWriter p)
    {
        var strings = DailyTaskWire.NewStringBudget();
        int reward_count = 0;
        DailyTaskWireSnapshot snapshot = PrepareWire(
            value,
            ref strings,
            ref reward_count,
            in p);
        WriteWire(snapshot, in p);
    }

    internal static DailyTask ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref DailyTaskStringBudget strings,
        ref int reward_count)
    {
        DailyTaskWire.RequireRemaining(
            in p,
            DailyTaskWire.TaskMinimumBytes,
            trailing_bytes,
            nameof(DailyTask));
        long task_id = p.ReadLong();
        string task_code = strings.Read(
            in p,
            nameof(TaskCode),
            checked(trailing_bytes + 24));
        string quest_type_code = strings.Read(
            in p,
            nameof(QuestTypeCode),
            checked(trailing_bytes + 22));
        bool is_bonus = p.ReadBool();
        string image_version = strings.Read(
            in p,
            nameof(ImageVersion),
            checked(trailing_bytes + 19));
        string catalog_name = strings.Read(
            in p,
            nameof(CatalogName),
            checked(trailing_bytes + 17));
        int required_repeats = p.ReadInt();
        int repeats = p.ReadInt();
        var status = (DailyTaskStatus)p.ReadByte();
        int seconds_left = p.ReadInt();
        int count = DailyTaskWire.ReadCount(
            in p,
            DailyTaskWire.RewardMinimumBytes,
            trailing_bytes,
            nameof(Rewards));
        reward_count = checked(reward_count + count);
        DailyTaskWire.RequireCount(reward_count, nameof(Rewards));
        var rewards = new DailyTaskReward[count];
        for (int index = 0; index < rewards.Length; index++)
        {
            int sibling_bytes = checked(
                trailing_bytes
                + (rewards.Length - index - 1) * DailyTaskWire.RewardMinimumBytes);
            rewards[index] = DailyTaskReward.ParseWire(in p, sibling_bytes, ref strings);
        }
        return new DailyTask(
            task_id,
            task_code,
            quest_type_code,
            is_bonus,
            image_version,
            catalog_name,
            required_repeats,
            repeats,
            status,
            seconds_left,
            DateTimeOffset.UtcNow,
            rewards);
    }

    internal static DailyTaskWireSnapshot PrepareWire(
        DailyTask value,
        ref DailyTaskStringBudget strings,
        ref int reward_count,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        IReadOnlyList<DailyTaskReward> values = value.Rewards;
        int count = DailyTaskWire.RequireListCount(values, nameof(value.Rewards));
        reward_count = checked(reward_count + count);
        DailyTaskWire.RequireCount(reward_count, nameof(value.Rewards));
        strings.Require(value.TaskCode, nameof(TaskCode), in p);
        strings.Require(value.QuestTypeCode, nameof(QuestTypeCode), in p);
        strings.Require(value.ImageVersion, nameof(ImageVersion), in p);
        strings.Require(value.CatalogName, nameof(CatalogName), in p);
        var rewards = new DailyTaskRewardWireSnapshot[count];
        for (int index = 0; index < rewards.Length; index++)
            rewards[index] = DailyTaskReward.PrepareWire(values[index], ref strings, in p);
        return new DailyTaskWireSnapshot(
            value.TaskId,
            value.TaskCode,
            value.QuestTypeCode,
            value.IsBonus,
            value.ImageVersion,
            value.CatalogName,
            value.RequiredRepeats,
            value.Repeats,
            value.Status,
            value.SecondsLeftAtArrival,
            rewards);
    }

    internal static void WriteWire(DailyTaskWireSnapshot value, in PacketWriter p)
    {
        p.WriteLong(value.TaskId);
        p.WriteString(value.TaskCode);
        p.WriteString(value.QuestTypeCode);
        p.WriteBool(value.IsBonus);
        p.WriteString(value.ImageVersion);
        p.WriteString(value.CatalogName);
        p.WriteInt(value.RequiredRepeats);
        p.WriteInt(value.Repeats);
        p.WriteByte((byte)value.Status);
        p.WriteInt(value.SecondsLeftAtArrival);
        p.WriteInt(value.Rewards.Length);
        foreach (DailyTaskRewardWireSnapshot reward in value.Rewards)
            DailyTaskReward.WriteWire(reward, in p);
    }
}

internal readonly record struct DailyTaskWireSnapshot(
    long TaskId,
    string TaskCode,
    string QuestTypeCode,
    bool IsBonus,
    string ImageVersion,
    string CatalogName,
    int RequiredRepeats,
    int Repeats,
    DailyTaskStatus Status,
    int SecondsLeftAtArrival,
    DailyTaskRewardWireSnapshot[] Rewards);

/// <summary>
/// Represents the <c>DailyTasksActiveList</c> message, received with the daily tasks currently running in answer to
/// a request.
/// </summary>
public sealed record DailyTasksActiveList : IParserComposer<DailyTasksActiveList>
{
    private IReadOnlyList<DailyTask> _tasks = Array.AsReadOnly(Array.Empty<DailyTask>());

    /// <summary>Initializes a new instance of the <see cref="DailyTasksActiveList"/> record.</summary>
    /// <param name="tasks">The active tasks.</param>
    public DailyTasksActiveList(IReadOnlyList<DailyTask> tasks)
    {
        Tasks = tasks;
    }

    /// <summary>Gets the active tasks, as a read only copy.</summary>
    public IReadOnlyList<DailyTask> Tasks
    {
        get => _tasks;
        init => _tasks = DailyTaskWire.FreezeReferences(value, nameof(Tasks));
    }

    /// <summary>Deconstructs the message into its tasks.</summary>
    /// <param name="tasks">The active tasks.</param>
    public void Deconstruct(out IReadOnlyList<DailyTask> tasks)
    {
        tasks = Tasks;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DailyTasksActiveList Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DailyTasksActiveList ParseFlash(in PacketReader p) =>
        new(DailyTaskListWire.Parse(in p, nameof(DailyTasksActiveList)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTasksActiveList value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        DailyTaskWireSnapshot[] tasks = DailyTaskListWire.Prepare(value.Tasks, in p);
        DailyTaskListWire.Write(tasks, in p);
    }
}

/// <summary>
/// Represents the <c>DailyTasksTasksAdded</c> message, received when the hotel adds tasks to the running set without
/// being asked.
/// </summary>
public sealed record DailyTasksTasksAdded : IParserComposer<DailyTasksTasksAdded>
{
    private IReadOnlyList<DailyTask> _tasks = Array.AsReadOnly(Array.Empty<DailyTask>());

    /// <summary>Initializes a new instance of the <see cref="DailyTasksTasksAdded"/> record.</summary>
    /// <param name="tasks">The added tasks.</param>
    public DailyTasksTasksAdded(IReadOnlyList<DailyTask> tasks)
    {
        Tasks = tasks;
    }

    /// <summary>Gets the added tasks, as a read only copy.</summary>
    public IReadOnlyList<DailyTask> Tasks
    {
        get => _tasks;
        init => _tasks = DailyTaskWire.FreezeReferences(value, nameof(Tasks));
    }

    /// <summary>Deconstructs the message into its tasks.</summary>
    /// <param name="tasks">The added tasks.</param>
    public void Deconstruct(out IReadOnlyList<DailyTask> tasks)
    {
        tasks = Tasks;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DailyTasksTasksAdded Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DailyTasksTasksAdded ParseFlash(in PacketReader p) =>
        new(DailyTaskListWire.Parse(in p, nameof(DailyTasksTasksAdded)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTasksTasksAdded value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        DailyTaskWireSnapshot[] tasks = DailyTaskListWire.Prepare(value.Tasks, in p);
        DailyTaskListWire.Write(tasks, in p);
    }
}

internal static class DailyTaskListWire
{
    public static IReadOnlyList<DailyTask> Parse(in PacketReader p, string name)
    {
        int count = DailyTaskWire.ReadCount(
            in p,
            DailyTaskWire.TaskMinimumBytes,
            0,
            nameof(DailyTask));
        var strings = DailyTaskWire.NewStringBudget();
        int reward_count = 0;
        var tasks = new DailyTask[count];
        for (int index = 0; index < tasks.Length; index++)
        {
            int sibling_bytes = checked(
                (tasks.Length - index - 1) * DailyTaskWire.TaskMinimumBytes);
            tasks[index] = DailyTask.ParseWire(
                in p,
                sibling_bytes,
                ref strings,
                ref reward_count);
        }
        DailyTaskWire.RequireEmpty(in p, name);
        return tasks;
    }

    public static DailyTaskWireSnapshot[] Prepare(
        IReadOnlyList<DailyTask> values,
        in PacketWriter p)
    {
        int count = DailyTaskWire.RequireListCount(values, nameof(values));
        var strings = DailyTaskWire.NewStringBudget();
        int reward_count = 0;
        var tasks = new DailyTaskWireSnapshot[count];
        for (int index = 0; index < tasks.Length; index++)
        {
            tasks[index] = DailyTask.PrepareWire(
                values[index],
                ref strings,
                ref reward_count,
                in p);
        }
        return tasks;
    }

    public static void Write(DailyTaskWireSnapshot[] tasks, in PacketWriter p)
    {
        DailyTaskWire.WriteCount(tasks.Length, in p);
        foreach (DailyTaskWireSnapshot task in tasks)
            DailyTask.WriteWire(task, in p);
    }
}

/// <summary>
/// Represents the <c>DailyTasksTaskUpdate</c> message, received with progress on one running task.
/// </summary>
/// <remarks>
/// Carries only the mutable fields, so it is an update to an already known task rather than a
/// replacement for it.
/// </remarks>
/// <param name="TaskId">Which task changed.</param>
/// <param name="Repeats">The new repeat count.</param>
/// <param name="Status">The new status.</param>
/// <param name="SecondsLeftAtArrival">The lifetime left when this was sent, in seconds.</param>
public sealed record DailyTasksTaskUpdate(
    long TaskId,
    int Repeats,
    DailyTaskStatus Status,
    int SecondsLeftAtArrival) : IParserComposer<DailyTasksTaskUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DailyTasksTaskUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DailyTasksTaskUpdate ParseFlash(in PacketReader p)
    {
        DailyTaskWire.RequireRemaining(
            in p,
            sizeof(long) + sizeof(int) * 2 + sizeof(byte),
            0,
            nameof(DailyTasksTaskUpdate));
        var value = new DailyTasksTaskUpdate(
            p.ReadLong(),
            p.ReadInt(),
            (DailyTaskStatus)p.ReadByte(),
            p.ReadInt());
        DailyTaskWire.RequireEmpty(in p, nameof(DailyTasksTaskUpdate));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DailyTasksTaskUpdate value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteLong(value.TaskId);
        p.WriteInt(value.Repeats);
        p.WriteByte((byte)value.Status);
        p.WriteInt(value.SecondsLeftAtArrival);
    }
}
