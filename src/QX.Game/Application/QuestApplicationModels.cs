using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>
/// Specifies which quest list a quest entry page reads.
/// </summary>
public enum QuestCollection
{
    /// <summary>The quests from the last available quest list.</summary>
    Available,
    /// <summary>The quests from the last seasonal quest list.</summary>
    Seasonal,
    /// <summary>The available quests followed by the seasonal quests.</summary>
    Combined
}

/// <summary>
/// Represents a request to read the quest state from a snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsState"/>. Up to four snapshots are retained, and a
/// retained snapshot becomes unavailable when the hotel session changes.
/// </remarks>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.
/// </param>
public sealed record QuestStateRequest(long? SnapshotRevision = null);

/// <summary>
/// Represents a summary of the stored quest state.
/// </summary>
/// <param name="AvailableLoaded">Whether an available quest list was received.</param>
/// <param name="SeasonalLoaded">Whether a seasonal quest list was received.</param>
/// <param name="DailyLoaded">Whether a daily quest message was received.</param>
/// <param name="OpenWindow">Whether the last available quest list asked the client to open the quest window.</param>
/// <param name="AvailableCount">The number of quests in the last available quest list.</param>
/// <param name="SeasonalCount">The number of quests in the last seasonal quest list.</param>
/// <param name="HasCurrent">Whether a quest update was received.</param>
/// <param name="HasCompletion">Whether a quest completion was received.</param>
/// <param name="HasCancellation">Whether a quest cancellation was received.</param>
/// <param name="HasDailyQuest">Whether the last daily quest message holds a quest.</param>
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

/// <summary>
/// Represents a quest with its campaign, progress and reward.
/// </summary>
/// <param name="CampaignCode">The code of the campaign the quest belongs to.</param>
/// <param name="CompletedQuestsInCampaign">The number of quests completed in the campaign.</param>
/// <param name="QuestCountInCampaign">The number of quests in the campaign.</param>
/// <param name="ActivityPointType">The activity point type the reward is paid in, 0 for duckets.</param>
/// <param name="Id">The quest id, less than 1 when the campaign is completed.</param>
/// <param name="IsAccepted">Whether the local user has accepted the quest.</param>
/// <param name="Type">The quest type code.</param>
/// <param name="ImageVersion">The image version of the quest.</param>
/// <param name="RewardCurrencyAmount">The amount of activity points the quest rewards.</param>
/// <param name="LocalizationCode">The localization code of the quest texts.</param>
/// <param name="CompletedSteps">The number of steps completed.</param>
/// <param name="TotalSteps">The number of steps the quest has.</param>
/// <param name="SortOrder">The sort order of the quest.</param>
/// <param name="CatalogPageName">The name of the catalog page linked to the quest.</param>
/// <param name="ChainCode">The code of the quest chain.</param>
/// <param name="IsEasy">Whether the quest is an easy quest.</param>
/// <param name="IsSeasonal">Whether the quest belongs to a seasonal campaign.</param>
/// <param name="SeasonalSecondsLeft">
/// The number of seconds left before the seasonal campaign closes, or <see langword="null"/> for a quest
/// that is not seasonal.
/// </param>
/// <param name="IsCompleted">
/// Whether every step is completed, which is when <paramref name="CompletedSteps"/> equals
/// <paramref name="TotalSteps"/>.
/// </param>
/// <param name="IsCampaignCompleted">Whether the whole campaign is completed, which is when <paramref name="Id"/> is less than 1.</param>
/// <param name="IsLastQuestInCampaign">
/// Whether the quest is the last one of its campaign, which is when <paramref name="CompletedQuestsInCampaign"/>
/// reaches <paramref name="QuestCountInCampaign"/>.
/// </param>
/// <param name="CampaignChainCode">
/// The campaign code, followed by <c>.</c> and <paramref name="ChainCode"/> for seasonal quests.
/// </param>
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

/// <summary>
/// Represents a quest completion reported by the hotel.
/// </summary>
/// <param name="Quest">The completed quest.</param>
/// <param name="ShowDialog">Whether the hotel asked the client to show the completion dialog.</param>
public sealed record QuestCompletionView(
    QuestView Quest,
    bool ShowDialog);

