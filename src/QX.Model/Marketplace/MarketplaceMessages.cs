using Qx.Messages;
using Qx.Model.Marketplace;

namespace Qx.Model.Messages.Incoming;

public sealed record MarketplaceOffer
{
    public Id OfferId { get; init; }
    public int Status { get; init; }
    public int WireType { get; init; }
    public int Kind { get; init; }
    public ItemData? Data { get; init; }
    public string WallData { get; init; } = "";
    public int UniqueSerialNumber { get; init; }
    public int UniqueSeriesSize { get; init; }
    public bool? IsUsed { get; init; }

    [Obsolete("Use IsUsed.")]
    public bool SoldOut
    {
        get => IsUsed ?? false;
        init => IsUsed = value;
    }

    public int Price { get; init; }
    public int MinutesRemaining { get; init; }
    public int AveragePrice { get; init; }

    [Obsolete("Use AveragePrice.")]
    public int Average
    {
        get => AveragePrice;
        init => AveragePrice = value;
    }

    public int TradeVolume { get; init; }
    public int Offers { get; init; }
    public long? StatusTimeMilliseconds { get; init; }

    [Obsolete("Use StatusTimeMilliseconds.")]
    public long? ExtraLong
    {
        get => StatusTimeMilliseconds;
        init => StatusTimeMilliseconds = value;
    }

    public bool IsFloor => WireType is 1 or 3 or 4;
    public bool IsWall => WireType == 2;
    public bool IsLimitedEdition => WireType == 3;
    public bool IsUsable => WireType == 4;
    public MarketplaceOfferStatus OfferStatus => (MarketplaceOfferStatus)Status;
    public MarketplaceOfferType OfferType => (MarketplaceOfferType)WireType;
    public DateTimeOffset? StatusTime => StatusTimeMilliseconds is long value
        ? DateTimeOffset.FromUnixTimeMilliseconds(value)
        : null;
}

internal static class MarketplaceCodec
{
    public static MarketplaceOffer ReadFlashOffer(
        in PacketReader p,
        bool own_format,
        bool status_time = false)
    {
        Id offer_id = p.ReadInt();
        int status = p.ReadInt();
        int type = p.ReadInt();
        MarketplaceWire.RequireOfferType(type);

        int kind = 0;
        ItemData? data = null;
        string wall_data = "";
        int serial_number = 0;
        int series_size = 0;
        bool? is_used = null;

        switch (type)
        {
            case 1:
                kind = p.ReadInt();
                data = p.Parse<ItemData>();
                break;
            case 2:
                kind = p.ReadInt();
                wall_data = p.ReadString();
                break;
            case 3:
                kind = p.ReadInt();
                serial_number = p.ReadInt();
                series_size = p.ReadInt();
                break;
            case 4:
                kind = p.ReadInt();
                data = p.Parse<ItemData>();
                is_used = p.ReadBool();
                break;
        }

        int price = p.ReadInt();
        int minutes_remaining = p.ReadInt();
        int average_price = p.ReadInt();
        int offers = 0;
        long? status_time_milliseconds = null;
        if (own_format)
        {
            if (status_time && status is 2 or 3)
                status_time_milliseconds = p.ReadLong();
        }
        else
        {
            offers = p.ReadInt();
        }

        return new MarketplaceOffer
        {
            OfferId = offer_id,
            Status = status,
            WireType = type,
            Kind = kind,
            Data = data,
            WallData = wall_data,
            UniqueSerialNumber = serial_number,
            UniqueSeriesSize = series_size,
            IsUsed = is_used,
            Price = price,
            MinutesRemaining = minutes_remaining,
            AveragePrice = average_price,
            Offers = offers,
            StatusTimeMilliseconds = status_time_milliseconds
        };
    }

    public static MarketplaceOffer ReadUnityOffer(
        in PacketReader p,
        bool own_format)
    {
        Id offer_id = p.ReadLong();
        int status = p.ReadInt();
        int type = p.ReadInt();
        MarketplaceWire.RequireOfferType(type);

        int kind = p.ReadInt();
        ItemData? data = null;
        string wall_data = "";
        int serial_number = 0;
        int series_size = 0;

        switch (type)
        {
            case 1:
                data = p.Parse<ItemData>();
                break;
            case 2:
                wall_data = p.ReadString();
                break;
            case 3:
                serial_number = p.ReadInt();
                series_size = p.ReadInt();
                break;
        }

        int price = p.ReadInt();
        int minutes_remaining = p.ReadInt();
        int average_price = p.ReadInt();
        int trade_volume = p.ReadInt();
        int offers = own_format ? 0 : p.ReadInt();

        return new MarketplaceOffer
        {
            OfferId = offer_id,
            Status = status,
            WireType = type,
            Kind = kind,
            Data = data,
            WallData = wall_data,
            UniqueSerialNumber = serial_number,
            UniqueSeriesSize = series_size,
            Price = price,
            MinutesRemaining = minutes_remaining,
            AveragePrice = average_price,
            TradeVolume = trade_volume,
            Offers = offers
        };
    }

