using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Registers a handler that runs when the regular quest list arrives.
    /// </summary>
    /// <remarks>
    /// It runs whether the list was requested by the script or by the game client.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the quest list together with the server's hint that the quest
    /// window should be opened.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnQuestsUpdated(Action<Quests> handler)
        => Subscribe(handler, value => Quests.AvailableChanged += value,
            value => Quests.AvailableChanged -= value);

    /// <summary>Registers a handler that runs when the seasonal campaign's quest list arrives.</summary>
    /// <param name="handler">The handler to call with the seasonal quest list.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnSeasonalQuestsUpdated(Action<QuestsSeasonal> handler)
        => Subscribe(handler, value => Quests.SeasonalChanged += value,
            value => Quests.SeasonalChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the server sends an update of the quest the local user is
    /// working on.
    /// </summary>
    /// <remarks>
    /// It fires after accepting or activating a quest and again on every progress update, so it is
    /// the hook for tracking step progress.
    /// </remarks>
    /// <param name="handler">The handler to call with the quest, including its completed and total step counts.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnCurrentQuestChanged(Action<QuestData> handler)
        => Subscribe(handler, value => Quests.CurrentChanged += value,
            value => Quests.CurrentChanged -= value);

    /// <summary>Registers a handler that runs when the server reports a completed quest.</summary>
    /// <param name="handler">
    /// The handler to call with the completed quest and whether the game client was told to show
    /// the reward dialog.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnQuestCompleted(Action<QuestCompleted> handler)
        => Subscribe(handler, value => Quests.Completed += value,
            value => Quests.Completed -= value);

    /// <summary>
    /// Registers a handler that runs when the server reports a canceled quest, either canceled by
    /// request or expired.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the canceled quest and the expiry flag that distinguishes the two
    /// cases.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnQuestCancelled(Action<QuestCancelled> handler)
        => Subscribe(handler, value => Quests.Cancelled += value,
            value => Quests.Cancelled -= value);

    /// <summary>Registers a handler that runs when the server sends the daily quest offer.</summary>
    /// <param name="handler">
    /// The handler to call with the daily quest, which may hold no quest at all, plus the easy and
    /// hard pool sizes.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnDailyQuestChanged(Action<QuestDaily> handler)
        => Subscribe(handler, value => Quests.DailyChanged += value,
            value => Quests.DailyChanged -= value);

    /// <summary>
    /// Registers a handler that runs after the cached quest state has been emptied.
    /// </summary>
    /// <remarks>
    /// The state is emptied when the hotel connection closes or a new session connects. Every
    /// quest list is empty and every last result value is unset by the time the handler runs.
    /// </remarks>
    /// <param name="handler">The handler to call with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnQuestsReset(Action handler)
        => Subscribe(handler, value => Quests.ResetCompleted += value,
            value => Quests.ResetCompleted -= value);
}
