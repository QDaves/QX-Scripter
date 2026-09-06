using Qx.Messages;

namespace Qx.Model.Wired;

// Shared furni-type descriptor (§_-dR§/ChestItemType). Empty legacyPosterId is normalised
// to null on read and back to "" on write, so storing the raw string round-trips byte-exact.
public sealed record ChestItemType(bool IsWallItem, int TypeId, string LegacyPosterId)
    : IParserComposer<ChestItemType>
{
    public static ChestItemType Parse(in PacketReader p) =>
        new(p.ReadBool(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteBool(IsWallItem);
        p.WriteInt(TypeId);
        p.WriteString(LegacyPosterId);
    }
}

// §_-dR§/ChestStorage — one furni slot inside a chest. `extra` is only on the wire for floor items.
public sealed record ChestStorage(
    int InventoryId,
    int LockState,
    long TransactionId,
    ChestItemType Type,
    bool Groupable,
    int SpecialType,
    ItemData StuffData,
    int Extra) : IParserComposer<ChestStorage>
{
    public static ChestStorage Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ChestStorage ParseFlash(in PacketReader p) => ParseStorage(in p, false);

    private static ChestStorage ParseUnity(in PacketReader p) => ParseStorage(in p, true);

    private static ChestStorage ParseStorage(in PacketReader p, bool unity)
    {
        int inventoryId = unity ? checked((int)p.ReadLong()) : p.ReadInt();
        int lockState = p.ReadInt();
        long transactionId = p.ReadLong();
        ChestItemType type = ChestItemType.Parse(p);
        bool groupable = p.ReadBool();
        int specialType = p.ReadInt();
        ItemData stuffData = p.Parse<ItemData>();
        int extra = type.IsWallItem
            ? 0
            : unity
                ? checked((int)p.ReadLong())
                : p.ReadInt();
        return new ChestStorage(inventoryId, lockState, transactionId, type, groupable, specialType, stuffData, extra);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ChestStorage value, in PacketWriter p) =>
        value.ComposeStorage(in p, false);

    private static void ComposeUnity(ChestStorage value, in PacketWriter p) =>
        value.ComposeStorage(in p, true);

    private void ComposeStorage(in PacketWriter p, bool unity)
    {
        if (unity)
            p.WriteLong(InventoryId);
        else
            p.WriteInt(InventoryId);
        p.WriteInt(LockState);
        p.WriteLong(TransactionId);
        Type.Compose(p);
        p.WriteBool(Groupable);
        p.WriteInt(SpecialType);
        p.Compose(StuffData);
        if (!Type.IsWallItem)
        {
            if (unity)
                p.WriteLong(Extra);
            else
                p.WriteInt(Extra);
        }
    }
}

// id 1174
public sealed record OpenChest(int ChestId) : IParserComposer<OpenChest>
{
    public static OpenChest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static OpenChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenChest value, in PacketWriter p) => p.WriteInt(value.ChestId);
}

// id 1022
public sealed record CoinsChestContents(int ChestId, int Coins, bool IsUpdate)
    : IParserComposer<CoinsChestContents>
{
    public static CoinsChestContents Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static CoinsChestContents ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(CoinsChestContents value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.Coins);
        p.WriteBool(value.IsUpdate);
    }
}

// id 2323
public sealed record ItemsChestContentsChunk(
    int ChestId,
    int TotalFragments,
    int FragmentNo,
    IReadOnlyList<ChestStorage> StorageChunk) : IParserComposer<ItemsChestContentsChunk>
{
    public static ItemsChestContentsChunk Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ItemsChestContentsChunk ParseFlash(in PacketReader p)
    {
        int chestId = p.ReadInt();
        int totalFragments = p.ReadInt();
        int fragmentNo = p.ReadInt();
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 32, nameof(StorageChunk));
        var chunk = new ChestStorage[n];
        for (int i = 0; i < n; i++)
            chunk[i] = ChestStorage.Parse(p);
        return new ItemsChestContentsChunk(chestId, totalFragments, fragmentNo, chunk);
    }

    private static ItemsChestContentsChunk ParseUnity(in PacketReader p)
    {
        int chest_id = checked((int)p.ReadLong());
        int total_fragments = p.ReadInt();
        int fragment_no = p.ReadInt();
        int count = unchecked((ushort)p.ReadShort());
        WiredWire.RequireBoundedCount(count, p.Available, 32, nameof(StorageChunk));
        var storage = new ChestStorage[count];
        for (int i = 0; i < storage.Length; i++)
            storage[i] = ChestStorage.Parse(p);
        return new ItemsChestContentsChunk(chest_id, total_fragments, fragment_no, storage);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ItemsChestContentsChunk value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.StorageChunk, in p, false);
        p.WriteInt(value.ChestId);
        p.WriteInt(value.TotalFragments);
        p.WriteInt(value.FragmentNo);
        p.WriteInt(value.StorageChunk.Count);
        foreach (ChestStorage s in value.StorageChunk)
            s.Compose(p);
    }

    private static void ComposeUnity(ItemsChestContentsChunk value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.StorageChunk, in p, true);
        WiredWire.RequireUnityCount(value.StorageChunk.Count, nameof(value.StorageChunk));
        p.WriteLong(value.ChestId);
        p.WriteInt(value.TotalFragments);
        p.WriteInt(value.FragmentNo);
        p.WriteShort(unchecked((short)(ushort)value.StorageChunk.Count));
        foreach (ChestStorage storage in value.StorageChunk)
            storage.Compose(p);
    }
}

