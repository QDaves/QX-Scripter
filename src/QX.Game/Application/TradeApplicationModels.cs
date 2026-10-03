using Qx.Game.Snapshots;
using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the trade state.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeState"/>.
/// </remarks>
/// <param name="OfferItemLimit">The maximum number of items returned for each participant offer, from 0 to 500.</param>
/// <param name="NftOfferLimit">The maximum number of NFT assets returned for each side's NFT offer, from 0 to 500.</param>
public sealed record TradeStateRequest(
    int OfferItemLimit = 100,
    int NftOfferLimit = 100);

/// <summary>
/// Represents a participant of a trade.
/// </summary>
/// <param name="UserId">The id of the participant.</param>
/// <param name="CanTrade">Whether the hotel reported that the participant can trade.</param>
/// <param name="Accepted">
/// Whether the participant accepted the current offers, cleared when the furni or NFT offers change.
/// </param>
public sealed record TradeParticipantView(
    Id UserId,
    bool CanTrade,
    bool Accepted);

/// <summary>
/// Represents a furni item in a trade offer.
/// </summary>
/// <param name="ItemId">The inventory item id, which addresses the item within the trade.</param>
/// <param name="Type">Whether the item is a floor or a wall item.</param>
/// <param name="Id">The room item id of the furni.</param>
/// <param name="Kind">The furni kind id.</param>
/// <param name="Category">The item category, which uses the <see cref="FurniCategory"/> numbering.</param>
/// <param name="IsGroupable">Whether the client may group the item with others of the same kind.</param>
/// <param name="Data">The item data.</param>
/// <param name="CreationDay">The day of the month the item was created.</param>
/// <param name="CreationMonth">The month the item was created.</param>
/// <param name="CreationYear">The year the item was created.</param>
/// <param name="Extra">The extra value of a floor item, or -1 for a wall item.</param>
public sealed record TradeItemView(
    Id ItemId,
    ItemType Type,
    Id Id,
    int Kind,
    int Category,
    bool IsGroupable,
    ItemDataSnapshot Data,
    int CreationDay,
    int CreationMonth,
    int CreationYear,
    long Extra);

/// <summary>
/// Represents one participant's furni offer in a trade, limited to a number of items.
/// </summary>
/// <param name="UserId">The id of the user who makes the offer.</param>
/// <param name="FurniCount">The furni count the hotel reports for the offer.</param>
/// <param name="CreditCount">The credit count the hotel reports for the offer.</param>
/// <param name="TotalItems">The number of items in the offer.</param>
/// <param name="ReturnedItems">The number of items in <paramref name="Items"/>.</param>
/// <param name="Truncated">Whether <paramref name="Items"/> holds fewer items than the offer because of the item limit.</param>
/// <param name="Items">The first items of the offer, up to the requested item limit.</param>
public sealed record TradeOfferView(
    Id UserId,
    int FurniCount,
    int CreditCount,
    int TotalItems,
    int ReturnedItems,
    bool Truncated,
    IReadOnlyList<TradeItemView> Items);

/// <summary>
/// Represents an NFT asset in a trade offer or in the trade NFT inventory.
/// </summary>
/// <remarks>
/// The values are passed through as the hotel sent them.
/// </remarks>
/// <param name="AssetId">The id of the asset.</param>
/// <param name="ProductTypeId">The product type id of the asset.</param>
/// <param name="ItemTypeId">The item type id of the asset.</param>
/// <param name="Score">The score of the asset.</param>
/// <param name="PetFigureString">The pet figure string of the asset.</param>
/// <param name="FigureSetIds">The figure set ids of the asset.</param>
/// <param name="ProductCode">The product code of the asset.</param>
/// <param name="Rarity">The rarity of the asset.</param>
public sealed record TradeNftAssetView(
    long AssetId,
    short ProductTypeId,
    string ItemTypeId,
    int Score,
    string PetFigureString,
    IReadOnlyList<int> FigureSetIds,
    string ProductCode,
    string Rarity);