    public static void ValidateFlashOffer(
        MarketplaceOffer offer,
        bool own_format,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(offer);
        MarketplaceWire.FlashId(offer.OfferId);
        MarketplaceWire.RequireOfferType(offer.WireType);
        switch (offer.WireType)
        {
            case 1:
                MarketplaceWire.ValidateItemData(
                    offer.Data ?? throw new InvalidDataException(
                        "Floor marketplace offers require item data."),
                    false,
                    in p);
                break;
            case 2:
                MarketplaceWire.RequireString(offer.WallData, nameof(offer.WallData), in p);
                break;
            case 4:
                MarketplaceWire.ValidateItemData(
                    offer.Data ?? throw new InvalidDataException(
                        "Flash marketplace type 4 offers require item data."),
                    false,
                    in p);
                if (offer.IsUsed is null)
                {
                    throw new InvalidDataException(
                        "Flash marketplace type 4 offers require the used state.");
                }
                break;
        }

        if (!own_format || offer.StatusTimeMilliseconds is not long)
            return;
        if (offer.Status is not (2 or 3))
        {
            throw new InvalidDataException(
                $"A marketplace own offer in status {offer.Status} cannot carry a status time.");
        }
    }

    public static void ValidateUnityOffer(
        MarketplaceOffer offer,
        bool own_format,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(offer);
        MarketplaceWire.RequireOfferType(offer.WireType);
        switch (offer.WireType)
        {
            case 1:
                MarketplaceWire.ValidateItemData(
                    offer.Data ?? throw new InvalidDataException(
                        "Floor marketplace offers require item data."),
                    true,
                    in p);
                break;
            case 2:
                MarketplaceWire.RequireString(offer.WallData, nameof(offer.WallData), in p);
                break;
        }

        if (own_format && offer.StatusTimeMilliseconds is not null)
        {
            throw new InvalidDataException(
                "Unity marketplace own offers cannot carry a status time.");
        }
    }

    public static void WriteFlashOffer(
        in PacketWriter p,
        MarketplaceOffer offer,
        bool own_format)
    {
        p.WriteInt(MarketplaceWire.FlashId(offer.OfferId));
        p.WriteInt(offer.Status);
        p.WriteInt(offer.WireType);
        switch (offer.WireType)
        {
            case 1:
                p.WriteInt(offer.Kind);
                p.Compose(offer.Data!);
                break;
            case 2:
                p.WriteInt(offer.Kind);
                p.WriteString(offer.WallData);
                break;
            case 3:
                p.WriteInt(offer.Kind);
                p.WriteInt(offer.UniqueSerialNumber);
                p.WriteInt(offer.UniqueSeriesSize);
                break;
            case 4:
                p.WriteInt(offer.Kind);
                p.Compose(offer.Data!);
                p.WriteBool(offer.IsUsed!.Value);
                break;
        }

        p.WriteInt(offer.Price);
        p.WriteInt(offer.MinutesRemaining);
        p.WriteInt(offer.AveragePrice);
        if (own_format)
        {
            if (offer.StatusTimeMilliseconds is long status_time)
                p.WriteLong(status_time);
        }
        else
        {
            p.WriteInt(offer.Offers);
        }
    }

    public static void WriteUnityOffer(
        in PacketWriter p,
        MarketplaceOffer offer,
        bool own_format)
    {
        p.WriteLong(offer.OfferId);
        p.WriteInt(offer.Status);
        p.WriteInt(offer.WireType);
        p.WriteInt(offer.Kind);
        switch (offer.WireType)
        {
            case 1:
                p.Compose(offer.Data!);
                break;
            case 2:
                p.WriteString(offer.WallData);
                break;
            case 3:
                p.WriteInt(offer.UniqueSerialNumber);
                p.WriteInt(offer.UniqueSeriesSize);
                break;
        }

        p.WriteInt(offer.Price);
        p.WriteInt(offer.MinutesRemaining);
        p.WriteInt(offer.AveragePrice);
        p.WriteInt(offer.TradeVolume);
        if (!own_format)
            p.WriteInt(offer.Offers);
    }
}