// id 2738
public sealed record ItemsChestContentsUpdated(
    int ChestId,
    IReadOnlyList<int> RemovedIds,
    IReadOnlyList<ChestStorage> AddedStorage) : IParserComposer<ItemsChestContentsUpdated>
{
    public static ItemsChestContentsUpdated Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ItemsChestContentsUpdated ParseFlash(in PacketReader p)
    {
        int chestId = p.ReadInt();
        int[] removed = WiredIo.IntArray(p);
        int added = p.ReadInt();
        WiredWire.RequireBoundedCount(added, p.Available, 32, nameof(AddedStorage));
        var storage = new ChestStorage[added];
        for (int i = 0; i < added; i++)
            storage[i] = ChestStorage.Parse(p);
        return new ItemsChestContentsUpdated(chestId, removed, storage);
    }

    private static ItemsChestContentsUpdated ParseUnity(in PacketReader p)
    {
        int chest_id = checked((int)p.ReadLong());
        int removed_count = unchecked((ushort)p.ReadShort());
        if (p.Available < sizeof(short))
            throw new InvalidDataException("RemovedIds leaves no room for the added-storage count.");
        WiredWire.RequireBoundedCount(
            removed_count,
            p.Available - sizeof(short),
            sizeof(int),
            nameof(RemovedIds));
        var removed = new int[removed_count];
        for (int i = 0; i < removed.Length; i++)
            removed[i] = p.ReadInt();

        int added_count = unchecked((ushort)p.ReadShort());
        WiredWire.RequireBoundedCount(added_count, p.Available, 32, nameof(AddedStorage));
        var added = new ChestStorage[added_count];
        for (int i = 0; i < added.Length; i++)
            added[i] = ChestStorage.Parse(p);
        return new ItemsChestContentsUpdated(chest_id, removed, added);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ItemsChestContentsUpdated value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RemovedIds);
        WiredWire.RequireUnityCount(value.RemovedIds.Count, nameof(value.RemovedIds));
        WiredChestWire.Validate(value.AddedStorage, in p, false);
        p.WriteInt(value.ChestId);
        WiredIo.WriteIntArray(p, value.RemovedIds);
        p.WriteInt(value.AddedStorage.Count);
        foreach (ChestStorage s in value.AddedStorage)
            s.Compose(p);
    }

    private static void ComposeUnity(ItemsChestContentsUpdated value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RemovedIds);
        WiredWire.RequireUnityCount(value.RemovedIds.Count, nameof(value.RemovedIds));
        WiredChestWire.Validate(value.AddedStorage, in p, true);
        WiredWire.RequireUnityCount(value.AddedStorage.Count, nameof(value.AddedStorage));
        p.WriteLong(value.ChestId);
        p.WriteShort(unchecked((short)(ushort)value.RemovedIds.Count));
        foreach (int removed_id in value.RemovedIds)
            p.WriteInt(removed_id);
        p.WriteShort(unchecked((short)(ushort)value.AddedStorage.Count));
        foreach (ChestStorage storage in value.AddedStorage)
            storage.Compose(p);
    }
}

// id 2721
public sealed record UpgradeChestResult(int ChestId, int ResultCode) : IParserComposer<UpgradeChestResult>
{
    public const int Success = 0;

    public static UpgradeChestResult Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static UpgradeChestResult ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(UpgradeChestResult value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.ResultCode);
    }
}

// id 1957
public sealed record ChestPreferencesUpdateSuccess(int ChestId, bool IsNotificationPreferences)
    : IParserComposer<ChestPreferencesUpdateSuccess>
{
    public static ChestPreferencesUpdateSuccess Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static ChestPreferencesUpdateSuccess ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(ChestPreferencesUpdateSuccess value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteBool(value.IsNotificationPreferences);
    }
}

// TradeRequirement tree — shared by contracts, transactions and trades.

// §_-o1t§/TradeRequirementNode — `type` is a single byte on the wire; itemType only for furni nodes.
public sealed record TradeRequirementNode(int Type, int Amount, ChestItemType? ItemType)
    : IParserComposer<TradeRequirementNode>
{
    public const int TypeCoin = 0;
    public const int TypeFurni = 1;

    public static TradeRequirementNode Parse(in PacketReader p)
    {
        int type = p.ReadByte();
        int amount = p.ReadInt();
        ChestItemType? itemType = type == TypeFurni ? ChestItemType.Parse(p) : null;
        return new TradeRequirementNode(type, amount, itemType);
    }

    public void Compose(in PacketWriter p)
    {
        byte type = checked((byte)Type);
        ChestItemType? item_type = ItemType;
        if (Type == TypeFurni)
        {
            if (item_type is null)
                throw new InvalidDataException("Furni trade requirement nodes need an item type.");
            WiredChestWire.Validate(item_type, in p);
        }
        else if (item_type is not null)
            throw new InvalidDataException("Only furni trade requirement nodes can carry an item type.");
        p.WriteByte(type);
        p.WriteInt(Amount);
        if (item_type is not null)
            item_type.Compose(p);
    }
}

public sealed record TradeRequirementRule(IReadOnlyList<TradeRequirementNode> Nodes)
    : IParserComposer<TradeRequirementRule>
{
    public static TradeRequirementRule Parse(in PacketReader p)
    {
        int n = p.ReadLength();
        var nodes = new TradeRequirementNode[n];
        for (int i = 0; i < n; i++)
            nodes[i] = TradeRequirementNode.Parse(p);
        return new TradeRequirementRule(nodes);
    }

    public void Compose(in PacketWriter p)
    {
        WiredChestWire.Validate(this, in p);
        p.WriteLength((Length)Nodes.Count);
        foreach (TradeRequirementNode node in Nodes)
            node.Compose(p);
    }
}

// null lists/rules encode the presence bool as false with no payload.
public sealed record TradeRequirementRulesDefinition(
    IReadOnlyList<TradeRequirementRule>? YouGiveRule,
    TradeRequirementRule? YouGetRule) : IParserComposer<TradeRequirementRulesDefinition>
{
    public static TradeRequirementRulesDefinition Parse(in PacketReader p)
    {
        TradeRequirementRule[]? give = null;
        if (p.ReadBool())
        {
            int n = p.ReadLength();
            give = new TradeRequirementRule[n];
            for (int i = 0; i < n; i++)
                give[i] = TradeRequirementRule.Parse(p);
        }
        TradeRequirementRule? get = p.ReadBool() ? TradeRequirementRule.Parse(p) : null;
        return new TradeRequirementRulesDefinition(give, get);
    }

    public void Compose(in PacketWriter p)
    {
        WiredChestWire.Validate(this, in p);
        p.WriteBool(YouGiveRule is not null);
        if (YouGiveRule is not null)
        {
            p.WriteLength((Length)YouGiveRule.Count);
            foreach (TradeRequirementRule rule in YouGiveRule)
                rule.Compose(p);
        }
        p.WriteBool(YouGetRule is not null);
        YouGetRule?.Compose(p);
    }
}

// TradeRequirementRules — multiplier is only present for type 1, autoMultiplierMax only for type 2.
public sealed record TradeRequirementRules(
    TradeRequirementRulesDefinition Definition,
    int Type,
    int Multiplier,
    int AutoMultiplierMax) : IParserComposer<TradeRequirementRules>
{
    public const int TypeNone = 0;
    public const int TypeFixedMultiplier = 1;
    public const int TypeAutoMultiplier = 2;

    public static TradeRequirementRules Parse(in PacketReader p)
    {
        TradeRequirementRulesDefinition definition = TradeRequirementRulesDefinition.Parse(p);
        int type = p.ReadInt();
        int multiplier = 1;
        int autoMultiplierMax = 1;
        if (type == TypeFixedMultiplier)
            multiplier = p.ReadInt();
        else if (type == TypeAutoMultiplier)
            autoMultiplierMax = p.ReadInt();
        return new TradeRequirementRules(definition, type, multiplier, autoMultiplierMax);
    }

    public void Compose(in PacketWriter p)
    {
        WiredChestWire.Validate(this, in p);
        Definition.Compose(p);
        p.WriteInt(Type);
        if (Type == TypeFixedMultiplier)
            p.WriteInt(Multiplier);
        else if (Type == TypeAutoMultiplier)
            p.WriteInt(AutoMultiplierMax);
    }
}

