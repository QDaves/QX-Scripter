using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

/// <summary>Represents a request for the gift state view.</summary>
/// <remarks>Used by the <c>gifts.state</c> query, which reads the current gift state.</remarks>
public sealed record GiftStateRequest;

/// <summary>Represents a summary of the gift wrapping configuration.</summary>
/// <param name="IsWrappingEnabled">Whether gift wrapping is enabled.</param>
/// <param name="WrappingPrice">The price of gift wrapping.</param>
/// <param name="StuffTypeCount">The number of gift furniture sprite ids offered as wrapping.</param>
/// <param name="BoxTypeCount">The number of box types offered for wrapping.</param>
/// <param name="RibbonTypeCount">The number of ribbon types offered for wrapping.</param>
/// <param name="DefaultStuffTypeCount">The number of default gift furniture sprite ids.</param>
public sealed record GiftWrappingSummaryView(
    bool IsWrappingEnabled,
    int WrappingPrice,
    int StuffTypeCount,
    int BoxTypeCount,
    int RibbonTypeCount,
    int DefaultStuffTypeCount);

/// <summary>Represents a summary of the club gift information.</summary>
/// <param name="DaysUntilNextGift">The number of days until the next club gift.</param>
/// <param name="GiftsAvailable">The number of club gifts that can be selected now.</param>
/// <param name="OfferCount">The number of catalog offers that can be chosen as a club gift.</param>
/// <param name="EligibilityCount">The number of eligibility entries.</param>
/// <param name="ProductCount">The total number of products across the club gift offers.</param>
public sealed record GiftClubInfoSummaryView(
    int DaysUntilNextGift,
    int GiftsAvailable,
    int OfferCount,
    int EligibilityCount,
    int ProductCount);

/// <summary>Represents a summary of a club gift selection the hotel confirmed.</summary>
/// <param name="ProductCode">The product code of the selected club gift.</param>
/// <param name="ProductCount">The number of products the club gift granted.</param>
public sealed record GiftClubSelectedSummaryView(
    string ProductCode,
    int ProductCount);

/// <summary>Represents a summary of the new user gift offer.</summary>
/// <param name="StepCount">The number of steps in the offer.</param>
/// <param name="OptionCount">The total number of options across all steps.</param>
/// <param name="ProductCount">The total number of products across all options.</param>
public sealed record GiftNewUserOfferSummaryView(
    int StepCount,
    int OptionCount,
    int ProductCount);

/// <summary>Represents a summary of the current gift state.</summary>
/// <remarks>
/// Returned by the <c>gifts.state</c> query. The view is read from the current state and is not
/// retained as a snapshot. Every revision increases when its part of the state is received or
/// cleared.
/// </remarks>
/// <param name="Connected">Whether the state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the state belongs to.</param>
/// <param name="Revision">The gift state revision, which increases with every change.</param>
/// <param name="WrappingRevision">The revision of the gift wrapping configuration.</param>
/// <param name="ClubInfoRevision">The revision of the club gift information.</param>
/// <param name="ClubSelectedRevision">The revision of the last confirmed club gift selection.</param>
/// <param name="PresentOpenedRevision">The revision of the last opened present.</param>
/// <param name="ReceiverNotFoundRevision">The revision of the receiver not found notices.</param>
/// <param name="ClubNotificationRevision">The revision of the club gift notification.</param>
/// <param name="OfferGiftabilityRevision">The revision of the offer giftability answers.</param>
/// <param name="NewUserOfferRevision">The revision of the new user gift offer.</param>
/// <param name="NewUserIncompleteRevision">The revision of the new user flow incomplete notice.</param>
/// <param name="Wrapping">The summary of the gift wrapping configuration, or <see langword="null"/> when it has not been received.</param>
/// <param name="ClubInfo">The summary of the club gift information, or <see langword="null"/> when it has not been received.</param>
/// <param name="LastClubSelected">The summary of the last club gift selection the hotel confirmed, or <see langword="null"/> when there is none.</param>
/// <param name="LastOpenedPresent">The contents of the last present the user opened, or <see langword="null"/> when there is none.</param>
/// <param name="LatestNotification">The last club gift notification, or <see langword="null"/> when there is none.</param>
/// <param name="NewUserOffer">The summary of the new user gift offer, or <see langword="null"/> when it has not been received.</param>
/// <param name="NewUserFlowIsIncomplete">Whether the hotel reported that the new user flow is incomplete.</param>
/// <param name="OfferGiftability">Whether each catalog offer can be sent as a gift, keyed by offer id, for up to 500 offers.</param>
public sealed record GiftStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long WrappingRevision,
    long ClubInfoRevision,
    long ClubSelectedRevision,
    long PresentOpenedRevision,
    long ReceiverNotFoundRevision,
    long ClubNotificationRevision,
    long OfferGiftabilityRevision,
    long NewUserOfferRevision,
    long NewUserIncompleteRevision,
    GiftWrappingSummaryView? Wrapping,
    GiftClubInfoSummaryView? ClubInfo,
    GiftClubSelectedSummaryView? LastClubSelected,
    PresentOpened? LastOpenedPresent,
    ClubGiftNotification? LatestNotification,
    GiftNewUserOfferSummaryView? NewUserOffer,
    bool NewUserFlowIsIncomplete,
    IReadOnlyDictionary<int, bool> OfferGiftability);

