using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Model.Quests;

namespace Qx.Scripting;

/// <content>
/// Quests and campaigns: cached quest state plus the fire-and-forget requests and actions that
/// drive it. Available on both the Flash and the Unity client.
/// <para>
/// Nothing here blocks or returns a value. Each request sends one message and returns
/// immediately; the answer surfaces later through the cached state and the quest events. To wait
/// for a specific reply, subscribe first and then send the request.
/// </para>
/// <para>
/// Every cached value is a copy taken under the tracker's lock, so a list read here never changes
/// while it is being enumerated. All of it is cleared when the session resets.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// The quests offered in the regular quest window, as of the last quest list the server sent.
    /// Empty until a quest list has arrived.
    /// </summary>
    /// <returns>A snapshot copy, not a live view.</returns>
    public IReadOnlyList<QuestData> AvailableQuests => Quests.Available;

    /// <summary>
    /// The quests of the current seasonal campaign, as of the last seasonal list the server sent.
    /// Empty until a seasonal list has arrived. Seasonal entries also carry the seconds left
    /// before the campaign closes.
    /// </summary>
    /// <returns>A snapshot copy, not a live view.</returns>
    public IReadOnlyList<QuestData> SeasonalQuests => Quests.Seasonal;

    /// <summary>
    /// The quest the local user is currently working on, or <see langword="null"/> until the
    /// server has pushed one. Progress is readable from its completed and total step counts.
    /// </summary>
    public QuestData? CurrentQuest => Quests.Current;

    /// <summary>
    /// The daily quest offer: the quest itself when one is active, plus how many easy and hard
    /// daily quests exist. <see langword="null"/> until a daily quest message has arrived.
    /// </summary>
    public QuestDaily? DailyQuest => Quests.Daily;

    /// <summary>
    /// The most recent quest completion the server announced, carrying the finished quest and
    /// whether the client was told to show the reward dialog. <see langword="null"/> when no quest
    /// has completed during this session.
    /// </summary>
    public QuestCompleted? LastCompletedQuest => Quests.LastCompletion;

    /// <summary>
    /// The most recent quest cancellation the server announced, carrying the quest and whether it
    /// ended because it expired rather than by request. <see langword="null"/> when no quest has
    /// been cancelled during this session.
    /// </summary>
    public QuestCancelled? LastCancelledQuest => Quests.LastCancellation;

    /// <summary>
    /// Asks for the regular quest list. Returns immediately; the list lands in the available-quest
    /// state and raises the quests-updated event.
    /// </summary>
    public void RequestQuests() => Quests.RequestAvailable();

    /// <summary>
    /// Asks for the seasonal campaign's quest list. Returns immediately; the list lands in the
    /// seasonal-quest state and raises the seasonal-quests-updated event.
    /// </summary>
    public void RequestSeasonalQuests() => Quests.RequestSeasonal();

    /// <summary>
    /// Asks for a daily quest. Returns immediately; the answer lands in the daily-quest state and
    /// raises the daily-quest event.
    /// </summary>
    /// <param name="is_easy">
    /// Whether to pick from the easy pool (<see langword="true"/>) or the hard pool
    /// (<see langword="false"/>).
    /// </param>
    /// <param name="index">
    /// Which entry of that pool to fetch, zero-based. The two pool sizes are reported by the
    /// daily quest state.
    /// </param>
    public void RequestDailyQuest(bool is_easy, int index) =>
        Quests.RequestDaily(is_easy, index);

    /// <param name="quest_id">The quest id taken from a quest list entry.</param>
    public void AcceptQuest(Id quest_id) => Quests.Accept(quest_id);

    /// <param name="quest_id">The quest id taken from a quest list entry.</param>
    public void ActivateQuest(Id quest_id) => Quests.Activate(quest_id);

    /// <summary>Declines a quest offer without accepting it. Returns immediately.</summary>
    /// <param name="quest_id">The quest id taken from a quest list entry.</param>
    public void RejectQuest(Id quest_id) => Quests.Reject(quest_id);

    public void CancelQuest() => Quests.Cancel();

    /// <summary>
    /// Tells the server the quest tracker was opened. This is the game client's own housekeeping
    /// message; its effect is that the server re-sends the current quest.
    /// </summary>
    public void OpenQuestTracker() => Quests.OpenTracker();

    /// <summary>
    /// Reports the "send a friend request" quest step as done. The game client sends this after
    /// the user completes that step in its own UI; the server still validates the claim.
    /// </summary>
    public void CompleteFriendRequestQuest() =>
        Quests.CompleteFriendRequestQuest();

    /// <summary>
    /// Returns the available quests, asking the hotel for them when this session never saw them.
    /// </summary>
    /// <remarks>
    /// The quest list arrives when the client opens its quest window, so QX attached to a session
    /// already in progress may never have received it. <see cref="AvailableQuests"/> then reads
    /// empty, which is indistinguishable from having no quests. Use this when the answer has to be
    /// right rather than merely available.
    /// </remarks>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    /// <exception cref="TimeoutException">The hotel did not answer in time.</exception>
    public async Task<IReadOnlyList<QuestData>> GetQuests(int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        QuestStateView state = await Application
            .InvokeAsync<QuestStateRequest, QuestStateView>(
                ApplicationMemberIds.QuestsState,
                new QuestStateRequest(),
                Ct)
            .ConfigureAwait(false);
        ValidateQuestState(state);

        QuestEntryPage first_page;
        if (state.Summary.AvailableLoaded)
        {
            first_page = await Application
                .InvokeAsync<QuestEntryPageRequest, QuestEntryPage>(
                    ApplicationMemberIds.QuestsEntriesList,
                    new QuestEntryPageRequest(
                        QuestCollection.Available,
                        Limit: 500,
                        SnapshotRevision: state.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateQuestStatePage(state, first_page);
        }
        else
        {
            QuestAvailableRefreshResult refreshed = await Application
                .InvokeAsync<QuestAvailableRefreshRequest, QuestAvailableRefreshResult>(
                    ApplicationMemberIds.QuestsAvailableRefresh,
                    new QuestAvailableRefreshRequest(
                        Limit: 500,
                        TimeoutMilliseconds: timeoutMs,
                        ExpectedSessionGeneration: state.SessionGeneration),
                    Ct)
                .ConfigureAwait(false);
            ValidateQuestRefresh(refreshed, state.SessionGeneration);
            first_page = refreshed.FirstPage;
        }

        QuestEntryPage page = first_page;
        ValidateQuestPage(first_page, page, QuestCollection.Available, 0);
        var quests = new List<QuestData>(page.Total);
        AddQuests(page, quests);
        while (page.NextOffset is int offset)
        {
            page = await Application
                .InvokeAsync<QuestEntryPageRequest, QuestEntryPage>(
                    ApplicationMemberIds.QuestsEntriesList,
                    new QuestEntryPageRequest(
                        QuestCollection.Available,
                        offset,
                        500,
                        first_page.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateQuestPage(first_page, page, QuestCollection.Available, offset);
            AddQuests(page, quests);
        }

        if (quests.Count != first_page.Total)
            throw new InvalidOperationException("The quest application returned an incomplete list.");
        return Array.AsReadOnly(quests.ToArray());
    }

    private static void ValidateQuestState(QuestStateView state)
    {
        if (!state.Connected ||
            state.Client is null ||
            state.SessionGeneration <= 0 ||
            state.SnapshotRevision <= 0 ||
            state.Summary is null ||
            state.Summary.AvailableCount < 0 ||
            state.Summary.SeasonalCount < 0)
        {
            throw new InvalidOperationException(
                "The quest application returned an invalid state snapshot.");
        }
    }

    private static void ValidateQuestStatePage(QuestStateView state, QuestEntryPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Connected != state.Connected ||
            page.Client != state.Client ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.AvailableRevision != state.AvailableRevision ||
            page.SeasonalRevision != state.SeasonalRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Summary != state.Summary)
        {
            throw new InvalidOperationException(
                "The quest application returned a page from another state snapshot.");
        }
    }

    private static void ValidateQuestRefresh(
        QuestAvailableRefreshResult refreshed,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(refreshed);
        QuestEntryPage page = refreshed.FirstPage;
        ArgumentNullException.ThrowIfNull(page);
        if (refreshed.SnapshotRevision <= 0 ||
            refreshed.MessagesDispatched is < 0 or > 1 ||
            refreshed.SessionGeneration != expected_session_generation ||
            !page.Connected ||
            page.Client != refreshed.Client ||
            page.SessionGeneration != refreshed.SessionGeneration ||
            page.StateRevision != refreshed.StateRevision ||
            page.AvailableRevision != refreshed.AvailableRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision ||
            page.Collection is not QuestCollection.Available ||
            page.Summary is null ||
            !page.Summary.AvailableLoaded)
        {
            throw new InvalidOperationException(
                "The quest application returned an invalid refresh result.");
        }
    }

    private static void ValidateQuestPage(
        QuestEntryPage first_page,
        QuestEntryPage page,
        QuestCollection collection,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(first_page);
        ArgumentNullException.ThrowIfNull(page);
        int consumed = checked(offset + page.Entries.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        int expected_total = collection switch
        {
            QuestCollection.Available => page.Summary.AvailableCount,
            QuestCollection.Seasonal => page.Summary.SeasonalCount,
            QuestCollection.Combined => checked(
                page.Summary.AvailableCount + page.Summary.SeasonalCount),
            _ => throw new ArgumentOutOfRangeException(nameof(collection))
        };
        if (first_page.SnapshotRevision <= 0 ||
            !first_page.Connected ||
            first_page.Client is null ||
            page.Connected != first_page.Connected ||
            page.Client != first_page.Client ||
            page.SessionGeneration != first_page.SessionGeneration ||
            page.StateRevision != first_page.StateRevision ||
            page.AvailableRevision != first_page.AvailableRevision ||
            page.SeasonalRevision != first_page.SeasonalRevision ||
            page.SnapshotRevision != first_page.SnapshotRevision ||
            page.Summary is null ||
            page.Summary != first_page.Summary ||
            page.Collection != collection ||
            page.Total < 0 ||
            page.Total != first_page.Total ||
            page.Total != expected_total ||
            page.Offset != offset ||
            page.Entries.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Entries.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException(
                "The quest application returned an invalid snapshot page.");
        }
    }

    private static void AddQuests(QuestEntryPage page, List<QuestData> quests)
    {
        for (int index = 0; index < page.Entries.Count; index++)
        {
            QuestEntryView entry = page.Entries[index];
            int ordinal = checked(page.Offset + index);
            if (entry.Ordinal != ordinal ||
                entry.Collection is not QuestCollection.Available ||
                entry.CollectionOrdinal != ordinal)
            {
                throw new InvalidOperationException(
                    "The quest application returned an invalid list entry.");
            }
            quests.Add(ToQuestData(entry.Quest));
        }
    }

    private static QuestData ToQuestData(QuestView quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var value = new QuestData(
            quest.CampaignCode,
            quest.CompletedQuestsInCampaign,
            quest.QuestCountInCampaign,
            quest.ActivityPointType,
            quest.Id,
            quest.IsAccepted,
            quest.Type,
            quest.ImageVersion,
            quest.RewardCurrencyAmount,
            quest.LocalizationCode,
            quest.CompletedSteps,
            quest.TotalSteps,
            quest.SortOrder,
            quest.CatalogPageName,
            quest.ChainCode,
            quest.IsEasy,
            quest.IsSeasonal,
            quest.SeasonalSecondsLeft);
        if (value.IsCompleted != quest.IsCompleted ||
            value.IsCampaignCompleted != quest.IsCampaignCompleted ||
            value.IsLastQuestInCampaign != quest.IsLastQuestInCampaign ||
            value.CampaignChainCode != quest.CampaignChainCode)
        {
            throw new InvalidOperationException(
                "The quest application returned inconsistent derived quest data.");
        }
        return value;
    }
}