public sealed record TradeRequirement(
    int Type,
    string YouGetText,
    string LayoutType,
    TradeRequirementRules? Rules) : IParserComposer<TradeRequirement>
{
    public const int TypeWithRules = 4;

    public static TradeRequirement Parse(in PacketReader p)
    {
        int type = p.ReadInt();
        string youGetText = p.ReadString();
        string layoutType = p.ReadString();
        TradeRequirementRules? rules = type == TypeWithRules ? TradeRequirementRules.Parse(p) : null;
        return new TradeRequirement(type, youGetText, layoutType, rules);
    }

    public void Compose(in PacketWriter p)
    {
        WiredChestWire.Validate(this, in p);
        TradeRequirementRules? rules = Rules;
        if (Type == TypeWithRules && rules is null)
            throw new InvalidDataException("Rule-backed trade requirements need rules.");
        p.WriteInt(Type);
        p.WriteString(YouGetText);
        p.WriteString(LayoutType);
        if (rules is not null)
            rules.Compose(p);
    }
}

// Transactions.

// §_-k1f§/WiredTransactionInfo — 13-field log row, transactionId and timestamp are 64-bit.
public sealed record WiredTransactionInfo(
    long TransactionId,
    int FlatId,
    int TransactionType,
    string TransactionDefinitionInfo,
    int UserId,
    string UserName,
    long Timestamp,
    string ReadableTimestamp,
    int ChestCount,
    int WithdrawFurniCount,
    int DepositFurniCount,
    int WithdrawCoinsCount,
    int DepositCoinsCount) : IParserComposer<WiredTransactionInfo>
{
    public static WiredTransactionInfo Parse(in PacketReader p) => new(
        p.ReadLong(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadString(),
        p.ReadInt(),
        p.ReadString(),
        p.ReadLong(),
        p.ReadString(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteLong(TransactionId);
        p.WriteInt(FlatId);
        p.WriteInt(TransactionType);
        p.WriteString(TransactionDefinitionInfo);
        p.WriteInt(UserId);
        p.WriteString(UserName);
        p.WriteLong(Timestamp);
        p.WriteString(ReadableTimestamp);
        p.WriteInt(ChestCount);
        p.WriteInt(WithdrawFurniCount);
        p.WriteInt(DepositFurniCount);
        p.WriteInt(WithdrawCoinsCount);
        p.WriteInt(DepositCoinsCount);
    }
}

// §_-c1v§ — the paged log container carried by WiredTransactionLogList.
public sealed record WiredTransactionLogPage(
    int LogListType,
    long LogListId,
    int TotalLogs,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredTransactionInfo> Logs) : IParserComposer<WiredTransactionLogPage>
{
    public const int TypeChestLogs = 0;
    public const int TypeRoomLogs = 1;

    public static WiredTransactionLogPage Parse(in PacketReader p)
    {
        int logListType = p.ReadInt();
        long logListId = p.ReadLong();
        int totalLogs = p.ReadInt();
        int currentPage = p.ReadInt();
        int amount = p.ReadInt();
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 54, nameof(Logs));
        var logs = new WiredTransactionInfo[n];
        for (int i = 0; i < n; i++)
            logs[i] = WiredTransactionInfo.Parse(p);
        return new WiredTransactionLogPage(logListType, logListId, totalLogs, currentPage, amount, logs);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(LogListType);
        p.WriteLong(LogListId);
        p.WriteInt(TotalLogs);
        p.WriteInt(CurrentPage);
        p.WriteInt(Amount);
        p.WriteInt(Logs.Count);
        foreach (WiredTransactionInfo info in Logs)
            info.Compose(p);
    }
}

// id 2910
public sealed record WiredTransactionLogList(WiredTransactionLogPage Logs)
    : IParserComposer<WiredTransactionLogList>
{
    public static WiredTransactionLogList Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredTransactionLogList ParseFlash(in PacketReader p) =>
        new(WiredTransactionLogPage.Parse(p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionLogList value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.Logs, in p);
        value.Logs.Compose(p);
    }
}

// One (furni-type, count) entry inside a transaction's deposited/withdrawn lists.
public sealed record ChestFurniCount(ChestItemType Type, int Count) : IParserComposer<ChestFurniCount>
{
    public static ChestFurniCount Parse(in PacketReader p) => new(ChestItemType.Parse(p), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        Type.Compose(p);
        p.WriteInt(Count);
    }
}

// §_-k1f§/WiredTransactionDetails
public sealed record WiredTransactionDetails(
    WiredTransactionInfo TransactionInfo,
    IReadOnlyList<int> ChestIds,
    IReadOnlyList<ChestFurniCount> DepositedFurnis,
    IReadOnlyList<ChestFurniCount> WithdrawnFurnis,
    bool IsIncompleteData) : IParserComposer<WiredTransactionDetails>
{
    public static WiredTransactionDetails Parse(in PacketReader p)
    {
        WiredTransactionInfo info = WiredTransactionInfo.Parse(p);
        int[] chestIds = WiredIo.IntArray(p);
        var deposited = read_pairs(p);
        var withdrawn = read_pairs(p);
        bool incomplete = p.ReadBool();
        return new WiredTransactionDetails(info, chestIds, deposited, withdrawn, incomplete);
    }

    public void Compose(in PacketWriter p)
    {
        TransactionInfo.Compose(p);
        WiredIo.WriteIntArray(p, ChestIds);
        write_pairs(p, DepositedFurnis);
        write_pairs(p, WithdrawnFurnis);
        p.WriteBool(IsIncompleteData);
    }

    private static ChestFurniCount[] read_pairs(in PacketReader p)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 11, "transaction furni pairs");
        var pairs = new ChestFurniCount[n];
        for (int i = 0; i < n; i++)
            pairs[i] = ChestFurniCount.Parse(p);
        return pairs;
    }

    private static void write_pairs(in PacketWriter p, IReadOnlyList<ChestFurniCount> pairs)
    {
        p.WriteInt(pairs.Count);
        foreach (ChestFurniCount pair in pairs)
            pair.Compose(p);
    }
}