/// <summary>
/// Represents a quest cancellation reported by the hotel.
/// </summary>
/// <param name="IsExpired">Whether the quest ended because it expired rather than by request.</param>
/// <param name="Quest">The canceled quest.</param>
public sealed record QuestCancellationView(
    bool IsExpired,
    QuestView Quest);

/// <summary>
/// Represents the daily quest offer received from the hotel.
/// </summary>
/// <param name="Quest">The daily quest, or <see langword="null"/> when the message holds no quest.</param>
/// <param name="EasyQuestCount">The number of quests in the easy daily pool.</param>
/// <param name="HardQuestCount">The number of quests in the hard daily pool.</param>
/// <param name="HasQuest">Whether the message holds a quest.</param>
public sealed record QuestDailyView(
    QuestView? Quest,
    int EasyQuestCount,
    int HardQuestCount,
    bool HasQuest);

/// <summary>
/// Represents the quest state read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsState"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="Revision">The quest state revision, increased when a quest message is stored or the state resets.</param>
/// <param name="AvailableRevision">The revision increased when an available quest list is stored or the state resets.</param>
/// <param name="SeasonalRevision">The revision increased when a seasonal quest list is stored or the state resets.</param>
/// <param name="CurrentRevision">The revision increased when a quest update is stored or the state resets.</param>
/// <param name="CompletionRevision">The revision increased when a quest completion is stored or the state resets.</param>
/// <param name="CancellationRevision">The revision increased when a quest cancellation is stored or the state resets.</param>
/// <param name="DailyRevision">The revision increased when a daily quest offer is stored or the state resets.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, used to read the same snapshot again.</param>
/// <param name="Summary">The summary of the stored quest state.</param>
/// <param name="Current">The quest from the last quest update, or <see langword="null"/> when none was received.</param>
/// <param name="LastCompletion">The last quest completion, or <see langword="null"/> when none was received.</param>
/// <param name="LastCancellation">The last quest cancellation, or <see langword="null"/> when none was received.</param>
/// <param name="Daily">The last daily quest offer, or <see langword="null"/> when none was received.</param>
public sealed record QuestStateView(
    bool Connected,
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

/// <summary>
/// Represents one quest of a quest entry page.
/// </summary>
/// <param name="Ordinal">The zero-based position of the entry in the collection that was read.</param>
/// <param name="Collection">
/// The quest list the quest comes from, <see cref="QuestCollection.Available"/> or
/// <see cref="QuestCollection.Seasonal"/>.
/// </param>
/// <param name="CollectionOrdinal">The zero-based position of the quest in its own quest list.</param>
/// <param name="Quest">The quest.</param>
public sealed record QuestEntryView(
    int Ordinal,
    QuestCollection Collection,
    int CollectionOrdinal,
    QuestView Quest);

/// <summary>
/// Represents a request to read a page of quests from a snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsEntriesList"/>. Quests are returned in the order the
/// hotel sent them.
/// </remarks>
/// <param name="Collection">The quest list to read.</param>
/// <param name="Offset">The zero-based offset of the first quest to return.</param>
/// <param name="Limit">The maximum number of quests to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.
/// Required when <paramref name="Offset"/> is greater than 0.
/// </param>
public sealed record QuestEntryPageRequest(
    QuestCollection Collection = QuestCollection.Available,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a page of quests read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsEntriesList"/> and included in the results of
/// <see cref="ApplicationMemberIds.QuestsAvailableRefresh"/> and <see cref="ApplicationMemberIds.QuestsSeasonalRefresh"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="StateRevision">The quest state revision of the snapshot.</param>
/// <param name="AvailableRevision">The revision increased when an available quest list is stored or the state resets.</param>
/// <param name="SeasonalRevision">The revision increased when a seasonal quest list is stored or the state resets.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, passed back to read the next page.</param>
/// <param name="Summary">The summary of the stored quest state.</param>
/// <param name="Collection">The quest list that was read.</param>
/// <param name="Total">The number of quests in the collection.</param>
/// <param name="Offset">The zero-based offset of the first quest in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Entries">The quests in the page.</param>
public sealed record QuestEntryPage(
    bool Connected,
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

/// <summary>
/// Represents a request to fetch the available quest list from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsAvailableRefresh"/>. Concurrent refreshes share one
/// request, which is sent once earlier requests for the list are answered. The refresh completes with
/// the first list stored after that request, since the response carries no request id. A timeout ends
/// the wait for this caller only and does not cancel a request already sent.
/// </remarks>
/// <param name="Limit">The maximum number of quests in the returned first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the list in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record QuestAvailableRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the result of refreshing the available quest list.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsAvailableRefresh"/>.
/// </remarks>
/// <param name="RefreshedAtUtc">The time the result was created.</param>
/// <param name="ObservedAtUtc">The time the matching quest list was stored.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The quest state revision after the list was stored.</param>
/// <param name="AvailableRevision">The available list revision after the list was stored.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the first page was read from.</param>
/// <param name="MessagesDispatched">
/// The number of request messages credited to this call, 1 for the call that sent the shared request
/// and 0 for calls that joined it.
/// </param>
/// <param name="FirstPage">The first page of the stored available quest list.</param>
public sealed record QuestAvailableRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long AvailableRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    QuestEntryPage FirstPage);

/// <summary>
/// Represents a request to fetch the seasonal quest list from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsSeasonalRefresh"/>. Concurrent refreshes share one
/// request, which is sent once earlier requests for the list are answered. The refresh completes with
/// the first list stored after that request, since the response carries no request id. A timeout ends
/// the wait for this caller only and does not cancel a request already sent.
/// </remarks>
/// <param name="Limit">The maximum number of quests in the returned first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the list in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record QuestSeasonalRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the result of refreshing the seasonal quest list.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsSeasonalRefresh"/>.
/// </remarks>
/// <param name="RefreshedAtUtc">The time the result was created.</param>
/// <param name="ObservedAtUtc">The time the matching quest list was stored.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The quest state revision after the list was stored.</param>
/// <param name="SeasonalRevision">The seasonal list revision after the list was stored.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the first page was read from.</param>
/// <param name="MessagesDispatched">
/// The number of request messages credited to this call, 1 for the call that sent the shared request
/// and 0 for calls that joined it.
/// </param>
/// <param name="FirstPage">The first page of the stored seasonal quest list.</param>
public sealed record QuestSeasonalRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long SeasonalRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    QuestEntryPage FirstPage);

/// <summary>
/// Represents a request to fetch a daily quest from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsDailyRefresh"/>. The response carries no request id,
/// so daily requests are sent one at a time in the order they were made, and identical requests for the
/// same pool and index share one request. A timeout ends the wait for this caller only and does not
/// cancel a request already sent.
/// </remarks>
/// <param name="IsEasy">Whether to request a quest from the easy pool instead of the hard pool.</param>
/// <param name="Index">The index of the quest within the selected pool.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the daily quest in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record QuestDailyRefreshRequest(
    bool IsEasy,
    int Index,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the result of refreshing a daily quest.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsDailyRefresh"/>.
/// </remarks>
/// <param name="RefreshedAtUtc">The time the result was created.</param>
/// <param name="ObservedAtUtc">The time the matching daily quest offer was stored.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The quest state revision after the offer was stored.</param>
/// <param name="DailyRevision">The daily quest revision after the offer was stored.</param>
/// <param name="SnapshotRevision">The revision of the snapshot retained after the offer was stored.</param>
/// <param name="MessagesDispatched">
/// The number of request messages credited to this call, 1 for the call that sent the shared request
/// and 0 for calls that joined it.
/// </param>
/// <param name="Daily">The daily quest offer that was received.</param>
public sealed record QuestDailyRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long DailyRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    QuestDailyView Daily);