public sealed record MarketplaceOffers : IParserComposer<MarketplaceOffers>
{
    private IReadOnlyList<MarketplaceOffer> _offers = Array.Empty<MarketplaceOffer>();

    public MarketplaceOffers(
        IReadOnlyList<MarketplaceOffer> offers,
        int total_items_found)
    {
        Offers = offers;
        TotalItemsFound = total_items_found;
    }

    public IReadOnlyList<MarketplaceOffer> Offers
    {
        get => _offers;
        init => _offers = MarketplaceWire.FreezeReferences(value, nameof(Offers));
    }

    public int TotalItemsFound { get; init; }

    public static MarketplaceOffers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceOffers ParseFlash(in PacketReader p)
    {
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(Offers));
        var offers = new MarketplaceOffer[count];
        for (int i = 0; i < count; i++)
            offers[i] = MarketplaceCodec.ReadFlashOffer(in p, false);
        return new MarketplaceOffers(offers, p.ReadInt());
    }

    private static MarketplaceOffers ParseUnity(in PacketReader p)
    {
        int count = p.ReadLength();
        var offers = new MarketplaceOffer[count];
        for (int i = 0; i < count; i++)
            offers[i] = MarketplaceCodec.ReadUnityOffer(in p, false);
        return new MarketplaceOffers(offers, p.ReadInt());
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MarketplaceOffers value, in PacketWriter p)
    {
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.ValidateFlashOffer(offer, false, in p);

        p.WriteInt(value.Offers.Count);
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.WriteFlashOffer(in p, offer, false);
        p.WriteInt(value.TotalItemsFound);
    }

    private static void ComposeUnity(MarketplaceOffers value, in PacketWriter p)
    {
        MarketplaceWire.RequireUnityCount(value.Offers.Count, nameof(Offers));
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.ValidateUnityOffer(offer, false, in p);

        p.WriteLength((Length)value.Offers.Count);
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.WriteUnityOffer(in p, offer, false);
        p.WriteInt(value.TotalItemsFound);
    }
}

public sealed record MarketplaceOwnOffers : IParserComposer<MarketplaceOwnOffers>
{
    private IReadOnlyList<MarketplaceOffer> _offers = Array.Empty<MarketplaceOffer>();

    public MarketplaceOwnOffers(
        int credits_waiting,
        IReadOnlyList<MarketplaceOffer> offers)
    {
        CreditsWaiting = credits_waiting;
        Offers = offers;
    }

    public int CreditsWaiting { get; init; }

    public IReadOnlyList<MarketplaceOffer> Offers
    {
        get => _offers;
        init => _offers = MarketplaceWire.FreezeReferences(value, nameof(Offers));
    }

    public static MarketplaceOwnOffers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceOwnOffers ParseFlash(in PacketReader p)
    {
        int credits_waiting = p.ReadInt();
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(Offers));
        int body_position = p.Pos;
        foreach (bool status_time in StatusTimeAttempts)
        {
            p.Pos = body_position;
            if (TryReadFlashOffers(in p, count, status_time, out MarketplaceOffer[] offers) &&
                p.Available == 0)
            {
                return new MarketplaceOwnOffers(credits_waiting, offers);
            }
        }

        throw new InvalidDataException(
            "The marketplace own-offer list matches neither Flash wire layout.");
    }

    private static MarketplaceOwnOffers ParseUnity(in PacketReader p)
    {
        int count = p.ReadLength();
        var offers = new MarketplaceOffer[count];
        for (int i = 0; i < count; i++)
            offers[i] = MarketplaceCodec.ReadUnityOffer(in p, true);
        return new MarketplaceOwnOffers(0, offers);
    }

    private static ReadOnlySpan<bool> StatusTimeAttempts => [true, false];

    private static bool TryReadFlashOffers(
        in PacketReader p,
        int count,
        bool status_time,
        out MarketplaceOffer[] offers)
    {
        offers = new MarketplaceOffer[count];
        try
        {
            for (int i = 0; i < count; i++)
            {
                offers[i] = MarketplaceCodec.ReadFlashOffer(
                    in p,
                    true,
                    status_time);
            }
            return true;
        }
        catch (Exception error) when (
            error is InvalidDataException or
                ArgumentOutOfRangeException or
                IndexOutOfRangeException or
                ArgumentException)
        {
            return false;
        }
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MarketplaceOwnOffers value, in PacketWriter p)
    {
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.ValidateFlashOffer(offer, true, in p);

        p.WriteInt(value.CreditsWaiting);
        p.WriteInt(value.Offers.Count);
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.WriteFlashOffer(in p, offer, true);
    }

    private static void ComposeUnity(MarketplaceOwnOffers value, in PacketWriter p)
    {
        MarketplaceWire.RequireUnityCount(value.Offers.Count, nameof(Offers));
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.ValidateUnityOffer(offer, true, in p);

        p.WriteLength((Length)value.Offers.Count);
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.WriteUnityOffer(in p, offer, true);
    }
}