// id 1306
public sealed record WiredTransactionLogDetails(WiredTransactionDetails Details)
    : IParserComposer<WiredTransactionLogDetails>
{
    public static WiredTransactionLogDetails Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredTransactionLogDetails ParseFlash(in PacketReader p) =>
        new(WiredTransactionDetails.Parse(p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionLogDetails value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.Details, in p);
        value.Details.Compose(p);
    }
}

// id 2677 — internalId is a client-side counter and is NOT on the wire. The reward tail is only
// present for success type 2 when trailing bytes remain.
public sealed record WiredTransactionSuccess(
    int TransactionSuccessTypeId,
    TradeRequirementRule? RewardContents,
    string RewardText,
    bool OpenByDefault) : IParserComposer<WiredTransactionSuccess>
{
    public const int TypeReward = 2;
    public bool HasReward => RewardContents is not null;

    public static WiredTransactionSuccess Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTransactionSuccess ParseFlash(in PacketReader p)
    {
        int type = p.ReadInt();
        TradeRequirementRule? reward = null;
        string rewardText = "";
        bool openByDefault = false;
        bool hasReward = type == TypeReward && p.Available > 0;
        if (hasReward)
        {
            reward = TradeRequirementRule.Parse(p);
            rewardText = p.ReadString();
            openByDefault = p.ReadBool();
        }
        return new WiredTransactionSuccess(type, reward, rewardText, openByDefault);
    }

    private static WiredTransactionSuccess ParseUnity(in PacketReader p)
    {
        int type = p.ReadInt();
        bool has_reward = type == TypeReward && p.ReadBool();
        TradeRequirementRule? reward = has_reward ? TradeRequirementRule.Parse(in p) : null;
        string reward_text = has_reward ? p.ReadString() : "";
        bool open_by_default = has_reward && p.ReadBool();
        return new WiredTransactionSuccess(type, reward, reward_text, open_by_default);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTransactionSuccess value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.TransactionSuccessTypeId);
        if (value.TransactionSuccessTypeId == TypeReward && value.HasReward)
            WriteReward(value, in p);
    }

    private static void ComposeUnity(WiredTransactionSuccess value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.TransactionSuccessTypeId);
        if (value.TransactionSuccessTypeId != TypeReward)
            return;
        p.WriteBool(value.HasReward);
        if (value.HasReward)
            WriteReward(value, in p);
    }

    private static void WriteReward(WiredTransactionSuccess value, in PacketWriter p)
    {
        TradeRequirementRule reward_contents = value.RewardContents ??
            throw new InvalidDataException("Reward contents are missing.");
        reward_contents.Compose(in p);
        p.WriteString(value.RewardText);
        p.WriteBool(value.OpenByDefault);
    }

    private static void Validate(WiredTransactionSuccess value, in PacketWriter p)
    {
        if (value.TransactionSuccessTypeId != TypeReward && value.HasReward)
            throw new InvalidDataException("Only reward transactions can carry reward contents.");
        if (value.HasReward)
        {
            ArgumentNullException.ThrowIfNull(value.RewardContents);
            WiredChestWire.Validate(value.RewardContents, in p);
            WiredWire.RequireString(value.RewardText, nameof(RewardText), in p);
        }
    }
}

// Contracts.

// id 2976 — discriminated by ContractType (short). WiredUpdateContract(1908) writes this exact layout.
public sealed record WiredContractContents(
    int ContractId,
    short ContractType,
    TradeRequirementRulesDefinition Definition,
    short PaymentMode,
    string ReceiveText,
    string LayoutType,
    short RewardCategory,
    bool ShowDialog,
    string RewardText) : IParserComposer<WiredContractContents>
{
    public const int TypePayment = 0;
    public const int TypeTrade = 1;
    public const int TypeReward = 2;

    public static WiredContractContents Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredContractContents ParseFlash(in PacketReader p) => Read(in p);

    internal static WiredContractContents Read(in PacketReader p)
    {
        int contractId = p.ReadInt();
        short contractType = p.ReadShort();
        TradeRequirementRulesDefinition definition = TradeRequirementRulesDefinition.Parse(p);
        short paymentMode = 0;
        string receiveText = "";
        string layoutType = "";
        short rewardCategory = 0;
        bool showDialog = false;
        string rewardText = "";
        if (contractType == TypePayment)
        {
            paymentMode = p.ReadShort();
            receiveText = p.ReadString();
            layoutType = p.ReadString();
        }
        if (contractType == TypeReward)
        {
            rewardCategory = p.ReadShort();
            showDialog = p.ReadBool();
            rewardText = p.ReadString();
        }
        return new WiredContractContents(contractId, contractType, definition,
            paymentMode, receiveText, layoutType, rewardCategory, showDialog, rewardText);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredContractContents value, in PacketWriter p) =>
        Write(value, in p);

    internal static void Write(WiredContractContents value, in PacketWriter p)
    {
        WiredChestWire.Validate(value, in p);
        p.WriteInt(value.ContractId);
        p.WriteShort(value.ContractType);
        value.Definition.Compose(p);
        if (value.ContractType == TypePayment)
        {
            p.WriteShort(value.PaymentMode);
            p.WriteString(value.ReceiveText);
            p.WriteString(value.LayoutType);
        }
        if (value.ContractType == TypeReward)
        {
            p.WriteShort(value.RewardCategory);
            p.WriteBool(value.ShowDialog);
            p.WriteString(value.RewardText);
        }
    }
}

// id 3720
public sealed record WiredContractUpdateResult(int ContractId, bool IsSuccess, string FailCode)
    : IParserComposer<WiredContractUpdateResult>
{
    public static WiredContractUpdateResult Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredContractUpdateResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredContractUpdateResult value, in PacketWriter p)
    {
        WiredWire.RequireString(value.FailCode, nameof(FailCode), in p);
        p.WriteInt(value.ContractId);
        p.WriteBool(value.IsSuccess);
        p.WriteString(value.FailCode);
    }
}

// id 1479 (INCOMING) — server pushes "open this contract editor". Distinct from the OUT id-1594
// message of the same name (a plain contractId composer, wired separately by the coordinator).
public sealed record WiredOpenContract(int ContractId) : IParserComposer<WiredOpenContract>
{
    public static WiredOpenContract Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredOpenContract ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredOpenContract value, in PacketWriter p) =>
        p.WriteInt(value.ContractId);

}

// Trades.

