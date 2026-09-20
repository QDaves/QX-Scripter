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
        FlashWire.Parse(in p, ParseFlash);

    private static ChestStorage ParseFlash(in PacketReader p) => ParseStorage(in p);

    private static ChestStorage ParseStorage(in PacketReader p)
    {
        int inventoryId = p.ReadInt();
        int lockState = p.ReadInt();
        long transactionId = p.ReadLong();
        ChestItemType type = ChestItemType.Parse(p);
        bool groupable = p.ReadBool();
        int specialType = p.ReadInt();
        ItemData stuffData = p.Parse<ItemData>();
        int extra = type.IsWallItem
            ? 0
            : p.ReadInt();
        return new ChestStorage(inventoryId, lockState, transactionId, type, groupable, specialType, stuffData, extra);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ChestStorage value, in PacketWriter p) =>
        value.ComposeStorage(in p);

    private void ComposeStorage(in PacketWriter p)
    {
        p.WriteInt(InventoryId);
        p.WriteInt(LockState);
        p.WriteLong(TransactionId);
        Type.Compose(p);
        p.WriteBool(Groupable);
        p.WriteInt(SpecialType);
        p.Compose(StuffData);
        if (!Type.IsWallItem)
        {
            p.WriteInt(Extra);
        }
    }
}

// id 1174
public sealed record OpenChest(int ChestId) : IParserComposer<OpenChest>
{
    public static OpenChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenChest value, in PacketWriter p) => p.WriteInt(value.ChestId);
}

// id 1022
public sealed record CoinsChestContents(int ChestId, int Coins, bool IsUpdate)
    : IParserComposer<CoinsChestContents>
{
    public static CoinsChestContents Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CoinsChestContents ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ItemsChestContentsChunk value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.StorageChunk, in p);
        p.WriteInt(value.ChestId);
        p.WriteInt(value.TotalFragments);
        p.WriteInt(value.FragmentNo);
        p.WriteInt(value.StorageChunk.Count);
        foreach (ChestStorage s in value.StorageChunk)
            s.Compose(p);
    }
}

// id 2738
public sealed record ItemsChestContentsUpdated(
    int ChestId,
    IReadOnlyList<int> RemovedIds,
    IReadOnlyList<ChestStorage> AddedStorage) : IParserComposer<ItemsChestContentsUpdated>
{
    public static ItemsChestContentsUpdated Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ItemsChestContentsUpdated value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RemovedIds);
        WiredChestWire.Validate(value.AddedStorage, in p);
        p.WriteInt(value.ChestId);
        WiredIo.WriteIntArray(p, value.RemovedIds);
        p.WriteInt(value.AddedStorage.Count);
        foreach (ChestStorage s in value.AddedStorage)
            s.Compose(p);
    }
}

// id 2721
public sealed record UpgradeChestResult(int ChestId, int ResultCode) : IParserComposer<UpgradeChestResult>
{
    public const int Success = 0;

    public static UpgradeChestResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpgradeChestResult ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static ChestPreferencesUpdateSuccess ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionLogList ParseFlash(in PacketReader p) =>
        new(WiredTransactionLogPage.Parse(p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionLogDetails ParseFlash(in PacketReader p) =>
        new(WiredTransactionDetails.Parse(p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionSuccess value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.TransactionSuccessTypeId);
        if (value.TransactionSuccessTypeId == TypeReward && value.HasReward)
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
        FlashWire.Parse(in p, ParseFlash);

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
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredContractUpdateResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredOpenContract ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradingItems value, in PacketWriter p)
    {
        WiredChestWire.Validate(value, in p);
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

    private static TradeItem[] read_items(in PacketReader p)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(
            n,
            p.Available,
            TradeWire.FlashTradeItemMinimumBytes,
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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeInitiate ParseFlash(in PacketReader p) =>
        new(TradeRequirement.Parse(p), p.ReadBool(), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeInitiate value, in PacketWriter p)
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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeItemsUpdate ParseFlash(in PacketReader p) =>
        new(WiredTradingItems.Parse(p), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeItemsUpdate value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.TradingItems, in p);
        value.TradingItems.Compose(p);
        p.WriteBool(value.CanAccept);
        p.WriteInt(value.Extra);
    }
}

public sealed record WiredTradeCancelled(int TransactionFailureTypeId) : IParserComposer<WiredTradeCancelled>
{
    public static WiredTradeCancelled Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeCancelled ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeCancelled value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);
}

public sealed record WiredTransactionFail(int TransactionFailureTypeId)
    : IParserComposer<WiredTransactionFail>
{
    public static WiredTransactionFail Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionFail ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionFail value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);
}

public sealed record WiredTradeTransactionNotification(int TradeTransactionNotificationId)
    : IParserComposer<WiredTradeTransactionNotification>
{
    public static WiredTradeTransactionNotification Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeTransactionNotification ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        WiredTradeTransactionNotification value,
        in PacketWriter p) => p.WriteInt(value.TradeTransactionNotificationId);
}

public sealed record WiredTradeCompleted : IParserComposer<WiredTradeCompleted>
{
    public static WiredTradeCompleted Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeCompleted ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCompleted));
        return new WiredTradeCompleted();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeCompleted value, in PacketWriter p) { }
}

// Outgoing composers. Each mirrors the SWF getMessageArray() push order exactly.

