using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using System.Collections.ObjectModel;

namespace Qx.Game;

/// <summary>Represents the key of a cached page of the forum list.</summary>
/// <param name="ListCode">The forum list the page belongs to.</param>
/// <param name="StartIndex">The index of the first forum on the page.</param>
public readonly record struct ForumListPageKey(
    ForumListCode ListCode,
    int StartIndex);

/// <summary>Represents the key of a cached page of threads in a forum.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="StartIndex">The index of the first thread on the page.</param>
public readonly record struct ForumThreadPageKey(
    Id GroupId,
    int StartIndex);

/// <summary>Represents the key of a cached page of messages in a forum thread.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="ThreadId">The id of the thread.</param>
/// <param name="StartIndex">The index of the first message on the page.</param>
public readonly record struct ForumMessagePageKey(
    Id GroupId,
    Id ThreadId,
    int StartIndex);

/// <summary>Represents the key of a cached forum thread.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="ThreadId">The id of the thread.</param>
public readonly record struct ForumThreadKey(
    Id GroupId,
    Id ThreadId);

/// <summary>Represents the key of a cached forum message.</summary>
/// <param name="GroupId">The id of the group that owns the forum.</param>
/// <param name="ThreadId">The id of the thread that contains the message.</param>
/// <param name="MessageId">The id of the message.</param>
public readonly record struct ForumMessageKey(
    Id GroupId,
    Id ThreadId,
    Id MessageId);

/// <summary>Represents an immutable snapshot of the forum data received in the current session.</summary>
/// <param name="ForumPages">The received pages of the forum list, by list and start index.</param>
/// <param name="KnownForums">The forum summaries by group id, collected from forum list pages and forum details.</param>
/// <param name="ForumDetails">The received forum details by group id.</param>
/// <param name="ThreadPages">The received pages of forum threads, by group id and start index.</param>
/// <param name="KnownThreads">
/// The forum threads by group and thread id, collected from thread pages and from created and updated threads.
/// </param>
/// <param name="MessagePages">The received pages of thread messages, by group id, thread id and start index.</param>
/// <param name="KnownMessages">
/// The forum messages by group, thread and message id, collected from message pages and from created and
/// updated messages.
/// </param>
/// <param name="UnreadForumsCount">
/// The number of forums with unread messages, or <see langword="null"/> if the server has not sent it.
/// </param>
public sealed record ForumSnapshot(
    IReadOnlyDictionary<ForumListPageKey, ForumsList> ForumPages,
    IReadOnlyDictionary<Id, ForumSummary> KnownForums,
    IReadOnlyDictionary<Id, ForumDetails> ForumDetails,
    IReadOnlyDictionary<ForumThreadPageKey, ForumThreads> ThreadPages,
    IReadOnlyDictionary<ForumThreadKey, ForumThread> KnownThreads,
    IReadOnlyDictionary<ForumMessagePageKey, ThreadMessages> MessagePages,
    IReadOnlyDictionary<ForumMessageKey, ForumPost> KnownMessages,
    int? UnreadForumsCount)
{
    /// <summary>Gets a snapshot that contains no forum data.</summary>
    public static ForumSnapshot Empty { get; } = new(
        EmptyMap<ForumListPageKey, ForumsList>(),
        EmptyMap<Id, ForumSummary>(),
        EmptyMap<Id, ForumDetails>(),
        EmptyMap<ForumThreadPageKey, ForumThreads>(),
        EmptyMap<ForumThreadKey, ForumThread>(),
        EmptyMap<ForumMessagePageKey, ThreadMessages>(),
        EmptyMap<ForumMessageKey, ForumPost>(),
        null);

    /// <summary>Gets the summary of a forum.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <returns>The forum summary, or <see langword="null"/> if it has not been received.</returns>
    public ForumSummary? FindForum(Id groupId) =>
        KnownForums.GetValueOrDefault(groupId);

    /// <summary>Gets the details of a forum.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <returns>The forum details, or <see langword="null"/> if they have not been received.</returns>
    public ForumDetails? FindDetails(Id groupId) =>
        ForumDetails.GetValueOrDefault(groupId);

    /// <summary>Gets a forum thread.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <returns>The thread, or <see langword="null"/> if it has not been received.</returns>
    public ForumThread? FindThread(Id groupId, Id threadId) =>
        KnownThreads.GetValueOrDefault(new ForumThreadKey(groupId, threadId));

    /// <summary>Gets a forum message.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread that contains the message.</param>
    /// <param name="messageId">The id of the message.</param>
    /// <returns>The message, or <see langword="null"/> if it has not been received.</returns>
    public ForumPost? FindMessage(
        Id groupId,
        Id threadId,
        Id messageId) =>
        KnownMessages.GetValueOrDefault(
            new ForumMessageKey(groupId, threadId, messageId));

    /// <summary>Gets a page of the forum list.</summary>
    /// <param name="listCode">The forum list.</param>
    /// <param name="startIndex">The index of the first forum on the page.</param>
    /// <returns>The page, or <see langword="null"/> if it has not been received.</returns>
    public ForumsList? FindForumPage(
        ForumListCode listCode,
        int startIndex = 0) =>
        ForumPages.GetValueOrDefault(
            new ForumListPageKey(listCode, startIndex));

    /// <summary>Gets a page of threads in a forum.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="startIndex">The index of the first thread on the page.</param>
    /// <returns>The page, or <see langword="null"/> if it has not been received.</returns>
    public ForumThreads? FindThreadPage(
        Id groupId,
        int startIndex = 0) =>
        ThreadPages.GetValueOrDefault(
            new ForumThreadPageKey(groupId, startIndex));

    /// <summary>Gets a page of messages in a forum thread.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <param name="startIndex">The index of the first message on the page.</param>
    /// <returns>The page, or <see langword="null"/> if it has not been received.</returns>
    public ThreadMessages? FindMessagePage(
        Id groupId,
        Id threadId,
        int startIndex = 0) =>
        MessagePages.GetValueOrDefault(
            new ForumMessagePageKey(groupId, threadId, startIndex));

    private static IReadOnlyDictionary<TKey, TValue>
        EmptyMap<TKey, TValue>() where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(
            new Dictionary<TKey, TValue>());
}

