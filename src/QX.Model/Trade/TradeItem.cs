using Qx.Messages;

namespace Qx.Model;

public sealed class TradeItem : IParserComposer<TradeItem>
{
    public Id ItemId { get; set; }
    public ItemType Type { get; set; }
    public Id Id { get; set; }
    public int Kind { get; set; }
    public int Category { get; set; }
    public bool IsGroupable { get; set; }
    public ItemData Data { get; set; } = new LegacyData();
    public int CreationDay { get; set; }
    public int CreationMonth { get; set; }
    public int CreationYear { get; set; }
    public long Extra { get; set; } = -1;

    public bool IsFloorItem => Type is ItemType.Floor;
    public bool IsWallItem => Type is ItemType.Wall;

    public TradeItem() { }

    public static TradeItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeItem ParseFlash(in PacketReader p)
    {
        Id item_id = p.ReadInt();
        string wire_type = p.ReadString();
        ItemType type = ItemTypes.FromShort(wire_type);
        if (type is not (ItemType.Floor or ItemType.Wall))
            throw new InvalidDataException($"Unknown Flash trade item type '{wire_type}'.");
        Id id = p.ReadInt();
        int kind = p.ReadInt();
        int category = p.ReadInt();
        bool is_groupable = p.ReadBool();
        ItemData data = p.Parse<ItemData>();
        int creation_day = p.ReadInt();
        int creation_month = p.ReadInt();
        int creation_year = p.ReadInt();
        long extra = type is ItemType.Floor ? p.ReadInt() : -1;
        var value = new TradeItem
        {
            ItemId = item_id,
            Type = type,
            Id = id,
            Kind = kind,
            Category = category,
            IsGroupable = is_groupable,
            Data = data,
            CreationDay = creation_day,
            CreationMonth = creation_month,
            CreationYear = creation_year,
            Extra = extra
        };
        TradeWire.RequirePositiveId(value.Id, nameof(Id));
        TradeWire.RequireFloorExtra(value.Type, value.Extra);
        return value;
    }

    private static TradeItem ParseUnity(in PacketReader p)
    {
        Id item_id = p.ReadLong();
        ItemType type = p.ReadShort() switch
        {
            0 => ItemType.Wall,
            1 => ItemType.Floor,
            short wire_type => throw new InvalidDataException(
                $"Unknown Unity trade item type '{wire_type}'.")
        };
        Id id = p.ReadLong();
        int kind = p.ReadInt();
        int category = p.ReadInt();
        bool is_groupable = p.ReadBool();
        ItemData data = p.Parse<ItemData>();
        int creation_day = p.ReadInt();
        int creation_month = p.ReadInt();
        int creation_year = p.ReadInt();
        long extra = type is ItemType.Floor ? p.ReadLong() : -1;
        var value = new TradeItem
        {
            ItemId = item_id,
            Type = type,
            Id = id,
            Kind = kind,
            Category = category,
            IsGroupable = is_groupable,
            Data = data,
            CreationDay = creation_day,
            CreationMonth = creation_month,
            CreationYear = creation_year,
            Extra = extra
        };
        TradeWire.RequirePositiveId(value.Id, nameof(Id));
        TradeWire.RequireFloorExtra(value.Type, value.Extra);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeItem value, in PacketWriter p)
    {
        value.ValidateFlash(in p);
        p.WriteInt(TradeWire.FlashId(value.ItemId, nameof(ItemId)));
        p.WriteString(value.Type is ItemType.Floor ? "S" : "I");
        p.WriteInt(TradeWire.FlashId(value.Id, nameof(Id)));
        p.WriteInt(value.Kind);
        p.WriteInt(value.Category);
        p.WriteBool(value.IsGroupable);
        p.Compose(value.Data);
        p.WriteInt(value.CreationDay);
        p.WriteInt(value.CreationMonth);
        p.WriteInt(value.CreationYear);
        if (value.Type is ItemType.Floor)
            p.WriteInt(checked((int)value.Extra));
    }

    private static void ComposeUnity(TradeItem value, in PacketWriter p)
    {
        value.ValidateUnity(in p);
        p.WriteLong(value.ItemId);
        p.WriteShort(value.Type is ItemType.Wall ? (short)0 : (short)1);
        p.WriteLong(value.Id);
        p.WriteInt(value.Kind);
        p.WriteInt(value.Category);
        p.WriteBool(value.IsGroupable);
        p.Compose(value.Data);
        p.WriteInt(value.CreationDay);
        p.WriteInt(value.CreationMonth);
        p.WriteInt(value.CreationYear);
        if (value.Type is ItemType.Floor)
            p.WriteLong(value.Extra);
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        TradeWire.RequireItemType(Type);
        _ = TradeWire.FlashId(ItemId, nameof(ItemId));
        TradeWire.RequirePositiveFlashId(Id, nameof(Id));
        TradeWire.ValidateItemData(Data, false, in p);
        TradeWire.RequireFloorExtra(Type, Extra);
        if (Type is ItemType.Floor)
            _ = checked((int)Extra);
        else if (Extra != -1)
            throw new InvalidDataException("Wall trade items cannot carry floor-item metadata.");
    }