/// <summary>Specifies which list of the gift wrapping configuration a page reads.</summary>
public enum GiftWrappingCollection
{
    /// <summary>The gift furniture sprite ids offered as wrapping.</summary>
    StuffTypes,
    /// <summary>The box types offered for wrapping.</summary>
    BoxTypes,
    /// <summary>The ribbon types offered for wrapping.</summary>
    RibbonTypes,
    /// <summary>The default gift furniture sprite ids.</summary>
    DefaultStuffTypes
}

/// <summary>Represents a request for a page of one gift wrapping list.</summary>
/// <remarks>
/// Used by the <c>gifts.wrapping.list</c> query. The application keeps the four most recent gift
/// snapshots of the active hotel session, and a retained snapshot can no longer be read once it is
/// dropped or the session changes.
/// </remarks>
/// <param name="Collection">The wrapping list to read.</param>
/// <param name="Offset">The zero-based index of the first value to return.</param>
/// <param name="Limit">The maximum number of values to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record GiftWrappingPageRequest(
    GiftWrappingCollection Collection = GiftWrappingCollection.StuffTypes,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of one gift wrapping list, read from one snapshot.</summary>
/// <remarks>Returned by the <c>gifts.wrapping.list</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="WrappingRevision">The revision of the gift wrapping configuration.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether the gift wrapping configuration has been received.</param>
/// <param name="IsWrappingEnabled">Whether gift wrapping is enabled, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="WrappingPrice">The price of gift wrapping, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="Collection">The wrapping list the page was read from.</param>
/// <param name="Total">The number of values in the selected list.</param>
/// <param name="Offset">The zero-based index of the first value in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more values.</param>
/// <param name="Values">The values in the page.</param>
public sealed record GiftWrappingPage(
    bool Connected,
    long SessionGeneration,
    long WrappingRevision,
    long SnapshotRevision,
    bool Loaded,
    bool? IsWrappingEnabled,
    int? WrappingPrice,
    GiftWrappingCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<int> Values);

/// <summary>Specifies which list of the club gift information a page reads.</summary>
public enum GiftClubInfoCollection
{
    /// <summary>The catalog offers that can be chosen as a club gift.</summary>
    Offers,
    /// <summary>The eligibility entries of the club gift offers.</summary>
    Eligibility,
    /// <summary>The products of all club gift offers, flattened in offer order.</summary>
    Products
}

/// <summary>Represents a request for a page of one club gift list.</summary>
/// <remarks>Used by the <c>gifts.club_info.list</c> query.</remarks>
/// <param name="Collection">The club gift list to read.</param>
/// <param name="Offset">The zero-based index of the first row to return.</param>
/// <param name="Limit">The maximum number of rows to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record GiftClubInfoPageRequest(
    GiftClubInfoCollection Collection = GiftClubInfoCollection.Offers,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a catalog offer that can be chosen as a club gift.</summary>