/// <summary>Manages the group forum data received in the current session.</summary>
/// <remarks>
/// <para>All members are safe to call from any thread.</para>
/// <para>
/// Every received forum message is merged into a new immutable <see cref="ForumSnapshot"/>. After each
/// change the specific event is raised first, then <see cref="SnapshotChanged"/>. Delivery stops when a
/// newer snapshot is published while listeners are running. The state is cleared when the hotel
/// connection closes.
/// </para>
/// </remarks>
public sealed class ForumManager : GameStateManager
{
    private readonly ManagerStateGate _state = new();
    private readonly Dictionary<ForumListPageKey, ForumsList> _forum_pages = [];
    private readonly Dictionary<Id, ForumSummary> _forums = [];
    private readonly Dictionary<Id, ForumDetails> _details = [];
    private readonly Dictionary<ForumThreadPageKey, ForumThreads> _thread_pages = [];
    private readonly Dictionary<ForumThreadKey, ForumThread> _threads = [];
    private readonly Dictionary<ForumMessagePageKey, ThreadMessages> _message_pages = [];
    private readonly Dictionary<ForumMessageKey, ForumPost> _messages = [];
    private ForumSnapshot _snapshot = ForumSnapshot.Empty;
    private int? _unread_forums_count;

    /// <summary>Gets the current snapshot of the forum data.</summary>
    public ForumSnapshot Snapshot => Volatile.Read(ref _snapshot);
    internal long SessionGeneration => CurrentStateGeneration;
    internal Session? Session => CurrentSession;

