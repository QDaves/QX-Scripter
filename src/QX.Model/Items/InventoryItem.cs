using Qx.Messages;

namespace Qx.Model;

public sealed class InventoryItem : IParserComposer<InventoryItem>
{
    public Id ItemId { get; set; }
    public ItemType Type { get; set; }
    public Id Id { get; set; }
    public int Kind { get; set; }
    public int Category { get; set; }
    public ItemData Data { get; set; } = new EmptyItemData();
    public bool IsRecyclable { get; set; }
    public bool IsTradeable { get; set; }
    public bool IsGroupable { get; set; }
    public bool IsSellable { get; set; }
    public int SecondsToExpiration { get; set; } = -1;
    public bool HasRentPeriodStarted { get; set; }
    public Id RoomId { get; set; }
    public bool IsUnseen { get; set; }
    public long Timestamp { get; set; }
    public bool IsNft { get; set; }
    public string NftName { get; set; } = "";
    public bool IsExternalImage { get; set; }
    public string SlotId { get; set; } = "";
    public long Extra { get; set; }

    public bool IsFloorItem => Type is ItemType.Floor;
    public bool IsWallItem => Type is ItemType.Wall;

    public InventoryItem() { }

    public static InventoryItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static InventoryItem ParseUnity(in PacketReader p)
    {
        Id item_id = p.ReadLong();
        ItemType type = p.ReadShort() switch
        {
            0 => ItemType.Wall,
            1 => ItemType.Floor,
            short value => throw new InvalidDataException(
                $"Unsupported Unity inventory item type {value}.")
        };
        var item = new InventoryItem
        {
            ItemId = item_id,
            Type = type,
            Id = p.ReadLong(),
            Kind = p.ReadInt(),
            Category = p.ReadInt(),
            Data = p.Parse<ItemData>(),
            IsRecyclable = p.ReadBool(),
            IsTradeable = p.ReadBool(),
            IsGroupable = p.ReadBool(),
            IsSellable = p.ReadBool(),
            SecondsToExpiration = p.ReadInt(),
            HasRentPeriodStarted = p.ReadBool(),
            RoomId = p.ReadLong(),
            IsUnseen = p.ReadBool(),
            Timestamp = p.ReadLong()
        };
        if (p.Context is null || p.Context.WireProfile.RequireUnityInventoryExtendedMetadata())
        {
            item.IsNft = p.ReadBool();
            if (item.IsNft)
            {
                item.NftName = p.ReadString();
                item.IsExternalImage = p.ReadBool();
            }
        }

        if (item.Type is ItemType.Floor)
        {
            item.SlotId = p.ReadString();
            item.Extra = p.ReadLong();
        }

        return item;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(InventoryItem value, in PacketWriter p)
    {
        value.ValidateUnity(in p);
        p.WriteLong(value.ItemId);
        p.WriteShort(value.Type switch
        {
            ItemType.Wall => 0,
            ItemType.Floor => 1,
            _ => throw new InvalidDataException(
                $"Unsupported inventory item type {value.Type}.")
        });
        p.WriteLong(value.Id);
        p.WriteInt(value.Kind);
        p.WriteInt(value.Category);
        p.Compose(value.Data);
        p.WriteBool(value.IsRecyclable);
        p.WriteBool(value.IsTradeable);
        p.WriteBool(value.IsGroupable);
        p.WriteBool(value.IsSellable);
        p.WriteInt(value.SecondsToExpiration);
        p.WriteBool(value.HasRentPeriodStarted);
        p.WriteLong(value.RoomId);
        p.WriteBool(value.IsUnseen);
        p.WriteLong(value.Timestamp);
        if (p.Context is null || p.Context.WireProfile.RequireUnityInventoryExtendedMetadata())
        {
            p.WriteBool(value.IsNft);
            if (value.IsNft)
            {
                p.WriteString(value.NftName);
                p.WriteBool(value.IsExternalImage);
            }
        }

        if (value.Type is ItemType.Floor)
        {
            p.WriteString(value.SlotId);
            p.WriteLong(value.Extra);
        }
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        InventoryWire.RequireItemType(Type);
        _ = InventoryWire.Int32Id(ItemId);
        _ = InventoryWire.Int32Id(Id);
        _ = InventoryWire.Int32Id(RoomId);
        InventoryWire.ValidateItemData(Data, false, in p);
        InventoryWire.RequireString(NftName, nameof(NftName), in p);
        if (IsUnseen || Timestamp != 0 || IsNft || NftName.Length != 0 || IsExternalImage)
        {
            throw new InvalidDataException(
                "Flash inventory items cannot carry Unity metadata.");
        }
        ValidatePlacement(in p, true);
    }

    internal void ValidateUnity(in PacketWriter p)
    {
        InventoryWire.RequireItemType(Type);
        InventoryWire.ValidateItemData(Data, true, in p);
        InventoryWire.RequireString(NftName, nameof(NftName), in p);
        bool extended_metadata =
            p.Context is null || p.Context.WireProfile.RequireUnityInventoryExtendedMetadata();
        if (!extended_metadata && (IsNft || NftName.Length != 0 || IsExternalImage))
        {
            throw new InvalidDataException(
                "The active Unity build cannot carry extended inventory metadata.");
        }
        if (!IsNft && (NftName.Length != 0 || IsExternalImage))
            throw new InvalidDataException("Extended inventory metadata requires the NFT flag.");
        ValidatePlacement(in p, false);
    }

    private void ValidatePlacement(in PacketWriter p, bool flash)
    {
        InventoryWire.RequireString(SlotId, nameof(SlotId), in p);
        if (Type is ItemType.Floor)
        {
            if (flash)
                _ = checked((int)Extra);
            return;
        }
        if (SlotId.Length != 0 || Extra != 0)
            throw new InvalidDataException("Wall inventory items cannot carry floor-item metadata.");
    }

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

    public static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
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

    public static void ValidateItemData(ItemData data, bool unity, in PacketWriter p)
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

        if (unity && data.IsLimitedRare)
            RequireString(data.UniqueLimitedData, nameof(data.UniqueLimitedData), in p);
    }

    private static void RequireNestedCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the wire limit.");
    }
}
