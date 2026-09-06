using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using ForumThreadData = Qx.Model.Forums.ForumThread;

namespace Qx.Scripting;

/// <content>
/// Group forums: cached forum state plus the fire-and-forget requests and actions that drive it.
/// <para>
/// <b>Flash only.</b> Every forum reply model refuses to parse on a non-Flash session
/// (<c>ForumProtocol.RequireFlash</c>), and the forum tracker registers all of its incoming
/// handlers for the Flash client alone. On a Unity session most of the request methods below
/// still compose — those messages do have a Unity wire layout — but no reply is ever accepted,
/// so the cached state stays empty and no forum event fires.
/// </para>
/// <para>
/// No request method here blocks or returns a value. Each sends one message and returns
/// immediately; the answer surfaces later through the cached state and the forum events. To wait
/// for a specific reply, subscribe first and then send the request.
/// </para>
/// <para>A forum is identified by the id of the group that owns it.</para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// The whole cached forum state in one immutable object: the forum, thread and message page
    /// caches, the per-id forum, thread and message maps, and the unread-forum count.
    /// </summary>
    /// <returns>
    /// An immutable snapshot. Reading again yields a new object once anything changed; an
    /// already-held snapshot never mutates underneath the caller.
    /// </returns>
    public ForumSnapshot ForumState => Forums.Snapshot;

    /// <summary>
    /// Every forum seen so far, keyed by group id. Filled from forum directory pages and from
    /// single-forum detail replies; empty until one of those arrives.
    /// </summary>
    public IReadOnlyDictionary<Id, ForumSummary> KnownForums =>
        ForumState.KnownForums;

    /// <summary>
    /// The full details of every forum whose detail reply has been seen, keyed by group id.
    /// Details add the viewer's read/post/moderate permissions and staff flag on top of the
    /// summary.
    /// </summary>
    public IReadOnlyDictionary<Id, ForumDetails> ForumDetails =>
        ForumState.ForumDetails;

    /// <summary>
    /// Every thread seen so far, keyed by group id and thread id. Threads arrive both from thread
    /// list pages and from single-thread create/update replies.
    /// </summary>
    public IReadOnlyDictionary<ForumThreadKey, ForumThreadData> ForumThreads =>
        ForumState.KnownThreads;

    /// <summary>
    /// Every forum post seen so far, keyed by group id, thread id and post id. Posts arrive both
    /// from message list pages and from single-post create/update replies.
    /// </summary>
    public IReadOnlyDictionary<ForumMessageKey, ForumPost> ForumMessages =>
        ForumState.KnownMessages;

    /// <summary>
    /// How many forums currently hold unread messages, or <see langword="null"/> until the count
    /// has been requested at least once.
    /// </summary>
    public int? UnreadForumsCount => ForumState.UnreadForumsCount;

    /// <summary>Finds a cached forum summary.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <returns>The summary, or <see langword="null"/> when this forum has not been seen.</returns>
    public ForumSummary? FindForum(Id group_id) =>
        Forums.FindForum(group_id);

    /// <summary>Finds the cached details of one forum, including the viewer's permissions.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <returns>
    /// The details, or <see langword="null"/> when no detail reply for this forum has arrived.
    /// </returns>
    public ForumDetails? FindForumDetails(Id group_id) =>
        Forums.FindDetails(group_id);

    /// <summary>Finds one cached thread.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread id.</param>
    /// <returns>The thread, or <see langword="null"/> when it has not been seen.</returns>
    public ForumThreadData? FindForumThread(
        Id group_id,
        Id thread_id) =>
        Forums.FindThread(group_id, thread_id);

    /// <summary>Finds one cached forum post.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread the post belongs to.</param>
    /// <param name="message_id">The post id.</param>
    /// <returns>The post, or <see langword="null"/> when it has not been seen.</returns>
    public ForumPost? FindForumMessage(
        Id group_id,
        Id thread_id,
        Id message_id) =>
        Forums.FindMessage(group_id, thread_id, message_id);

    /// <summary>
    /// Asks for one forum's details and permissions. Returns immediately; the answer lands in the
    /// forum-details cache and raises the forum-details event.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    public void RequestForumStats(Id group_id) =>
        Application.Invoke<ForumDetailsRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumDetailsRequest,
            new ForumDetailsRequest(group_id));

    /// <summary>
    /// Asks for a page of the forum directory. Returns immediately; the page lands in the forum
    /// cache and raises the forums-listed event.
    /// </summary>
    /// <param name="list_code">
    /// Which directory to list: <c>Active</c> (0), <c>Popular</c> (1) or <c>MyForums</c> (2).
    /// </param>
    /// <param name="start_index">Zero-based index of the first entry on the page.</param>
    /// <param name="max_count">How many entries to return; the game client's own page size is 20.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start_index"/> is negative, or <paramref name="max_count"/> is zero or
    /// negative.
    /// </exception>
    public void RequestForums(
        ForumListCode list_code,
        int start_index = 0,
        int max_count = 20) =>
        Application.Invoke<ForumListRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumsListRequest,
            new ForumListRequest(list_code, start_index, max_count));

    /// <summary>
    /// Asks for a page of threads in one forum. Returns immediately; the page lands in the thread
    /// cache and raises the threads-listed event.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="start_index">Zero-based index of the first thread on the page.</param>
    /// <param name="max_count">How many threads to return; the game client's own page size is 20.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start_index"/> is negative, or <paramref name="max_count"/> is zero or
    /// negative.
    /// </exception>
    public void RequestForumThreads(
        Id group_id,
        int start_index = 0,
        int max_count = 20) =>
        Application.Invoke<ForumThreadsRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumThreadsRequest,
            new ForumThreadsRequest(group_id, start_index, max_count));

    /// <summary>
    /// Asks for a page of posts in one thread. Returns immediately; the page lands in the message
    /// cache and raises the messages-listed event.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread to read.</param>
    /// <param name="start_index">Zero-based index of the first post on the page.</param>
    /// <param name="max_count">How many posts to return; the game client's own page size is 20.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start_index"/> is negative, or <paramref name="max_count"/> is zero or
    /// negative.
    /// </exception>
    public void RequestForumMessages(
        Id group_id,
        Id thread_id,
        int start_index = 0,
        int max_count = 20) =>
        Application.Invoke<ForumMessagesRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumMessagesRequest,
            new ForumMessagesRequest(
                group_id,
                thread_id,
                start_index,
                max_count));

    /// <summary>
    /// Asks for one thread's header row on its own, without its posts. Returns immediately; the
    /// thread lands in the thread cache and raises the thread-changed event.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread id.</param>
    public void RequestForumThread(
        Id group_id,
        Id thread_id) =>
        Application.Invoke<ForumThreadRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumThreadRequest,
            new ForumThreadRequest(group_id, thread_id));

    /// <summary>
    /// Asks how many forums hold unread messages. Returns immediately; the number lands in the
    /// unread-count state and raises the unread-count event.
    /// </summary>
    public void RequestUnreadForumsCount() =>
        Application.Invoke<ForumUnreadRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumsUnreadRequest,
            new ForumUnreadRequest());

    /// <summary>
    /// Starts a new thread. This is a post with a thread id of 0, which is how the client signals
    /// "create" rather than "reply".
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="subject">The thread title; the game client requires at least 10 characters.</param>
    /// <param name="message_text">The first post's body; the game client requires at least 10 characters.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="subject"/> or <paramref name="message_text"/> is null.
    /// </exception>
    public void CreateForumThread(
        Id group_id,
        string subject,
        string message_text) =>
        Application.Invoke<ForumPostActionRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumsPost,
            new ForumPostActionRequest(group_id, 0, subject, message_text));

    /// <summary>Posts a reply into an existing thread, with an empty subject.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread to reply to.</param>
    /// <param name="message_text">The reply body.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message_text"/> is null.</exception>
    public void ReplyToForumThread(
        Id group_id,
        Id thread_id,
        string message_text) =>
        Application.Invoke<ForumPostActionRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumsPost,
            new ForumPostActionRequest(group_id, thread_id, "", message_text));

    /// <summary>
    /// Sends the raw post message that thread creation and replying both wrap.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread to post into, or 0 to start a new thread.</param>
    /// <param name="subject">The subject; only meaningful when starting a thread.</param>
    /// <param name="message_text">The post body.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="subject"/> or <paramref name="message_text"/> is null.
    /// </exception>
    public void PostForumMessage(
        Id group_id,
        Id thread_id,
        string subject,
        string message_text) =>
        Application.Invoke<ForumPostActionRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumsPost,
            new ForumPostActionRequest(group_id, thread_id, subject, message_text));

    /// <summary>Hides or restores a whole thread as a forum admin or as hotel staff.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread to moderate.</param>
    /// <param name="state">
    /// The new moderation state on the client's scale: 0 default, 1 restored by admin, 10 hidden
    /// by admin, 20 permanently hidden by staff. The Flash client sends 10 when the viewer only
    /// holds forum-moderate rights, 20 when the viewer is staff, and 1 to undelete.
    /// </param>
    public void ModerateForumThread(
        Id group_id,
        Id thread_id,
        int state) =>
        Application.Invoke<ForumThreadModerationRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumThreadModerate,
            new ForumThreadModerationRequest(group_id, thread_id, state));

    /// <summary>Hides or restores a single post as a forum admin or as hotel staff.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread the post belongs to.</param>
    /// <param name="message_id">The post to moderate.</param>
    /// <param name="state">
    /// The new moderation state, on the same scale as thread moderation: 0 default, 1 restored,
    /// 10 hidden by admin, 20 permanently hidden by staff.
    /// </param>
    public void ModerateForumMessage(
        Id group_id,
        Id thread_id,
        Id message_id,
        int state) =>
        Application.Invoke<ForumMessageModerationRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumMessageModerate,
            new ForumMessageModerationRequest(group_id, thread_id, message_id, state));

    /// <summary>
    /// Rewrites a forum's four permission levels at once. All four travel in one message, so read
    /// the current values out of the forum details first when only one of them should change.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="read_level">Who may read the forum. The dialog offers levels 0 to 3, least to most restrictive.</param>
    /// <param name="post_message_level">Who may reply; the dialog keeps this at or above <paramref name="read_level"/>.</param>
    /// <param name="post_thread_level">Who may start threads; the dialog keeps this at or above <paramref name="post_message_level"/>.</param>
    /// <param name="moderate_level">Who may moderate; the dialog offers only 2 and 3 here.</param>
    /// <remarks>
    /// The ordering rules come from the Flash settings dialog and are not enforced by this method.
    /// The server decides what it accepts.
    /// </remarks>
    public void UpdateForumSettings(
        Id group_id,
        int read_level,
        int post_message_level,
        int post_thread_level,
        int moderate_level) =>
        Application.Invoke<ForumSettingsUpdateRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumSettingsUpdate,
            new ForumSettingsUpdateRequest(
                group_id,
                read_level,
                post_message_level,
                post_thread_level,
                moderate_level));

    /// <summary>
    /// Marks forums as read up to a given post. Several markers travel in one message.
    /// </summary>
    /// <param name="markers">
    /// One entry per forum: the group id, the last post id that was read, and whether the whole
    /// forum should be treated as read.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="markers"/> is null.</exception>
    public void UpdateForumReadMarkers(
        params ForumReadMarker[] markers) =>
        Application.Invoke<ForumReadMarkersUpdateRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumReadMarkersUpdate,
            new ForumReadMarkersUpdateRequest(markers));

    /// <summary>
    /// Sets a thread's sticky and locked flags. Both travel together, so pass the current value
    /// for whichever flag should stay as it is.
    /// </summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread to change.</param>
    /// <param name="is_sticky">Whether the thread is pinned to the top of the list.</param>
    /// <param name="is_locked">Whether the thread rejects further replies.</param>
    public void UpdateForumThread(
        Id group_id,
        Id thread_id,
        bool is_sticky,
        bool is_locked) =>
        Application.Invoke<ForumThreadUpdateRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumThreadUpdate,
            new ForumThreadUpdateRequest(group_id, thread_id, is_sticky, is_locked));

    /// <summary>Reports a thread to hotel moderation through the call-for-help flow.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread being reported.</param>
    /// <param name="category_id">The help-tool report category id.</param>
    /// <param name="report">The free-text description sent with the report.</param>
    /// <param name="first_context">Extra context string; carried by the Flash message only.</param>
    /// <param name="second_context">Extra context string; carried by the Flash message only.</param>
    /// <exception cref="ArgumentNullException">Any of the string arguments is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The session is Unity and a non-empty context string was supplied; the Unity report message
    /// has no context fields.
    /// </exception>
    /// <remarks>
    /// Reporting is the one forum action with a real Unity message: Flash sends
    /// <c>CallForHelpFromForumThread</c>, Unity sends <c>ReportForumThread</c> without contexts.
    /// </remarks>
    public void ReportForumThread(
        Id group_id,
        Id thread_id,
        int category_id,
        string report,
        string first_context = "",
        string second_context = "") =>
        Application.Invoke<ForumThreadReportRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumThreadReport,
            new ForumThreadReportRequest(
                group_id,
                thread_id,
                category_id,
                report,
                first_context,
                second_context));

    /// <summary>Reports a single post to hotel moderation through the call-for-help flow.</summary>
    /// <param name="group_id">The group that owns the forum.</param>
    /// <param name="thread_id">The thread the post belongs to.</param>
    /// <param name="message_id">The post being reported.</param>
    /// <param name="category_id">The help-tool report category id.</param>
    /// <param name="report">The free-text description sent with the report.</param>
    /// <param name="first_context">Extra context string; carried by the Flash message only.</param>
    /// <param name="second_context">Extra context string; carried by the Flash message only.</param>
    /// <exception cref="ArgumentNullException">Any of the string arguments is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The session is Unity and a non-empty context string was supplied; the Unity report message
    /// has no context fields.
    /// </exception>
    public void ReportForumMessage(
        Id group_id,
        Id thread_id,
        Id message_id,
        int category_id,
        string report,
        string first_context = "",
        string second_context = "") =>
        Application.Invoke<ForumMessageReportRequest, ForumDispatchResult>(
            ApplicationMemberIds.ForumMessageReport,
            new ForumMessageReportRequest(
                group_id,
                thread_id,
                message_id,
                category_id,
                report,
                first_context,
                second_context));
}
