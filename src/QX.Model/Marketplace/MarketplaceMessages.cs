using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents a marketplace offer, either from a search result or from the local user's own offers.</summary>
/// <remarks>
/// The fields that are filled depend on <see cref="WireType"/>. Search results carry
/// <see cref="Offers"/>; the local user's own offers may carry <see cref="StatusTimeMilliseconds"/> instead.
/// </remarks>
public sealed record MarketplaceOffer
{
    /// <summary>Gets the id of the offer.</summary>
    public Id OfferId { get; init; }
    /// <summary>Gets the raw status code of the offer, see <see cref="OfferStatus"/>.</summary>
    public int Status { get; init; }
    /// <summary>Gets the raw offer type code, see <see cref="OfferType"/>.</summary>
    public int WireType { get; init; }
    /// <summary>Gets the furni type id of the offered item.</summary>
    public int Kind { get; init; }
    /// <summary>Gets the item data of a floor or used floor offer, or <see langword="null"/> for other offer types.</summary>
    public ItemData? Data { get; init; }
    /// <summary>Gets the data string of a wall offer, or an empty string for other offer types.</summary>
    public string WallData { get; init; } = "";
    /// <summary>Gets the serial number of a limited edition offer, or 0 for other offer types.</summary>
    public int UniqueSerialNumber { get; init; }
    /// <summary>Gets the series size of a limited edition offer, or 0 for other offer types.</summary>
    public int UniqueSeriesSize { get; init; }
    /// <summary>Gets whether the item of a used floor offer has been used, or <see langword="null"/> for other offer types.</summary>
    public bool? IsUsed { get; init; }
    /// <summary>Gets the price of the offer in credits.</summary>
    public int Price { get; init; }
    /// <summary>Gets the number of minutes until the offer expires.</summary>
    public int MinutesRemaining { get; init; }
    /// <summary>Gets the average price of the item in credits.</summary>
    public int AveragePrice { get; init; }
    /// <summary>Gets the trade volume of the item, which the offer layout does not carry, so parsed offers report 0.</summary>
    public int TradeVolume { get; init; }
    /// <summary>Gets the number of open offers for the item in a search result, or 0 for the local user's own offers.</summary>
    public int Offers { get; init; }
    /// <summary>
    /// Gets the time the local user's own offer sold or expired, in Unix milliseconds, or
    /// <see langword="null"/> when the hotel did not send it.
    /// </summary>
    public long? StatusTimeMilliseconds { get; init; }
    /// <summary>Gets whether the offer contains a floor item, which includes limited edition and used floor offers.</summary>
    public bool IsFloor => WireType is 1 or 3 or 4;
    /// <summary>Gets whether the offer contains a wall item.</summary>
    public bool IsWall => WireType == 2;
    /// <summary>Gets whether the offer contains a limited edition item.</summary>
    public bool IsLimitedEdition => WireType == 3;
    /// <summary>Gets whether the offer is a used floor offer that carries <see cref="IsUsed"/>.</summary>
    public bool IsUsable => WireType == 4;
    /// <summary>Gets the status of the offer.</summary>
    public MarketplaceOfferStatus OfferStatus => (MarketplaceOfferStatus)Status;
    /// <summary>Gets the type of the offer.</summary>
    public MarketplaceOfferType OfferType => (MarketplaceOfferType)WireType;
    /// <summary>Gets <see cref="StatusTimeMilliseconds"/> as a UTC time, or <see langword="null"/> when it was not sent.</summary>
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
                    in p);
                break;
            case 2:
                MarketplaceWire.RequireString(offer.WallData, nameof(offer.WallData), in p);
                break;
            case 4:
                MarketplaceWire.ValidateItemData(
                    offer.Data ?? throw new InvalidDataException(
                        "Flash marketplace type 4 offers require item data."),
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
}

/// <summary>Represents the hotel's result for a marketplace offer search.</summary>
/// <remarks>Received as the Flash <c>MarketPlaceOffers</c> message.</remarks>
public sealed record MarketplaceOffers : IParserComposer<MarketplaceOffers>
{
    private IReadOnlyList<MarketplaceOffer> _offers = Array.Empty<MarketplaceOffer>();

    /// <summary>Initializes a new instance of the <see cref="MarketplaceOffers"/> record.</summary>
    /// <param name="offers">The offers in the result.</param>
    /// <param name="totalItemsFound">The total number of items the hotel found for the search.</param>
    public MarketplaceOffers(
        IReadOnlyList<MarketplaceOffer> offers,
        int totalItemsFound)
    {
        Offers = offers;
        TotalItemsFound = totalItemsFound;
    }

