using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CatalogProduct(
    string ProductType,
    int FurniClassId,
    string ExtraParam,
    int ProductCount,
    bool UniqueLimitedItem,
    int UniqueLimitedItemSeriesSize,
    int UniqueLimitedItemsLeft) : IParserComposer<CatalogProduct>
{
    public const string TypeItem = "i";
    public const string TypeStuff = "s";
    public const string TypeEffect = "e";
    public const string TypeBadge = "b";

    public static CatalogProduct Parse(in PacketReader p)
    {
        string productType = p.ReadString();
        bool isBadge = productType == TypeBadge;
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
            return new CatalogProduct(productType, furniClassId, extraParam, productCount, uniqueLimited, seriesSize, itemsLeft);
        }

        return new CatalogProduct(productType, 0, p.ReadString(), 1, false, 0, 0);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteString(ProductType);
        bool isBadge = ProductType == TypeBadge;
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
}