    internal void ValidateUnity(in PacketWriter p)
    {
        TradeWire.RequireItemType(Type);
        TradeWire.RequirePositiveId(Id, nameof(Id));
        TradeWire.ValidateItemData(Data, true, in p);
        TradeWire.RequireFloorExtra(Type, Extra);
        if (Type is ItemType.Wall && Extra != -1)
            throw new InvalidDataException("Wall trade items cannot carry floor-item metadata.");
    }

    public override string ToString() => $"{nameof(TradeItem)}#{ItemId}/{Kind}";
}

internal static class TradeWire
{
    public const int FlashTradeItemMinimumBytes = 35;
    public const int UnityTradeItemMinimumBytes = 43;
    public const int NftAssetMinimumBytes = 26;

    public static int FlashId(Id value, string name)
    {
        try
        {
            return checked((int)(long)value);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{name} does not fit the Flash wire format.", exception);
        }
    }

    public static int RequireCount(int count, int available, int minimum_bytes, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (available < 0 || minimum_bytes <= 0 || count > available / minimum_bytes)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
        }
        return count;
    }

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
    }

    public static void RequireItemType(ItemType type)
    {
        if (type is not (ItemType.Floor or ItemType.Wall))
            throw new InvalidDataException($"Unsupported trade item type {type}.");
    }

    public static void RequireFloorExtra(ItemType type, long extra)
    {
        if (type is ItemType.Floor && extra < 0)
            throw new InvalidDataException("Floor trade-item metadata cannot be negative.");
    }

    public static void RequirePositiveId(Id value, string name)
    {
        if ((long)value <= 0)
            throw new InvalidDataException($"{name} must be positive.");
    }

    public static void RequirePositiveFlashId(Id value, string name)
    {
        RequirePositiveId(value, name);
        _ = FlashId(value, name);
    }

    public static void RequireNonZeroFlashId(Id value, string name)
    {
        RequireNonZeroId(value, name);
        _ = FlashId(value, name);
    }

    public static void RequireNonZeroId(Id value, string name)
    {
        if ((long)value == 0)
            throw new InvalidDataException($"{name} cannot be zero.");
    }

    public static void RequireNonNegative(int value, string name)
    {
        if (value < 0)
            throw new InvalidDataException($"{name} cannot be negative.");
    }

    public static bool ReadBooleanInt(int value, string name) => value switch
    {
        0 => false,
        1 => true,
        _ => throw new InvalidDataException($"{name} contains invalid Boolean value {value}.")
    };

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }

    public static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    public static IReadOnlyList<T> FreezeReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        T[] copy = values.ToArray();
        foreach (T value in copy)
            ArgumentNullException.ThrowIfNull(value, name);
        return Array.AsReadOnly(copy);
    }

    public static void RequireDistinctIds(
        IReadOnlyList<Id> values,
        bool flash,
        string name)
    {
        var seen = new HashSet<long>();
        foreach (Id value in values)
        {
            if (flash)
                RequireNonZeroFlashId(value, name);
            else
                RequireNonZeroId(value, name);
            if (!seen.Add(value))
                throw new InvalidDataException($"{name} contains duplicate ID {value}.");
        }
    }

    public static void ValidateItemData(ItemData data, bool unity, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (unity && data.IsLimitedRare)
            RequireString(data.UniqueLimitedData, nameof(data.UniqueLimitedData), in p);
        switch (data)
        {
            case LegacyData legacy:
                RequireString(legacy.Value, nameof(legacy.Value), in p);
                break;
            case MapData map:
                RequireNestedCount(map.Entries.Count, nameof(map.Entries));
                foreach ((string key, string value) in map.Entries)
                {
                    RequireString(key, nameof(map.Entries), in p);
                    RequireString(value, nameof(map.Entries), in p);
                }
                break;
            case StringArrayData strings:
                RequireNestedCount(strings.Values.Count, nameof(strings.Values));
                foreach (string value in strings.Values)
                    RequireString(value, nameof(strings.Values), in p);
                break;
            case VoteResultData vote:
                RequireString(vote.Value, nameof(vote.Value), in p);
                break;
            case EmptyItemData:
                break;
            case IntArrayData integers:
                RequireNestedCount(integers.Values.Count, nameof(integers.Values));
                break;
            case HighScoreData scores:
                RequireString(scores.Value, nameof(scores.Value), in p);
                RequireNestedCount(scores.Scores.Count, nameof(scores.Scores));
                foreach (HighScore score in scores.Scores)
                {
                    ArgumentNullException.ThrowIfNull(score);
                    ArgumentNullException.ThrowIfNull(score.Names);
                    RequireNestedCount(score.Names.Count, nameof(score.Names));
                    foreach (string name in score.Names)
                        RequireString(name, nameof(score.Names), in p);
                }
                break;
            case CrackableFurniData crackable:
                RequireString(crackable.Value, nameof(crackable.Value), in p);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported trade item-data type {data.GetType().FullName}.");
        }
    }

    private static void RequireNestedCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the wire limit.");
    }
}
