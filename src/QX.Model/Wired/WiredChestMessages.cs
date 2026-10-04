using Qx.Messages;

namespace Qx.Model.Wired;

// Shared furni-type descriptor (§_-dR§/ChestItemType). Empty legacyPosterId is normalised
// to null on read and back to "" on write, so storing the raw string round-trips byte-exact.
/// <summary>Represents a furni type as the wired chest, transaction and trade messages describe it.</summary>
/// <param name="IsWallItem">Whether the furni type is a wall item.</param>
/// <param name="TypeId">The furni type id.</param>
/// <param name="LegacyPosterId">The poster id of a legacy poster, or an empty string when there is none.</param>
public sealed record ChestItemType(bool IsWallItem, int TypeId, string LegacyPosterId)
    : IParserComposer<ChestItemType>
{
    /// <summary>Parses the furni type from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ChestItemType Parse(in PacketReader p) =>
        new(p.ReadBool(), p.ReadInt(), p.ReadString());

    /// <summary>Composes the furni type into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteBool(IsWallItem);
        p.WriteInt(TypeId);
        p.WriteString(LegacyPosterId);
    }
}

// §_-dR§/ChestStorage — one furni slot inside a chest. `extra` is only on the wire for floor items.
/// <summary>Represents one furni slot inside a wired chest.</summary>
/// <param name="InventoryId">The inventory id of the stored item.</param>
/// <param name="LockState">The lock state code of the item.</param>
/// <param name="TransactionId">The transaction id the hotel sends with the item, as a 64 bit integer.</param>
/// <param name="Type">The furni type of the item.</param>
/// <param name="Groupable">Whether the item can be grouped with identical items.</param>
/// <param name="SpecialType">The special type code of the item.</param>
/// <param name="StuffData">The item data of the item.</param>
/// <param name="Extra">The extra value of a floor item, or 0 for a wall item, which does not carry it on the wire.</param>
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
    /// <summary>Parses the storage entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the storage entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received when the hotel asks the client to open a wired chest.</summary>
/// <remarks>Received as the Flash <c>OpenChest</c> message.</remarks>
/// <param name="ChestId">The id of the chest.</param>
public sealed record OpenChest(int ChestId) : IParserComposer<OpenChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static OpenChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenChest value, in PacketWriter p) => p.WriteInt(value.ChestId);
}

// id 1022
/// <summary>Received with the coins in a wired chest.</summary>
/// <remarks>Received as the Flash <c>CoinsChestContents</c> message.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="Coins">The number of coins in the chest.</param>
/// <param name="IsUpdate">Whether the hotel marks the message as an update of contents sent before.</param>
public sealed record CoinsChestContents(int ChestId, int Coins, bool IsUpdate)
    : IParserComposer<CoinsChestContents>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CoinsChestContents Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CoinsChestContents ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with one fragment of the items in a wired chest.</summary>
/// <remarks>Received as the Flash <c>ItemsChestContentsChunk</c> message.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="TotalFragments">The number of fragments the item list is sent in.</param>
/// <param name="FragmentNo">The number of this fragment.</param>
/// <param name="StorageChunk">The items in this fragment.</param>
public sealed record ItemsChestContentsChunk(
    int ChestId,
    int TotalFragments,
    int FragmentNo,
    IReadOnlyList<ChestStorage> StorageChunk) : IParserComposer<ItemsChestContentsChunk>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received when items are added to or removed from a wired chest.</summary>
/// <remarks>Received as the Flash <c>ItemsChestContentsUpdated</c> message.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="RemovedIds">The inventory ids of the removed items.</param>
/// <param name="AddedStorage">The added items.</param>
public sealed record ItemsChestContentsUpdated(
    int ChestId,
    IReadOnlyList<int> RemovedIds,
    IReadOnlyList<ChestStorage> AddedStorage) : IParserComposer<ItemsChestContentsUpdated>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with the result of a wired chest capacity upgrade.</summary>
/// <remarks>Received as the Flash <c>UpgradeChestResult</c> message. The hotel sends it in answer to <see cref="UpgradeChest"/>.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="ResultCode">The result code, where 0 means the upgrade succeeded.</param>
public sealed record UpgradeChestResult(int ChestId, int ResultCode) : IParserComposer<UpgradeChestResult>
{
    /// <summary>The result code of a successful upgrade.</summary>
    public const int Success = 0;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UpgradeChestResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpgradeChestResult ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpgradeChestResult value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.ResultCode);
    }
}

// id 1957
/// <summary>Received when the hotel confirms a wired chest preferences update.</summary>
/// <remarks>Received as the Flash <c>ChestPreferencesUpdateSuccess</c> message.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="IsNotificationPreferences">Whether the confirmed update was of the notification preferences instead of the general preferences.</param>
public sealed record ChestPreferencesUpdateSuccess(int ChestId, bool IsNotificationPreferences)
    : IParserComposer<ChestPreferencesUpdateSuccess>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ChestPreferencesUpdateSuccess Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ChestPreferencesUpdateSuccess ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Represents one requirement of a wired trade rule, an amount of coins or of one furni type.</summary>