/// <param name="OfferOrdinal">The zero-based position of the offer in the club gift information.</param>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="LocalizationId">The localization key of the offer name.</param>
/// <param name="IsRent">Whether the offer is a rental.</param>
/// <param name="PriceInCredits">The price in credits.</param>
/// <param name="PriceInActivityPoints">The price in activity points.</param>
/// <param name="ActivityPointType">The activity point type the activity point price is paid in.</param>
/// <param name="PriceInSilver">The price in silver.</param>
/// <param name="Giftable">Whether the offer can be bought as a gift.</param>
/// <param name="ClubLevel">The club level required to buy the offer.</param>
/// <param name="BundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
/// <param name="IsPet">Whether the offer is a pet.</param>
/// <param name="PreviewImage">The preview image of the offer.</param>
/// <param name="ProductCount">The number of products the offer contains.</param>
public sealed record GiftClubOfferView(
    int OfferOrdinal,
    int OfferId,
    string LocalizationId,
    bool IsRent,
    int PriceInCredits,
    int PriceInActivityPoints,
    int ActivityPointType,
    int PriceInSilver,
    bool Giftable,
    int ClubLevel,
    bool BundlePurchaseAllowed,
    bool IsPet,
    string PreviewImage,
    int ProductCount);

/// <summary>Represents the eligibility of a club gift offer.</summary>
/// <param name="EligibilityOrdinal">The zero-based position of the entry in the club gift information.</param>
/// <param name="OfferId">The id of the catalog offer the entry applies to.</param>
/// <param name="IsVip">Whether the gift is a VIP club gift.</param>
/// <param name="DaysRequired">The number of club days the gift requires.</param>
/// <param name="IsSelectable">Whether the gift can be selected.</param>
public sealed record GiftClubEligibilityView(
    int EligibilityOrdinal,
    int OfferId,
    bool IsVip,
    int DaysRequired,
    bool IsSelectable);

/// <summary>Represents a product of a club gift offer.</summary>
/// <param name="OfferOrdinal">The zero-based position of the offer that contains the product.</param>
/// <param name="ProductOrdinal">The zero-based position of the product within its offer.</param>
/// <param name="Product">The catalog product.</param>
public sealed record GiftClubProductView(
    int OfferOrdinal,
    int ProductOrdinal,
    CatalogProduct Product);

/// <summary>Represents a page of one club gift list, read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>gifts.club_info.list</c> query. Only the list selected by
/// <paramref name="Collection"/> holds rows, and the other lists are empty.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="ClubInfoRevision">The revision of the club gift information.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether the club gift information has been received.</param>
/// <param name="DaysUntilNextGift">The number of days until the next club gift, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="GiftsAvailable">The number of club gifts that can be selected now, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="TotalOffers">The number of club gift offers.</param>
/// <param name="TotalEligibility">The number of eligibility entries.</param>
/// <param name="TotalProducts">The total number of products across the club gift offers.</param>
/// <param name="Collection">The club gift list the page was read from.</param>
/// <param name="Total">The number of rows in the selected list.</param>
/// <param name="Offset">The zero-based index of the first row in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more rows.</param>
/// <param name="Offers">The offers in the page when <paramref name="Collection"/> is <see cref="GiftClubInfoCollection.Offers"/>.</param>
/// <param name="Eligibility">The eligibility entries in the page when <paramref name="Collection"/> is <see cref="GiftClubInfoCollection.Eligibility"/>.</param>
/// <param name="Products">The products in the page when <paramref name="Collection"/> is <see cref="GiftClubInfoCollection.Products"/>.</param>
public sealed record GiftClubInfoPage(
    bool Connected,
    long SessionGeneration,
    long ClubInfoRevision,
    long SnapshotRevision,
    bool Loaded,
    int? DaysUntilNextGift,
    int? GiftsAvailable,
    int TotalOffers,
    int TotalEligibility,
    int TotalProducts,
    GiftClubInfoCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<GiftClubOfferView> Offers,
    IReadOnlyList<GiftClubEligibilityView> Eligibility,
    IReadOnlyList<GiftClubProductView> Products);

/// <summary>Specifies which list of the last confirmed club gift selection a page reads.</summary>
public enum GiftClubSelectedCollection
{
    /// <summary>The products the selected club gift granted.</summary>
    Products
}

/// <summary>Represents a request for a page of the last confirmed club gift selection.</summary>
/// <remarks>Used by the <c>gifts.club_selected.list</c> query.</remarks>
/// <param name="Collection">The list to read.</param>
/// <param name="Offset">The zero-based index of the first product to return.</param>
/// <param name="Limit">The maximum number of products to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record GiftClubSelectedPageRequest(
    GiftClubSelectedCollection Collection = GiftClubSelectedCollection.Products,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of the last confirmed club gift selection, read from one snapshot.</summary>