public readonly record struct MarketplaceTradeInfo(
    int DayOffset,
    int AverageSalePrice,
    int SoldAmount)
{
    [Obsolete("Use SoldAmount.")]
    public int TradeVolume => SoldAmount;
}

public sealed record MarketplaceItemStats : IParserComposer<MarketplaceItemStats>
{
    private IReadOnlyList<MarketplaceTradeInfo> _history =
        Array.Empty<MarketplaceTradeInfo>();

    public MarketplaceItemStats(
        int average_sale_price,
        int offer_count,
        int history_length_days,
        IReadOnlyList<MarketplaceTradeInfo> history,
        int furni_type_id,
        int furni_category_id,
        int? lowest_price,
        int? suggested_price)
    {
        AverageSalePrice = average_sale_price;
        OfferCount = offer_count;
        HistoryLengthDays = history_length_days;
        History = history;
        FurniTypeId = furni_type_id;
        FurniCategoryId = furni_category_id;
        LowestPrice = lowest_price;
        SuggestedPrice = suggested_price;
    }

    public int AverageSalePrice { get; init; }
    public int OfferCount { get; init; }
    public int HistoryLengthDays { get; init; }

    public IReadOnlyList<MarketplaceTradeInfo> History
    {
        get => _history;
        init => _history = MarketplaceWire.FreezeValues(value, nameof(History));
    }

    public int FurniTypeId { get; init; }
    public int FurniCategoryId { get; init; }
    public int? LowestPrice { get; init; }
    public int? SuggestedPrice { get; init; }
    public int ItemType => FurniCategoryId;
    public int Kind => FurniTypeId;
    public MarketplaceFurniCategory FurniCategory =>
        (MarketplaceFurniCategory)FurniCategoryId;