// §_-ru§/§_-z1l§ — the classic two-user trading-window snapshot. Item elements are §_-X12§,
// which is byte-identical to the shared TradeItem parser.
public sealed record WiredTradingItems(
    Id FirstUserId,
    IReadOnlyList<TradeItem> FirstUserItems,
    int FirstUserNumItems,
    int FirstUserNumCredits,
    Id SecondUserId,
    IReadOnlyList<TradeItem> SecondUserItems,
    int SecondUserNumItems,
    int SecondUserNumCredits) : IParserComposer<WiredTradingItems>
{
    public static WiredTradingItems Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradingItems ParseFlash(in PacketReader p)
    {
        Id firstUserId = p.ReadInt();
        TradeItem[] firstItems = read_items(p);
        int firstNumItems = p.ReadInt();
        int firstNumCredits = p.ReadInt();
        Id secondUserId = p.ReadInt();
        TradeItem[] secondItems = read_items(p);
        int secondNumItems = p.ReadInt();
        int secondNumCredits = p.ReadInt();
        return new WiredTradingItems(firstUserId, firstItems, firstNumItems, firstNumCredits,
            secondUserId, secondItems, secondNumItems, secondNumCredits);
    }

    private static WiredTradingItems ParseUnity(in PacketReader p)
    {
        Id first_user_id = p.ReadLong();
        TradeItem[] first_items = read_items(p, true);
        int first_num_items = p.ReadInt();
        int first_num_credits = p.ReadInt();
        Id second_user_id = p.ReadLong();
        TradeItem[] second_items = read_items(p, true);
        int second_num_items = p.ReadInt();
        int second_num_credits = p.ReadInt();
        return new WiredTradingItems(
            first_user_id,
            first_items,
            first_num_items,
            first_num_credits,
            second_user_id,
            second_items,
            second_num_items,
            second_num_credits);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradingItems value, in PacketWriter p)
    {
        WiredChestWire.Validate(value, in p, false);
        int first_user_id = WiredWire.FlashId(value.FirstUserId);
        int second_user_id = WiredWire.FlashId(value.SecondUserId);
        p.WriteInt(first_user_id);
        write_items(p, value.FirstUserItems);
        p.WriteInt(value.FirstUserNumItems);
        p.WriteInt(value.FirstUserNumCredits);
        p.WriteInt(second_user_id);
        write_items(p, value.SecondUserItems);
        p.WriteInt(value.SecondUserNumItems);
        p.WriteInt(value.SecondUserNumCredits);
    }

    private static void ComposeUnity(WiredTradingItems value, in PacketWriter p)
    {
        WiredChestWire.Validate(value, in p, true);
        p.WriteLong(value.FirstUserId);
        write_items(p, value.FirstUserItems);
        p.WriteInt(value.FirstUserNumItems);
        p.WriteInt(value.FirstUserNumCredits);
        p.WriteLong(value.SecondUserId);
        write_items(p, value.SecondUserItems);
        p.WriteInt(value.SecondUserNumItems);
        p.WriteInt(value.SecondUserNumCredits);
    }

    private static TradeItem[] read_items(in PacketReader p, bool unity = false)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(
            n,
            p.Available,
            unity ? TradeWire.UnityTradeItemMinimumBytes : TradeWire.FlashTradeItemMinimumBytes,
            "wired trading items");
        var items = new TradeItem[n];
        for (int i = 0; i < n; i++)
            items[i] = TradeItem.Parse(p);
        return items;
    }

    private static void write_items(in PacketWriter p, IReadOnlyList<TradeItem> items)
    {
        p.WriteInt(items.Count);
        foreach (TradeItem item in items)
            item.Compose(p);
    }

}

// id 3650
public sealed record WiredTradeInitiate(
    TradeRequirement Requirement,
    bool ShowRequirementsImmediate,
    bool OverridePreviousTrade,
    int TimeoutSeconds) : IParserComposer<WiredTradeInitiate>
{
    public static WiredTradeInitiate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeInitiate ParseFlash(in PacketReader p) =>
        new(TradeRequirement.Parse(p), p.ReadBool(), p.ReadBool(), p.ReadInt());

    private static WiredTradeInitiate ParseUnity(in PacketReader p) =>
        new(TradeRequirement.Parse(p), p.ReadBool(), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeInitiate value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.Requirement, in p);
        value.Requirement.Compose(p);
        p.WriteBool(value.ShowRequirementsImmediate);
        p.WriteBool(value.OverridePreviousTrade);
        p.WriteInt(value.TimeoutSeconds);
    }

    private static void ComposeUnity(WiredTradeInitiate value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.Requirement, in p);
        value.Requirement.Compose(p);
        p.WriteBool(value.ShowRequirementsImmediate);
        p.WriteBool(value.OverridePreviousTrade);
        p.WriteInt(value.TimeoutSeconds);
    }
}

// id 2488
public sealed record WiredTradeItemsUpdate(WiredTradingItems TradingItems, bool CanAccept, int Extra)
    : IParserComposer<WiredTradeItemsUpdate>
{
    public static WiredTradeItemsUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeItemsUpdate ParseFlash(in PacketReader p) =>
        new(WiredTradingItems.Parse(p), p.ReadBool(), p.ReadInt());

    private static WiredTradeItemsUpdate ParseUnity(in PacketReader p) =>
        new(WiredTradingItems.Parse(p), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeItemsUpdate value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.TradingItems, in p, false);
        value.TradingItems.Compose(p);
        p.WriteBool(value.CanAccept);
        p.WriteInt(value.Extra);
    }

    private static void ComposeUnity(WiredTradeItemsUpdate value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.TradingItems, in p, true);
        value.TradingItems.Compose(p);
        p.WriteBool(value.CanAccept);
        p.WriteInt(value.Extra);
    }
}

public sealed record WiredTradeCancelled(int TransactionFailureTypeId) : IParserComposer<WiredTradeCancelled>
{
    public static WiredTradeCancelled Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeCancelled ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static WiredTradeCancelled ParseUnity(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeCancelled value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);

    private static void ComposeUnity(WiredTradeCancelled value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);
}

public sealed record WiredTransactionFail(int TransactionFailureTypeId)
    : IParserComposer<WiredTransactionFail>
{
    public static WiredTransactionFail Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTransactionFail ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static WiredTransactionFail ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTransactionFail value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);

    private static void ComposeUnity(WiredTransactionFail value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);
}

public sealed record WiredTradeTransactionNotification(int TradeTransactionNotificationId)
    : IParserComposer<WiredTradeTransactionNotification>
{
    public static WiredTradeTransactionNotification Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeTransactionNotification ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static WiredTradeTransactionNotification ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        WiredTradeTransactionNotification value,
        in PacketWriter p) => p.WriteInt(value.TradeTransactionNotificationId);

    private static void ComposeUnity(
        WiredTradeTransactionNotification value,
        in PacketWriter p) => p.WriteInt(value.TradeTransactionNotificationId);
}