/// <remarks>Returned by the <c>gifts.club_selected.list</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="ClubSelectedRevision">The revision of the last confirmed club gift selection.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether the hotel has confirmed a club gift selection in the session.</param>
/// <param name="ProductCode">The product code of the selected club gift, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="TotalProducts">The number of products the selected club gift granted.</param>
/// <param name="Collection">The list the page was read from.</param>
/// <param name="Total">The number of rows in the selected list.</param>
/// <param name="Offset">The zero-based index of the first product in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more products.</param>
/// <param name="Products">The products in the page.</param>
public sealed record GiftClubSelectedPage(
    bool Connected,
    long SessionGeneration,
    long ClubSelectedRevision,
    long SnapshotRevision,
    bool Loaded,
    string? ProductCode,
    int TotalProducts,
    GiftClubSelectedCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CatalogProduct> Products);

/// <summary>Specifies which list of the new user gift offer a page reads.</summary>
public enum GiftNewUserOfferCollection
{
    /// <summary>The steps of the offer.</summary>
    Steps,
    /// <summary>The options of all steps, flattened in step order.</summary>
    Options,
    /// <summary>The products of all options, flattened in step and option order.</summary>
    Products
}

/// <summary>Represents a request for a page of one new user gift offer list.</summary>
/// <remarks>Used by the <c>gifts.new_user_offer.list</c> query.</remarks>
/// <param name="Collection">The list to read.</param>
/// <param name="Offset">The zero-based index of the first row to return.</param>
/// <param name="Limit">The maximum number of rows to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record GiftNewUserOfferPageRequest(
    GiftNewUserOfferCollection Collection = GiftNewUserOfferCollection.Steps,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents one step of the new user gift offer.</summary>
/// <param name="StepOrdinal">The zero-based position of the step in the offer.</param>
/// <param name="DayIndex">The day index of the step.</param>
/// <param name="StepIndex">The step index.</param>
/// <param name="OptionCount">The number of options to choose from in the step.</param>
public sealed record GiftNewUserStepView(
    int StepOrdinal,
    int DayIndex,
    int StepIndex,
    int OptionCount);

/// <summary>Represents one choice in a step of the new user gift offer.</summary>
/// <param name="StepOrdinal">The zero-based position of the step that contains the option.</param>
/// <param name="OptionOrdinal">The zero-based position of the option within its step.</param>
/// <param name="ThumbnailUrl">The thumbnail image URL, or <see langword="null"/> when there is none.</param>
/// <param name="ProductCount">The number of products the option grants.</param>
public sealed record GiftNewUserOptionView(
    int StepOrdinal,
    int OptionOrdinal,
    string? ThumbnailUrl,
    int ProductCount);

/// <summary>Represents a product inside a new user gift option.</summary>
/// <param name="StepOrdinal">The zero-based position of the step that contains the product.</param>
/// <param name="OptionOrdinal">The zero-based position of the option within its step.</param>
/// <param name="ProductOrdinal">The zero-based position of the product within its option.</param>
/// <param name="ProductCode">The product code.</param>
/// <param name="LocalizationKey">The localization key of the product, or <see langword="null"/> when the server sent an empty string.</param>
public sealed record GiftNewUserProductView(
    int StepOrdinal,
    int OptionOrdinal,
    int ProductOrdinal,
    string ProductCode,
    string? LocalizationKey);

/// <summary>Represents a page of one new user gift offer list, read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>gifts.new_user_offer.list</c> query. Only the list selected by
/// <paramref name="Collection"/> holds rows, and the other lists are empty.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="NewUserOfferRevision">The revision of the new user gift offer.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether the new user gift offer has been received.</param>
/// <param name="TotalSteps">The number of steps in the offer.</param>
/// <param name="TotalOptions">The total number of options across all steps.</param>
/// <param name="TotalProducts">The total number of products across all options.</param>
/// <param name="Collection">The list the page was read from.</param>
/// <param name="Total">The number of rows in the selected list.</param>
/// <param name="Offset">The zero-based index of the first row in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more rows.</param>
/// <param name="Steps">The steps in the page when <paramref name="Collection"/> is <see cref="GiftNewUserOfferCollection.Steps"/>.</param>
/// <param name="Options">The options in the page when <paramref name="Collection"/> is <see cref="GiftNewUserOfferCollection.Options"/>.</param>
/// <param name="Products">The products in the page when <paramref name="Collection"/> is <see cref="GiftNewUserOfferCollection.Products"/>.</param>
public sealed record GiftNewUserOfferPage(
    bool Connected,
    long SessionGeneration,
    long NewUserOfferRevision,
    long SnapshotRevision,
    bool Loaded,
    int TotalSteps,
    int TotalOptions,
    int TotalProducts,
    GiftNewUserOfferCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<GiftNewUserStepView> Steps,
    IReadOnlyList<GiftNewUserOptionView> Options,
    IReadOnlyList<GiftNewUserProductView> Products);