/// <summary>
/// Represents one side's NFT offer in a trade, limited to a number of assets.
/// </summary>
/// <param name="TotalAssets">The number of assets in the offer.</param>
/// <param name="ReturnedAssets">The number of assets in <paramref name="Assets"/>.</param>
/// <param name="Truncated">Whether <paramref name="Assets"/> holds fewer assets than the offer because of the asset limit.</param>
/// <param name="Assets">The first assets of the offer, up to the requested NFT offer limit.</param>
public sealed record TradeNftOfferView(
    int TotalAssets,
    int ReturnedAssets,
    bool Truncated,
    IReadOnlyList<TradeNftAssetView> Assets);

/// <summary>
/// Represents the open trade with its offers.
/// </summary>
/// <param name="Epoch">The trade epoch that identifies the trade.</param>
/// <param name="Phase">The phase of the trade.</param>
/// <param name="FirstParticipant">The first participant reported by the hotel when the trade opened.</param>
/// <param name="SecondParticipant">The second participant reported by the hotel when the trade opened.</param>
/// <param name="FirstOffer">The furni offer of the first participant, or <see langword="null"/> when no offers were received.</param>
/// <param name="SecondOffer">The furni offer of the second participant, or <see langword="null"/> when no offers were received.</param>
/// <param name="OwnNftOffers">The NFT assets offered by the local user, or <see langword="null"/> when no NFT offers were received.</param>
/// <param name="OtherNftOffers">The NFT assets offered by the other user, or <see langword="null"/> when no NFT offers were received.</param>
/// <param name="OwnSilver">The local user's silver amount last reported by the hotel, or 0 when none was reported.</param>
/// <param name="OtherSilver">The other user's silver amount last reported by the hotel, or 0 when none was reported.</param>
/// <param name="SilverFee">The silver fee last reported by the hotel, or 0 when none was reported.</param>
/// <param name="SilverFeeReached">
/// Whether <paramref name="OwnSilver"/> plus <paramref name="OtherSilver"/> is at least <paramref name="SilverFee"/>.
/// </param>
public sealed record TradeEpochView(
    long Epoch,
    TradePhase Phase,
    TradeParticipantView FirstParticipant,
    TradeParticipantView SecondParticipant,
    TradeOfferView? FirstOffer,
    TradeOfferView? SecondOffer,
    TradeNftOfferView? OwnNftOffers,
    TradeNftOfferView? OtherNftOffers,
    int OwnSilver,
    int OtherSilver,
    int SilverFee,
    bool SilverFeeReached);

/// <summary>
/// Represents a summary of one participant's furni offer in a trade.
/// </summary>
/// <param name="UserId">The id of the user who makes the offer.</param>
/// <param name="FurniCount">The furni count the hotel reports for the offer.</param>
/// <param name="CreditCount">The credit count the hotel reports for the offer.</param>
/// <param name="ItemCount">The number of items in the offer.</param>
public sealed record TradeOfferSummary(
    Id UserId,
    int FurniCount,
    int CreditCount,
    int ItemCount);

/// <summary>
/// Represents a summary of a trade without its item and asset lists.
/// </summary>
/// <param name="Epoch">The trade epoch that identifies the trade.</param>
/// <param name="Phase">The phase of the trade.</param>
/// <param name="FirstParticipant">The first participant reported by the hotel when the trade opened.</param>
/// <param name="SecondParticipant">The second participant reported by the hotel when the trade opened.</param>
/// <param name="FirstOffer">The furni offer summary of the first participant, or <see langword="null"/> when no offers were received.</param>
/// <param name="SecondOffer">The furni offer summary of the second participant, or <see langword="null"/> when no offers were received.</param>
/// <param name="OwnNftOfferCount">The number of NFT assets offered by the local user.</param>
/// <param name="OtherNftOfferCount">The number of NFT assets offered by the other user.</param>
/// <param name="OwnSilver">The local user's silver amount last reported by the hotel, or 0 when none was reported.</param>
/// <param name="OtherSilver">The other user's silver amount last reported by the hotel, or 0 when none was reported.</param>
/// <param name="SilverFee">The silver fee last reported by the hotel, or 0 when none was reported.</param>
/// <param name="SilverFeeReached">
/// Whether <paramref name="OwnSilver"/> plus <paramref name="OtherSilver"/> is at least <paramref name="SilverFee"/>.
/// </param>
public sealed record TradeEpochSummary(
    long Epoch,
    TradePhase Phase,
    TradeParticipantView FirstParticipant,
    TradeParticipantView SecondParticipant,
    TradeOfferSummary? FirstOffer,
    TradeOfferSummary? SecondOffer,
    int OwnNftOfferCount,
    int OtherNftOfferCount,
    int OwnSilver,
    int OtherSilver,
    int SilverFee,
    bool SilverFeeReached);