    public static MarketplaceItemStats Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceItemStats ParseFlash(in PacketReader p)
    {
        int average_sale_price = p.ReadInt();
        int offer_count = p.ReadInt();
        int history_length_days = p.ReadInt();
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(History));
        MarketplaceTradeInfo[] history = ReadHistory(in p, count);
        int furni_category_id = p.ReadInt();
        int furni_type_id = p.ReadInt();
        (int? lowest_price, int? suggested_price) =
            ReadPriceTail(in p, "Flash");
        return new MarketplaceItemStats(
            average_sale_price,
            offer_count,
            history_length_days,
            history,
            furni_type_id,
            furni_category_id,
            lowest_price,
            suggested_price);
    }

    private static MarketplaceItemStats ParseUnity(in PacketReader p)
    {
        int average_sale_price = p.ReadInt();
        int offer_count = p.ReadInt();
        int history_length_days = p.ReadInt();
        int count = p.ReadLength();
        MarketplaceTradeInfo[] history = ReadHistory(in p, count);
        int furni_category_id = p.ReadInt();
        int furni_type_id = p.ReadInt();
        (int? lowest_price, int? suggested_price) =
            ReadPriceTail(in p, "Unity");
        return new MarketplaceItemStats(
            average_sale_price,
            offer_count,
            history_length_days,
            history,
            furni_type_id,
            furni_category_id,
            lowest_price,
            suggested_price);
    }

    private static MarketplaceTradeInfo[] ReadHistory(
        in PacketReader p,
        int count)
    {
        var history = new MarketplaceTradeInfo[count];
        for (int i = 0; i < count; i++)
        {
            history[i] = new MarketplaceTradeInfo(
                p.ReadInt(),
                p.ReadInt(),
                p.ReadInt());
        }
        return history;
    }

    private static (int? LowestPrice, int? SuggestedPrice) ReadPriceTail(
        in PacketReader p,
        string client) => p.Available switch
    {
        0 => (null, null),
        8 => (p.ReadInt(), p.ReadInt()),
        _ => throw new InvalidDataException(
            $"Unsupported {client} marketplace item-stats tail length {p.Available}.")
    };

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MarketplaceItemStats value, in PacketWriter p)
    {
        MarketplaceWire.RequirePriceTail(
            value.LowestPrice,
            value.SuggestedPrice,
            "Flash");
        WriteFlash(value, in p);
    }

    private static void ComposeUnity(MarketplaceItemStats value, in PacketWriter p)
    {
        MarketplaceWire.RequireUnityCount(value.History.Count, nameof(History));
        MarketplaceWire.RequirePriceTail(
            value.LowestPrice,
            value.SuggestedPrice,
            "Unity");
        WriteUnity(value, in p);
    }

    private static void WriteFlash(MarketplaceItemStats value, in PacketWriter p)
    {
        p.WriteInt(value.AverageSalePrice);
        p.WriteInt(value.OfferCount);
        p.WriteInt(value.HistoryLengthDays);
        p.WriteInt(value.History.Count);
        WriteHistory(value.History, in p);
        WriteIdentityAndPriceTail(value, in p);
    }

    private static void WriteUnity(MarketplaceItemStats value, in PacketWriter p)
    {
        p.WriteInt(value.AverageSalePrice);
        p.WriteInt(value.OfferCount);
        p.WriteInt(value.HistoryLengthDays);
        p.WriteLength((Length)value.History.Count);
        WriteHistory(value.History, in p);
        WriteIdentityAndPriceTail(value, in p);
    }

    private static void WriteHistory(
        IReadOnlyList<MarketplaceTradeInfo> history,
        in PacketWriter p)
    {
        foreach (MarketplaceTradeInfo info in history)
        {
            p.WriteInt(info.DayOffset);
            p.WriteInt(info.AverageSalePrice);
            p.WriteInt(info.SoldAmount);
        }
    }

    private static void WriteIdentityAndPriceTail(
        MarketplaceItemStats value,
        in PacketWriter p)
    {
        p.WriteInt(value.FurniCategoryId);
        p.WriteInt(value.FurniTypeId);
        if (value.LowestPrice is int lowest_price &&
            value.SuggestedPrice is int suggested_price)
        {
            p.WriteInt(lowest_price);
            p.WriteInt(suggested_price);
        }
    }
}

public sealed record MarketplaceBuyResult(
    int Result,
    Id RequestedOfferId,
    Id NewOfferId,
    int NewPrice) : IParserComposer<MarketplaceBuyResult>
{
    public MarketplaceBuyResultCode ResultCode =>
        (MarketplaceBuyResultCode)Result;

    public static MarketplaceBuyResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceBuyResult ParseFlash(in PacketReader p)
    {
        int result = p.ReadInt();
        Id new_offer_id = p.ReadInt();
        int new_price = p.ReadInt();
        Id requested_offer_id = p.ReadInt();
        return new MarketplaceBuyResult(
            result,
            requested_offer_id,
            new_offer_id,
            new_price);
    }

    private static MarketplaceBuyResult ParseUnity(in PacketReader p)
    {
        int result = p.ReadInt();
        Id new_offer_id = p.ReadLong();
        int new_price = p.ReadInt();
        Id requested_offer_id = p.ReadLong();
        return new MarketplaceBuyResult(
            result,
            requested_offer_id,
            new_offer_id,
            new_price);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MarketplaceBuyResult value, in PacketWriter p)
    {
        int new_offer_id = MarketplaceWire.FlashId(value.NewOfferId);
        int requested_offer_id = MarketplaceWire.FlashId(value.RequestedOfferId);
        p.WriteInt(value.Result);
        p.WriteInt(new_offer_id);
        p.WriteInt(value.NewPrice);
        p.WriteInt(requested_offer_id);
    }

    private static void ComposeUnity(MarketplaceBuyResult value, in PacketWriter p)
    {
        p.WriteInt(value.Result);
        p.WriteLong(value.NewOfferId);
        p.WriteInt(value.NewPrice);
        p.WriteLong(value.RequestedOfferId);
    }
}