/// <summary>Represents a request to reload the gift wrapping configuration and the club gift information.</summary>
/// <remarks>
/// Used by the <c>gifts.refresh</c> operation. Both requests are sent together and the call waits
/// for the first fresh answer to each. Gift refreshes run one at a time, and the timeout covers the
/// whole call, including the wait for an earlier refresh.
/// </remarks>
/// <param name="Limit">The maximum number of club gift offers in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time the call may take, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record GiftRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a gift configuration refresh.</summary>
/// <remarks>Returned by the <c>gifts.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="CompletedAtUtc">The UTC time the refresh completed.</param>
/// <param name="WrappingObservedAtUtc">The UTC time the gift wrapping configuration was observed.</param>
/// <param name="ClubInfoObservedAtUtc">The UTC time the club gift information was observed.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="WrappingRevision">The revision of the gift wrapping configuration that was received.</param>
/// <param name="ClubInfoRevision">The revision of the club gift information that was received.</param>
/// <param name="Wrapping">The summary of the received gift wrapping configuration.</param>
/// <param name="ClubInfo">The summary of the received club gift information.</param>
/// <param name="ClubInfoPage">The first page of club gift offers from the refreshed snapshot.</param>
public sealed record GiftRefreshResult(
    long SessionGeneration,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset WrappingObservedAtUtc,
    DateTimeOffset ClubInfoObservedAtUtc,
    long SnapshotRevision,
    long WrappingRevision,
    long ClubInfoRevision,
    GiftWrappingSummaryView Wrapping,
    GiftClubInfoSummaryView ClubInfo,
    GiftClubInfoPage ClubInfoPage);

/// <summary>Represents a request to open a present placed in the current room.</summary>
/// <remarks>
/// Used by the <c>gifts.present.open</c> operation, which requires a ready room. The operation sends
/// the request and returns without waiting, and the contents are published by the
/// <c>gifts.changed</c> event as a <see cref="GiftChangeKind.PresentOpened"/> change.
/// </remarks>
/// <param name="FurniId">The room id of the present, which must be positive.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record GiftPresentOpenRequest(
    Id FurniId,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the receipt for a present open request that was sent.</summary>
/// <remarks>Returned by the <c>gifts.present.open</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the request was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the request was sent in.</param>
/// <param name="RoomId">The id of the room the request was sent in.</param>
/// <param name="RoomGeneration">The generation of the room session the request was sent in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="FurniId">The room id of the present.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record GiftPresentOpenDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    Id FurniId,
    int MessagesDispatched);

/// <summary>Represents a request to buy a catalog offer as a gift for another user.</summary>
/// <remarks>
/// Used by the <c>gifts.purchase</c> operation. The operation sends one purchase and returns without
/// waiting for the outcome. The catalog state must belong to the same hotel session as the gift
/// state. Each gift purchase sends one item whatever <paramref name="Quantity"/> is.
/// </remarks>
/// <param name="PageId">The id of the catalog page, sent unchanged.</param>
/// <param name="OfferId">The id of the catalog offer, sent unchanged.</param>
/// <param name="ExtraData">The catalog extra data, sent unchanged and at most 65535 UTF-8 bytes.</param>
/// <param name="ReceiverName">The name of the user who receives the gift, at most 65535 UTF-8 bytes.</param>
/// <param name="GiftMessage">The message shown with the gift, at most 65535 UTF-8 bytes.</param>
/// <param name="SpriteId">The gift furniture sprite id used as wrapping.</param>
/// <param name="BoxType">The box type of the wrapping.</param>
/// <param name="RibbonType">The ribbon type of the wrapping.</param>
/// <param name="ShowPurchaserName">Whether the purchaser name is shown to the receiver.</param>
/// <param name="Quantity">The requested quantity, at least 1.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call must run in, or <see langword="null"/> to use the current catalog.</param>
public sealed record GiftPurchaseRequest(
    int PageId,
    int OfferId,
    string ExtraData,
    string ReceiverName,
    string GiftMessage,
    int SpriteId,
    int BoxType,
    int RibbonType,
    bool ShowPurchaserName,
    int Quantity = 1,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents the receipt for a gift purchase that was sent.</summary>
/// <remarks>Returned by the <c>gifts.purchase</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the purchase was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the purchase was sent in.</param>
/// <param name="CatalogGeneration">The catalog generation the purchase was sent in.</param>
/// <param name="PageId">The id of the catalog page.</param>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="Quantity">The number of items sent, which is always 1 for a gift purchase.</param>
/// <param name="ShowPurchaserName">Whether the purchaser name is shown to the receiver.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record GiftPurchaseDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long CatalogGeneration,
    int PageId,
    int OfferId,
    int Quantity,
    bool ShowPurchaserName,
    int MessagesDispatched);

