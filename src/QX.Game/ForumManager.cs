using ForumThreadData = Qx.Model.Forums.ForumThread;
using Qx.Model.Forums;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using System.Collections.ObjectModel;

namespace Qx.Game;

public readonly record struct ForumListPageKey(
    ForumListCode ListCode,
    int StartIndex);

public readonly record struct ForumThreadPageKey(
    Id GroupId,
    int StartIndex);

public readonly record struct ForumMessagePageKey(
    Id GroupId,
    Id ThreadId,
    int StartIndex);

public readonly record struct ForumThreadKey(
    Id GroupId,
    Id ThreadId);

public readonly record struct ForumMessageKey(
    Id GroupId,
    Id ThreadId,
    Id MessageId);

public sealed record ForumSnapshot(
    IReadOnlyDictionary<ForumListPageKey, ForumsList> ForumPages,
    IReadOnlyDictionary<Id, ForumSummary> KnownForums,
    IReadOnlyDictionary<Id, ForumDetails> ForumDetails,
    IReadOnlyDictionary<ForumThreadPageKey, ForumThreads> ThreadPages,
    IReadOnlyDictionary<ForumThreadKey, ForumThreadData> KnownThreads,
    IReadOnlyDictionary<ForumMessagePageKey, ThreadMessages> MessagePages,
    IReadOnlyDictionary<ForumMessageKey, ForumPost> KnownMessages,
    int? UnreadForumsCount)
{
    public static ForumSnapshot Empty { get; } = new(
        EmptyMap<ForumListPageKey, ForumsList>(),
        EmptyMap<Id, ForumSummary>(),
        EmptyMap<Id, ForumDetails>(),
        EmptyMap<ForumThreadPageKey, ForumThreads>(),
        EmptyMap<ForumThreadKey, ForumThreadData>(),
        EmptyMap<ForumMessagePageKey, ThreadMessages>(),
        EmptyMap<ForumMessageKey, ForumPost>(),
        null);

    public ForumSummary? FindForum(Id group_id) =>
        KnownForums.GetValueOrDefault(group_id);

    public ForumDetails? FindDetails(Id group_id) =>
        ForumDetails.GetValueOrDefault(group_id);

    public ForumThreadData? FindThread(Id group_id, Id thread_id) =>
        KnownThreads.GetValueOrDefault(new ForumThreadKey(group_id, thread_id));

    public ForumPost? FindMessage(
        Id group_id,
        Id thread_id,
        Id message_id) =>
        KnownMessages.GetValueOrDefault(
            new ForumMessageKey(group_id, thread_id, message_id));

    public ForumsList? FindForumPage(
        ForumListCode list_code,
        int start_index = 0) =>
        ForumPages.GetValueOrDefault(
            new ForumListPageKey(list_code, start_index));

    public ForumThreads? FindThreadPage(
        Id group_id,
        int start_index = 0) =>
        ThreadPages.GetValueOrDefault(
            new ForumThreadPageKey(group_id, start_index));

    public ThreadMessages? FindMessagePage(
        Id group_id,
        Id thread_id,
        int start_index = 0) =>
        MessagePages.GetValueOrDefault(
            new ForumMessagePageKey(group_id, thread_id, start_index));

    private static IReadOnlyDictionary<TKey, TValue>
        EmptyMap<TKey, TValue>() where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(
            new Dictionary<TKey, TValue>());
}

public sealed class ForumManager : GameStateManager
{
    private readonly ManagerStateGate _state = new();
    private readonly Dictionary<ForumListPageKey, ForumsList> _forum_pages = [];
    private readonly Dictionary<Id, ForumSummary> _forums = [];
    private readonly Dictionary<Id, ForumDetails> _details = [];
    private readonly Dictionary<ForumThreadPageKey, ForumThreads> _thread_pages = [];
    private readonly Dictionary<ForumThreadKey, ForumThreadData> _threads = [];
    private readonly Dictionary<ForumMessagePageKey, ThreadMessages> _message_pages = [];
    private readonly Dictionary<ForumMessageKey, ForumPost> _messages = [];
    private ForumSnapshot _snapshot = ForumSnapshot.Empty;
    private int? _unread_forums_count;

    public ForumSnapshot Snapshot => Volatile.Read(ref _snapshot);
    internal long SessionGeneration => CurrentStateGeneration;
    internal Session? Session => CurrentSession;

    public event Action<ForumSnapshot>? SnapshotChanged;
    public event Action<ForumDetails>? DetailsChanged;
    public event Action<ForumsList>? ForumPageReceived;
    public event Action<ForumThreads>? ThreadPageReceived;
    public event Action<ThreadMessages>? MessagePageReceived;
    public event Action<Id, ForumThreadData>? ThreadChanged;
    public event Action<Id, Id, ForumPost>? MessageChanged;
    public event Action<int>? UnreadForumsCountChanged;
    public event Action? ResetCompleted;

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