/// <summary>
/// Represents a request to accept, activate or reject a quest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsAccept"/>, <see cref="ApplicationMemberIds.QuestsActivate"/>
/// and <see cref="ApplicationMemberIds.QuestsReject"/>. One message is sent, and the call returns without
/// waiting for the hotel's answer.
/// </remarks>
/// <param name="QuestId">The id of the quest, taken from a quest list entry.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the message must be sent in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record QuestSelectionActionRequest(
    long QuestId,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the receipt of a sent quest accept, activate or reject message.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsAccept"/>, <see cref="ApplicationMemberIds.QuestsActivate"/>
/// and <see cref="ApplicationMemberIds.QuestsReject"/>.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the message was sent in.</param>
/// <param name="QuestId">The id of the quest the message named.</param>
/// <param name="MessagesDispatched">The number of messages sent, which is always 1.</param>
public sealed record QuestSelectionDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long QuestId,
    int MessagesDispatched);

/// <summary>
/// Represents a request to send a quest message that takes no arguments.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.QuestsCancel"/>, <see cref="ApplicationMemberIds.QuestsTrackerOpen"/>
/// and <see cref="ApplicationMemberIds.QuestsFriendRequestComplete"/>. One message is sent, and the call
/// returns without waiting for the hotel's answer.
/// </remarks>
/// <param name="ExpectedSessionGeneration">
/// The session generation the message must be sent in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record QuestDispatchRequest(
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the receipt of a sent quest message that takes no arguments.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.QuestsCancel"/>, <see cref="ApplicationMemberIds.QuestsTrackerOpen"/>
/// and <see cref="ApplicationMemberIds.QuestsFriendRequestComplete"/>.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the message was sent in.</param>
/// <param name="MessagesDispatched">The number of messages sent, which is always 1.</param>
public sealed record QuestDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    int MessagesDispatched);