    /// <summary>Gets the offers in the result, as a read only copy.</summary>
    public IReadOnlyList<MarketplaceOffer> Offers
    {
        get => _offers;
        init => _offers = MarketplaceWire.FreezeReferences(value, nameof(Offers));
    }

    /// <summary>Gets the total number of items the hotel found for the search.</summary>
    public int TotalItemsFound { get; init; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceOffers ParseFlash(in PacketReader p)
    {
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(Offers));
        var offers = new MarketplaceOffer[count];
        for (int i = 0; i < count; i++)
            offers[i] = MarketplaceCodec.ReadFlashOffer(in p, false);
        return new MarketplaceOffers(offers, p.ReadInt());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MarketplaceOffers value, in PacketWriter p)
    {
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.ValidateFlashOffer(offer, false, in p);

        p.WriteInt(value.Offers.Count);
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.WriteFlashOffer(in p, offer, false);
        p.WriteInt(value.TotalItemsFound);
    }
}

/// <summary>Represents the local user's own marketplace offers.</summary>
/// <remarks>Received as the Flash <c>MarketPlaceOwnOffers</c> message.</remarks>
public sealed record MarketplaceOwnOffers : IParserComposer<MarketplaceOwnOffers>
{
    private IReadOnlyList<MarketplaceOffer> _offers = Array.Empty<MarketplaceOffer>();

    /// <summary>Initializes a new instance of the <see cref="MarketplaceOwnOffers"/> record.</summary>
    /// <param name="creditsWaiting">The credits from sold offers that are waiting to be redeemed.</param>
    /// <param name="offers">The local user's offers.</param>
    public MarketplaceOwnOffers(
        int creditsWaiting,
        IReadOnlyList<MarketplaceOffer> offers)
    {
        CreditsWaiting = creditsWaiting;
        Offers = offers;
    }

    /// <summary>Gets the credits from sold offers that are waiting to be redeemed.</summary>
    public int CreditsWaiting { get; init; }

    /// <summary>Gets the local user's offers, as a read only copy.</summary>
    public IReadOnlyList<MarketplaceOffer> Offers
    {
        get => _offers;
        init => _offers = MarketplaceWire.FreezeReferences(value, nameof(Offers));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>
    /// Both Flash offer layouts are tried, first with the sold or expired time after sold and expired
    /// offers, then without it, and the one that consumes the whole packet wins.
    /// </remarks>
    /// <exception cref="InvalidDataException">Thrown when the packet matches neither layout.</exception>
    public static MarketplaceOwnOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MarketplaceOwnOffers value, in PacketWriter p)
    {
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.ValidateFlashOffer(offer, true, in p);

        p.WriteInt(value.CreditsWaiting);
        p.WriteInt(value.Offers.Count);
        foreach (MarketplaceOffer offer in value.Offers)
            MarketplaceCodec.WriteFlashOffer(in p, offer, true);
    }
}

/// <summary>Represents one day of the marketplace trade history of a furni kind.</summary>
/// <param name="DayOffset">The day of the entry, as an offset in days sent by the hotel.</param>
/// <param name="AverageSalePrice">The average sale price on that day in credits.</param>
/// <param name="SoldAmount">The number of items sold on that day.</param>
public readonly record struct MarketplaceTradeInfo(
    int DayOffset,
    int AverageSalePrice,
    int SoldAmount);

/// <summary>Represents the marketplace statistics for one furni kind.</summary>
public sealed record MarketplaceItemStats : IParserComposer<MarketplaceItemStats>
{
    private IReadOnlyList<MarketplaceTradeInfo> _history =
        Array.Empty<MarketplaceTradeInfo>();

    /// <summary>Initializes a new instance of the <see cref="MarketplaceItemStats"/> record.</summary>
    /// <param name="averageSalePrice">The average sale price in credits.</param>
    /// <param name="offerCount">The number of open offers for the item.</param>
    /// <param name="historyLengthDays">The number of days the history covers.</param>
    /// <param name="history">The daily trade history.</param>
    /// <param name="furniTypeId">The furni type id.</param>
    /// <param name="furniCategoryId">The raw marketplace category of the furni.</param>
    /// <param name="lowestPrice">The lowest open offer price, or <see langword="null"/> when the hotel did not send it.</param>
    /// <param name="suggestedPrice">The suggested price, or <see langword="null"/> when the hotel did not send it.</param>
    public MarketplaceItemStats(
        int averageSalePrice,
        int offerCount,
        int historyLengthDays,
        IReadOnlyList<MarketplaceTradeInfo> history,
        int furniTypeId,
        int furniCategoryId,
        int? lowestPrice,
        int? suggestedPrice)
    {
        AverageSalePrice = averageSalePrice;
        OfferCount = offerCount;
        HistoryLengthDays = historyLengthDays;
        History = history;
        FurniTypeId = furniTypeId;
        FurniCategoryId = furniCategoryId;
        LowestPrice = lowestPrice;
        SuggestedPrice = suggestedPrice;
    }

