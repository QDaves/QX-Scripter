namespace Qx.Model.Marketplace;

/// <summary>
/// Which pool a marketplace request addresses, sent as the <c>furniCategoryId</c> of item
/// stats and offer requests. The Flash client picks it with
/// <c>MarketplaceModel.resolveStatsRequestCategory</c>: a unique serial number wins over
/// everything, otherwise a wall item is 2 and anything else is 1.
/// </summary>
public enum MarketplaceFurniCategory
{
    /// <summary>1: ordinary floor furni.</summary>
    Floor = 1,
    /// <summary>2: wall furni.</summary>
    Wall = 2,
    /// <summary>
    /// 3: limited-edition items, which are tracked per serial number rather than per kind.
    /// Only valid for lookups; an offer can never be created in this category.
    /// </summary>
    Limited = 3
}

/// <summary>The lifecycle state of a marketplace offer, the <c>status</c> field of an offer.</summary>
public enum MarketplaceOfferStatus
{
    /// <summary>1: still listed and buyable.</summary>
    Open = 1,
    /// <summary>2: someone bought it. On modern Flash the sale time follows in the packet.</summary>
    Sold = 2,
    /// <summary>3: it ran out its listing time unsold. On modern Flash the expiry time follows in the packet.</summary>
    Expired = 3
}

/// <summary>
/// What kind of item a marketplace offer contains, which also decides how the rest of the
/// offer is laid out on the wire.
/// </summary>
public enum MarketplaceOfferType
{
    /// <summary>1: a floor item; the offer carries its kind and a furni payload.</summary>
    Floor = 1,
    /// <summary>2: a wall item; the offer carries its kind and a plain data string.</summary>
    Wall = 2,
    /// <summary>3: a limited edition; the offer carries its kind, serial number and series size.</summary>
    LimitedEdition = 3,
    /// <summary>
    /// 4: a floor item that has already been used, which additionally carries a used flag.
    /// Rejected on legacy Flash builds, whose offer layout only knows types 1 to 3, and never
    /// sent by Unity.
    /// </summary>
    UsableFloor = 4
}

/// <summary>Which slice of the local user's own marketplace offers a request asks for.</summary>
public enum MarketplaceOwnOffersCategory
{
    /// <summary>1: offers still listed and awaiting a buyer.</summary>
    Open = 1,
    /// <summary>2: offers that sold. Only this and <see cref="Expired"/> may be cleared from history.</summary>
    Sold = 2,
    /// <summary>3: offers that ran out unsold. Only this and <see cref="Sold"/> may be cleared from history.</summary>
    Expired = 3
}

/// <summary>
/// How marketplace search results are ordered. The Flash catalog exposes 1 and 2 on the
/// value search, 3 to 6 on the activity search and all six on the advanced search, labelling
/// them <c>catalog.marketplace.sort.{1..6}</c>.
/// </summary>
public enum MarketplaceSortOrder
{
    /// <summary>1: most expensive offer first.</summary>
    HighestPrice = 1,
    /// <summary>2: cheapest offer first.</summary>
    LowestPrice = 2,
    /// <summary>3: most traded kind first.</summary>
    MostTrades = 3,
    /// <summary>4: least traded kind first.</summary>
    LeastTrades = 4,
    /// <summary>5: most currently listed offers first.</summary>
    MostOffers = 5,
    /// <summary>6: fewest currently listed offers first.</summary>
    LeastOffers = 6
}

/// <summary>
/// The hotel's verdict on whether the local user may list an offer, the <c>resultCode</c> of
/// the can-make-offer reply. Flash routes each value in
/// <c>MarketplaceModel.proceedOfferMaking</c>.
/// </summary>
public enum MarketplaceEligibilityResult
{
    /// <summary>1: the listing may proceed; the client opens the make-offer dialog.</summary>
    Allowed = 1,
    /// <summary>
    /// 2: the account lacks trading privileges.
    /// <c>inventory.marketplace.no_trading_privilege</c>.
    /// </summary>
    MissingTradingPrivileges = 2,
    /// <summary>3: the account holds no trading pass. <c>inventory.marketplace.no_trading_pass</c>.</summary>
    MissingTradingPass = 3,
    /// <summary>4: the account must buy listing tokens first; the client opens the token purchase dialog.</summary>
    RequiresTokens = 4,
    /// <summary>5: the hotel refused the offer outright and the held items are released again.</summary>
    OfferRejected = 5,
    /// <summary>6: the account is under a trading lock. <c>inventory.marketplace.trading_lock</c>.</summary>
    TradingLocked = 6
}

/// <summary>The outcome of a marketplace purchase, the <c>result</c> of the buy reply.</summary>
public enum MarketplaceBuyResultCode
{
    /// <summary>1: the purchase went through and the item lands in the buyer's hand.</summary>
    Success = 1,
    /// <summary>2: the offer is gone, normally because someone else bought it first.</summary>
    OfferUnavailable = 2,
    /// <summary>
    /// 3: the offer changed before the purchase landed; the reply carries the replacement
    /// offer identifier and its new price.
    /// </summary>
    OfferUpdated = 3,
    /// <summary>4: the buyer cannot afford the offer.</summary>
    NotEnoughCredits = 4
}