// id 806
public sealed record OpenChestAndGetContents(Id ChestId) : IParserComposer<OpenChestAndGetContents>
{
    public static OpenChestAndGetContents Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenChestAndGetContents ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenChestAndGetContents value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 2935
public sealed record CloseChest(Id ChestId) : IParserComposer<CloseChest>
{
    public static CloseChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CloseChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CloseChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 1630
public sealed record LockAllChests(bool Lock, bool ApplyToAllInRoom) : IParserComposer<LockAllChests>
{
    public static LockAllChests Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static LockAllChests ParseFlash(in PacketReader p) => new(p.ReadBool(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static UpgradeChest ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WithdrawAllFromChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WithdrawAllFromChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 2843
public sealed record WithdrawCoinsFromChest(Id ChestId, int CoinAmount) : IParserComposer<WithdrawCoinsFromChest>
{
    public static WithdrawCoinsFromChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WithdrawCoinsFromChest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WithdrawCoinsFromChest value, in PacketWriter p)
    {
        p.WriteInt(WiredWire.FlashId(value.ChestId));
        p.WriteInt(value.CoinAmount);
    }
}

// id 873 — ChestItemType expands to bool/int/string between the two ints.
public sealed record WithdrawItemsFromChest(Id ChestId, ChestItemType ItemType, int Count)
    : IParserComposer<WithdrawItemsFromChest>
{
    public static WithdrawItemsFromChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WithdrawItemsFromChest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), ChestItemType.Parse(p), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WithdrawItemsFromChest value, in PacketWriter p)
    {
        int chest_id = WiredWire.FlashId(value.ChestId);
        WiredChestWire.Validate(value.ItemType, in p);
        p.WriteInt(chest_id);
        value.ItemType.Compose(p);
        p.WriteInt(value.Count);
    }
}

// id 3514
public sealed record StartAddingToChest(Id ChestId) : IParserComposer<StartAddingToChest>
{
    public static StartAddingToChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static StartAddingToChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(StartAddingToChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
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
        FlashWire.Parse(in p, ParseFlash);

    private static SetChestNotificationPreferences ParseFlash(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static SetChestOptions ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadBool(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SetChestOptions value, in PacketWriter p)
    {
        p.WriteInt(WiredWire.FlashId(value.ChestId));
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
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

// id 1999
public sealed record WiredTransactionGetChestLogs(int LogListId, int PageSize, int Page)
    : IParserComposer<WiredTransactionGetChestLogs>
{
    public static WiredTransactionGetChestLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionGetChestLogs ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionGetLogDetails ParseFlash(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionGetLogDetails value, in PacketWriter p) =>
        p.WriteLong(value.TransactionId);

}

// id 2016
public sealed record WiredTransactionGetRoomLogs(int PageSize, int Page)
    : IParserComposer<WiredTransactionGetRoomLogs>
{
    public static WiredTransactionGetRoomLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionGetRoomLogs ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

    private static WiredUpdateContract ParseFlash(in PacketReader p) =>
        new(WiredContractContents.Read(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

}

// id 2646 — empty body.
public sealed record WiredTradeCancel : IParserComposer<WiredTradeCancel>
{
    public static WiredTradeCancel Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeCancel ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCancel));
        return new WiredTradeCancel();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeCancel value, in PacketWriter p) { }
}

// id 2818
public sealed record WiredTradeConfirm(bool Confirm) : IParserComposer<WiredTradeConfirm>
{
    public static WiredTradeConfirm Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeConfirm ParseFlash(in PacketReader p) => new(p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeConfirm value, in PacketWriter p) =>
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
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (ChestStorage value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            Validate(value.Type, in p);
            Validate(value.StuffData, in p);
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
        foreach (TradeRequirementNode node in value.Nodes)
            Validate(node, in p);
    }

    public static void Validate(TradeRequirementRulesDefinition value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.YouGiveRule is not null)
        {
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

    public static void Validate(WiredTradingItems value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        {
            _ = WiredWire.FlashId(value.FirstUserId);
            _ = WiredWire.FlashId(value.SecondUserId);
        }
        ArgumentNullException.ThrowIfNull(value.FirstUserItems);
        ArgumentNullException.ThrowIfNull(value.SecondUserItems);
        foreach (TradeItem item in value.FirstUserItems)
            Validate(item, in p);
        foreach (TradeItem item in value.SecondUserItems)
            Validate(item, in p);
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

    private static void Validate(TradeItem value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Type is not (ItemType.Floor or ItemType.Wall))
            throw new InvalidDataException($"Unsupported wired trade item type {value.Type}.");
        {
            _ = WiredWire.FlashId(value.ItemId);
            _ = WiredWire.FlashId(value.Id);
            if (value.Type == ItemType.Floor)
                _ = checked((int)value.Extra);
        }
        Validate(value.Data, in p);
    }

    private static void Validate(ItemData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (value)
        {
            case LegacyData legacy:
                WiredWire.RequireString(legacy.Value, nameof(legacy.Value), in p);
                return;
            case MapData map:
                ; foreach ((string key, string entry_value) in map.Entries)
                {
                    WiredWire.RequireString(key, nameof(map.Entries), in p);
                    WiredWire.RequireString(entry_value, nameof(map.Entries), in p);
                }
                return;
            case StringArrayData strings:
                ; foreach (string entry in strings.Values)
                    WiredWire.RequireString(entry, nameof(strings.Values), in p);
                return;
            case VoteResultData vote:
                WiredWire.RequireString(vote.Value, nameof(vote.Value), in p);
                return;
            case EmptyItemData:
                return;
            case IntArrayData integers:
                ; return;
            case HighScoreData high_score:
                WiredWire.RequireString(high_score.Value, nameof(high_score.Value), in p);
                ; foreach (HighScore score in high_score.Scores)
                {
                    ArgumentNullException.ThrowIfNull(score);
                    ArgumentNullException.ThrowIfNull(score.Names);
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