    /// <summary>Gets the average sale price in credits.</summary>
    public int AverageSalePrice { get; init; }
    /// <summary>Gets the number of open offers for the item.</summary>
    public int OfferCount { get; init; }
    /// <summary>Gets the number of days the history covers.</summary>
    public int HistoryLengthDays { get; init; }

    /// <summary>Gets the daily trade history, as a read only copy.</summary>
    public IReadOnlyList<MarketplaceTradeInfo> History
    {
        get => _history;
        init => _history = MarketplaceWire.FreezeValues(value, nameof(History));
    }

    /// <summary>Gets the furni type id.</summary>
    public int FurniTypeId { get; init; }
    /// <summary>Gets the raw marketplace category of the furni, see <see cref="FurniCategory"/>.</summary>
    public int FurniCategoryId { get; init; }
    /// <summary>Gets the lowest open offer price, or <see langword="null"/> when the hotel did not send it.</summary>
    public int? LowestPrice { get; init; }
    /// <summary>Gets the suggested price, or <see langword="null"/> when the hotel did not send it.</summary>
    public int? SuggestedPrice { get; init; }
    /// <summary>Gets the raw marketplace category of the furni, the same value as <see cref="FurniCategoryId"/>.</summary>
    public int ItemType => FurniCategoryId;
    /// <summary>Gets the furni type id, the same value as <see cref="FurniTypeId"/>.</summary>
    public int Kind => FurniTypeId;
    /// <summary>Gets the marketplace category of the furni.</summary>
    public MarketplaceFurniCategory FurniCategory =>
        (MarketplaceFurniCategory)FurniCategoryId;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>The lowest and suggested prices are read only when exactly 8 bytes remain after the furni type id.</remarks>
    public static MarketplaceItemStats Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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
            ReadPriceTail(in p);
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
        in PacketReader p) => p.Available switch
        {
            0 => (null, null),
            8 => (p.ReadInt(), p.ReadInt()),
            _ => throw new InvalidDataException(
                $"Unsupported Flash marketplace item-stats tail length {p.Available}.")
        };

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MarketplaceItemStats value, in PacketWriter p)
    {
        MarketplaceWire.RequirePriceTail(
            value.LowestPrice,
            value.SuggestedPrice);
        WriteFlash(value, in p);
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

/// <summary>Represents the hotel's answer to a marketplace purchase.</summary>
/// <remarks>Received as the Flash <c>MarketplaceBuyOfferResult</c> message.</remarks>
/// <param name="Result">The raw result code, see <see cref="ResultCode"/>.</param>
/// <param name="RequestedOfferId">The id of the offer the purchase asked for.</param>
/// <param name="NewOfferId">The id of the replacement offer when the offer changed, as sent by the hotel.</param>
/// <param name="NewPrice">The price of the replacement offer in credits when the offer changed, as sent by the hotel.</param>
public sealed record MarketplaceBuyResult(
    int Result,
    Id RequestedOfferId,
    Id NewOfferId,
    int NewPrice) : IParserComposer<MarketplaceBuyResult>
{
    /// <summary>Gets the result of the purchase.</summary>
    public MarketplaceBuyResultCode ResultCode =>
        (MarketplaceBuyResultCode)Result;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceBuyResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MarketplaceBuyResult value, in PacketWriter p)
    {
        int new_offer_id = MarketplaceWire.FlashId(value.NewOfferId);
        int requested_offer_id = MarketplaceWire.FlashId(value.RequestedOfferId);
        p.WriteInt(value.Result);
        p.WriteInt(new_offer_id);
        p.WriteInt(value.NewPrice);
        p.WriteInt(requested_offer_id);
    }
}

/// <summary>Represents the hotel's answer to whether the local user may list a marketplace offer.</summary>
/// <param name="ResultCode">The raw result code, see <see cref="Result"/>.</param>
/// <param name="TokenCount">The number of listing tokens the account holds.</param>
public sealed record MarketplaceCanMakeOfferResult(
    int ResultCode,
    int TokenCount) : IParserComposer<MarketplaceCanMakeOfferResult>
{
    /// <summary>Gets the verdict on whether the local user may list an offer.</summary>
    public MarketplaceEligibilityResult Result =>
        (MarketplaceEligibilityResult)ResultCode;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceCanMakeOfferResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceCanMakeOfferResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        MarketplaceCanMakeOfferResult value,
        in PacketWriter p)
    {
        p.WriteInt(value.ResultCode);
        p.WriteInt(value.TokenCount);
    }
}

