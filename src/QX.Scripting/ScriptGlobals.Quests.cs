using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the quests offered in the regular quest window, as of the last quest list the server
    /// sent.
    /// </summary>
    /// <remarks>
    /// It is empty until a quest list has arrived. Every read returns a snapshot copy, not a live
    /// view.
    /// </remarks>
    public IReadOnlyList<QuestData> AvailableQuests => Quests.Available;

    /// <summary>
    /// Gets the quests of the current seasonal campaign, as of the last seasonal list the server
    /// sent.
    /// </summary>
    /// <remarks>
    /// It is empty until a seasonal list has arrived. Seasonal entries also carry the seconds left
    /// before the campaign closes. Every read returns a snapshot copy, not a live view.
    /// </remarks>
    public IReadOnlyList<QuestData> SeasonalQuests => Quests.Seasonal;

    /// <summary>
    /// Gets the quest from the last quest update, or <see langword="null"/> until the server has
    /// pushed one.
    /// </summary>
    /// <remarks>
    /// It is the quest the local user is working on; progress is readable from its completed and
    /// total step counts.
    /// </remarks>
    public QuestData? CurrentQuest => Quests.Current;

    /// <summary>
    /// Gets the daily quest offer, or <see langword="null"/> until a daily quest message has
    /// arrived.
    /// </summary>
    /// <remarks>
    /// The offer holds the quest itself when one is active, plus how many easy and hard daily
    /// quests exist.
    /// </remarks>
    public QuestDaily? DailyQuest => Quests.Daily;

    /// <summary>
    /// Gets the most recent quest completion the server announced, or <see langword="null"/> when
    /// no quest has completed during this session.
    /// </summary>
    /// <remarks>
    /// The completion carries the finished quest and whether the client was told to show the
    /// reward dialog.
    /// </remarks>
    public QuestCompleted? LastCompletedQuest => Quests.LastCompletion;

    /// <summary>
    /// Gets the most recent quest cancellation the server announced, or <see langword="null"/>
    /// when no quest has been canceled during this session.
    /// </summary>
    /// <remarks>
    /// The cancellation carries the quest and whether it ended because it expired rather than by
    /// request.
    /// </remarks>
    public QuestCancelled? LastCancelledQuest => Quests.LastCancellation;

    /// <summary>
    /// Asks for the regular quest list.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the list lands in <see cref="AvailableQuests"/> and runs the
    /// <see cref="OnQuestsUpdated(Action{Qx.Model.Messages.Incoming.Quests})"/> handlers.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RequestQuests() => Quests.RequestAvailable();

    /// <summary>
    /// Asks for the seasonal campaign's quest list.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the list lands in <see cref="SeasonalQuests"/> and runs the
    /// <see cref="OnSeasonalQuestsUpdated(Action{QuestsSeasonal})"/> handlers.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RequestSeasonalQuests() => Quests.RequestSeasonal();

    /// <summary>
    /// Asks for a daily quest.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the answer lands in <see cref="DailyQuest"/> and runs the
    /// <see cref="OnDailyQuestChanged(Action{QuestDaily})"/> handlers.
    /// </remarks>
    /// <param name="isEasy">
    /// <see langword="true"/> to pick from the easy pool; otherwise, <see langword="false"/> to
    /// pick from the hard pool.
    /// </param>
    /// <param name="index">
    /// The zero-based entry of that pool to fetch. The two pool sizes are reported by the daily
    /// quest state.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RequestDailyQuest(bool isEasy, int index) =>
        Quests.RequestDaily(isEasy, index);

    /// <summary>
    /// Sends a request to accept a quest.
    /// </summary>
    /// <remarks>
    /// It returns immediately. Quest updates that follow run the
    /// <see cref="OnCurrentQuestChanged(Action{QuestData})"/> handlers.
    /// </remarks>
    /// <param name="questId">The quest id taken from a quest list entry.</param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void AcceptQuest(Id questId) => Quests.Accept(questId);

    /// <summary>
    /// Sends a request to activate a quest.
    /// </summary>
    /// <remarks>
    /// It returns immediately. Quest updates that follow run the
    /// <see cref="OnCurrentQuestChanged(Action{QuestData})"/> handlers.
    /// </remarks>
    /// <param name="questId">The quest id taken from a quest list entry.</param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void ActivateQuest(Id questId) => Quests.Activate(questId);

    /// <summary>Declines a quest offer without accepting it.</summary>
    /// <remarks>It returns immediately.</remarks>
    /// <param name="questId">The quest id taken from a quest list entry.</param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RejectQuest(Id questId) => Quests.Reject(questId);

    /// <summary>Sends a request to cancel the current quest.</summary>
    /// <remarks>
    /// It returns immediately; the server confirms through
    /// <see cref="OnQuestCancelled(Action{QuestCancelled})"/> and <see cref="LastCancelledQuest"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void CancelQuest() => Quests.Cancel();

    /// <summary>
    /// Tells the server the quest tracker was opened.
    /// </summary>
    /// <remarks>
    /// It is the game client's own housekeeping message; its effect is that the server sends the
    /// current quest again.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void OpenQuestTracker() => Quests.OpenTracker();

    /// <summary>
    /// Reports the "send a friend request" quest step as done.
    /// </summary>
    /// <remarks>
    /// The game client sends it after the user completes that step in its own UI; the server still
    /// validates the claim.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void CompleteFriendRequestQuest() =>
        Quests.CompleteFriendRequestQuest();

    /// <summary>
    /// Gets the available quests, asking the hotel for them when this session has not seen them.
    /// </summary>
    /// <remarks>
    /// The quest list arrives when the client opens its quest window, so QX attached to a session
    /// already in progress may never have received it. <see cref="AvailableQuests"/> then reads
    /// empty, which is indistinguishable from having no quests. Use it when the answer has to be
    /// right rather than merely available. A list already loaded is returned without a request.
    /// </remarks>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds, from 1 to 120000.</param>
    /// <returns>The available quests.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
    /// <exception cref="TimeoutException">Thrown when the hotel did not answer in time.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the quest state changed while the list was read.</exception>
    public async Task<IReadOnlyList<QuestData>> GetQuests(int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        QuestStateView state = await _application
            .InvokeAsync<QuestStateRequest, QuestStateView>(
                ApplicationMemberIds.QuestsState,
                new QuestStateRequest(),
                Ct)
            .ConfigureAwait(false);
        ValidateQuestState(state);

        QuestEntryPage first_page;
        if (state.Summary.AvailableLoaded)
        {
            first_page = await _application
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
            QuestAvailableRefreshResult refreshed = await _application
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
            page = await _application
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
            page.Connected != first_page.Connected ||
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