public sealed record WiredTradeCompleted : IParserComposer<WiredTradeCompleted>
{
    public static WiredTradeCompleted Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeCompleted ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCompleted));
        return new WiredTradeCompleted();
    }

    private static WiredTradeCompleted ParseUnity(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCompleted));
        return new WiredTradeCompleted();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeCompleted value, in PacketWriter p) { }

    private static void ComposeUnity(WiredTradeCompleted value, in PacketWriter p) { }
}

// Outgoing composers. Each mirrors the SWF getMessageArray() push order exactly.

// id 806
public sealed record OpenChestAndGetContents(Id ChestId) : IParserComposer<OpenChestAndGetContents>
{
    public static OpenChestAndGetContents Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OpenChestAndGetContents ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static OpenChestAndGetContents ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OpenChestAndGetContents value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));

    private static void ComposeUnity(OpenChestAndGetContents value, in PacketWriter p) =>
        p.WriteLong(value.ChestId);
}

// id 2935
public sealed record CloseChest(Id ChestId) : IParserComposer<CloseChest>
{
    public static CloseChest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CloseChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static CloseChest ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CloseChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));

    private static void ComposeUnity(CloseChest value, in PacketWriter p) =>
        p.WriteLong(value.ChestId);
}

// id 1630
public sealed record LockAllChests(bool Lock, bool ApplyToAllInRoom) : IParserComposer<LockAllChests>
{
    public static LockAllChests Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static LockAllChests ParseFlash(in PacketReader p) => new(p.ReadBool(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(LockAllChests value, in PacketWriter p)
    {
        p.WriteBool(value.Lock);
        p.WriteBool(value.ApplyToAllInRoom);
    }

}

// id 3407
public sealed record UpgradeChest(int ChestId, int UpgradeAmount) : IParserComposer<UpgradeChest>
{
    public static UpgradeChest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static UpgradeChest ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(UpgradeChest value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.UpgradeAmount);
    }

}

// id 3611
public sealed record WithdrawAllFromChest(Id ChestId) : IParserComposer<WithdrawAllFromChest>
{
    public static WithdrawAllFromChest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WithdrawAllFromChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static WithdrawAllFromChest ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WithdrawAllFromChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));

    private static void ComposeUnity(WithdrawAllFromChest value, in PacketWriter p) =>
        p.WriteLong(value.ChestId);
}

// id 2843
public sealed record WithdrawCoinsFromChest(Id ChestId, int CoinAmount) : IParserComposer<WithdrawCoinsFromChest>
{
    public static WithdrawCoinsFromChest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WithdrawCoinsFromChest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static WithdrawCoinsFromChest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WithdrawCoinsFromChest value, in PacketWriter p)
    {
        p.WriteInt(WiredWire.FlashId(value.ChestId));
        p.WriteInt(value.CoinAmount);
    }

    private static void ComposeUnity(WithdrawCoinsFromChest value, in PacketWriter p)
    {
        p.WriteLong(value.ChestId);
        p.WriteInt(value.CoinAmount);
    }
}

// id 873 — ChestItemType expands to bool/int/string between the two ints.
public sealed record WithdrawItemsFromChest(Id ChestId, ChestItemType ItemType, int Count)
    : IParserComposer<WithdrawItemsFromChest>
{
    public static WithdrawItemsFromChest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WithdrawItemsFromChest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), ChestItemType.Parse(p), p.ReadInt());

    private static WithdrawItemsFromChest ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), ChestItemType.Parse(p), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WithdrawItemsFromChest value, in PacketWriter p)
    {
        int chest_id = WiredWire.FlashId(value.ChestId);
        WiredChestWire.Validate(value.ItemType, in p);
        p.WriteInt(chest_id);
        value.ItemType.Compose(p);
        p.WriteInt(value.Count);
    }

    private static void ComposeUnity(WithdrawItemsFromChest value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.ItemType, in p);
        p.WriteLong(value.ChestId);
        value.ItemType.Compose(p);
        p.WriteInt(value.Count);
    }
}

// id 3514
public sealed record StartAddingToChest(Id ChestId) : IParserComposer<StartAddingToChest>
{
    public static StartAddingToChest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static StartAddingToChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static StartAddingToChest ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(StartAddingToChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));

    private static void ComposeUnity(StartAddingToChest value, in PacketWriter p) =>
        p.WriteLong(value.ChestId);
}

// id 2905
public sealed record SetChestNotificationPreferences(
    int ChestId,
    int NotificationMode,
    bool NotifyFlagA,
    bool NotifyFlagB,
    bool EventFlagA,
    bool EventFlagB,
    bool EventFlagC) : IParserComposer<SetChestNotificationPreferences>
{
    public static SetChestNotificationPreferences Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static SetChestNotificationPreferences ParseFlash(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(SetChestNotificationPreferences value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.NotificationMode);
        p.WriteBool(value.NotifyFlagA);
        p.WriteBool(value.NotifyFlagB);
        p.WriteBool(value.EventFlagA);
        p.WriteBool(value.EventFlagB);
        p.WriteBool(value.EventFlagC);
    }

}

// id 2907
public sealed record SetChestOptions(Id ChestId, bool LockChest, bool AutoLockChest, int Capacity)
    : IParserComposer<SetChestOptions>
{
    public static SetChestOptions Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SetChestOptions ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadBool(), p.ReadInt());

    private static SetChestOptions ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadBool(), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SetChestOptions value, in PacketWriter p)
    {
        p.WriteInt(WiredWire.FlashId(value.ChestId));
        p.WriteBool(value.LockChest);
        p.WriteBool(value.AutoLockChest);
        p.WriteInt(value.Capacity);
    }

    private static void ComposeUnity(SetChestOptions value, in PacketWriter p)
    {
        p.WriteLong(value.ChestId);
        p.WriteBool(value.LockChest);
        p.WriteBool(value.AutoLockChest);
        p.WriteInt(value.Capacity);
    }
}