/// <summary>Represents a request to select a club gift.</summary>
/// <remarks>
/// Used by the <c>gifts.club.select</c> operation. The operation sends the selection and returns
/// without waiting, and a confirmation is published by the <c>gifts.changed</c> event as a
/// <see cref="GiftChangeKind.ClubSelected"/> change.
/// </remarks>
/// <param name="ProductCode">The product code of the club gift, which must not be blank and must fit in 65535 UTF-8 bytes.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedClubInfoRevision">The club gift information revision the selection is based on, or <see langword="null"/> to skip the check. When set, the club gift information must be loaded and unchanged until the selection is sent.</param>
public sealed record GiftClubSelectRequest(
    string ProductCode,
    long? ExpectedSessionGeneration = null,
    long? ExpectedClubInfoRevision = null);

/// <summary>Represents the receipt for a club gift selection that was sent.</summary>
/// <remarks>Returned by the <c>gifts.club.select</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the selection was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the selection was sent in.</param>
/// <param name="ClubInfoRevision">The club gift information revision when the selection was sent.</param>
/// <param name="ProductCode">The product code that was sent.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record GiftClubSelectDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long ClubInfoRevision,
    string ProductCode,
    int MessagesDispatched);

/// <summary>Represents a request to ask the hotel whether a catalog offer can be sent as a gift.</summary>
/// <remarks>
/// Used by the <c>gifts.offer_giftability.refresh</c> operation. The call waits for the answer for
/// the same offer id. A request that times out is sent once more, and the timeout is split across
/// both attempts.
/// </remarks>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the answer, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record GiftOfferGiftabilityRefreshRequest(
    int OfferId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of an offer giftability request.</summary>
/// <remarks>Returned by the <c>gifts.offer_giftability.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the request ran in.</param>
/// <param name="Revision">The gift state revision after the answer was received.</param>
/// <param name="OfferGiftabilityRevision">The revision of the offer giftability answers after the answer was received.</param>
/// <param name="ObservedAtUtc">The UTC time the answer was observed.</param>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="IsGiftable">Whether the offer can be sent as a gift.</param>
public sealed record GiftOfferGiftabilityRefreshResult(
    long SessionGeneration,
    long Revision,
    long OfferGiftabilityRevision,
    DateTimeOffset ObservedAtUtc,
    int OfferId,
    bool IsGiftable);

/// <summary>Represents a request to choose gifts from the new user gift offer.</summary>
/// <remarks>
/// Used by the <c>gifts.new_user.select</c> operation, which sends the selection and returns without
/// waiting for the hotel to accept it.
/// </remarks>
/// <param name="Selections">The chosen gift for each day and step, in order, at most 21845 entries.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedNewUserOfferRevision">The new user offer revision the selection is based on, or <see langword="null"/> to skip the check. When set, the new user offer must be loaded and unchanged until the selection is sent.</param>
public sealed record GiftNewUserSelectRequest(
    IReadOnlyList<NuxGiftSelection> Selections,
    long? ExpectedSessionGeneration = null,
    long? ExpectedNewUserOfferRevision = null);

/// <summary>Represents the receipt for a new user gift selection that was sent.</summary>
/// <remarks>Returned by the <c>gifts.new_user.select</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the selection was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the selection was sent in.</param>
/// <param name="NewUserOfferRevision">The new user offer revision when the selection was sent.</param>
/// <param name="SelectionCount">The number of selections that were sent.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record GiftNewUserSelectDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long NewUserOfferRevision,
    int SelectionCount,
    int MessagesDispatched);