/// <remarks>Composing throws <see cref="InvalidDataException"/> when <paramref name="ItemType"/> is set for a coin node or missing for a furni node.</remarks>
/// <param name="Type">The node type, 0 for coins or 1 for furni, written as a single byte.</param>
/// <param name="Amount">The number of coins or furni.</param>
/// <param name="ItemType">The furni type of a furni node, or <see langword="null"/> for a coin node.</param>
public sealed record TradeRequirementNode(int Type, int Amount, ChestItemType? ItemType)
    : IParserComposer<TradeRequirementNode>
{
    /// <summary>The node type of a coin requirement.</summary>
    public const int TypeCoin = 0;
    /// <summary>The node type of a furni requirement.</summary>
    public const int TypeFurni = 1;

    /// <summary>Parses the node from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeRequirementNode Parse(in PacketReader p)
    {
        int type = unchecked((sbyte)p.ReadByte());
        int amount = p.ReadInt();
        ChestItemType? itemType = type == TypeFurni ? ChestItemType.Parse(p) : null;
        return new TradeRequirementNode(type, amount, itemType);
    }

    /// <summary>Composes the node into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        byte type = unchecked((byte)checked((sbyte)Type));
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

/// <summary>Represents one rule of a wired trade requirement as a list of nodes.</summary>
/// <param name="Nodes">The requirement nodes of the rule.</param>
public sealed record TradeRequirementRule(IReadOnlyList<TradeRequirementNode> Nodes)
    : IParserComposer<TradeRequirementRule>
{
    /// <summary>Parses the rule from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeRequirementRule Parse(in PacketReader p)
    {
        int n = p.ReadLength();
        var nodes = new TradeRequirementNode[n];
        for (int i = 0; i < n; i++)
            nodes[i] = TradeRequirementNode.Parse(p);
        return new TradeRequirementRule(nodes);
    }

    /// <summary>Composes the rule into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        WiredChestWire.Validate(this, in p);
        p.WriteLength((Length)Nodes.Count);
        foreach (TradeRequirementNode node in Nodes)
            node.Compose(p);
    }
}

