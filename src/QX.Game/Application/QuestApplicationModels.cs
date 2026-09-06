using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Quests;

namespace Qx.Game.Application;

public enum QuestCollection
{
    Available,
    Seasonal,
    Combined
}

public sealed record QuestStateRequest(long? SnapshotRevision = null);

public sealed record QuestSummary(
    bool AvailableLoaded,
    bool SeasonalLoaded,
    bool DailyLoaded,
    bool OpenWindow,
    int AvailableCount,
    int SeasonalCount,
    bool HasCurrent,
    bool HasCompletion,
    bool HasCancellation,
    bool HasDailyQuest);

public sealed record QuestView(
    string CampaignCode,
    int CompletedQuestsInCampaign,
    int QuestCountInCampaign,
    int ActivityPointType,
    int Id,
    bool IsAccepted,
    string Type,
    string ImageVersion,
    int RewardCurrencyAmount,
    string LocalizationCode,
    int CompletedSteps,
    int TotalSteps,
    int SortOrder,
    string CatalogPageName,
    string ChainCode,
    bool IsEasy,
    bool IsSeasonal,
    int? SeasonalSecondsLeft,
    bool IsCompleted,
    bool IsCampaignCompleted,
    bool IsLastQuestInCampaign,
    string CampaignChainCode);

public sealed record QuestCompletionView(
    QuestView Quest,
    bool ShowDialog);

public sealed record QuestCancellationView(
    bool IsExpired,
    QuestView Quest);

public sealed record QuestDailyView(
    QuestView? Quest,
    int EasyQuestCount,
    int HardQuestCount,
    bool HasQuest);

public sealed record QuestStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long AvailableRevision,
    long SeasonalRevision,
    long CurrentRevision,
    long CompletionRevision,
    long CancellationRevision,
    long DailyRevision,
    long SnapshotRevision,
    QuestSummary Summary,
    QuestView? Current,
    QuestCompletionView? LastCompletion,
    QuestCancellationView? LastCancellation,
    QuestDailyView? Daily);

public sealed record QuestEntryView(
    int Ordinal,
    QuestCollection Collection,
    int CollectionOrdinal,
    QuestView Quest);

public sealed record QuestEntryPageRequest(
    QuestCollection Collection = QuestCollection.Available,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record QuestEntryPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long AvailableRevision,
    long SeasonalRevision,
    long SnapshotRevision,
    QuestSummary Summary,
    QuestCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<QuestEntryView> Entries);

public sealed record QuestAvailableRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record QuestAvailableRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long AvailableRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    QuestEntryPage FirstPage);

public sealed record QuestSeasonalRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record QuestSeasonalRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long SeasonalRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    QuestEntryPage FirstPage);

public sealed record QuestDailyRefreshRequest(
    bool IsEasy,
    int Index,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record QuestDailyRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long DailyRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    QuestDailyView Daily);

public sealed record QuestSelectionActionRequest(
    long QuestId,
    long? ExpectedSessionGeneration = null);

public sealed record QuestSelectionDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long QuestId,
    int MessagesDispatched);

public sealed record QuestDispatchRequest(
    long? ExpectedSessionGeneration = null);

public sealed record QuestDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    int MessagesDispatched);

public enum QuestChangeKind
{
    Available,
    Seasonal,
    Current,
    Completed,
    Cancelled,
    Daily,
    Reset
}

public sealed record QuestChanged(
    QuestChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    QuestSummary? Summary,
    QuestView? Quest,
    bool? ShowDialog,
    bool? IsExpired,
    QuestDailyView? Daily);

internal interface IQuestOperations
{
    void RequestAvailable();
    Task<IReadOnlyList<QuestData>> EnsureAvailableLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
    void RequestSeasonal();
    void RequestDaily(bool is_easy, int index);
    void Accept(Id quest_id);
    void Activate(Id quest_id);
    void Reject(Id quest_id);
    void Cancel();
    void OpenTracker();
    void CompleteFriendRequestQuest();
}
