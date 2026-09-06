using Qx.Model.Messages.Incoming;
using Qx.Model.Quests;

namespace Qx.Scripting;

/// <content>
/// Quest event subscriptions. Available on both the Flash and the Unity client.
/// <para>
/// Every <c>On*</c> method registers a handler and returns the handle that removes it again. The
/// subscription is also tracked by the script and torn down when the script stops, so the handle
/// only has to be kept when the script wants to unsubscribe earlier. Disposing it more than once
/// is harmless.
/// </para>
/// <para>
/// Handlers run inline on the interception thread while the triggering packet is dispatched, not
/// on the script thread, and after the cached quest state has already been updated. Keep them
/// short and do not block inside them.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Raised when the regular quest list arrives, whether it was requested by the script or by
    /// the game client.
    /// </summary>
    /// <param name="handler">
    /// Receives the quest list together with the server's hint that the quest window should be
    /// opened.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnQuestsUpdated(Action<Quests> handler)
        => Subscribe(handler, value => Quests.AvailableChanged += value,
            value => Quests.AvailableChanged -= value);

    /// <summary>Raised when the seasonal campaign's quest list arrives.</summary>
    /// <param name="handler">Receives the seasonal quest list.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnSeasonalQuestsUpdated(Action<QuestsSeasonal> handler)
        => Subscribe(handler, value => Quests.SeasonalChanged += value,
            value => Quests.SeasonalChanged -= value);

    /// <summary>
    /// Raised when the server pushes the quest the local user is working on. This fires after
    /// accepting or activating a quest and again on every progress update, so it is the hook for
    /// tracking step progress.
    /// </summary>
    /// <param name="handler">Receives the quest, including its completed and total step counts.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnCurrentQuestChanged(Action<QuestData> handler)
        => Subscribe(handler, value => Quests.CurrentChanged += value,
            value => Quests.CurrentChanged -= value);

    /// <summary>Raised when a quest was completed.</summary>
    /// <param name="handler">
    /// Receives the completed quest and whether the game client was told to show the reward
    /// dialog.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnQuestCompleted(Action<QuestCompleted> handler)
        => Subscribe(handler, value => Quests.Completed += value,
            value => Quests.Completed -= value);

    /// <summary>Raised when a quest was cancelled, either by request or because it expired.</summary>
    /// <param name="handler">
    /// Receives the cancelled quest and the expiry flag that distinguishes the two cases.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnQuestCancelled(Action<QuestCancelled> handler)
        => Subscribe(handler, value => Quests.Cancelled += value,
            value => Quests.Cancelled -= value);

    /// <summary>Raised when the daily quest offer arrives or changes.</summary>
    /// <param name="handler">
    /// Receives the daily quest, which may hold no quest at all, plus the easy and hard pool
    /// sizes.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnDailyQuestChanged(Action<QuestDaily> handler)
        => Subscribe(handler, value => Quests.DailyChanged += value,
            value => Quests.DailyChanged -= value);

    /// <summary>
    /// Raised after the cached quest state was emptied for a new session, which happens on
    /// reconnect. Every quest list is empty and every last-result value is unset by the time the
    /// handler runs.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnQuestsReset(Action handler)
        => Subscribe(handler, value => Quests.ResetCompleted += value,
            value => Quests.ResetCompleted -= value);
}