/// <summary>Represents a request to advance the new user flow to its next step.</summary>
/// <remarks>
/// Used by the <c>gifts.new_user.advance</c> operation, which requires a ready room and sends the
/// request without waiting for an answer.
/// </remarks>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record GiftNewUserAdvanceRequest(
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the receipt for a new user flow advance that was sent.</summary>
/// <remarks>Returned by the <c>gifts.new_user.advance</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the request was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the request was sent in.</param>
/// <param name="RoomId">The id of the room the request was sent in.</param>
/// <param name="RoomGeneration">The generation of the room session the request was sent in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record GiftNewUserAdvanceDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int MessagesDispatched);

/// <summary>Specifies the kind of a gift change.</summary>
public enum GiftChangeKind
{
    /// <summary>The gift wrapping configuration was received.</summary>
    Wrapping,
    /// <summary>The club gift information was received.</summary>
    ClubInfo,
    /// <summary>The hotel confirmed a club gift selection.</summary>
    ClubSelected,
    /// <summary>The hotel reported the contents of an opened present.</summary>
    PresentOpened,
    /// <summary>The hotel reported that the receiver of a gift does not exist.</summary>
    ReceiverNotFound,
    /// <summary>A club gift notification was received.</summary>
    ClubNotification,
    /// <summary>The hotel answered whether an offer can be sent as a gift.</summary>
    OfferGiftability,
    /// <summary>The new user gift offer was received.</summary>
    NewUserOffer,
    /// <summary>The hotel reported that the new user flow is incomplete.</summary>
    NewUserIncomplete,
    /// <summary>The gift state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the gift state.</summary>
/// <remarks>Published by the <c>gifts.changed</c> event. Only the value that matches <paramref name="Kind"/> is set.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The gift state revision after the change.</param>
/// <param name="SourceRevision">The revision of the part that changed, or <paramref name="Revision"/> for <see cref="GiftChangeKind.Reset"/>.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot for <see cref="GiftChangeKind.Wrapping"/>, <see cref="GiftChangeKind.ClubInfo"/>, <see cref="GiftChangeKind.ClubSelected"/> and <see cref="GiftChangeKind.NewUserOffer"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Wrapping">The summary of the received wrapping configuration for <see cref="GiftChangeKind.Wrapping"/>.</param>
/// <param name="ClubInfo">The summary of the received club gift information for <see cref="GiftChangeKind.ClubInfo"/>.</param>
/// <param name="ClubSelected">The summary of the confirmed selection for <see cref="GiftChangeKind.ClubSelected"/>.</param>
/// <param name="PresentOpened">The contents of the opened present for <see cref="GiftChangeKind.PresentOpened"/>.</param>
/// <param name="ReceiverNotFound">Whether the change is a <see cref="GiftChangeKind.ReceiverNotFound"/> notice.</param>
/// <param name="ClubNotification">The club gift notification for <see cref="GiftChangeKind.ClubNotification"/>.</param>
/// <param name="OfferGiftability">The giftability answer for <see cref="GiftChangeKind.OfferGiftability"/>.</param>
/// <param name="NewUserOffer">The summary of the received new user offer for <see cref="GiftChangeKind.NewUserOffer"/>.</param>
/// <param name="NewUserFlowIncomplete">Whether the change is a <see cref="GiftChangeKind.NewUserIncomplete"/> notice.</param>
public sealed record GiftChanged(
    GiftChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    GiftWrappingSummaryView? Wrapping,
    GiftClubInfoSummaryView? ClubInfo,
    GiftClubSelectedSummaryView? ClubSelected,
    PresentOpened? PresentOpened,
    bool ReceiverNotFound,
    ClubGiftNotification? ClubNotification,
    IsOfferGiftable? OfferGiftability,
    GiftNewUserOfferSummaryView? NewUserOffer,
    bool NewUserFlowIncomplete);

internal interface IGiftOperations
{
    void RequestWrappingConfiguration();
    void OpenPresent(Id furni_id);
    void Purchase(PurchaseFromCatalogAsGift request);
    void RequestClubGifts();
    void SelectClubGift(string product_code);
    void RequestOfferGiftability(int offer_id);
    void SelectNewUserGifts(IReadOnlyList<NuxGiftSelection> selections);
    void AdvanceNewUserFlow();
}