public sealed record MarketplaceCanMakeOfferResult(
    int ResultCode,
    int? TokenCount) : IParserComposer<MarketplaceCanMakeOfferResult>
{
    public MarketplaceEligibilityResult Result =>
        (MarketplaceEligibilityResult)ResultCode;

    public static MarketplaceCanMakeOfferResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceCanMakeOfferResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static MarketplaceCanMakeOfferResult ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), null);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        MarketplaceCanMakeOfferResult value,
        in PacketWriter p)
    {
        int token_count = value.TokenCount ??
            throw new InvalidDataException(
                "Flash marketplace eligibility results require the token count.");
        p.WriteInt(value.ResultCode);
        p.WriteInt(token_count);
    }

    private static void ComposeUnity(
        MarketplaceCanMakeOfferResult value,
        in PacketWriter p) => p.WriteInt(value.ResultCode);
}

public sealed record MarketplaceMakeOfferResult(int Result)
    : IParserComposer<MarketplaceMakeOfferResult>
{
    public static MarketplaceMakeOfferResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceMakeOfferResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static MarketplaceMakeOfferResult ParseUnity(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        MarketplaceMakeOfferResult value,
        in PacketWriter p) => p.WriteInt(value.Result);

    private static void ComposeUnity(
        MarketplaceMakeOfferResult value,
        in PacketWriter p) => p.WriteInt(value.Result);
}

public sealed record MarketplaceCancelOfferResult(Id OfferId, bool Success)
    : IParserComposer<MarketplaceCancelOfferResult>
{
    public static MarketplaceCancelOfferResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceCancelOfferResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    private static MarketplaceCancelOfferResult ParseUnity(in PacketReader p) =>
        throw new NotSupportedException(
            "The Unity marketplace cancel-offer result has no verified payload layout.");

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        MarketplaceCancelOfferResult value,
        in PacketWriter p)
    {
        int offer_id = MarketplaceWire.FlashId(value.OfferId);
        p.WriteInt(offer_id);
        p.WriteBool(value.Success);
    }

    private static void ComposeUnity(
        MarketplaceCancelOfferResult value,
        in PacketWriter p) => throw new NotSupportedException(
            "The Unity marketplace cancel-offer result has no verified payload layout.");
}

public sealed record MarketplaceConfiguration(
    bool IsEnabled,
    int Commission,
    int TokenBatchPrice,
    int TokenBatchSize,
    int OfferMinimumPrice,
    int OfferMaximumPrice,
    int ExpirationHours,
    int AveragePricePeriod,
    int SellingFeePercentage,
    int RevenueLimit,
    int HalfTaxLimit) : IParserComposer<MarketplaceConfiguration>
{
    public static MarketplaceConfiguration Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceConfiguration ParseFlash(in PacketReader p) =>
        Read(in p);

    private static MarketplaceConfiguration ParseUnity(in PacketReader p) =>
        Read(in p);

    private static MarketplaceConfiguration Read(in PacketReader p) => new(
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        MarketplaceConfiguration value,
        in PacketWriter p) => Write(value, in p);

    private static void ComposeUnity(
        MarketplaceConfiguration value,
        in PacketWriter p) => Write(value, in p);

    private static void Write(MarketplaceConfiguration value, in PacketWriter p)
    {
        p.WriteBool(value.IsEnabled);
        p.WriteInt(value.Commission);
        p.WriteInt(value.TokenBatchPrice);
        p.WriteInt(value.TokenBatchSize);
        p.WriteInt(value.OfferMinimumPrice);
        p.WriteInt(value.OfferMaximumPrice);
        p.WriteInt(value.ExpirationHours);
        p.WriteInt(value.AveragePricePeriod);
        p.WriteInt(value.SellingFeePercentage);
        p.WriteInt(value.RevenueLimit);
        p.WriteInt(value.HalfTaxLimit);
    }
}