// null lists/rules encode the presence bool as false with no payload.
/// <summary>Represents what the user gives and gets in a wired trade or contract.</summary>
/// <remarks>Each part is preceded by a presence flag on the wire, and a part that was not sent is <see langword="null"/>.</remarks>
/// <param name="YouGiveRule">The rules for what the user gives, or <see langword="null"/> when none were sent.</param>
/// <param name="YouGetRule">The rule for what the user gets, or <see langword="null"/> when none was sent.</param>
public sealed record TradeRequirementRulesDefinition(
    IReadOnlyList<TradeRequirementRule>? YouGiveRule,
    TradeRequirementRule? YouGetRule) : IParserComposer<TradeRequirementRulesDefinition>
{
    /// <summary>Parses the definition from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the definition into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Represents the rules of a wired trade requirement with their multiplier.</summary>
/// <param name="Definition">What the user gives and gets.</param>
/// <param name="Type">The multiplier type, 0 for none, 1 for a fixed multiplier or 2 for an automatic multiplier.</param>
/// <param name="Multiplier">The fixed multiplier, on the wire only when <paramref name="Type"/> is 1, otherwise 1.</param>
/// <param name="AutoMultiplierMax">The maximum automatic multiplier, on the wire only when <paramref name="Type"/> is 2, otherwise 1.</param>
public sealed record TradeRequirementRules(
    TradeRequirementRulesDefinition Definition,
    int Type,
    int Multiplier,
    int AutoMultiplierMax) : IParserComposer<TradeRequirementRules>
{
    /// <summary>The multiplier type without a multiplier.</summary>
    public const int TypeNone = 0;
    /// <summary>The multiplier type with a fixed multiplier.</summary>
    public const int TypeFixedMultiplier = 1;
    /// <summary>The multiplier type with an automatic multiplier.</summary>
    public const int TypeAutoMultiplier = 2;

    /// <summary>Parses the rules from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the rules into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>Represents the requirement of a wired trade.</summary>
/// <remarks>Composing throws <see cref="InvalidDataException"/> when <paramref name="Rules"/> is missing for type 4 or set for another type.</remarks>
/// <param name="Type">The requirement type code. Rules are on the wire only for type 4.</param>
/// <param name="YouGetText">The text that describes what the user gets.</param>
/// <param name="LayoutType">The layout type of the trade window.</param>
/// <param name="Rules">The rules, or <see langword="null"/> when <paramref name="Type"/> is not 4.</param>
public sealed record TradeRequirement(
    int Type,
    string YouGetText,
    string LayoutType,
    TradeRequirementRules? Rules) : IParserComposer<TradeRequirement>
{
    /// <summary>The requirement type that carries rules.</summary>
    public const int TypeWithRules = 4;

    /// <summary>Parses the requirement from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeRequirement Parse(in PacketReader p)
    {
        int type = p.ReadInt();
        string youGetText = p.ReadString();
        string layoutType = p.ReadString();
        TradeRequirementRules? rules = type == TypeWithRules ? TradeRequirementRules.Parse(p) : null;
        return new TradeRequirement(type, youGetText, layoutType, rules);
    }

    /// <summary>Composes the requirement into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Represents one row of a wired transaction log.</summary>
/// <param name="TransactionId">The id of the transaction, sent as a 64 bit integer.</param>
/// <param name="FlatId">The id of the room the transaction happened in.</param>
/// <param name="TransactionType">The transaction type code.</param>
/// <param name="TransactionDefinitionInfo">The text that describes the transaction definition.</param>
/// <param name="UserId">The id of the user who made the transaction.</param>
/// <param name="UserName">The name of the user who made the transaction.</param>
/// <param name="Timestamp">The time of the transaction as a 64 bit value sent by the hotel.</param>
/// <param name="ReadableTimestamp">The time of the transaction as text formatted by the hotel.</param>
/// <param name="ChestCount">The number of chests involved.</param>
/// <param name="WithdrawFurniCount">The number of furni withdrawn.</param>
/// <param name="DepositFurniCount">The number of furni deposited.</param>
/// <param name="WithdrawCoinsCount">The number of coins withdrawn.</param>
/// <param name="DepositCoinsCount">The number of coins deposited.</param>
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
    /// <summary>Parses the log row from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the log row into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Represents one page of a wired transaction log.</summary>
/// <param name="LogListType">The kind of log, 0 for a chest log or 1 for the room log.</param>
/// <param name="LogListId">The id of the log list, sent as a 64 bit integer.</param>
/// <param name="TotalLogs">The total number of rows in the log.</param>
/// <param name="CurrentPage">The number of the page.</param>
/// <param name="Amount">The page size the hotel reports.</param>
/// <param name="Logs">The rows on the page.</param>
public sealed record WiredTransactionLogPage(
    int LogListType,
    long LogListId,
    int TotalLogs,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredTransactionInfo> Logs) : IParserComposer<WiredTransactionLogPage>
{
    /// <summary>The log list type of a chest transaction log.</summary>
    public const int TypeChestLogs = 0;
    /// <summary>The log list type of the room transaction log.</summary>
    public const int TypeRoomLogs = 1;

    /// <summary>Parses the page from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the page into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with one page of a wired transaction log.</summary>
/// <remarks>Received as the Flash <c>WiredTransactionLogList</c> message. The hotel sends it in answer to <see cref="WiredTransactionGetChestLogs"/> and <see cref="WiredTransactionGetRoomLogs"/>.</remarks>
/// <param name="Logs">The log page.</param>
public sealed record WiredTransactionLogList(WiredTransactionLogPage Logs)
    : IParserComposer<WiredTransactionLogList>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionLogList Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionLogList ParseFlash(in PacketReader p) =>
        new(WiredTransactionLogPage.Parse(p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionLogList value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.Logs, in p);
        value.Logs.Compose(p);
    }
}

// One (furni-type, count) entry inside a transaction's deposited/withdrawn lists.
/// <summary>Represents a furni type and a count in the details of a wired transaction.</summary>
/// <param name="Type">The furni type.</param>
/// <param name="Count">The number of furni of the type.</param>
public sealed record ChestFurniCount(ChestItemType Type, int Count) : IParserComposer<ChestFurniCount>
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ChestFurniCount Parse(in PacketReader p) => new(ChestItemType.Parse(p), p.ReadInt());

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        Type.Compose(p);
        p.WriteInt(Count);
    }
}

// §_-k1f§/WiredTransactionDetails
/// <summary>Represents the details of one wired transaction.</summary>
/// <param name="TransactionInfo">The log row of the transaction.</param>
/// <param name="ChestIds">The ids of the chests involved.</param>
/// <param name="DepositedFurnis">The deposited furni types with their counts.</param>
/// <param name="WithdrawnFurnis">The withdrawn furni types with their counts.</param>
/// <param name="IsIncompleteData">Whether the hotel marks the details as incomplete.</param>
public sealed record WiredTransactionDetails(
    WiredTransactionInfo TransactionInfo,
    IReadOnlyList<int> ChestIds,
    IReadOnlyList<ChestFurniCount> DepositedFurnis,
    IReadOnlyList<ChestFurniCount> WithdrawnFurnis,
    bool IsIncompleteData) : IParserComposer<WiredTransactionDetails>
{
    /// <summary>Parses the details from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionDetails Parse(in PacketReader p)
    {
        WiredTransactionInfo info = WiredTransactionInfo.Parse(p);
        int[] chestIds = WiredIo.IntArray(p);
        var deposited = read_pairs(p);
        var withdrawn = read_pairs(p);
        bool incomplete = p.ReadBool();
        return new WiredTransactionDetails(info, chestIds, deposited, withdrawn, incomplete);
    }

    /// <summary>Composes the details into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with the details of one wired transaction.</summary>
/// <remarks>Received as the Flash <c>WiredTransactionLogDetails</c> message. The hotel sends it in answer to <see cref="WiredTransactionGetLogDetails"/>.</remarks>
/// <param name="Details">The transaction details.</param>
public sealed record WiredTransactionLogDetails(WiredTransactionDetails Details)
    : IParserComposer<WiredTransactionLogDetails>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionLogDetails Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionLogDetails ParseFlash(in PacketReader p) =>
        new(WiredTransactionDetails.Parse(p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received when a wired transaction succeeds.</summary>
/// <remarks>Received as the Flash <c>WiredTransactionSuccess</c> message. The reward fields are on the wire only when <paramref name="TransactionSuccessTypeId"/> is 2 and bytes remain after it.</remarks>
/// <param name="TransactionSuccessTypeId">The success type code.</param>
/// <param name="RewardContents">The reward, or <see langword="null"/> when none was sent.</param>
/// <param name="RewardText">The reward text, empty when no reward was sent.</param>
/// <param name="OpenByDefault">Whether the reward is shown open by default, <see langword="false"/> when no reward was sent.</param>
public sealed record WiredTransactionSuccess(
    int TransactionSuccessTypeId,
    TradeRequirementRule? RewardContents,
    string RewardText,
    bool OpenByDefault) : IParserComposer<WiredTransactionSuccess>
{
    /// <summary>The success type code of a transaction that carries a reward.</summary>
    public const int TypeReward = 2;
    /// <summary>Gets whether the message carries a reward.</summary>
    public bool HasReward => RewardContents is not null;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with the contents of a wired contract.</summary>
/// <remarks>Received as the Flash <c>WiredContractContents</c> message. <see cref="WiredUpdateContract"/> writes the same layout. The payment fields are on the wire only for a payment contract and the reward fields only for a reward contract.</remarks>
/// <param name="ContractId">The id of the contract.</param>
/// <param name="ContractType">The contract type, 0 for payment, 1 for trade or 2 for reward, sent as a 16 bit integer.</param>
/// <param name="Definition">What the user gives and gets.</param>
/// <param name="PaymentMode">The payment mode of a payment contract, sent as a 16 bit integer, otherwise 0.</param>
/// <param name="ReceiveText">The receive text of a payment contract, otherwise empty.</param>
/// <param name="LayoutType">The layout type of a payment contract, otherwise empty.</param>
/// <param name="RewardCategory">The reward category of a reward contract, sent as a 16 bit integer, otherwise 0.</param>
/// <param name="ShowDialog">Whether a reward contract shows a dialog; otherwise, <see langword="false"/>.</param>
/// <param name="RewardText">The reward text of a reward contract, otherwise empty.</param>
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
    /// <summary>Gets or initializes the earnings category of a reward contract; game is 11 and agency is 13.</summary>
    /// <remarks>The raw RewardCategory short remains available for unknown codes.</remarks>
    public Qx.Model.Messages.Incoming.EarningCategory EarningsCategory
    {
        get => (Qx.Model.Messages.Incoming.EarningCategory)RewardCategory;
        init => RewardCategory = checked((short)value);
    }

    /// <summary>The contract type of a payment contract.</summary>
    public const int TypePayment = 0;
    /// <summary>The contract type of a trade contract.</summary>
    public const int TypeTrade = 1;
    /// <summary>The contract type of a reward contract.</summary>
    public const int TypeReward = 2;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with the result of a wired contract update.</summary>
/// <remarks>Received as the Flash <c>WiredContractUpdateResult</c> message. The hotel sends it in answer to <see cref="WiredUpdateContract"/>.</remarks>
/// <param name="ContractId">The id of the contract.</param>
/// <param name="IsSuccess">Whether the update succeeded.</param>
/// <param name="FailCode">The failure code of a failed update.</param>
public sealed record WiredContractUpdateResult(int ContractId, bool IsSuccess, string FailCode)
    : IParserComposer<WiredContractUpdateResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredContractUpdateResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredContractUpdateResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Represents the message that requests a wired contract or asks the client to open its editor.</summary>
/// <remarks>Sent and received as the Flash <c>WiredOpenContract</c> message. Sent, it requests the contract, which arrives as <see cref="WiredContractContents"/>. Received, it asks the client to open the contract editor.</remarks>
/// <param name="ContractId">The id of the contract.</param>
public sealed record WiredOpenContract(int ContractId) : IParserComposer<WiredOpenContract>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredOpenContract Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredOpenContract ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredOpenContract value, in PacketWriter p) =>
        p.WriteInt(value.ContractId);

}

// Trades.

// §_-ru§/§_-z1l§ — the classic two-user trading-window snapshot. Item elements are §_-X12§,
// which is byte-identical to the shared TradeItem parser.
/// <summary>Represents the offers of both users in a wired trade.</summary>
/// <param name="FirstUserId">The id of the first user, written as a 32 bit integer.</param>
/// <param name="FirstUserItems">The items the first user offers.</param>
/// <param name="FirstUserNumItems">The item count the hotel reports for the first user.</param>
/// <param name="FirstUserNumCredits">The credit count the hotel reports for the first user.</param>
/// <param name="SecondUserId">The id of the second user, written as a 32 bit integer.</param>
/// <param name="SecondUserItems">The items the second user offers.</param>
/// <param name="SecondUserNumItems">The item count the hotel reports for the second user.</param>
/// <param name="SecondUserNumCredits">The credit count the hotel reports for the second user.</param>
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
    /// <summary>Parses the offers from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the offers into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received when the hotel starts a wired trade.</summary>
/// <remarks>Received as the Flash <c>WiredTradeInitiate</c> message.</remarks>
/// <param name="Requirement">The requirement of the trade.</param>
/// <param name="ShowRequirementsImmediate">Whether the requirements are shown right away.</param>
/// <param name="OverridePreviousTrade">Whether the trade replaces a wired trade that is already open.</param>
/// <param name="TimeoutSeconds">The timeout of the trade in seconds.</param>
public sealed record WiredTradeInitiate(
    TradeRequirement Requirement,
    bool ShowRequirementsImmediate,
    bool OverridePreviousTrade,
    int TimeoutSeconds) : IParserComposer<WiredTradeInitiate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeInitiate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeInitiate ParseFlash(in PacketReader p) =>
        new(TradeRequirement.Parse(p), p.ReadBool(), p.ReadBool(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received when the offers in a wired trade change.</summary>
/// <remarks>Received as the Flash <c>WiredTradeItemsUpdate</c> message.</remarks>
/// <param name="TradingItems">The offers of both users.</param>
/// <param name="CanAccept">Whether the trade can be accepted.</param>
/// <param name="RequirementsMetCount">The number of times the current offer meets its requirements.</param>
public sealed record WiredTradeItemsUpdate(WiredTradingItems TradingItems, bool CanAccept, int RequirementsMetCount)
    : IParserComposer<WiredTradeItemsUpdate>
{
    /// <summary>Gets or initializes RequirementsMetCount; retained for migration.</summary>
    [Obsolete("Use RequirementsMetCount.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public int Extra { get => RequirementsMetCount; init => RequirementsMetCount = value; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeItemsUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeItemsUpdate ParseFlash(in PacketReader p) =>
        new(WiredTradingItems.Parse(p), p.ReadBool(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeItemsUpdate value, in PacketWriter p)
    {
        WiredChestWire.Validate(value.TradingItems, in p);
        value.TradingItems.Compose(p);
        p.WriteBool(value.CanAccept);
        p.WriteInt(value.RequirementsMetCount);
    }
}

/// <summary>Received when a wired trade is canceled.</summary>
/// <remarks>Received as the Flash <c>WiredTradeCancelled</c> message.</remarks>
/// <param name="TransactionFailureTypeId">The failure type code that explains the outcome.</param>
public sealed record WiredTradeCancelled(int TransactionFailureTypeId) : IParserComposer<WiredTradeCancelled>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeCancelled Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeCancelled ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeCancelled value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);
}

/// <summary>Received when a wired transaction fails.</summary>
/// <remarks>Received as the Flash <c>WiredTransactionFail</c> message.</remarks>
/// <param name="TransactionFailureTypeId">The failure type code that explains the outcome.</param>
public sealed record WiredTransactionFail(int TransactionFailureTypeId)
    : IParserComposer<WiredTransactionFail>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionFail Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionFail ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionFail value, in PacketWriter p) =>
        p.WriteInt(value.TransactionFailureTypeId);
}

/// <summary>Received with a wired trade transaction notification.</summary>
/// <remarks>Received as the Flash <c>WiredTradeTransactionNotification</c> message.</remarks>
/// <param name="TradeErrorId">The trade-error localization code.</param>
public sealed record WiredTradeTransactionNotification(int TradeErrorId)
    : IParserComposer<WiredTradeTransactionNotification>
{
    /// <summary>Gets or initializes TradeErrorId; retained for migration.</summary>
    [Obsolete("Use TradeErrorId.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public int TradeTransactionNotificationId { get => TradeErrorId; init => TradeErrorId = value; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeTransactionNotification Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeTransactionNotification ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        WiredTradeTransactionNotification value,
        in PacketWriter p) => p.WriteInt(value.TradeErrorId);
}

/// <summary>Received when a wired trade completes.</summary>
/// <remarks>Received as the Flash <c>WiredTradeCompleted</c> message. The message carries no fields.</remarks>
public sealed record WiredTradeCompleted : IParserComposer<WiredTradeCompleted>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeCompleted Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeCompleted ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCompleted));
        return new WiredTradeCompleted();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeCompleted value, in PacketWriter p) { }
}

// Outgoing composers. Each mirrors the SWF getMessageArray() push order exactly.

// id 806
/// <summary>Requests the current contents of a wired chest.</summary>
/// <remarks>Sent as the Flash <c>OpenChestAndGetContents</c> message.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
public sealed record OpenChestAndGetContents(Id ChestId) : IParserComposer<OpenChestAndGetContents>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static OpenChestAndGetContents Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenChestAndGetContents ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenChestAndGetContents value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 2935
/// <summary>Sent when the user closes a wired chest.</summary>
/// <remarks>Sent as the Flash <c>CloseChest</c> message.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
public sealed record CloseChest(Id ChestId) : IParserComposer<CloseChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CloseChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CloseChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CloseChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 1630
/// <summary>Sent when the user locks or unlocks wired chests.</summary>
/// <remarks>Sent as the Flash <c>LockAllChests</c> message.</remarks>
/// <param name="Lock">Whether the chests are locked.</param>
/// <param name="ApplyToAllInRoom">Whether the change applies to every chest in the room.</param>
public sealed record LockAllChests(bool Lock, bool ApplyToAllInRoom) : IParserComposer<LockAllChests>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static LockAllChests Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static LockAllChests ParseFlash(in PacketReader p) => new(p.ReadBool(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(LockAllChests value, in PacketWriter p)
    {
        p.WriteBool(value.Lock);
        p.WriteBool(value.ApplyToAllInRoom);
    }

}

// id 3407
/// <summary>Sent when the user buys capacity upgrades for a wired chest.</summary>
/// <remarks>Sent as the Flash <c>UpgradeChest</c> message. The hotel answers with <see cref="UpgradeChestResult"/>.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="UpgradeAmount">The number of upgrades to buy.</param>
public sealed record UpgradeChest(int ChestId, int UpgradeAmount) : IParserComposer<UpgradeChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UpgradeChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpgradeChest ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpgradeChest value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.UpgradeAmount);
    }

}