/// <summary>
/// Represents a summary of the trade NFT inventory.
/// </summary>
/// <param name="Revision">
/// The inventory revision, increased when an NFT inventory is received and when the inventory is cleared
/// for a reset or a new session.
/// </param>
/// <param name="Loaded">Whether an NFT inventory was received in the hotel session.</param>
/// <param name="TotalAssets">The number of assets in the inventory.</param>
public sealed record TradeNftInventorySummary(
    long Revision,
    bool Loaded,
    int TotalAssets);

/// <summary>
/// Represents the trade state with the open trade and its offers.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.TradeState"/>.
/// </remarks>
/// <param name="Connected">Whether the state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the state belongs to.</param>
/// <param name="RoomGeneration">The room state generation when the state was read.</param>
/// <param name="Revision">The trade state revision, increased by every committed trade change and reset.</param>
/// <param name="LatestEpoch">
/// The epoch of the latest trade, increased when a trade opens and when an open trade ends because the room changed.
/// </param>
/// <param name="Active">The open trade, or <see langword="null"/> when no trade is open.</param>
/// <param name="NftInventory">The summary of the trade NFT inventory.</param>
public sealed record TradeStateView(
    bool Connected,
    long SessionGeneration,
    long RoomGeneration,
    long Revision,
    long LatestEpoch,
    TradeEpochView? Active,
    TradeNftInventorySummary NftInventory);

/// <summary>
/// Represents a summary of the trade state without item and asset lists.
/// </summary>
/// <param name="Connected">Whether the state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the state belongs to.</param>
/// <param name="Revision">The trade state revision, increased by every committed trade change and reset.</param>
/// <param name="LatestEpoch">
/// The epoch of the latest trade, increased when a trade opens and when an open trade ends because the room changed.
/// </param>
/// <param name="Active">The summary of the open trade, or <see langword="null"/> when no trade is open.</param>
/// <param name="NftInventory">The summary of the trade NFT inventory.</param>
public sealed record TradeStateSummary(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long LatestEpoch,
    TradeEpochSummary? Active,
    TradeNftInventorySummary NftInventory);

/// <summary>
/// Represents a request to open a trade with a user in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeOpen"/>. The room must be ready, no trade may be open and
/// the index must belong to a user other than the local user. The checks are repeated right before the
/// message is sent, and the hotel's response is not awaited.
/// </remarks>
/// <param name="UserIndex">The room index of the user to trade with. Must not be negative.</param>
/// <param name="ExpectedSessionGeneration">
/// The trade session generation that must be active, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRevision">
/// The trade state revision that must still be current when the message is sent, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedEpoch">The trade epoch that must be current, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedRoomGeneration">
/// The room generation that must be current, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedUserId">
/// The id the user at <paramref name="UserIndex"/> must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record TradeOpenRequest(
    int UserIndex,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null,
    long? ExpectedRoomGeneration = null,
    Id? ExpectedUserId = null);