    /// <summary>Occurs when the forum data changes.</summary>
    /// <remarks>
    /// The argument is the new value of <see cref="Snapshot"/>. Raised after the event that describes
    /// the specific change, including <see cref="ResetCompleted"/>.
    /// </remarks>
    public event Action<ForumSnapshot>? SnapshotChanged;
    /// <summary>Occurs when the server sends the details of a forum.</summary>
    /// <remarks>The argument is the received details. The forum summary they contain is also stored.</remarks>
    public event Action<ForumDetails>? DetailsChanged;
    /// <summary>Occurs when the server sends a page of the forum list.</summary>
    /// <remarks>The argument is the received page.</remarks>
    public event Action<ForumsList>? ForumPageReceived;
    /// <summary>Occurs when the server sends a page of threads in a forum.</summary>
    /// <remarks>The argument is the received page.</remarks>
    public event Action<ForumThreads>? ThreadPageReceived;
    /// <summary>Occurs when the server sends a page of messages in a forum thread.</summary>
    /// <remarks>The argument is the received page.</remarks>
    public event Action<ThreadMessages>? MessagePageReceived;
    /// <summary>Occurs when the server reports a created or updated forum thread.</summary>
    /// <remarks>The arguments are the id of the group that owns the forum and the thread.</remarks>
    public event Action<Id, ForumThread>? ThreadChanged;
    /// <summary>Occurs when the server reports a created or updated forum message.</summary>
    /// <remarks>The arguments are the group id, the thread id and the message.</remarks>
    public event Action<Id, Id, ForumPost>? MessageChanged;
    /// <summary>Occurs when the server sends the number of forums with unread messages.</summary>
    /// <remarks>The argument is the received count.</remarks>
    public event Action<int>? UnreadForumsCountChanged;
    /// <summary>Occurs when the forum data is cleared after the hotel connection closes.</summary>
    public event Action? ResetCompleted;

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        OnIncoming(
            MessageContracts.Forums.Stats,
            (message, generation) =>
                StoreDetails(message.Data, generation));

        OnIncoming(
            MessageContracts.Forums.List,
            StoreForumPage);

        OnIncoming(
            MessageContracts.Forums.Threads,
            StoreThreadPage);

        OnIncoming(
            MessageContracts.Forums.Messages,
            StoreMessagePage);

        OnIncoming(
            MessageContracts.Forums.ThreadCreated,
            (message, generation) =>
                StoreThread(
                    message.GroupId,
                    message.Thread,
                    generation));

        OnIncoming(
            MessageContracts.Forums.MessageCreated,
            (message, generation) =>
                StoreMessage(
                    message.GroupId,
                    message.ThreadId,
                    message.Message ??
                        throw new InvalidDataException(
                            "Incoming PostMessage did not contain a forum post."),
                    generation));

        OnIncoming(
            MessageContracts.Forums.ThreadUpdated,
            (message, generation) =>
                StoreThread(
                    message.GroupId,
                    message.Thread ??
                        throw new InvalidDataException(
                            "Incoming UpdateThread did not contain a forum thread."),
                    generation));

        OnIncoming(
            MessageContracts.Forums.MessageUpdated,
            (message, generation) =>
                StoreMessage(
                    message.GroupId,
                    message.ThreadId,
                    message.Message,
                    generation));