public sealed record MarketplaceCancelAllOffersResult
    : IParserComposer<MarketplaceCancelAllOffersResult>
{
    private IReadOnlyList<Id> _offer_ids = Array.Empty<Id>();

    public MarketplaceCancelAllOffersResult(
        IReadOnlyList<Id> offer_ids,
        bool success)
    {
        OfferIds = offer_ids;
        Success = success;
    }

    public IReadOnlyList<Id> OfferIds
    {
        get => _offer_ids;
        init => _offer_ids = MarketplaceWire.FreezeValues(value, nameof(OfferIds));
    }

    public bool Success { get; init; }

    public static MarketplaceCancelAllOffersResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceCancelAllOffersResult ParseFlash(in PacketReader p)
    {
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(OfferIds));
        var offer_ids = new Id[count];
        for (int i = 0; i < count; i++)
            offer_ids[i] = p.ReadInt();
        return new MarketplaceCancelAllOffersResult(offer_ids, p.ReadBool());
    }

    private static MarketplaceCancelAllOffersResult ParseUnity(in PacketReader p)
    {
        int count = p.ReadLength();
        var offer_ids = new Id[count];
        for (int i = 0; i < count; i++)
            offer_ids[i] = p.ReadLong();
        return new MarketplaceCancelAllOffersResult(offer_ids, p.ReadBool());
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        MarketplaceCancelAllOffersResult value,
        in PacketWriter p)
    {
        var offer_ids = new int[value.OfferIds.Count];
        for (int i = 0; i < offer_ids.Length; i++)
            offer_ids[i] = MarketplaceWire.FlashId(value.OfferIds[i]);

        p.WriteInt(offer_ids.Length);
        foreach (int offer_id in offer_ids)
            p.WriteInt(offer_id);
        p.WriteBool(value.Success);
    }

    private static void ComposeUnity(
        MarketplaceCancelAllOffersResult value,
        in PacketWriter p)
    {
        MarketplaceWire.RequireUnityCount(value.OfferIds.Count, nameof(OfferIds));
        p.WriteLength((Length)value.OfferIds.Count);
        foreach (Id offer_id in value.OfferIds)
            p.WriteLong(offer_id);
        p.WriteBool(value.Success);
    }
}

public sealed record MarketplaceClearOwnHistoryResult(bool Success)
    : IParserComposer<MarketplaceClearOwnHistoryResult>
{
    public static MarketplaceClearOwnHistoryResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceClearOwnHistoryResult ParseFlash(in PacketReader p) =>
        new(p.ReadBool());

    private static MarketplaceClearOwnHistoryResult ParseUnity(in PacketReader p) =>
        throw new NotSupportedException(
            "Marketplace history clearing is only verified for Flash.");

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        MarketplaceClearOwnHistoryResult value,
        in PacketWriter p) => p.WriteBool(value.Success);

    private static void ComposeUnity(
        MarketplaceClearOwnHistoryResult value,
        in PacketWriter p) => throw new NotSupportedException(
            "Marketplace history clearing is only verified for Flash.");
}

internal static class MarketplaceWire
{
    public static int ReadFlashCount(in PacketReader p, string name)
    {
        int count = p.ReadInt();
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        return count;
    }

    public static int FlashId(Id id) => checked((int)(long)id);

