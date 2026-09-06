using Qx.Game;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;
using ForumThreadData = Qx.Model.Forums.ForumThread;

namespace Qx.Scripting;

/// <content>
/// Group-forum event subscriptions.
/// <para>
/// <b>Flash only.</b> The forum tracker registers its incoming handlers for the Flash client
/// alone, so on a Unity session none of these events ever fire.
/// </para>
/// <para>
/// Every <c>On*</c> method registers a handler and returns the handle that removes it again. The
/// subscription is also tracked by the script and torn down when the script stops, so the handle
/// only has to be kept when the script wants to unsubscribe earlier. Disposing it more than once
/// is harmless.
/// </para>
/// <para>
/// Handlers run inline on the interception thread while the triggering packet is dispatched, not
/// on the script thread, and after the cached forum state has already been updated. Keep them
/// short and do not block inside them.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Raised whenever any part of the forum state changes, carrying the complete new snapshot.
    /// This is the coarsest forum event: it also fires alongside each of the specific events
    /// below, and once more when the state is reset.
    /// </summary>
    /// <param name="handler">Receives the new immutable snapshot.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumStateChanged(Action<ForumSnapshot> handler)
        => Subscribe(handler, value => Forums.SnapshotChanged += value,
            value => Forums.SnapshotChanged -= value);

    /// <summary>
    /// Raised when the details of a single forum arrive: its summary plus the viewer's
    /// read/post/moderate permissions, whether the viewer may change settings, and whether the
    /// viewer is hotel staff.
    /// </summary>
    /// <param name="handler">Receives the details, which carry their own group id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumDetailsChanged(Action<ForumDetails> handler)
        => Subscribe(handler, value => Forums.DetailsChanged += value,
            value => Forums.DetailsChanged -= value);

    /// <summary>
    /// Raised when a page of the forum directory arrives, carrying the list code and start index
    /// it answers along with the entries.
    /// </summary>
    /// <param name="handler">Receives the page.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumsListed(Action<ForumsList> handler)
        => Subscribe(handler, value => Forums.ForumPageReceived += value,
            value => Forums.ForumPageReceived -= value);

    /// <summary>
    /// Raised when a page of threads for one forum arrives, carrying the group id and start index
    /// it answers along with the threads.
    /// </summary>
    /// <param name="handler">Receives the page.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumThreadsListed(Action<ForumThreads> handler)
        => Subscribe(handler, value => Forums.ThreadPageReceived += value,
            value => Forums.ThreadPageReceived -= value);

    /// <summary>
    /// Raised when a page of posts for one thread arrives, carrying the group id, thread id and
    /// start index it answers along with the posts.
    /// </summary>
    /// <param name="handler">Receives the page.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumMessagesListed(Action<ThreadMessages> handler)
        => Subscribe(handler, value => Forums.MessagePageReceived += value,
            value => Forums.MessagePageReceived -= value);

    /// <summary>
    /// Raised when a single thread was created or updated — after a new thread is posted, after a
    /// sticky or lock change, after moderation, or in reply to a single-thread request.
    /// </summary>
    /// <param name="handler">
    /// Receives the group id the thread belongs to, then the thread. The thread record carries no
    /// group id of its own, which is why it is passed separately.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumThreadChanged(Action<Id, ForumThreadData> handler)
        => Subscribe(handler, value => Forums.ThreadChanged += value,
            value => Forums.ThreadChanged -= value);

    /// <summary>
    /// Raised when a single post was created or updated — after a reply is posted, or after a post
    /// was hidden or restored.
    /// </summary>
    /// <param name="handler">
    /// Receives the group id, then the thread id, then the post. The post record carries neither
    /// id of its own, which is why both are passed separately.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumMessageChanged(Action<Id, Id, ForumPost> handler)
        => Subscribe(handler, value => Forums.MessageChanged += value,
            value => Forums.MessageChanged -= value);

    /// <summary>Raised when the number of forums holding unread messages changes.</summary>
    /// <param name="handler">Receives the new count.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnUnreadForumsCountChanged(Action<int> handler)
        => Subscribe(handler, value => Forums.UnreadForumsCountChanged += value,
            value => Forums.UnreadForumsCountChanged -= value);

    /// <summary>
    /// Raised after the cached forum state was emptied for a new session, which happens on
    /// reconnect. Every forum cache is empty and the unread count is unset by the time the handler
    /// runs.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnForumsReset(Action handler)
        => Subscribe(handler, value => Forums.ResetCompleted += value,
            value => Forums.ResetCompleted -= value);
}