    public ForumSummary? FindForum(Id group_id) =>
        Snapshot.FindForum(group_id);

    public ForumDetails? FindDetails(Id group_id) =>
        Snapshot.FindDetails(group_id);

    public ForumThreadData? FindThread(Id group_id, Id thread_id) =>
        Snapshot.FindThread(group_id, thread_id);

    public ForumPost? FindMessage(
        Id group_id,
        Id thread_id,
        Id message_id) =>
        Snapshot.FindMessage(group_id, thread_id, message_id);

    public void RequestStats(Id group_id) =>
        SendMessage(
            MessageContracts.Forums.StatsRequest,
            new GetForumStats(group_id));

    public void RequestForums(
        ForumListCode list_code,
        int start_index = 0,
        int max_count = 20)
    {
        ValidatePage(start_index, max_count);
        SendMessage(
            MessageContracts.Forums.ListRequest,
            new GetForumsList(list_code, start_index, max_count));
    }

    public void RequestThreads(
        Id group_id,
        int start_index = 0,
        int max_count = 20)
    {
        ValidatePage(start_index, max_count);
        SendMessage(
            MessageContracts.Forums.ThreadsRequest,
            new GetForumThreads(group_id, start_index, max_count));
    }

    public void RequestMessages(
        Id group_id,
        Id thread_id,
        int start_index = 0,
        int max_count = 20)
    {
        ValidatePage(start_index, max_count);
        SendMessage(
            MessageContracts.Forums.MessagesRequest,
            new GetForumThreadMessages(
                group_id,
                thread_id,
                start_index,
                max_count));
    }

    public void RequestThread(Id group_id, Id thread_id) =>
        SendMessage(
            MessageContracts.Forums.ThreadRequest,
            new GetForumThread(group_id, thread_id));

    public void RequestUnreadForumsCount() =>
        SendMessage(
            MessageContracts.Forums.UnreadCountRequest,
            new GetUnreadForumsCount());

    public void Post(
        Id group_id,
        Id thread_id,
        string subject,
        string message_text)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(message_text);
        SendMessage(
            MessageContracts.Forums.Post,
            new PostMessage(
                group_id,
                thread_id,
                subject,
                message_text));
    }

    public void CreateThread(
        Id group_id,
        string subject,
        string message_text) =>
        Post(group_id, 0, subject, message_text);

    public void Reply(
        Id group_id,
        Id thread_id,
        string message_text) =>
        Post(group_id, thread_id, "", message_text);

    public void ModerateThread(
        Id group_id,
        Id thread_id,
        int state) =>
        SendMessage(
            MessageContracts.Forums.ThreadModerate,
            new ModerateForumThread(group_id, thread_id, state));

    public void ModerateMessage(
        Id group_id,
        Id thread_id,
        Id message_id,
        int state) =>
        SendMessage(
            MessageContracts.Forums.MessageModerate,
            new ModerateForumMessage(
                group_id,
                thread_id,
                message_id,
                state));

    public void UpdateSettings(
        Id group_id,
        int read_level,
        int post_message_level,
        int post_thread_level,
        int moderate_level) =>
        SendMessage(
            MessageContracts.Forums.SettingsUpdate,
            new UpdateForumSettings(
                group_id,
                read_level,
                post_message_level,
                post_thread_level,
                moderate_level));

    public void UpdateReadMarkers(
        IReadOnlyList<ForumReadMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        ForumReadMarker[] snapshot = markers.ToArray();
        SendMessage(
            MessageContracts.Forums.ReadMarkersUpdate,
            new UpdateForumReadMarkers(snapshot));
    }

    public void UpdateThread(
        Id group_id,
        Id thread_id,
        bool is_sticky,
        bool is_locked) =>
        SendMessage(
            MessageContracts.Forums.ThreadUpdate,
            new UpdateThread(
                group_id,
                thread_id,
                is_sticky,
                is_locked));

    public void ReportThread(
        Id group_id,
        Id thread_id,
        int category_id,
        string report,
        string first_context = "",
        string second_context = "")
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(first_context);
        ArgumentNullException.ThrowIfNull(second_context);
        SendMessage(
            MessageContracts.Forums.ThreadReport,
            new CallForHelpFromForumThread(
                group_id,
                thread_id,
                category_id,
                report,
                first_context,
                second_context));
    }

    public void ReportMessage(
        Id group_id,
        Id thread_id,
        Id message_id,
        int category_id,
        string report,
        string first_context = "",
        string second_context = "")
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(first_context);
        ArgumentNullException.ThrowIfNull(second_context);
        SendMessage(
            MessageContracts.Forums.MessageReport,
            new CallForHelpFromForumMessage(
                group_id,
                thread_id,
                message_id,
                category_id,
                report,
                first_context,
                second_context));
    }

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
                foreach (ForumThreadData thread in page.Threads)
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
        ForumThreadData thread,
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