    public static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the Unity wire limit {ushort.MaxValue}.");
        }
    }

    public static void RequireEmpty(in PacketReader p, string message_name)
    {
        if (p.Available != 0)
        {
            throw new InvalidDataException(
                $"{message_name} contains {p.Available} unexpected bytes.");
        }
    }

    public static FlashMarketplaceWireLayout FlashLayout(in PacketReader p) =>
        p.Context?.WireProfile.RequireFlashMarketplaceLayout() ??
        throw new NotSupportedException(
            "The Flash marketplace message has no wire-profile context.");

    public static FlashMarketplaceWireLayout FlashLayout(in PacketWriter p) =>
        p.Context?.WireProfile.RequireFlashMarketplaceLayout() ??
        throw new NotSupportedException(
            "The Flash marketplace message has no wire-profile context.");

    public static void RequireModernFlash(in PacketReader p)
    {
        if (FlashLayout(in p) is not FlashMarketplaceWireLayout.Modern)
        {
            throw new NotSupportedException(
                "The active Flash marketplace layout does not support this message.");
        }
    }

    public static void RequireModernFlash(in PacketWriter p)
    {
        if (FlashLayout(in p) is not FlashMarketplaceWireLayout.Modern)
        {
            throw new NotSupportedException(
                "The active Flash marketplace layout does not support this message.");
        }
    }

    public static MarketplaceBuyWireLayout UnityBuyLayout(in PacketReader p) =>
        p.Context?.WireProfile.RequireUnityMarketplaceBuyLayout() ??
        throw new NotSupportedException(
            "The Unity marketplace message has no wire-profile context.");

    public static MarketplaceBuyWireLayout UnityBuyLayout(in PacketWriter p) =>
        p.Context?.WireProfile.RequireUnityMarketplaceBuyLayout() ??
        throw new NotSupportedException(
            "The Unity marketplace message has no wire-profile context.");

    public static MarketplaceFurniCategory ReadCategory(in PacketReader p)
    {
        var category = (MarketplaceFurniCategory)p.ReadInt();
        RequireCategory(category);
        return category;
    }

    public static MarketplaceFurniCategory ReadSellableCategory(
        in PacketReader p)
    {
        MarketplaceFurniCategory category = ReadCategory(in p);
        RequireSellableCategory(category);
        return category;
    }

    public static void RequireCategory(MarketplaceFurniCategory category)
    {
        if (category is not (
            MarketplaceFurniCategory.Floor or
            MarketplaceFurniCategory.Wall or
            MarketplaceFurniCategory.Limited))
        {
            throw new InvalidDataException(
                $"Unsupported marketplace furni category {(int)category}.");
        }
    }

    public static void RequireSellableCategory(
        MarketplaceFurniCategory category)
    {
        if (category is not (
            MarketplaceFurniCategory.Floor or
            MarketplaceFurniCategory.Wall))
        {
            throw new InvalidDataException(
                "Marketplace offers can only contain floor or wall inventory items.");
        }
    }

    public static void WriteCategory(
        in PacketWriter p,
        MarketplaceFurniCategory category) => p.WriteInt((int)category);

    public static MarketplaceSortOrder ReadSortOrder(in PacketReader p)
    {
        var sort_order = (MarketplaceSortOrder)p.ReadInt();
        RequireSortOrder(sort_order);
        return sort_order;
    }

    public static void RequireSortOrder(MarketplaceSortOrder sort_order)
    {
        if (sort_order is < MarketplaceSortOrder.HighestPrice or
            > MarketplaceSortOrder.LeastOffers)
        {
            throw new InvalidDataException(
                $"Unsupported marketplace sort order {(int)sort_order}.");
        }
    }

    public static void WriteSortOrder(
        in PacketWriter p,
        MarketplaceSortOrder sort_order) => p.WriteInt((int)sort_order);

    public static MarketplaceOwnOffersCategory ReadOwnOffersCategory(
        in PacketReader p)
    {
        var category = (MarketplaceOwnOffersCategory)p.ReadInt();
        RequireOwnOffersCategory(category);
        return category;
    }

    public static void RequireOwnOffersCategory(
        MarketplaceOwnOffersCategory category)
    {
        if (category is < MarketplaceOwnOffersCategory.Open or
            > MarketplaceOwnOffersCategory.Expired)
        {
            throw new InvalidDataException(
                $"Unsupported marketplace own-offers category {(int)category}.");
        }
    }

    public static void WriteOwnOffersCategory(
        in PacketWriter p,
        MarketplaceOwnOffersCategory category) => p.WriteInt((int)category);

    public static void RequireHistoryCategory(
        MarketplaceOwnOffersCategory category)
    {
        if (category is not (
            MarketplaceOwnOffersCategory.Sold or
            MarketplaceOwnOffersCategory.Expired))
        {
            throw new InvalidDataException(
                "Marketplace history can only target sold or expired offers.");
        }
    }

    public static void RequireOfferType(int type)
    {
        if (type is < 1 or > 4)
            throw new InvalidDataException($"Unsupported marketplace offer type {type}.");
    }

    public static void RequirePriceTail(
        int? lowest_price,
        int? suggested_price,
        string client)
    {
        if (lowest_price.HasValue != suggested_price.HasValue)
        {
            throw new InvalidDataException(
                $"{client} marketplace item stats require both price-tail fields or neither.");
        }
    }

    public static void RequireString(
        string value,
        string name,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                name);
        }
    }

    public static void ValidateItemData(
        ItemData data,
        bool unity,
        in PacketWriter p)
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
                    $"Unsupported marketplace item-data type {data.GetType().FullName}.");
        }

        if (unity && data.IsLimitedRare)
        {
            RequireString(
                data.UniqueLimitedData,
                nameof(data.UniqueLimitedData),
                in p);
        }
    }

    public static IReadOnlyList<T> FreezeValues<T>(
        IReadOnlyList<T> values,
        string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    public static IReadOnlyList<T> FreezeReferences<T>(
        IReadOnlyList<T> values,
        string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        T[] copy = values.ToArray();
        foreach (T value in copy)
            ArgumentNullException.ThrowIfNull(value, name);
        return Array.AsReadOnly(copy);
    }

    private static void RequireNestedCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the wire limit {ushort.MaxValue}.");
        }
    }
}
