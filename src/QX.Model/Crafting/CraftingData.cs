using Qx.Messages;

namespace Qx.Model.Crafting;

public sealed record CraftingProduct(
    string RecipeCode,
    string? ProductCode,
    string FurnitureClassName) : IComposer
{
    public bool HasProductCode => ProductCode is not null;

    public static CraftingProduct Parse(
        in PacketReader p,
        bool has_product_code)
    {
        CraftingWire.RequireSupportedClient(p.Client);
        var strings = CraftingWire.NewStringBudget();
        return CraftingWire.ParseProduct(
            in p,
            has_product_code,
            0,
            ref strings);
    }

    public void Compose(in PacketWriter p)
    {
        CraftingWire.RequireSupportedClient(p.Client);
        var strings = CraftingWire.NewStringBudget();
        CraftingWire.PrepareProduct(this, ref strings, in p);
        CraftingWire.WriteProduct(this, in p);
    }
}

public sealed record CraftingIngredient(
    int Count,
    string FurnitureClassName) : IParserComposer<CraftingIngredient>
{
    public static CraftingIngredient Parse(in PacketReader p)
    {
        CraftingWire.RequireSupportedClient(p.Client);
        var strings = CraftingWire.NewStringBudget();
        return CraftingWire.ParseIngredient(in p, 0, ref strings);
    }

    public void Compose(in PacketWriter p)
    {
        CraftingWire.RequireSupportedClient(p.Client);
        var strings = CraftingWire.NewStringBudget();
        CraftingWire.PrepareIngredient(this, ref strings, in p);
        CraftingWire.WriteIngredient(this, in p);
    }
}

