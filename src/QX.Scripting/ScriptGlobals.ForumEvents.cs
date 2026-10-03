using Qx.Game;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Registers a handler that runs whenever any part of the forum state changes.
    /// </summary>
    /// <remarks>
    /// It is the coarsest forum event: it runs after each of the specific forum events, including
    /// the reset.
    /// </remarks>
    /// <param name="handler">The handler to call with the new immutable snapshot.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumStateChanged(Action<ForumSnapshot> handler)
        => Subscribe(handler, value => Forums.SnapshotChanged += value,
            value => Forums.SnapshotChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the details of a single forum arrive.
    /// </summary>
    /// <remarks>
    /// The details hold the forum summary plus the viewer's read, post and moderate permissions,
    /// whether the viewer may change settings, and whether the viewer is hotel staff.
    /// </remarks>
    /// <param name="handler">The handler to call with the details, which carry their own group id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumDetailsChanged(Action<ForumDetails> handler)
        => Subscribe(handler, value => Forums.DetailsChanged += value,
            value => Forums.DetailsChanged -= value);

    /// <summary>
    /// Registers a handler that runs when a page of the forum directory arrives.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the page, which carries the list code and start index it answers
    /// along with the entries.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumsListed(Action<ForumsList> handler)
        => Subscribe(handler, value => Forums.ForumPageReceived += value,
            value => Forums.ForumPageReceived -= value);

    /// <summary>
    /// Registers a handler that runs when a page of threads for one forum arrives.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the page, which carries the group id and start index it answers
    /// along with the threads.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumThreadsListed(Action<ForumThreads> handler)
        => Subscribe(handler, value => Forums.ThreadPageReceived += value,
            value => Forums.ThreadPageReceived -= value);

    /// <summary>
    /// Registers a handler that runs when a page of posts for one thread arrives.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the page, which carries the group id, thread id and start index it
    /// answers along with the posts.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumMessagesListed(Action<ThreadMessages> handler)
        => Subscribe(handler, value => Forums.MessagePageReceived += value,
            value => Forums.MessagePageReceived -= value);

    /// <summary>
    /// Registers a handler that runs when the server reports a created or updated thread.
    /// </summary>
    /// <remarks>
    /// It runs after a new thread is posted, after a sticky or lock change, after moderation, or in
    /// reply to a single thread request.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the group id the thread belongs to, then the thread. The thread
    /// record carries no group id of its own, which is why it is passed separately.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumThreadChanged(Action<Id, ForumThread> handler)
        => Subscribe(handler, value => Forums.ThreadChanged += value,
            value => Forums.ThreadChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the server reports a created or updated post.
    /// </summary>
    /// <remarks>
    /// It runs after a reply is posted, or after a post was hidden or restored.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the group id, then the thread id, then the post. The post record
    /// carries neither id of its own, which is why both are passed separately.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumMessageChanged(Action<Id, Id, ForumPost> handler)
        => Subscribe(handler, value => Forums.MessageChanged += value,
            value => Forums.MessageChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the server sends the number of forums holding unread
    /// messages.
    /// </summary>
    /// <param name="handler">The handler to call with the received count.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnUnreadForumsCountChanged(Action<int> handler)
        => Subscribe(handler, value => Forums.UnreadForumsCountChanged += value,
            value => Forums.UnreadForumsCountChanged -= value);

    /// <summary>
    /// Registers a handler that runs after the cached forum state has been emptied.
    /// </summary>
    /// <remarks>
    /// The state is emptied when the hotel connection closes. Every forum cache is empty and the
    /// unread count is unset by the time the handler runs.
    /// </remarks>
    /// <param name="handler">The handler to call with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnForumsReset(Action handler)
        => Subscribe(handler, value => Forums.ResetCompleted += value,
            value => Forums.ResetCompleted -= value);
}