// id 3611
/// <summary>Sent when the user withdraws everything available from a wired chest.</summary>
/// <remarks>Sent as the Flash <c>WithdrawAllFromChest</c> message.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
public sealed record WithdrawAllFromChest(Id ChestId) : IParserComposer<WithdrawAllFromChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WithdrawAllFromChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WithdrawAllFromChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WithdrawAllFromChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 2843
/// <summary>Sent when the user withdraws coins from a wired chest.</summary>
/// <remarks>Sent as the Flash <c>WithdrawCoinsFromChest</c> message.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
/// <param name="CoinAmount">The number of coins to withdraw.</param>
public sealed record WithdrawCoinsFromChest(Id ChestId, int CoinAmount) : IParserComposer<WithdrawCoinsFromChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WithdrawCoinsFromChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WithdrawCoinsFromChest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WithdrawCoinsFromChest value, in PacketWriter p)
    {
        p.WriteInt(WiredWire.FlashId(value.ChestId));
        p.WriteInt(value.CoinAmount);
    }
}

// id 873 — ChestItemType expands to bool/int/string between the two ints.
/// <summary>Sent when the user withdraws items of one furni type from a wired chest.</summary>
/// <remarks>Sent as the Flash <c>WithdrawItemsFromChest</c> message.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
/// <param name="ItemType">The furni type to withdraw.</param>
/// <param name="Count">The number of items to withdraw.</param>
public sealed record WithdrawItemsFromChest(Id ChestId, ChestItemType ItemType, int Count)
    : IParserComposer<WithdrawItemsFromChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WithdrawItemsFromChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WithdrawItemsFromChest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), ChestItemType.Parse(p), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Sent when the user starts depositing inventory items into a wired chest.</summary>
