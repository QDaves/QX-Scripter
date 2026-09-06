using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CatalogProduct(
    string ProductType,
    int FurniClassId,
    string ExtraParam,
    int ProductCount,
    bool UniqueLimitedItem,
    int UniqueLimitedItemSeriesSize,
    int UniqueLimitedItemsLeft,
    short? UnityProductType = null) : IParserComposer<CatalogProduct>
{
    public const string TypeItem = "i";
    public const string TypeStuff = "s";
    public const string TypeEffect = "e";
    public const string TypeBadge = "b";

    public static CatalogProduct Parse(in PacketReader p)
    {
        short? unityProductType = p.Client is ClientType.Unity ? p.ReadShort() : null;
        string productType = unityProductType is short value ? FromUnityType(value) : p.ReadString();
        bool isBadge = unityProductType is 4 || unityProductType is null && productType == TypeBadge;
        if (!isBadge)
        {
            int furniClassId = p.ReadInt();
            string extraParam = p.ReadString();
            int productCount = p.ReadInt();
            bool uniqueLimited = p.ReadBool();
            int seriesSize = 0, itemsLeft = 0;
            if (uniqueLimited)
            {
                seriesSize = p.ReadInt();
                itemsLeft = p.ReadInt();
            }
            return new CatalogProduct(productType, furniClassId, extraParam, productCount, uniqueLimited, seriesSize, itemsLeft, unityProductType);
        }

        return new CatalogProduct(productType, 0, p.ReadString(), 1, false, 0, 0, unityProductType);
    }

    public void Compose(in PacketWriter p)
    {
        short? unityProductType = null;
        if (p.Client is ClientType.Unity)
        {
            unityProductType = UnityProductType ?? ToUnityType(ProductType);
            p.WriteShort(unityProductType.Value);
        }
        else
        {
            p.WriteString(ProductType);
        }

        bool isBadge = unityProductType is 4 || unityProductType is null && ProductType == TypeBadge;
        if (!isBadge)
        {
            p.WriteInt(FurniClassId);
            p.WriteString(ExtraParam);
            p.WriteInt(ProductCount);
            p.WriteBool(UniqueLimitedItem);
            if (UniqueLimitedItem)
            {
                p.WriteInt(UniqueLimitedItemSeriesSize);
                p.WriteInt(UniqueLimitedItemsLeft);
            }
        }
        else
        {
            p.WriteString(ExtraParam);
        }
    }

    private static string FromUnityType(short value) => value switch
    {
        0 => TypeItem,
        1 => TypeStuff,
        2 => TypeEffect,
        4 => TypeBadge,
        _ => $"unity:{value}"
    };

    private static short ToUnityType(string value)
    {
        if (value.StartsWith("unity:", StringComparison.Ordinal) &&
            short.TryParse(value.AsSpan(6), out short unityType))
            return unityType;

        return value.ToLowerInvariant() switch
        {
            TypeItem => 0,
            TypeStuff => 1,
            TypeEffect => 2,
            TypeBadge => 4,
            _ => throw new ArgumentException($"Unknown Unity catalog product type: {value}.", nameof(value))
        };
    }
}
