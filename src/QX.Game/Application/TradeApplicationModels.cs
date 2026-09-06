using Qx.Game.Snapshots;
using Qx.Model;

namespace Qx.Game.Application;

public sealed record TradeStateRequest(
    int OfferItemLimit = 100,
    int NftOfferLimit = 100);

public sealed record TradeParticipantView(
    Id UserId,
    bool CanTrade,
    bool Accepted);

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

public sealed record TradeOfferView(
    Id UserId,
    int FurniCount,
    int CreditCount,
    int TotalItems,
    int ReturnedItems,
    bool Truncated,
    IReadOnlyList<TradeItemView> Items);

public sealed record TradeNftAssetView(
    long AssetId,
    short ProductTypeId,
    string ItemTypeId,
    int Score,
    string PetFigureString,
    IReadOnlyList<int> FigureSetIds,
    string ProductCode,
    string Rarity);

public sealed record TradeNftOfferView(
    int TotalAssets,
    int ReturnedAssets,
    bool Truncated,
    IReadOnlyList<TradeNftAssetView> Assets);

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

public sealed record TradeOfferSummary(
    Id UserId,
    int FurniCount,
    int CreditCount,
    int ItemCount);

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

public sealed record TradeNftInventorySummary(
    long Revision,
    bool Loaded,
    int TotalAssets);

public sealed record TradeStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long RoomGeneration,
    long Revision,
    long LatestEpoch,
    TradeEpochView? Active,
    TradeNftInventorySummary NftInventory);

public sealed record TradeStateSummary(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long LatestEpoch,
    TradeEpochSummary? Active,
    TradeNftInventorySummary NftInventory);

public sealed record TradeOpenRequest(
    int UserIndex,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null,
    long? ExpectedRoomGeneration = null,
    Id? ExpectedUserId = null);

public sealed record TradeItemsAddRequest(
    IReadOnlyList<Id> ItemIds,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null);

public sealed record TradeItemRemoveRequest(
    Id ItemId,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null);

public sealed record TradeCommandRequest(
    long? ExpectedSessionGeneration = null,
    long? ExpectedRevision = null,
    long? ExpectedEpoch = null);

public sealed record TradeDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long RoomGeneration,
    long StateRevision,
    long Epoch);

public sealed record TradeNftInventoryPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record TradeNftInventoryRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000);

public sealed record TradeNftInventoryPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long SnapshotRevision,
    long InventoryRevision,
    bool Loaded,
    int TotalAssets,
    int Offset,
    int? NextOffset,
    IReadOnlyList<TradeNftAssetView> Assets);

public enum TradeChangeKind
{
    Opened,
    OffersUpdated,
    AcceptanceUpdated,
    Confirmation,
    Completed,
    Closed,
    OpenFailed,
    NftOffersUpdated,
    SilverUpdated,
    SilverFeeUpdated,
    NftInventoryUpdated,
    RoomChanged,
    Reset
}

public sealed record TradeAcceptanceChange(Id UserId, bool Accepted);

public sealed record TradeCloseResult(Id UserId, int Reason);

public sealed record TradeOpenFailure(int Reason, string OtherUserName);

public sealed record TradeChanged(
    TradeChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    TradeStateSummary State,
    TradeEpochSummary? PreviousEpoch,
    TradeAcceptanceChange? Acceptance,
    TradeCloseResult? Close,
    TradeOpenFailure? OpenFailure);