/// <summary>Represents the hotel's answer to a request to list a marketplace offer.</summary>
/// <param name="Result">The result code as sent by the hotel.</param>
public sealed record MarketplaceMakeOfferResult(int Result)
    : IParserComposer<MarketplaceMakeOfferResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceMakeOfferResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceMakeOfferResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        MarketplaceMakeOfferResult value,
        in PacketWriter p) => p.WriteInt(value.Result);
}

/// <summary>Represents the hotel's answer to a request to cancel one of the local user's marketplace offers.</summary>
/// <param name="OfferId">The id of the offer.</param>
/// <param name="Success">Whether the hotel reported success.</param>
public sealed record MarketplaceCancelOfferResult(Id OfferId, bool Success)
    : IParserComposer<MarketplaceCancelOfferResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceCancelOfferResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceCancelOfferResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        MarketplaceCancelOfferResult value,
        in PacketWriter p)
    {
        int offer_id = MarketplaceWire.FlashId(value.OfferId);
        p.WriteInt(offer_id);
        p.WriteBool(value.Success);
    }
}

/// <summary>Represents the hotel's marketplace settings.</summary>
/// <param name="IsEnabled">Whether the marketplace is enabled.</param>
/// <param name="Commission">The commission setting as sent by the hotel.</param>
/// <param name="TokenBatchPrice">The price in credits of one batch of listing tokens.</param>
/// <param name="TokenBatchSize">The number of listing tokens in one batch.</param>
/// <param name="OfferMinimumPrice">The lowest price an offer may ask, in credits.</param>
/// <param name="OfferMaximumPrice">The highest price an offer may ask, in credits.</param>
/// <param name="ExpirationHours">The number of hours an offer stays listed.</param>
/// <param name="AveragePricePeriod">The period the average price is computed over, as sent by the hotel.</param>
/// <param name="SellingFeePercentage">The selling fee as a percentage.</param>
/// <param name="RevenueLimit">The revenue limit as sent by the hotel.</param>
/// <param name="HalfTaxLimit">The half tax limit as sent by the hotel.</param>
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
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceConfiguration Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceConfiguration ParseFlash(in PacketReader p) =>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
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

/// <summary>Represents the hotel's answer to a request to cancel all of the local user's marketplace offers.</summary>
public sealed record MarketplaceCancelAllOffersResult
    : IParserComposer<MarketplaceCancelAllOffersResult>
{
    private IReadOnlyList<Id> _offer_ids = Array.Empty<Id>();

    /// <summary>Initializes a new instance of the <see cref="MarketplaceCancelAllOffersResult"/> record.</summary>
    /// <param name="offerIds">The ids of the canceled offers.</param>
    /// <param name="success">Whether the hotel reported success.</param>
    public MarketplaceCancelAllOffersResult(
        IReadOnlyList<Id> offerIds,
        bool success)
    {
        OfferIds = offerIds;
        Success = success;
    }

    /// <summary>Gets the ids of the canceled offers, as a read only copy.</summary>
    public IReadOnlyList<Id> OfferIds
    {
        get => _offer_ids;
        init => _offer_ids = MarketplaceWire.FreezeValues(value, nameof(OfferIds));
    }

    /// <summary>Gets whether the hotel reported success.</summary>
    public bool Success { get; init; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceCancelAllOffersResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceCancelAllOffersResult ParseFlash(in PacketReader p)
    {
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(OfferIds));
        var offer_ids = new Id[count];
        for (int i = 0; i < count; i++)
            offer_ids[i] = p.ReadInt();
        return new MarketplaceCancelAllOffersResult(offer_ids, p.ReadBool());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Represents the hotel's answer to a request to clear the local user's sold or expired offer history.</summary>
/// <param name="Success">Whether the hotel reported success.</param>
public sealed record MarketplaceClearOwnHistoryResult(bool Success)
    : IParserComposer<MarketplaceClearOwnHistoryResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceClearOwnHistoryResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceClearOwnHistoryResult ParseFlash(in PacketReader p) =>
        new(p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        MarketplaceClearOwnHistoryResult value,
        in PacketWriter p) => p.WriteBool(value.Success);
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
        int? suggested_price)
    {
        if (lowest_price.HasValue != suggested_price.HasValue)
        {
            throw new InvalidDataException(
                "Flash marketplace item stats require both price-tail fields or neither.");
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
