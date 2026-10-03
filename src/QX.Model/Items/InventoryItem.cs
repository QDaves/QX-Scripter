using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a furni in the local user's inventory.</summary>
public sealed class InventoryItem : IParserComposer<InventoryItem>
{
    /// <summary>Gets or sets the inventory item identifier, which addresses the item while it is in the inventory.</summary>
    public Id ItemId { get; set; }
    /// <summary>Gets or sets whether the item is a floor or a wall item.</summary>
    public ItemType Type { get; set; }
    /// <summary>Gets or sets the room item identifier the furni has when placed.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the furni kind identifier.</summary>
    public int Kind { get; set; }
    /// <summary>Gets or sets the item's category, which uses the <see cref="FurniCategory"/> numbering.</summary>
    public int Category { get; set; }
    /// <summary>Gets or sets the item's payload.</summary>
    public ItemData Data { get; set; } = new EmptyItemData();
    /// <summary>Gets or sets whether the item may be recycled.</summary>
    public bool IsRecyclable { get; set; }
    /// <summary>Gets or sets whether the item may be traded.</summary>
    public bool IsTradeable { get; set; }
    /// <summary>Gets or sets whether the client may group the item with others of the same kind.</summary>
    public bool IsGroupable { get; set; }
    /// <summary>Gets or sets whether the item may be sold on the marketplace.</summary>
    public bool IsSellable { get; set; }
    /// <summary>Gets or sets the seconds until a rented item expires, or -1 when it does not expire.</summary>
    public int SecondsToExpiration { get; set; } = -1;
    /// <summary>Gets or sets whether the rent period of a rented item has started.</summary>
    public bool HasRentPeriodStarted { get; set; }
    /// <summary>Gets or sets the room identifier the hotel sends with the item.</summary>
    public Id RoomId { get; set; }
    /// <summary>Gets or sets the slot identifier of a floor item, empty for a wall item.</summary>
    public string SlotId { get; set; } = "";
    /// <summary>Gets or sets the extra value of a floor item, 0 for a wall item.</summary>
    public long Extra { get; set; }

    /// <summary>Gets whether the item is a floor item.</summary>
    public bool IsFloorItem => Type is ItemType.Floor;
    /// <summary>Gets whether the item is a wall item.</summary>
    public bool IsWallItem => Type is ItemType.Wall;

    /// <summary>Initializes a new instance of the <see cref="InventoryItem"/> class.</summary>
    public InventoryItem() { }

    /// <summary>Reads an inventory item from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when the item type is neither floor nor wall.</exception>
    public static InventoryItem Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static InventoryItem ParseFlash(in PacketReader p)
    {
        Id item_id = p.ReadInt();
        ItemType type = ItemTypes.FromShort(p.ReadString());
        InventoryWire.RequireItemType(type);
        var item = new InventoryItem
        {
            ItemId = item_id,
            Type = type,
            Id = p.ReadInt(),
            Kind = p.ReadInt(),
            Category = p.ReadInt(),
            Data = p.Parse<ItemData>(),
            IsRecyclable = p.ReadBool(),
            IsTradeable = p.ReadBool(),
            IsGroupable = p.ReadBool(),
            IsSellable = p.ReadBool(),
            SecondsToExpiration = p.ReadInt(),
            HasRentPeriodStarted = p.ReadBool(),
            RoomId = p.ReadInt()
        };

        if (item.Type is ItemType.Floor)
        {
            item.SlotId = p.ReadString();
            item.Extra = p.ReadInt();
        }

        return item;
    }

    /// <summary>Writes the inventory item to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the item type is neither floor nor wall, or a wall item carries a slot identifier
    /// or extra value.
    /// </exception>
    /// <exception cref="OverflowException">Thrown when an identifier or <see cref="Extra"/> does not fit in 32 bits.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(InventoryItem value, in PacketWriter p)
    {
        value.ValidateFlash(in p);
        p.WriteInt(InventoryWire.Int32Id(value.ItemId));
        p.WriteString(value.Type.ToShort());
        p.WriteInt(InventoryWire.Int32Id(value.Id));
        p.WriteInt(value.Kind);
        p.WriteInt(value.Category);
        p.Compose(value.Data);
        p.WriteBool(value.IsRecyclable);
        p.WriteBool(value.IsTradeable);
        p.WriteBool(value.IsGroupable);
        p.WriteBool(value.IsSellable);
        p.WriteInt(value.SecondsToExpiration);
        p.WriteBool(value.HasRentPeriodStarted);
        p.WriteInt(InventoryWire.Int32Id(value.RoomId));

        if (value.Type is ItemType.Floor)
        {
            p.WriteString(value.SlotId);
            p.WriteInt(checked((int)value.Extra));
        }
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        InventoryWire.RequireItemType(Type);
        _ = InventoryWire.Int32Id(ItemId);
        _ = InventoryWire.Int32Id(Id);
        _ = InventoryWire.Int32Id(RoomId);
        InventoryWire.ValidateItemData(Data, in p);
        ValidatePlacement(in p);
    }

    private void ValidatePlacement(in PacketWriter p)
    {
        InventoryWire.RequireString(SlotId, nameof(SlotId), in p);
        if (Type is ItemType.Floor)
        {
            _ = checked((int)Extra);
            return;
        }
        if (SlotId.Length != 0 || Extra != 0)
            throw new InvalidDataException("Wall inventory items cannot carry floor-item metadata.");
    }

    /// <summary>Returns the inventory item identifier and kind.</summary>
    /// <returns>A string in the form <c>InventoryItem#ItemId/Kind</c>.</returns>
    public override string ToString() => $"{nameof(InventoryItem)}#{ItemId}/{Kind}";
}

internal static class InventoryWire
{
    public static int Int32Id(Id value) => checked((int)(long)value);

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
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

    public static void RequireFragment(int total, int index, string name)
    {
        if (total <= 0)
            throw new InvalidDataException($"{name} fragment count must be positive, received {total}.");
        if ((uint)index >= (uint)total)
        {
            throw new InvalidDataException(
                $"{name} fragment index {index} is outside 0..{total - 1}.");
        }
    }

    public static void RequireItemType(ItemType type)
    {
        if (type is not (ItemType.Floor or ItemType.Wall))
            throw new InvalidDataException($"Unsupported inventory item type {type}.");
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
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
                    $"Unsupported inventory item-data type {data.GetType().FullName}.");
        }
    }

    private static void RequireNestedCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the wire limit.");
    }
}