internal static class CraftingWire
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int MaximumStrings = 196_608;
    public const int MaximumStringBytes = 16 * 1024 * 1024;
    public const int StringPrefixBytes = sizeof(short);

    public static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }

    public static int CountWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(short),
        _ => throw new UnsupportedClientException(client)
    };

    public static int IdWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(long),
        _ => throw new UnsupportedClientException(client)
    };

    public static int ReadCount(
        in PacketReader p,
        int minimum_element_bytes,
        int trailing_bytes,
        string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_element_bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        RequireRemaining(in p, CountWidth(p.Client), trailing_bytes, name);
        int count = p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => unchecked((ushort)p.ReadShort()),
            _ => throw new UnsupportedClientException(p.Client)
        };
        RequireCount(count, name);
        int available = p.Available - trailing_bytes;
        if (available < 0 || count > available / minimum_element_bytes)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
        }
        return count;
    }

    public static int RequireCount(int count, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (count > MaximumCollectionCount)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the limit {MaximumCollectionCount}.");
        }
        return count;
    }

    public static int RequireListCount<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return RequireCount(values.Count, name);
    }

    public static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        int count = RequireListCount(values, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
            copy[index] = values[index];
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<T> FreezeReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        int count = RequireListCount(values, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
        {
            T value = values[index];
            ArgumentNullException.ThrowIfNull(value, name);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<string> FreezeStrings(
        IReadOnlyList<string> values,
        string name) =>
        FreezeReferences(values, name);

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireRemaining(
        in PacketReader p,
        int required_bytes,
        int trailing_bytes,
        string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(required_bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        int total = checked(required_bytes + trailing_bytes);
        if (p.Available < total)
        {
            throw new InvalidDataException(
                $"{name} requires {total} bytes but only {p.Available} remain.");
        }
    }

    public static Id ReadId(
        in PacketReader p,
        int trailing_bytes,
        string name)
    {
        RequireRemaining(in p, IdWidth(p.Client), trailing_bytes, name);
        return p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => p.ReadLong(),
            _ => throw new UnsupportedClientException(p.Client)
        };
    }

    public static void RequireId(Id value, ClientType client)
    {
        RequireSupportedClient(client);
        if (client is ClientType.Flash)
            _ = checked((int)(long)value);
    }

    public static void WriteId(Id value, in PacketWriter p)
    {
        if (p.Client is ClientType.Flash)
            p.WriteInt(checked((int)(long)value));
        else if (p.Client is ClientType.Unity)
            p.WriteLong(value);
        else
            throw new UnsupportedClientException(p.Client);
    }

    public static void WriteCount(int count, in PacketWriter p) =>
        p.WriteLength((Length)count);

    public static CraftingProduct ParseProduct(
        in PacketReader p,
        bool has_product_code,
        int trailing_bytes,
        ref CraftingStringBudget strings)
    {
        RequireSupportedClient(p.Client);
        if (p.Client is ClientType.Flash && !has_product_code)
        {
            throw new InvalidDataException(
                "Flash crafting products require a product code.");
        }

        int remaining_prefixes = has_product_code ? 2 : 1;
        string recipe_code = strings.Read(
            in p,
            nameof(CraftingProduct.RecipeCode),
            checked(trailing_bytes + remaining_prefixes * StringPrefixBytes));
        string? product_code = null;
        if (has_product_code)
        {
            product_code = strings.Read(
                in p,
                nameof(CraftingProduct.ProductCode),
                checked(trailing_bytes + StringPrefixBytes));
        }
        string furniture_class_name = strings.Read(
            in p,
            nameof(CraftingProduct.FurnitureClassName),
            trailing_bytes);
        return new CraftingProduct(
            recipe_code,
            product_code,
            furniture_class_name);
    }

    public static void PrepareProduct(
        CraftingProduct value,
        ref CraftingStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSupportedClient(p.Client);
        if (p.Client is ClientType.Flash && value.ProductCode is null)
        {
            throw new InvalidDataException(
                "Flash crafting products require a product code.");
        }
        strings.Require(value.RecipeCode, nameof(value.RecipeCode), in p);
        if (value.ProductCode is string product_code)
            strings.Require(product_code, nameof(value.ProductCode), in p);
        strings.Require(
            value.FurnitureClassName,
            nameof(value.FurnitureClassName),
            in p);
    }

    public static void WriteProduct(CraftingProduct value, in PacketWriter p)
    {
        p.WriteString(value.RecipeCode);
        if (value.ProductCode is string product_code)
            p.WriteString(product_code);
        p.WriteString(value.FurnitureClassName);
    }

    public static CraftingIngredient ParseIngredient(
        in PacketReader p,
        int trailing_bytes,
        ref CraftingStringBudget strings)
    {
        RequireSupportedClient(p.Client);
        RequireRemaining(
            in p,
            checked(sizeof(int) + StringPrefixBytes),
            trailing_bytes,
            nameof(CraftingIngredient));
        int count = p.ReadInt();
        string furniture_class_name = strings.Read(
            in p,
            nameof(CraftingIngredient.FurnitureClassName),
            trailing_bytes);
        return new CraftingIngredient(count, furniture_class_name);
    }

    public static void PrepareIngredient(
        CraftingIngredient value,
        ref CraftingStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSupportedClient(p.Client);
        strings.Require(
            value.FurnitureClassName,
            nameof(value.FurnitureClassName),
            in p);
    }

    public static void WriteIngredient(CraftingIngredient value, in PacketWriter p)
    {
        p.WriteInt(value.Count);
        p.WriteString(value.FurnitureClassName);
    }

    public static CraftingStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);
}

internal struct CraftingStringBudget
{
    private readonly int _maximum_count;
    private readonly int _maximum_bytes;
    private int _count;
    private int _bytes;

    public CraftingStringBudget(int maximum_count, int maximum_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        _maximum_count = maximum_count;
        _maximum_bytes = maximum_bytes;
    }

    public string Read(
        in PacketReader p,
        string name,
        int trailing_bytes)
    {
        CraftingWire.RequireRemaining(
            in p,
            CraftingWire.StringPrefixBytes,
            trailing_bytes,
            name);
        int byte_count = unchecked((ushort)p.ReadShort());
        CraftingWire.RequireRemaining(in p, byte_count, trailing_bytes, name);
        Take(byte_count, name);
        return p.Encoding.GetString(p.ReadSpan(byte_count));
    }

    public void Require(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int byte_count = p.Encoding.GetByteCount(value);
        if (byte_count > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
        Take(byte_count, name);
    }

    private void Take(int byte_count, string name)
    {
        if (_count >= _maximum_count)
        {
            throw new InvalidDataException(
                $"{name} exceeds the string-count limit {_maximum_count}.");
        }
        if (byte_count > _maximum_bytes - _bytes)
        {
            throw new InvalidDataException(
                $"{name} exceeds the string-byte budget {_maximum_bytes}.");
        }
        _count++;
        _bytes += byte_count;
    }
}