/// <remarks>Sent as the Flash <c>StartAddingToChest</c> message. The hotel answers by starting a wired trade with <see cref="WiredTradeInitiate"/>.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
public sealed record StartAddingToChest(Id ChestId) : IParserComposer<StartAddingToChest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static StartAddingToChest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static StartAddingToChest ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(StartAddingToChest value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.ChestId));
}

// id 2905
/// <summary>Sent when the user changes the notification preferences of a wired chest.</summary>
/// <remarks>Sent as the Flash <c>SetChestNotificationPreferences</c> message. The hotel confirms with <see cref="ChestPreferencesUpdateSuccess"/>.</remarks>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="NotificationMode">The notification mode code.</param>
/// <param name="NotifyWhenFull">Whether to notify when the chest is full.</param>
/// <param name="NotifyOnDonation">Whether to notify when someone donates.</param>
/// <param name="NotifyOnWithdrawal">Whether to notify when someone withdraws.</param>
/// <param name="NotifyWhenEmpty">Whether to notify when the chest is empty.</param>
/// <param name="NotifyOnWiredTransaction">Whether to notify on a Wired transaction.</param>
public sealed record SetChestNotificationPreferences(
    int ChestId,
    int NotificationMode,
    bool NotifyWhenFull,
    bool NotifyOnDonation,
    bool NotifyOnWithdrawal,
    bool NotifyWhenEmpty,
    bool NotifyOnWiredTransaction) : IParserComposer<SetChestNotificationPreferences>
{
    /// <summary>Gets or initializes the typed notification mode, preserving unknown integer codes.</summary>
    public WiredChestNotificationMode Notifications
    {
        get => (WiredChestNotificationMode)NotificationMode;
        init => NotificationMode = (int)value;
    }

    /// <summary>Gets or initializes NotifyWhenFull; retained for migration.</summary>
    [Obsolete("Use NotifyWhenFull.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NotifyFlagA { get => NotifyWhenFull; init => NotifyWhenFull = value; }

    /// <summary>Gets or initializes NotifyOnDonation; retained for migration.</summary>
    [Obsolete("Use NotifyOnDonation.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NotifyFlagB { get => NotifyOnDonation; init => NotifyOnDonation = value; }

    /// <summary>Gets or initializes NotifyOnWithdrawal; retained for migration.</summary>
    [Obsolete("Use NotifyOnWithdrawal.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool EventFlagA { get => NotifyOnWithdrawal; init => NotifyOnWithdrawal = value; }

    /// <summary>Gets or initializes NotifyWhenEmpty; retained for migration.</summary>
    [Obsolete("Use NotifyWhenEmpty.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool EventFlagB { get => NotifyWhenEmpty; init => NotifyWhenEmpty = value; }

    /// <summary>Gets or initializes NotifyOnWiredTransaction; retained for migration.</summary>
    [Obsolete("Use NotifyOnWiredTransaction.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool EventFlagC { get => NotifyOnWiredTransaction; init => NotifyOnWiredTransaction = value; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SetChestNotificationPreferences value, in PacketWriter p)
    {
        p.WriteInt(value.ChestId);
        p.WriteInt(value.NotificationMode);
        p.WriteBool(value.NotifyWhenFull);
        p.WriteBool(value.NotifyOnDonation);
        p.WriteBool(value.NotifyOnWithdrawal);
        p.WriteBool(value.NotifyWhenEmpty);
        p.WriteBool(value.NotifyOnWiredTransaction);
    }

}

// id 2907
/// <summary>Sent when the user changes the lock and capacity options of a wired chest.</summary>
/// <remarks>Sent as the Flash <c>SetChestOptions</c> message.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
/// <param name="LockChest">Whether the chest is locked.</param>
/// <param name="AutoLockChest">Whether the chest locks automatically.</param>
/// <param name="Capacity">The capacity of the chest.</param>
public sealed record SetChestOptions(Id ChestId, bool LockChest, bool AutoLockChest, int Capacity)
    : IParserComposer<SetChestOptions>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SetChestOptions Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SetChestOptions ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadBool(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Sent when the user changes the name, description, state and preview preferences of a wired chest.</summary>
/// <remarks>Sent as the Flash <c>SetChestPreferences</c> message. The hotel confirms with <see cref="ChestPreferencesUpdateSuccess"/>.</remarks>
/// <param name="ChestId">The id of the chest, written as a 32 bit integer.</param>
/// <param name="ChestName">The name of the chest.</param>
/// <param name="ChestDescription">The description of the chest.</param>
/// <param name="EveryoneCanOpen">Whether everyone can open the chest.</param>
/// <param name="EveryoneCanDonate">Whether everyone can donate to the chest.</param>
/// <param name="StateControlMode">The raw chest state-control mode; see StateControl.</param>
/// <param name="PreviewMode">The raw furniture preview mode; see Preview.</param>
/// <param name="PreviewAmount">The number of items to preview, normally 1 through 4.</param>
/// <param name="WiredEnabled">Whether Wired is enabled for the chest.</param>
public sealed record SetChestPreferences(
    Id ChestId,
    string ChestName,
    string ChestDescription,
    bool EveryoneCanOpen,
    bool EveryoneCanDonate,
    int StateControlMode,
    int PreviewMode,
    int PreviewAmount,
    bool WiredEnabled) : IParserComposer<SetChestPreferences>
{
    /// <summary>Gets or initializes the typed state-control mode, preserving unknown integer codes.</summary>
    public WiredChestStateMode StateControl
    {
        get => (WiredChestStateMode)StateControlMode;
        init => StateControlMode = (int)value;
    }

    /// <summary>Gets or initializes the typed preview mode, preserving unknown integer codes.</summary>
    public WiredChestPreviewMode Preview
    {
        get => (WiredChestPreviewMode)PreviewMode;
        init => PreviewMode = (int)value;
    }

    /// <summary>Gets or initializes EveryoneCanOpen; retained for migration.</summary>
    [Obsolete("Use EveryoneCanOpen.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool PrefFlagA { get => EveryoneCanOpen; init => EveryoneCanOpen = value; }

    /// <summary>Gets or initializes EveryoneCanDonate; retained for migration.</summary>
    [Obsolete("Use EveryoneCanDonate.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool PrefFlagB { get => EveryoneCanDonate; init => EveryoneCanDonate = value; }

    /// <summary>Gets or initializes StateControlMode; retained for migration.</summary>
    [Obsolete("Use StateControlMode.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public int ChestState { get => StateControlMode; init => StateControlMode = value; }

    /// <summary>Gets or initializes PreviewMode; retained for migration.</summary>
    [Obsolete("Use PreviewMode.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public int OpenState { get => PreviewMode; init => PreviewMode = value; }

    /// <summary>Gets or initializes PreviewAmount; retained for migration.</summary>
    [Obsolete("Use PreviewAmount.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public int AmountPreview { get => PreviewAmount; init => PreviewAmount = value; }

    /// <summary>Gets or initializes WiredEnabled; retained for migration.</summary>
    [Obsolete("Use WiredEnabled.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool DisabledFlag { get => WiredEnabled; init => WiredEnabled = value; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
        p.WriteBool(value.EveryoneCanOpen);
        p.WriteBool(value.EveryoneCanDonate);
        p.WriteInt(value.StateControlMode);
        p.WriteInt(value.PreviewMode);
        p.WriteInt(value.PreviewAmount);
        p.WriteBool(value.WiredEnabled);
    }
}

// id 1999
/// <summary>Requests one page of the transaction log of a wired chest.</summary>
/// <remarks>Sent as the Flash <c>WiredTransactionGetChestLogs</c> message. The hotel answers with <see cref="WiredTransactionLogList"/>.</remarks>
/// <param name="LogListId">The id of the chest transaction log list.</param>
/// <param name="PageSize">The number of rows per page.</param>
/// <param name="Page">The one based page number.</param>
public sealed record WiredTransactionGetChestLogs(int LogListId, int PageSize, int Page)
    : IParserComposer<WiredTransactionGetChestLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionGetChestLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionGetChestLogs ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Requests the details of one wired transaction.</summary>
/// <remarks>Sent as the Flash <c>WiredTransactionGetLogDetails</c> message. The hotel answers with <see cref="WiredTransactionLogDetails"/>.</remarks>
/// <param name="TransactionId">The id of the transaction, written as a 64 bit integer.</param>
public sealed record WiredTransactionGetLogDetails(long TransactionId)
    : IParserComposer<WiredTransactionGetLogDetails>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionGetLogDetails Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionGetLogDetails ParseFlash(in PacketReader p) => new(p.ReadLong());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionGetLogDetails value, in PacketWriter p) =>
        p.WriteLong(value.TransactionId);

}

// id 2016
/// <summary>Requests one page of the room's wired transaction log.</summary>
/// <remarks>Sent as the Flash <c>WiredTransactionGetRoomLogs</c> message. The hotel answers with <see cref="WiredTransactionLogList"/>.</remarks>
/// <param name="PageSize">The number of rows per page.</param>
/// <param name="Page">The one based page number.</param>
public sealed record WiredTransactionGetRoomLogs(int PageSize, int Page)
    : IParserComposer<WiredTransactionGetRoomLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTransactionGetRoomLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTransactionGetRoomLogs ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTransactionGetRoomLogs value, in PacketWriter p)
    {
        p.WriteInt(value.PageSize);
        p.WriteInt(value.Page);
    }

}

// id 1908 — writes the exact same layout WiredContractContents(2976) reads.
/// <summary>Sent when the user saves a wired contract.</summary>
/// <remarks>Sent as the Flash <c>WiredUpdateContract</c> message. It writes the same layout that <see cref="WiredContractContents"/> reads, and the hotel answers with <see cref="WiredContractUpdateResult"/>.</remarks>
/// <param name="Contract">The complete contract definition.</param>
public sealed record WiredUpdateContract(WiredContractContents Contract) : IParserComposer<WiredUpdateContract>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredUpdateContract Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredUpdateContract ParseFlash(in PacketReader p) =>
        new(WiredContractContents.Read(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUpdateContract value, in PacketWriter p) =>
        WiredContractContents.Write(value.Contract, in p);

}

// id 3111. The flag says REMOVE, not add: WiredTradingModel.requestAddItemsToTrading sends false
// and requestRemoveItemFromTrading sends true. Naming it the other way round makes a deposit
// arrive as a withdrawal of items the offer does not hold, which the hotel answers by refusing.
/// <summary>Sent when the user adds items to or removes items from the open wired trade.</summary>
/// <remarks>Sent as the Flash <c>WiredTradeAddDeleteItems</c> message. Use <see cref="WiredTradeAddDeleteItems.Add"/> or <see cref="WiredTradeAddDeleteItems.Remove"/> to create an instance.</remarks>
/// <param name="IsRemove">Whether the items are taken back out of the offer instead of added.</param>
/// <param name="Ids">The inventory ids of the items, each written as a 32 bit integer.</param>
public sealed record WiredTradeAddDeleteItems(bool IsRemove, IReadOnlyList<Id> Ids)
    : IParserComposer<WiredTradeAddDeleteItems>
{
    /// <summary>Creates a request that offers items in the open wired trade.</summary>
    /// <param name="ids">The inventory ids of the items to offer.</param>
    /// <returns>A request with <see cref="IsRemove"/> cleared.</returns>
    public static WiredTradeAddDeleteItems Add(IReadOnlyList<Id> ids) => new(false, ids);

    /// <summary>Creates a request that takes items back out of the open wired trade.</summary>
    /// <param name="ids">The inventory ids of the items to take back.</param>
    /// <returns>A request with <see cref="IsRemove"/> set.</returns>
    public static WiredTradeAddDeleteItems Remove(IReadOnlyList<Id> ids) => new(true, ids);

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Sent when the user cancels the open wired trade.</summary>
/// <remarks>Sent as the Flash <c>WiredTradeCancel</c> message, which carries no fields.</remarks>
public sealed record WiredTradeCancel : IParserComposer<WiredTradeCancel>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeCancel Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeCancel ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredTradeCancel));
        return new WiredTradeCancel();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTradeCancel value, in PacketWriter p) { }
}

// id 2818
/// <summary>Sent for initial acceptance or final confirmation of an open wired trade.</summary>
/// <remarks>Sent as the Flash <c>WiredTradeConfirm</c> message.</remarks>
/// <param name="Confirm">False for initial acceptance; true for final confirmation after the countdown.</param>
public sealed record WiredTradeConfirm(bool Confirm) : IParserComposer<WiredTradeConfirm>
{
    /// <summary>Creates an explicit acceptance or final-confirmation message.</summary>
    /// <param name="stage">The stage to send.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stage is unknown.</exception>
    public WiredTradeConfirm(WiredTradeConfirmationStage stage) : this(stage switch
    {
        WiredTradeConfirmationStage.Accept => false,
        WiredTradeConfirmationStage.Confirm => true,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    }) { }

    /// <summary>Gets the confirmation stage represented by the wire flag.</summary>
    public WiredTradeConfirmationStage Stage => Confirm
        ? WiredTradeConfirmationStage.Confirm
        : WiredTradeConfirmationStage.Accept;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTradeConfirm Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTradeConfirm ParseFlash(in PacketReader p) => new(p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
        _ = checked((sbyte)value.Type);
        if (value.Type == TradeRequirementNode.TypeFurni)
            Validate(value.ItemType, in p);
        else if (value.ItemType is not null)
            throw new InvalidDataException("Only furni trade requirement nodes can carry an item type.");
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