/// <summary>
/// Represents a request to add inventory items to the local user's trade offer.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeItemsAdd"/>. The trade must be in the
/// <see cref="TradePhase.Trading"/> phase, and the hotel's response is not awaited.
/// </remarks>
/// <param name="ItemIds">The inventory item ids to add, from 1 to 65535 distinct nonzero ids.</param>
/// <param name="ExpectedSessionGeneration">
/// The trade session generation that must be active, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRevision">
/// The trade state revision that must still be current when the message is sent, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedEpoch">The trade epoch that must be current, or <see langword="null"/> to skip the check.</param>
public sealed record TradeItemsAddRequest(
    IReadOnlyList<Id> ItemIds,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null);

/// <summary>
/// Represents a request to remove an inventory item from the local user's trade offer.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeItemRemove"/>. The trade must be in the
/// <see cref="TradePhase.Trading"/> phase, and the hotel's response is not awaited.
/// </remarks>
/// <param name="ItemId">The inventory item id to remove. Must not be 0.</param>
/// <param name="ExpectedSessionGeneration">
/// The trade session generation that must be active, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRevision">
/// The trade state revision that must still be current when the message is sent, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedEpoch">The trade epoch that must be current, or <see langword="null"/> to skip the check.</param>
public sealed record TradeItemRemoveRequest(
    Id ItemId,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null);

/// <summary>
/// Represents a request to accept, unaccept, confirm or close the open trade.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeAccept"/>, <see cref="ApplicationMemberIds.TradeUnaccept"/>,
/// <see cref="ApplicationMemberIds.TradeConfirm"/> and <see cref="ApplicationMemberIds.TradeClose"/>.
/// Accepting requires the <see cref="TradePhase.Trading"/> phase and confirming requires the
/// <see cref="TradePhase.AwaitingConfirmation"/> phase. Both also require the loaded local profile, a
/// local participant the hotel marked as able to trade and a reached silver fee, and are not sent when
/// the trade changes before dispatch. The hotel's response is not awaited.
/// </remarks>
/// <param name="ExpectedSessionGeneration">
/// The trade session generation that must be active, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRevision">
/// The trade state revision that must still be current when the message is sent, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedEpoch">The trade epoch that must be current, or <see langword="null"/> to skip the check.</param>
public sealed record TradeCommandRequest(
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null);

/// <summary>
/// Represents the result of sending a trade message to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.TradeOpen"/>, <see cref="ApplicationMemberIds.TradeItemsAdd"/>,
/// <see cref="ApplicationMemberIds.TradeItemRemove"/>, <see cref="ApplicationMemberIds.TradeAccept"/>,
/// <see cref="ApplicationMemberIds.TradeUnaccept"/>, <see cref="ApplicationMemberIds.TradeConfirm"/> and
/// <see cref="ApplicationMemberIds.TradeClose"/>. The hotel's reaction arrives through
/// <see cref="ApplicationMemberIds.TradeChanged"/>.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="SessionGeneration">The trade session generation the message was sent in.</param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="StateRevision">The trade state revision the operation was checked against.</param>
/// <param name="Epoch">The trade epoch the operation was checked against.</param>
public sealed record TradeDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long RoomGeneration,
    long StateRevision,
    long Epoch);

/// <summary>
/// Represents a request to read a page of the trade NFT inventory from a snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeNftInventoryList"/>. Up to 16 snapshots are retained, and a
/// retained snapshot becomes unavailable when the hotel session changes.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first asset to return.</param>
/// <param name="Limit">The maximum number of assets to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read, or <see langword="null"/> to capture the current inventory.
/// Required when <paramref name="Offset"/> is greater than 0.
/// </param>
public sealed record TradeNftInventoryPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a request to fetch the trade NFT inventory from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.TradeNftInventoryRefresh"/>. The refresh sends the inventory
/// request and completes when a new inventory matching the response is stored.
/// </remarks>
/// <param name="Limit">The maximum number of assets in the returned first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record TradeNftInventoryRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a page of the trade NFT inventory read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.TradeNftInventoryList"/> and
/// <see cref="ApplicationMemberIds.TradeNftInventoryRefresh"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="StateRevision">The trade state revision of the snapshot.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, passed back to read the next page.</param>
/// <param name="InventoryRevision">The NFT inventory revision of the snapshot.</param>
/// <param name="Loaded">Whether an NFT inventory was received in the hotel session.</param>
/// <param name="TotalAssets">The number of assets in the inventory.</param>
/// <param name="Offset">The zero-based offset of the first asset in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Assets">The assets in the page.</param>
public sealed record TradeNftInventoryPage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long SnapshotRevision,
    long InventoryRevision,
    bool Loaded,
    int TotalAssets,
    int Offset,
    int? NextOffset,
    IReadOnlyList<TradeNftAssetView> Assets);