/// <summary>
/// Specifies the kind of change reported by <see cref="QuestChanged"/>.
/// </summary>
public enum QuestChangeKind
{
    /// <summary>An available quest list was received and stored.</summary>
    Available,
    /// <summary>A seasonal quest list was received and stored.</summary>
    Seasonal,
    /// <summary>A quest update was received and stored as the current quest.</summary>
    Current,
    /// <summary>A quest completion was received and stored.</summary>
    Completed,
    /// <summary>A quest cancellation was received and stored.</summary>
    Cancelled,
    /// <summary>A daily quest offer was received and stored.</summary>
    Daily,
    /// <summary>The quest state was cleared by a manager reset or a new hotel session.</summary>
    Reset
}

/// <summary>
/// Represents a change of the quest state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.QuestsChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The state generation of the hotel session.</param>
/// <param name="Revision">The quest state revision after the change.</param>
/// <param name="SourceRevision">
/// The revision of the part that changed, such as the available list revision for an
/// <see cref="QuestChangeKind.Available"/> change, or the state revision for a <see cref="QuestChangeKind.Reset"/>.
/// </param>
/// <param name="SnapshotRevision">
/// The revision of the snapshot retained for the change, or <see langword="null"/> when none could be stored.
/// </param>
/// <param name="Summary">
/// The summary of the quest state for <see cref="QuestChangeKind.Available"/>, <see cref="QuestChangeKind.Seasonal"/>
/// and <see cref="QuestChangeKind.Reset"/> changes; otherwise, <see langword="null"/>.
/// </param>
/// <param name="Quest">
/// The quest of a <see cref="QuestChangeKind.Current"/>, <see cref="QuestChangeKind.Completed"/> or
/// <see cref="QuestChangeKind.Cancelled"/> change; otherwise, <see langword="null"/>.
/// </param>
/// <param name="ShowDialog">
/// Whether the hotel asked the client to show the completion dialog for a <see cref="QuestChangeKind.Completed"/>
/// change; otherwise, <see langword="null"/>.
/// </param>
/// <param name="IsExpired">
/// Whether the quest expired for a <see cref="QuestChangeKind.Cancelled"/> change; otherwise, <see langword="null"/>.
/// </param>
/// <param name="Daily">
/// The daily quest offer of a <see cref="QuestChangeKind.Daily"/> change; otherwise, <see langword="null"/>.
/// </param>
public sealed record QuestChanged(
    QuestChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
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