// id 3830
public sealed record SetChestPreferences(
    Id ChestId,
    string ChestName,
    string ChestDescription,
    bool PrefFlagA,
    bool PrefFlagB,
    int ChestState,
    int OpenState,
    int AmountPreview,
    bool DisabledFlag) : IParserComposer<SetChestPreferences>
{
    public static SetChestPreferences Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SetChestPreferences ParseFlash(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadString(),
        p.ReadString(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool());

    private static SetChestPreferences ParseUnity(in PacketReader p) => new(
        p.ReadLong(),
        p.ReadString(),
        p.ReadString(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SetChestPreferences value, in PacketWriter p)
    {
        int chest_id = WiredWire.FlashId(value.ChestId);
        WiredWire.RequireString(value.ChestName, nameof(ChestName), in p);
        WiredWire.RequireString(value.ChestDescription, nameof(ChestDescription), in p);
        p.WriteInt(chest_id);
        p.WriteString(value.ChestName);
        p.WriteString(value.ChestDescription);
        p.WriteBool(value.PrefFlagA);
        p.WriteBool(value.PrefFlagB);
        p.WriteInt(value.ChestState);
        p.WriteInt(value.OpenState);
        p.WriteInt(value.AmountPreview);
        p.WriteBool(value.DisabledFlag);
    }

    private static void ComposeUnity(SetChestPreferences value, in PacketWriter p)
    {
        WiredWire.RequireString(value.ChestName, nameof(ChestName), in p);
        WiredWire.RequireString(value.ChestDescription, nameof(ChestDescription), in p);
        p.WriteLong(value.ChestId);
        p.WriteString(value.ChestName);
        p.WriteString(value.ChestDescription);
        p.WriteBool(value.PrefFlagA);
        p.WriteBool(value.PrefFlagB);
        p.WriteInt(value.ChestState);
        p.WriteInt(value.OpenState);
        p.WriteInt(value.AmountPreview);
        p.WriteBool(value.DisabledFlag);
    }
}

// id 1999
public sealed record WiredTransactionGetChestLogs(int LogListId, int PageSize, int Page)
    : IParserComposer<WiredTransactionGetChestLogs>
{
    public static WiredTransactionGetChestLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredTransactionGetChestLogs ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionGetChestLogs value, in PacketWriter p)
    {
        p.WriteInt(value.LogListId);
        p.WriteInt(value.PageSize);
        p.WriteInt(value.Page);
    }

}

// id 475 — transactionId is pushed as new Long(): 8 bytes on the wire, not an int.
public sealed record WiredTransactionGetLogDetails(long TransactionId)
    : IParserComposer<WiredTransactionGetLogDetails>
{
    public static WiredTransactionGetLogDetails Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredTransactionGetLogDetails ParseFlash(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionGetLogDetails value, in PacketWriter p) =>
        p.WriteLong(value.TransactionId);

}

// id 2016
public sealed record WiredTransactionGetRoomLogs(int PageSize, int Page)
    : IParserComposer<WiredTransactionGetRoomLogs>
{
    public static WiredTransactionGetRoomLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredTransactionGetRoomLogs ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionGetRoomLogs value, in PacketWriter p)
    {
        p.WriteInt(value.PageSize);
        p.WriteInt(value.Page);
    }

}

// id 1908 — writes the exact same layout WiredContractContents(2976) reads.
public sealed record WiredUpdateContract(WiredContractContents Contract) : IParserComposer<WiredUpdateContract>
{
    public static WiredUpdateContract Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredUpdateContract ParseFlash(in PacketReader p) =>
        new(WiredContractContents.Read(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUpdateContract value, in PacketWriter p) =>
        WiredContractContents.Write(value.Contract, in p);

}

// id 3111. The flag says REMOVE, not add: WiredTradingModel.requestAddItemsToTrading sends false
// and requestRemoveItemFromTrading sends true. Naming it the other way round makes a deposit
// arrive as a withdrawal of items the offer does not hold, which the hotel answers by refusing.
public sealed record WiredTradeAddDeleteItems(bool IsRemove, IReadOnlyList<Id> Ids)
    : IParserComposer<WiredTradeAddDeleteItems>
{
    /// <summary>Offers items to the open trade.</summary>
    public static WiredTradeAddDeleteItems Add(IReadOnlyList<Id> ids) => new(false, ids);

    /// <summary>Takes items back off the open trade.</summary>
    public static WiredTradeAddDeleteItems Remove(IReadOnlyList<Id> ids) => new(true, ids);

    public static WiredTradeAddDeleteItems Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeAddDeleteItems ParseFlash(in PacketReader p)
    {
        bool is_remove = p.ReadBool();
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, sizeof(int), nameof(Ids));
        var ids = new Id[count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = p.ReadInt();
        return new WiredTradeAddDeleteItems(is_remove, ids);
    }

    private static WiredTradeAddDeleteItems ParseUnity(in PacketReader p)
    {
        bool is_remove = p.ReadBool();
        int count = unchecked((ushort)p.ReadShort());
        WiredWire.RequireBoundedCount(count, p.Available, sizeof(long), nameof(Ids));
        var ids = new Id[count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = p.ReadLong();
        return new WiredTradeAddDeleteItems(is_remove, ids);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeAddDeleteItems value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Ids);
        var ids = new int[value.Ids.Count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = WiredWire.FlashId(value.Ids[i]);
        p.WriteBool(value.IsRemove);
        p.WriteInt(ids.Length);
        foreach (int id in ids)
            p.WriteInt(id);
    }

    private static void ComposeUnity(WiredTradeAddDeleteItems value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Ids);
        WiredWire.RequireUnityCount(value.Ids.Count, nameof(Ids));
        p.WriteBool(value.IsRemove);
        p.WriteShort(unchecked((short)(ushort)value.Ids.Count));
        foreach (Id id in value.Ids)
            p.WriteLong(id);
    }

}

// id 2646 — empty body.
public sealed record WiredTradeCancel : IParserComposer<WiredTradeCancel>
{
    public static WiredTradeCancel Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeCancel ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCancel));
        return new WiredTradeCancel();
    }

    private static WiredTradeCancel ParseUnity(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCancel));
        return new WiredTradeCancel();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeCancel value, in PacketWriter p) { }

    private static void ComposeUnity(WiredTradeCancel value, in PacketWriter p) { }
}

// id 2818
public sealed record WiredTradeConfirm(bool Confirm) : IParserComposer<WiredTradeConfirm>
{
    public static WiredTradeConfirm Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTradeConfirm ParseFlash(in PacketReader p) => new(p.ReadBool());

    private static WiredTradeConfirm ParseUnity(in PacketReader p) => new(p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTradeConfirm value, in PacketWriter p) =>
        p.WriteBool(value.Confirm);

    private static void ComposeUnity(WiredTradeConfirm value, in PacketWriter p) =>
        p.WriteBool(value.Confirm);
}

internal static class WiredChestWire
{
    public static void Validate(ChestItemType? value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(value.LegacyPosterId, nameof(value.LegacyPosterId), in p);
    }

