using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a furni offered in a trade.</summary>
public sealed class TradeItem : IParserComposer<TradeItem>
{
    private string? wire_type;

    /// <summary>Gets or sets the inventory item identifier, which addresses the item within the trade.</summary>
    public Id ItemId { get; set; }
    /// <summary>Gets or sets whether the item is a floor or a wall item.</summary>
    public ItemType Type { get; set; }
    /// <summary>Gets or sets the original floor or wall type string carried by the packet.</summary>
    /// <remarks>Changing the type invalidates incompatible spelling; newly created items use uppercase S or I.</remarks>
    /// <exception cref="ArgumentException">The value does not identify a floor or wall item.</exception>
    public string WireType
    {
        get => wire_type is not null && ItemTypes.FromShort(wire_type) == Type
            ? wire_type
            : Type is ItemType.Floor ? "S" : "I";
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ItemType type = ItemTypes.FromShort(value);
            if (type is not (ItemType.Floor or ItemType.Wall))
                throw new ArgumentException("The wire type must identify a floor or wall item.", nameof(value));
            wire_type = value;
            Type = type;
        }
    }
    /// <summary>Gets or sets the room item identifier of the furni.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the furni kind identifier.</summary>
    public int Kind { get; set; }
    /// <summary>Gets or sets the item's category, which uses the <see cref="FurniCategory"/> numbering.</summary>
    public int Category { get; set; }
    /// <summary>Gets or sets whether the client may group the item with others of the same kind.</summary>
    public bool IsGroupable { get; set; }
    /// <summary>Gets or sets the item's payload.</summary>
    public ItemData Data { get; set; } = new LegacyData();
    /// <summary>Gets or sets the day of the month the item was created.</summary>
    public int CreationDay { get; set; }
    /// <summary>Gets or sets the month the item was created.</summary>
    public int CreationMonth { get; set; }
    /// <summary>Gets or sets the year the item was created.</summary>
    public int CreationYear { get; set; }
    /// <summary>Gets or sets the extra value of a floor item, which the client also uses as its song identifier, or -1 for a wall item.</summary>
    public long Extra { get; set; } = -1;

    /// <summary>Gets whether the item is a floor item.</summary>
    public bool IsFloorItem => Type is ItemType.Floor;
    /// <summary>Gets whether the item is a wall item.</summary>
    public bool IsWallItem => Type is ItemType.Wall;

    /// <summary>Initializes a new instance of the <see cref="TradeItem"/> class.</summary>
    public TradeItem() { }

    /// <summary>Reads a trade item from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when the item type is unknown.</exception>
    public static TradeItem Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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
            WireType = wire_type,
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
        return value;
    }

    /// <summary>Writes the trade item to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the item type is neither floor nor wall, an identifier is out of range, or
    /// <see cref="Extra"/> does not match the item type.
    /// </exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeItem value, in PacketWriter p)
    {
        value.ValidateFlash(in p);
        p.WriteInt(TradeWire.FlashId(value.ItemId, nameof(ItemId)));
        p.WriteString(value.WireType);
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

    internal void ValidateFlash(in PacketWriter p)
    {
        TradeWire.RequireItemType(Type);
        _ = TradeWire.FlashId(ItemId, nameof(ItemId));
        _ = TradeWire.FlashId(Id, nameof(Id));
        TradeWire.ValidateItemData(Data, in p);
        if (Type is ItemType.Floor)
            _ = checked((int)Extra);
        else if (Extra != -1)
            throw new InvalidDataException("Wall trade items cannot carry floor-item metadata.");
    }

    /// <summary>Returns the inventory item identifier and kind.</summary>
    /// <returns>A string in the form <c>TradeItem#ItemId/Kind</c>.</returns>
    public override string ToString() => $"{nameof(TradeItem)}#{ItemId}/{Kind}";
}

internal static class TradeWire
{
    public const int FlashTradeItemMinimumBytes = 35;
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

    public static void RequireItemType(ItemType type)
    {
        if (type is not (ItemType.Floor or ItemType.Wall))
            throw new InvalidDataException($"Unsupported trade item type {type}.");
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
        string name)
    {
        var seen = new HashSet<long>();
        foreach (Id value in values)
        {
            RequireNonZeroFlashId(value, name);
            if (!seen.Add(value))
                throw new InvalidDataException($"{name} contains duplicate ID {value}.");
        }
    }

    public static void ValidateItemData(ItemData data, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(data);
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