        OnIncoming(
            MessageContracts.Forums.UnreadCount,
            StoreUnreadForumsCount);
    }

    /// <summary>Gets the summary of a forum from the current snapshot.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <returns>The forum summary, or <see langword="null"/> if it has not been received.</returns>
    public ForumSummary? FindForum(Id groupId) =>
        Snapshot.FindForum(groupId);

    /// <summary>Gets the details of a forum from the current snapshot.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <returns>The forum details, or <see langword="null"/> if they have not been received.</returns>
    public ForumDetails? FindDetails(Id groupId) =>
        Snapshot.FindDetails(groupId);

    /// <summary>Gets a forum thread from the current snapshot.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <returns>The thread, or <see langword="null"/> if it has not been received.</returns>
    public ForumThread? FindThread(Id groupId, Id threadId) =>
        Snapshot.FindThread(groupId, threadId);

    /// <summary>Gets a forum message from the current snapshot.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread that contains the message.</param>
    /// <param name="messageId">The id of the message.</param>
    /// <returns>The message, or <see langword="null"/> if it has not been received.</returns>
    public ForumPost? FindMessage(
        Id groupId,
        Id threadId,
        Id messageId) =>
        Snapshot.FindMessage(groupId, threadId, messageId);

    /// <summary>Requests the details of a forum from the server.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <remarks>
    /// The request is sent without waiting for a response. The response raises
    /// <see cref="DetailsChanged"/>.
    /// </remarks>
    public void RequestStats(Id groupId) =>
        SendMessage(
            MessageContracts.Forums.StatsRequest,
            new GetForumStats(groupId));

    /// <summary>Requests a page of the forum list from the server.</summary>
    /// <param name="listCode">The forum list to request.</param>
    /// <param name="startIndex">The index of the first forum on the page.</param>
    /// <param name="maxCount">The maximum number of forums on the page.</param>
    /// <remarks>
    /// The request is sent without waiting for a response. The response raises
    /// <see cref="ForumPageReceived"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="startIndex"/> is negative or <paramref name="maxCount"/> is zero or negative.
    /// </exception>
    public void RequestForums(
        ForumListCode listCode,
        int startIndex = 0,
        int maxCount = 20)
    {
        ValidatePage(startIndex, maxCount);
        SendMessage(
            MessageContracts.Forums.ListRequest,
            new GetForumsList(listCode, startIndex, maxCount));
    }

    /// <summary>Requests a page of threads in a forum from the server.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="startIndex">The index of the first thread on the page.</param>
    /// <param name="maxCount">The maximum number of threads on the page.</param>
    /// <remarks>
    /// The request is sent without waiting for a response. The response raises
    /// <see cref="ThreadPageReceived"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="startIndex"/> is negative or <paramref name="maxCount"/> is zero or negative.
    /// </exception>
    public void RequestThreads(
        Id groupId,
        int startIndex = 0,
        int maxCount = 20)
    {
        ValidatePage(startIndex, maxCount);
        SendMessage(
            MessageContracts.Forums.ThreadsRequest,
            new GetForumThreads(groupId, startIndex, maxCount));
    }

    /// <summary>Requests a page of messages in a forum thread from the server.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <param name="startIndex">The index of the first message on the page.</param>
    /// <param name="maxCount">The maximum number of messages on the page.</param>
    /// <remarks>
    /// The request is sent without waiting for a response. The response raises
    /// <see cref="MessagePageReceived"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="startIndex"/> is negative or <paramref name="maxCount"/> is zero or negative.
    /// </exception>
    public void RequestMessages(
        Id groupId,
        Id threadId,
        int startIndex = 0,
        int maxCount = 20)
    {
        ValidatePage(startIndex, maxCount);
        SendMessage(
            MessageContracts.Forums.MessagesRequest,
            new GetForumThreadMessages(
                groupId,
                threadId,
                startIndex,
                maxCount));
    }

    /// <summary>Requests a forum thread from the server.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <remarks>
    /// The request is sent without waiting for a response. The response raises
    /// <see cref="ThreadChanged"/>.
    /// </remarks>
    public void RequestThread(Id groupId, Id threadId) =>
        SendMessage(
            MessageContracts.Forums.ThreadRequest,
            new GetForumThread(groupId, threadId));

    /// <summary>Requests the number of forums with unread messages from the server.</summary>
    /// <remarks>
    /// The request is sent without waiting for a response. The response raises
    /// <see cref="UnreadForumsCountChanged"/>.
    /// </remarks>
    public void RequestUnreadForumsCount() =>
        SendMessage(
            MessageContracts.Forums.UnreadCountRequest,
            new GetUnreadForumsCount());

    /// <summary>Posts a message to a forum, creating a new thread when <paramref name="threadId"/> is 0.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread to reply to, or 0 to create a new thread.</param>
    /// <param name="subject">The subject of a new thread, or an empty string for a reply.</param>
    /// <param name="messageText">The text of the message.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="subject"/> or <paramref name="messageText"/> is <see langword="null"/>.
    /// </exception>
    public void Post(
        Id groupId,
        Id threadId,
        string subject,
        string messageText)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(messageText);
        SendMessage(
            MessageContracts.Forums.Post,
            new PostMessage(
                groupId,
                threadId,
                subject,
                messageText));
    }

    /// <summary>Creates a new thread in a forum.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="subject">The subject of the thread.</param>
    /// <param name="messageText">The text of the first message.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="subject"/> or <paramref name="messageText"/> is <see langword="null"/>.
    /// </exception>
    public void CreateThread(
        Id groupId,
        string subject,
        string messageText) =>
        Post(groupId, 0, subject, messageText);

    /// <summary>Replies to a forum thread.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <param name="messageText">The text of the reply.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageText"/> is <see langword="null"/>.</exception>
    public void Reply(
        Id groupId,
        Id threadId,
        string messageText) =>
        Post(groupId, threadId, "", messageText);

    /// <summary>Sets the moderation state of a forum thread.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <param name="state">
    /// The moderation state to set, using the values of <see cref="ForumThread.State"/>.
    /// </param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    public void ModerateThread(
        Id groupId,
        Id threadId,
        int state) =>
        SendMessage(
            MessageContracts.Forums.ThreadModerate,
            new ModerateForumThread(groupId, threadId, state));

    /// <summary>Sets the moderation state of a forum message.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread that contains the message.</param>
    /// <param name="messageId">The id of the message.</param>
    /// <param name="state">The moderation state to set, using the values of <see cref="ForumPost.State"/>.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    public void ModerateMessage(
        Id groupId,
        Id threadId,
        Id messageId,
        int state) =>
        SendMessage(
            MessageContracts.Forums.MessageModerate,
            new ModerateForumMessage(
                groupId,
                threadId,
                messageId,
                state));

    /// <summary>Updates the permission levels of a forum.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="readLevel">The permission level required to read the forum.</param>
    /// <param name="postMessageLevel">The permission level required to reply to threads.</param>
    /// <param name="postThreadLevel">The permission level required to create threads.</param>
    /// <param name="moderateLevel">The permission level required to moderate the forum.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    public void UpdateSettings(
        Id groupId,
        int readLevel,
        int postMessageLevel,
        int postThreadLevel,
        int moderateLevel) =>
        SendMessage(
            MessageContracts.Forums.SettingsUpdate,
            new UpdateForumSettings(
                groupId,
                readLevel,
                postMessageLevel,
                postThreadLevel,
                moderateLevel));

    /// <summary>Updates the read markers of one or more forums.</summary>
    /// <param name="markers">The read markers to send. The list is copied before sending.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markers"/> is <see langword="null"/>.</exception>
    public void UpdateReadMarkers(
        IReadOnlyList<ForumReadMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        ForumReadMarker[] snapshot = markers.ToArray();
        SendMessage(
            MessageContracts.Forums.ReadMarkersUpdate,
            new UpdateForumReadMarkers(snapshot));
    }

    /// <summary>Sets whether a forum thread is sticky and whether it is locked.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <param name="isSticky">Whether the thread is pinned to the top of the forum.</param>
    /// <param name="isLocked">Whether the thread is closed to new replies.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    public void UpdateThread(
        Id groupId,
        Id threadId,
        bool isSticky,
        bool isLocked) =>
        SendMessage(
            MessageContracts.Forums.ThreadUpdate,
            new UpdateThread(
                groupId,
                threadId,
                isSticky,
                isLocked));

    /// <summary>Reports a forum thread to the moderators.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread.</param>
    /// <param name="categoryId">The id of the report category.</param>
    /// <param name="report">The text of the report.</param>
    /// <param name="firstContext">The first context string of the Flash report message.</param>
    /// <param name="secondContext">The second context string of the Flash report message.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when a string argument is <see langword="null"/>.</exception>
    public void ReportThread(
        Id groupId,
        Id threadId,
        int categoryId,
        string report,
        string firstContext = "",
        string secondContext = "")
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(firstContext);
        ArgumentNullException.ThrowIfNull(secondContext);
        SendMessage(
            MessageContracts.Forums.ThreadReport,
            new CallForHelpFromForumThread(
                groupId,
                threadId,
                categoryId,
                report,
                firstContext,
                secondContext));
    }

    /// <summary>Reports a forum message to the moderators.</summary>
    /// <param name="groupId">The id of the group that owns the forum.</param>
    /// <param name="threadId">The id of the thread that contains the message.</param>
    /// <param name="messageId">The id of the message.</param>
    /// <param name="categoryId">The id of the report category.</param>
    /// <param name="report">The text of the report.</param>
    /// <param name="firstContext">The first context string of the Flash report message.</param>
    /// <param name="secondContext">The second context string of the Flash report message.</param>
    /// <remarks>The request is sent without waiting for a response.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when a string argument is <see langword="null"/>.</exception>
    public void ReportMessage(
        Id groupId,
        Id threadId,
        Id messageId,
        int categoryId,
        string report,
        string firstContext = "",
        string secondContext = "")
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(firstContext);
        ArgumentNullException.ThrowIfNull(secondContext);
        SendMessage(
            MessageContracts.Forums.MessageReport,
            new CallForHelpFromForumMessage(
                groupId,
                threadId,
                messageId,
                categoryId,
                report,
                firstContext,
                secondContext));
    }

    /// <inheritdoc/>
    protected override void Reset()
    {
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Reset(
            CurrentStateGeneration,
            () =>
            {
                _forum_pages.Clear();
                _forums.Clear();
                _details.Clear();
                _thread_pages.Clear();
                _threads.Clear();
                _message_pages.Clear();
                _messages.Clear();
                _unread_forums_count = null;
                snapshot = PublishSnapshot();
            },
            () => Publish(snapshot, ResetCompleted, listener => listener()));
    }

    private void StoreDetails(
        ForumDetails details,
        long generation)
    {
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _details[details.GroupId] = details;
                _forums[details.GroupId] = details.Summary;
                snapshot = PublishSnapshot();
            },
            () => Publish(snapshot, DetailsChanged, listener => listener(details)));
    }

    private void StoreForumPage(
        ForumsList message,
        long generation)
    {
        ForumsList page = Freeze(message);
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _forum_pages[
                    new ForumListPageKey(
                        page.ListCode,
                        page.StartIndex)] = page;
                foreach (ForumSummary forum in page.Forums)
                    _forums[forum.GroupId] = forum;
                snapshot = PublishSnapshot();
            },
            () => Publish(snapshot, ForumPageReceived, listener => listener(page)));
    }

    private void StoreThreadPage(
        ForumThreads message,
        long generation)
    {
        ForumThreads page = Freeze(message);
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _thread_pages[
                    new ForumThreadPageKey(
                        page.GroupId,
                        page.StartIndex)] = page;
                foreach (ForumThread thread in page.Threads)
                    _threads[
                        new ForumThreadKey(
                            page.GroupId,
                            thread.ThreadId)] = thread;
                snapshot = PublishSnapshot();
            },
            () => Publish(snapshot, ThreadPageReceived, listener => listener(page)));
    }

    private void StoreMessagePage(
        ThreadMessages message,
        long generation)
    {
        ThreadMessages page = Freeze(message);
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _message_pages[
                    new ForumMessagePageKey(
                        page.GroupId,
                        page.ThreadId,
                        page.StartIndex)] = page;
                foreach (ForumPost post in page.Messages)
                    _messages[
                        new ForumMessageKey(
                            page.GroupId,
                            page.ThreadId,
                            post.MessageId)] = post;
                snapshot = PublishSnapshot();
            },
            () => Publish(snapshot, MessagePageReceived, listener => listener(page)));
    }

    private void StoreThread(
        Id group_id,
        ForumThread thread,
        long generation)
    {
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _threads[
                    new ForumThreadKey(
                        group_id,
                        thread.ThreadId)] = thread;
                snapshot = PublishSnapshot();
            },
            () => Publish(
                snapshot,
                ThreadChanged,
                listener => listener(group_id, thread)));
    }

    private void StoreMessage(
        Id group_id,
        Id thread_id,
        ForumPost message,
        long generation)
    {
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _messages[
                    new ForumMessageKey(
                        group_id,
                        thread_id,
                        message.MessageId)] = message;
                snapshot = PublishSnapshot();
            },
            () => Publish(
                snapshot,
                MessageChanged,
                listener => listener(group_id, thread_id, message)));
    }

    private void StoreUnreadForumsCount(
        UnreadForumsCount message,
        long generation)
    {
        ForumSnapshot snapshot = ForumSnapshot.Empty;
        _state.Commit(
            generation,
            () =>
            {
                _unread_forums_count = message.Count;
                snapshot = PublishSnapshot();
            },
            () => Publish(
                snapshot,
                UnreadForumsCountChanged,
                listener => listener(message.Count)));
    }

    private ForumSnapshot PublishSnapshot()
    {
        var snapshot = new ForumSnapshot(
            ReadOnly(_forum_pages),
            ReadOnly(_forums),
            ReadOnly(_details),
            ReadOnly(_thread_pages),
            ReadOnly(_threads),
            ReadOnly(_message_pages),
            ReadOnly(_messages),
            _unread_forums_count);
        Volatile.Write(ref _snapshot, snapshot);
        return snapshot;
    }

    private void Publish<TDelegate>(
        ForumSnapshot snapshot,
        TDelegate? legacy,
        Action<TDelegate> invoke)
        where TDelegate : Delegate
    {
        List<Exception>? failures = null;
        PublishListeners(snapshot, legacy, invoke, ref failures);
        if (ReferenceEquals(Snapshot, snapshot))
        {
            PublishListeners(
                snapshot,
                SnapshotChanged,
                listener => listener(snapshot),
                ref failures);
        }
        if (failures is { Count: 1 })
            throw failures[0];
        if (failures is { Count: > 1 })
            throw new AggregateException(failures);
    }

    private void PublishListeners<TDelegate>(
        ForumSnapshot snapshot,
        TDelegate? listeners,
        Action<TDelegate> invoke,
        ref List<Exception>? failures)
        where TDelegate : Delegate
    {
        if (listeners is null)
            return;
        foreach (TDelegate listener in listeners.GetInvocationList().Cast<TDelegate>())
        {
            if (!ReferenceEquals(Snapshot, snapshot))
                return;
            try
            {
                invoke(listener);
            }
            catch (Exception error)
            {
                (failures ??= []).Add(error);
            }
        }
    }

    private static void ValidatePage(
        int start_index,
        int max_count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start_index);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max_count);
    }

    private static ForumsList Freeze(ForumsList message) =>
        message with
        {
            Forums = Array.AsReadOnly(message.Forums.ToArray())
        };

    private static ForumThreads Freeze(ForumThreads message) =>
        message with
        {
            Threads = Array.AsReadOnly(message.Threads.ToArray())
        };

    private static ThreadMessages Freeze(ThreadMessages message) =>
        message with
        {
            Messages = Array.AsReadOnly(message.Messages.ToArray())
        };

    private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(
        Dictionary<TKey, TValue> values) where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(
            new Dictionary<TKey, TValue>(values));
}