    public static void Validate(
        IReadOnlyList<ChestStorage> values,
        in PacketWriter p,
        bool unity)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (ChestStorage value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            Validate(value.Type, in p);
            Validate(value.StuffData, in p, unity);
        }
    }

    public static void Validate(TradeRequirementNode value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = checked((byte)value.Type);
        switch (value.Type)
        {
            case TradeRequirementNode.TypeCoin when value.ItemType is null:
                return;
            case TradeRequirementNode.TypeFurni:
                Validate(value.ItemType, in p);
                return;
            case TradeRequirementNode.TypeCoin:
                throw new InvalidDataException("Coin trade requirement nodes cannot carry an item type.");
            default:
                throw new InvalidDataException($"Unsupported trade requirement node type {value.Type}.");
        }
    }

    public static void Validate(TradeRequirementRule value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Nodes);
        WiredWire.RequireUnityCount(value.Nodes.Count, nameof(value.Nodes));
        foreach (TradeRequirementNode node in value.Nodes)
            Validate(node, in p);
    }

    public static void Validate(TradeRequirementRulesDefinition value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.YouGiveRule is not null)
        {
            WiredWire.RequireUnityCount(value.YouGiveRule.Count, nameof(value.YouGiveRule));
            foreach (TradeRequirementRule rule in value.YouGiveRule)
                Validate(rule, in p);
        }
        if (value.YouGetRule is not null)
            Validate(value.YouGetRule, in p);
    }

    public static void Validate(TradeRequirementRules value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value.Definition, in p);
        if (value.Type is not (
            TradeRequirementRules.TypeNone or
            TradeRequirementRules.TypeFixedMultiplier or
            TradeRequirementRules.TypeAutoMultiplier))
            throw new InvalidDataException($"Unsupported trade requirement rule type {value.Type}.");
    }

    public static void Validate(TradeRequirement value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(value.YouGetText, nameof(value.YouGetText), in p);
        WiredWire.RequireString(value.LayoutType, nameof(value.LayoutType), in p);
        if (value.Type == TradeRequirement.TypeWithRules)
        {
            ArgumentNullException.ThrowIfNull(value.Rules);
            Validate(value.Rules, in p);
        }
        else if (value.Rules is not null)
        {
            throw new InvalidDataException("Only rule-backed trade requirements can carry rules.");
        }
    }

    public static void Validate(WiredTransactionLogPage value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Logs);
        foreach (WiredTransactionInfo log in value.Logs)
            Validate(log, in p);
    }

    public static void Validate(WiredTransactionDetails value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value.TransactionInfo, in p);
        ArgumentNullException.ThrowIfNull(value.ChestIds);
        WiredWire.RequireUnityCount(value.ChestIds.Count, nameof(value.ChestIds));
        Validate(value.DepositedFurnis, in p);
        Validate(value.WithdrawnFurnis, in p);
    }

    public static void Validate(WiredContractContents value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.ContractType is not (
            WiredContractContents.TypePayment or
            WiredContractContents.TypeTrade or
            WiredContractContents.TypeReward))
            throw new InvalidDataException($"Unsupported wired contract type {value.ContractType}.");
        Validate(value.Definition, in p);
        if (value.ContractType == WiredContractContents.TypePayment)
        {
            WiredWire.RequireString(value.ReceiveText, nameof(value.ReceiveText), in p);
            WiredWire.RequireString(value.LayoutType, nameof(value.LayoutType), in p);
        }
        if (value.ContractType == WiredContractContents.TypeReward)
            WiredWire.RequireString(value.RewardText, nameof(value.RewardText), in p);
    }

    public static void Validate(WiredTradingItems value, in PacketWriter p, bool unity)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!unity)
        {
            _ = WiredWire.FlashId(value.FirstUserId);
            _ = WiredWire.FlashId(value.SecondUserId);
        }
        ArgumentNullException.ThrowIfNull(value.FirstUserItems);
        ArgumentNullException.ThrowIfNull(value.SecondUserItems);
        foreach (TradeItem item in value.FirstUserItems)
            Validate(item, in p, unity);
        foreach (TradeItem item in value.SecondUserItems)
            Validate(item, in p, unity);
    }

    private static void Validate(WiredTransactionInfo value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(
            value.TransactionDefinitionInfo,
            nameof(value.TransactionDefinitionInfo),
            in p);
        WiredWire.RequireString(value.UserName, nameof(value.UserName), in p);
        WiredWire.RequireString(value.ReadableTimestamp, nameof(value.ReadableTimestamp), in p);
    }

    private static void Validate(IReadOnlyList<ChestFurniCount> values, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (ChestFurniCount value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            Validate(value.Type, in p);
        }
    }

    private static void Validate(TradeItem value, in PacketWriter p, bool unity)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Type is not (ItemType.Floor or ItemType.Wall))
            throw new InvalidDataException($"Unsupported wired trade item type {value.Type}.");
        if (!unity)
        {
            _ = WiredWire.FlashId(value.ItemId);
            _ = WiredWire.FlashId(value.Id);
            if (value.Type == ItemType.Floor)
                _ = checked((int)value.Extra);
        }
        Validate(value.Data, in p, unity);
    }

    private static void Validate(ItemData value, in PacketWriter p, bool unity)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (unity && value.IsLimitedRare)
            WiredWire.RequireString(value.UniqueLimitedData, nameof(value.UniqueLimitedData), in p);
        switch (value)
        {
            case LegacyData legacy:
                WiredWire.RequireString(legacy.Value, nameof(legacy.Value), in p);
                return;
            case MapData map:
                WiredWire.RequireUnityCount(map.Entries.Count, nameof(map.Entries));
                foreach ((string key, string entry_value) in map.Entries)
                {
                    WiredWire.RequireString(key, nameof(map.Entries), in p);
                    WiredWire.RequireString(entry_value, nameof(map.Entries), in p);
                }
                return;
            case StringArrayData strings:
                WiredWire.RequireUnityCount(strings.Values.Count, nameof(strings.Values));
                foreach (string entry in strings.Values)
                    WiredWire.RequireString(entry, nameof(strings.Values), in p);
                return;
            case VoteResultData vote:
                WiredWire.RequireString(vote.Value, nameof(vote.Value), in p);
                return;
            case EmptyItemData:
                return;
            case IntArrayData integers:
                WiredWire.RequireUnityCount(integers.Values.Count, nameof(integers.Values));
                return;
            case HighScoreData high_score:
                WiredWire.RequireString(high_score.Value, nameof(high_score.Value), in p);
                WiredWire.RequireUnityCount(high_score.Scores.Count, nameof(high_score.Scores));
                foreach (HighScore score in high_score.Scores)
                {
                    ArgumentNullException.ThrowIfNull(score);
                    ArgumentNullException.ThrowIfNull(score.Names);
                    WiredWire.RequireUnityCount(score.Names.Count, nameof(score.Names));
                    foreach (string name in score.Names)
                        WiredWire.RequireString(name, nameof(score.Names), in p);
                }
                return;
            case CrackableFurniData crackable:
                WiredWire.RequireString(crackable.Value, nameof(crackable.Value), in p);
                return;
            default:
                throw new NotSupportedException($"Unsupported wired chest item-data type {value.GetType().Name}.");
        }
    }
}