/// <summary>
/// Specifies the kind of change reported by <see cref="TradeChanged"/>.
/// </summary>
public enum TradeChangeKind
{
    /// <summary>A trade was opened.</summary>
    Opened,
    /// <summary>The furni offers changed and both acceptances were cleared.</summary>
    OffersUpdated,
    /// <summary>A participant accepted the offers or withdrew acceptance.</summary>
    AcceptanceUpdated,
    /// <summary>The hotel asked both participants to confirm the trade.</summary>
    Confirmation,
    /// <summary>The trade was completed.</summary>
    Completed,
    /// <summary>A participant closed the trade.</summary>
    Closed,
    /// <summary>The hotel refused to open a trade.</summary>
    OpenFailed,
    /// <summary>The NFT offers changed and both acceptances were cleared.</summary>
    NftOffersUpdated,
    /// <summary>The silver amounts of the trade changed.</summary>
    SilverUpdated,
    /// <summary>The silver fee of the trade changed.</summary>
    SilverFeeUpdated,
    /// <summary>A trade NFT inventory was received.</summary>
    NftInventoryUpdated,
    /// <summary>The open trade ended because the local user entered or left a room.</summary>
    RoomChanged,
    /// <summary>The trade state was cleared because the manager was reset or a new hotel session connected.</summary>
    Reset
}

/// <summary>
/// Represents an acceptance change of a trade participant.
/// </summary>
/// <param name="UserId">The id of the participant.</param>
/// <param name="Accepted">Whether the participant accepted the offers instead of withdrawing acceptance.</param>
public sealed record TradeAcceptanceChange(Id UserId, bool Accepted);

/// <summary>
/// Represents the closing of a trade by a participant.
/// </summary>
/// <param name="UserId">The id of the participant who closed the trade.</param>
/// <param name="Reason">The close reason code sent by the hotel.</param>
public sealed record TradeCloseResult(Id UserId, int Reason);

/// <summary>
/// Represents a trade request the hotel refused.
/// </summary>
/// <param name="Reason">The failure reason code sent by the hotel.</param>
/// <param name="OtherUserName">The name of the other user sent by the hotel.</param>
public sealed record TradeOpenFailure(int Reason, string OtherUserName);

/// <summary>
/// Represents a change of the trade state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.TradeChanged"/>. Offer and NFT inventory lists are not
/// included; read them with <see cref="ApplicationMemberIds.TradeState"/> and
/// <see cref="ApplicationMemberIds.TradeNftInventoryList"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="State">The summary of the trade state after the change.</param>
/// <param name="PreviousEpoch">
/// The summary of the trade that the change ended or replaced, or <see langword="null"/> when none.
/// </param>
/// <param name="Acceptance">
/// The acceptance change for <see cref="TradeChangeKind.AcceptanceUpdated"/>; otherwise, <see langword="null"/>.
/// </param>
/// <param name="Close">The close details for <see cref="TradeChangeKind.Closed"/>; otherwise, <see langword="null"/>.</param>
/// <param name="OpenFailure">
/// The failure details for <see cref="TradeChangeKind.OpenFailed"/>; otherwise, <see langword="null"/>.
/// </param>
public sealed record TradeChanged(
    TradeChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    TradeStateSummary State,
    TradeEpochSummary? PreviousEpoch,
    TradeAcceptanceChange? Acceptance,
    TradeCloseResult? Close,
    TradeOpenFailure? OpenFailure);
