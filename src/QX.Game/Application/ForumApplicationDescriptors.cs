using System.Diagnostics.CodeAnalysis;
using Qx.Game.Protocol;
using Qx.Model;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class ForumApplicationDescriptors
{
    private static readonly ApplicationExposure dispatch_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static readonly ApplicationDescriptor State = new(
        ApplicationMemberIds.ForumsState,
        "Forum state",
        "Reads the immutable forum cache for the active or disconnected session.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(ForumStateRequest),
        typeof(ForumStateView),
        [SnapshotRevisionParameter()],
        stateEffects: [ReadEffect()],
        toolHints: QueryHints());

    public static readonly ApplicationDescriptor ListRefresh = Refresh(
        ApplicationMemberIds.ForumsListRefresh,
        "Refresh forum list",
        "Requests a page of the forum directory and waits for it.",
        typeof(ForumListRefreshRequest),
        typeof(ForumListRefreshResult),
        MessageKeys.Forums.ListRequest,
        MessageKeys.Forums.List,
        ListParameters());

    public static readonly ApplicationDescriptor DetailsRequest = Request(
        ApplicationMemberIds.ForumDetailsRequest,
        "Request forum details",
        typeof(ForumDetailsRequest),
        MessageKeys.Forums.StatsRequest,
        [GroupParameter(), GenerationParameter()]);

    public static readonly ApplicationDescriptor ListRequest = Request(
        ApplicationMemberIds.ForumsListRequest,
        "Request forum list",
        typeof(ForumListRequest),
        MessageKeys.Forums.ListRequest,
        [.. ListParameters().Where(parameter => parameter.Name != "timeout_milliseconds")]);

    public static readonly ApplicationDescriptor ThreadsRequest = Request(
        ApplicationMemberIds.ForumThreadsRequest,
        "Request forum threads",
        typeof(ForumThreadsRequest),
        MessageKeys.Forums.ThreadsRequest,
        [.. ThreadPageParameters().Where(parameter => parameter.Name != "timeout_milliseconds")]);

    public static readonly ApplicationDescriptor MessagesRequest = Request(
        ApplicationMemberIds.ForumMessagesRequest,
        "Request forum messages",
        typeof(ForumMessagesRequest),
        MessageKeys.Forums.MessagesRequest,
        [.. MessagePageParameters().Where(parameter => parameter.Name != "timeout_milliseconds")]);

    public static readonly ApplicationDescriptor ThreadRequest = Request(
        ApplicationMemberIds.ForumThreadRequest,
        "Request forum thread",
        typeof(ForumThreadRequest),
        MessageKeys.Forums.ThreadRequest,
        [GroupParameter(), ThreadParameter(), GenerationParameter()]);

    public static readonly ApplicationDescriptor UnreadRequest = Request(
        ApplicationMemberIds.ForumsUnreadRequest,
        "Request unread forum count",
        typeof(ForumUnreadRequest),
        MessageKeys.Forums.UnreadCountRequest,
        [GenerationParameter()]);

    public static readonly ApplicationDescriptor ThreadsRefresh = Refresh(
        ApplicationMemberIds.ForumThreadsRefresh,
        "Refresh forum threads",
        "Requests a page of forum threads and waits for it.",
        typeof(ForumThreadsRefreshRequest),
        typeof(ForumThreadsRefreshResult),
        MessageKeys.Forums.ThreadsRequest,
        MessageKeys.Forums.Threads,
        ThreadPageParameters());

    public static readonly ApplicationDescriptor MessagesRefresh = Refresh(
        ApplicationMemberIds.ForumMessagesRefresh,
        "Refresh forum messages",
        "Requests a page of thread messages and waits for it.",
        typeof(ForumMessagesRefreshRequest),
        typeof(ForumMessagesRefreshResult),
        MessageKeys.Forums.MessagesRequest,
        MessageKeys.Forums.Messages,
        MessagePageParameters());

    public static readonly ApplicationDescriptor DetailsRefresh = Refresh(
        ApplicationMemberIds.ForumDetailsRefresh,
        "Refresh forum details",
        "Requests the details of a group forum and waits for them.",
        typeof(ForumDetailsRefreshRequest),
        typeof(ForumDetailsRefreshResult),
        MessageKeys.Forums.StatsRequest,
        MessageKeys.Forums.Stats,
        [GroupParameter(), TimeoutParameter(), GenerationParameter()]);

    public static readonly ApplicationDescriptor ThreadRefresh = Refresh(
        ApplicationMemberIds.ForumThreadRefresh,
        "Refresh forum thread",
        "Requests a single forum thread and waits for it.",
        typeof(ForumThreadRefreshRequest),
        typeof(ForumThreadRefreshResult),
        MessageKeys.Forums.ThreadRequest,
        MessageKeys.Forums.ThreadUpdated,
        [GroupParameter(), ThreadParameter(), TimeoutParameter(), GenerationParameter()]);

    public static readonly ApplicationDescriptor UnreadRefresh = Refresh(
        ApplicationMemberIds.ForumsUnreadRefresh,
        "Refresh unread forum count",
        "Requests the number of forums with unread messages and waits for it.",
        typeof(ForumUnreadRefreshRequest),
        typeof(ForumUnreadRefreshResult),
        MessageKeys.Forums.UnreadCountRequest,
        MessageKeys.Forums.UnreadCount,
        [TimeoutParameter(), GenerationParameter()]);

    public static readonly ApplicationDescriptor Post = Action(
        ApplicationMemberIds.ForumsPost,
        "Post forum message",
        "Posts a forum message or starts a new thread.",
        typeof(ForumPostActionRequest),
        MessageKeys.Forums.Post,
        [
            GroupParameter(),
            ThreadParameter(),
            StringParameter("subject", "Forum thread subject."),
            StringParameter("message_text", "Forum post body."),
            GenerationParameter()
        ],
        true);

    public static readonly ApplicationDescriptor ThreadModerate = Action(
        ApplicationMemberIds.ForumThreadModerate,
        "Moderate forum thread",
        "Hides or restores a forum thread.",
        typeof(ForumThreadModerationRequest),
        MessageKeys.Forums.ThreadModerate,
        [GroupParameter(), ThreadParameter(), StateParameter(), GenerationParameter()],
        true);

    public static readonly ApplicationDescriptor MessageModerate = Action(
        ApplicationMemberIds.ForumMessageModerate,
        "Moderate forum message",
        "Hides or restores a forum message.",
        typeof(ForumMessageModerationRequest),
        MessageKeys.Forums.MessageModerate,
        [
            GroupParameter(),
            ThreadParameter(),
            MessageParameter(),
            StateParameter(),
            GenerationParameter()
        ],
        true);

    public static readonly ApplicationDescriptor SettingsUpdate = Action(
        ApplicationMemberIds.ForumSettingsUpdate,
        "Update forum settings",
        "Changes the permission levels of a group forum.",
        typeof(ForumSettingsUpdateRequest),
        MessageKeys.Forums.SettingsUpdate,
        [
            GroupParameter(),
            IntegerParameter("read_level", "Forum read permission level."),
            IntegerParameter("post_message_level", "Forum reply permission level."),
            IntegerParameter("post_thread_level", "Forum thread permission level."),
            IntegerParameter("moderate_level", "Forum moderation permission level."),
            GenerationParameter()
        ],
        true);

    public static readonly ApplicationDescriptor ReadMarkersUpdate = Action(
        ApplicationMemberIds.ForumReadMarkersUpdate,
        "Update forum read markers",
        "Updates the read markers of group forums.",
        typeof(ForumReadMarkersUpdateRequest),
        MessageKeys.Forums.ReadMarkersUpdate,
        [
            new("markers", typeof(IReadOnlyList<ForumReadMarker>), true, null,
                "Forum read markers.", new(MaxItems: ushort.MaxValue)),
            GenerationParameter()
        ],
        false);

    public static readonly ApplicationDescriptor ThreadUpdate = Action(
        ApplicationMemberIds.ForumThreadUpdate,
        "Update forum thread",
        "Changes the sticky and locked flags of a forum thread.",
        typeof(ForumThreadUpdateRequest),
        MessageKeys.Forums.ThreadUpdate,
        [
            GroupParameter(),
            ThreadParameter(),
            new("is_sticky", typeof(bool), true, null, "Whether the thread is sticky."),
            new("is_locked", typeof(bool), true, null, "Whether the thread is locked."),
            GenerationParameter()
        ],
        true);

    public static readonly ApplicationDescriptor ThreadReport = Action(
        ApplicationMemberIds.ForumThreadReport,
        "Report forum thread",
        "Reports a forum thread to the moderators.",
        typeof(ForumThreadReportRequest),
        MessageKeys.Forums.ThreadReport,
        [
            GroupParameter(),
            ThreadParameter(),
            IntegerParameter("category_id", "Report category identifier."),
            StringParameter("report", "Report text."),
            OptionalStringParameter("first_context", "Flash report context."),
            OptionalStringParameter("second_context", "Flash report context."),
            GenerationParameter()
        ],
        true);

    public static readonly ApplicationDescriptor MessageReport = Action(
        ApplicationMemberIds.ForumMessageReport,
        "Report forum message",
        "Reports a forum message to the moderators.",
        typeof(ForumMessageReportRequest),
        MessageKeys.Forums.MessageReport,
        [
            GroupParameter(),
            ThreadParameter(),
            MessageParameter(),
            IntegerParameter("category_id", "Report category identifier."),
            StringParameter("report", "Report text."),
            OptionalStringParameter("first_context", "Flash report context."),
            OptionalStringParameter("second_context", "Flash report context."),
            GenerationParameter()
        ],
        true);

    public static readonly ApplicationDescriptor Changed = new(
        ApplicationMemberIds.ForumsChanged,
        "Forum state changed",
        "Publishes immutable forum snapshots after accepted route messages and resets.",
        ApplicationMemberKind.Event,
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting,
        null,
        typeof(ForumChanged),
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints());

    private static ApplicationDescriptor Refresh(
        string id,
        string title,
        string summary,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        Type result_type,
        MessageKey request,
        MessageKey response,
        IReadOnlyList<ApplicationParameterDescriptor> parameters) => new(
            id,
            title,
            summary + " Sends one session-pinned request and accepts only the matching forum response.",
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            request_type,
            result_type,
            parameters,
            [ApplicationStateKey.HotelConnected],
            [ChangeEffect()],
            [Send(request), Observe(response)],
            RefreshHints());

    private static ApplicationDescriptor Action(
        string id,
        string title,
        string summary,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        MessageKey request,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        bool destructive) => new(
            id,
            title,
            summary + " Dispatches exactly one forum action in the pinned active session.",
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
        request_type,
        typeof(ForumDispatchResult),
        parameters,
        requiredStates: [ApplicationStateKey.HotelConnected],
            stateEffects: [ChangeEffect()],
            messages: [Send(request)],
            toolHints: new(false, destructive, false, true));

    private static ApplicationDescriptor Request(
        string id,
        string title,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        MessageKey request,
        IReadOnlyList<ApplicationParameterDescriptor> parameters) => new(
            id,
            title,
            "Dispatches exactly one forum read request and returns immediately.",
            ApplicationMemberKind.Operation,
            dispatch_exposure,
        request_type,
        typeof(ForumDispatchResult),
        parameters,
        requiredStates: [ApplicationStateKey.HotelConnected],
            stateEffects: [ReadEffect()],
            messages: [Send(request)],
            toolHints: new(true, false, false, true));

    private static IReadOnlyList<ApplicationParameterDescriptor> ListParameters() =>
    [
        new("list_code", typeof(ForumListCode), true, null, "Hotel forum directory."),
        OffsetParameter("start_index"),
        CountParameter(),
        TimeoutParameter(),
        GenerationParameter()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> ThreadPageParameters() =>
    [
        GroupParameter(),
        OffsetParameter("start_index"),
        CountParameter(),
        TimeoutParameter(),
        GenerationParameter()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> MessagePageParameters() =>
    [
        GroupParameter(),
        ThreadParameter(),
        OffsetParameter("start_index"),
        CountParameter(),
        TimeoutParameter(),
        GenerationParameter()
    ];

    private static ApplicationParameterDescriptor GroupParameter() =>
        new("group_id", typeof(Id), true, null, "Forum-owning group identifier.");

    private static ApplicationParameterDescriptor ThreadParameter() =>
        new("thread_id", typeof(Id), true, null, "Forum thread identifier.");

    private static ApplicationParameterDescriptor MessageParameter() =>
        new("message_id", typeof(Id), true, null, "Forum message identifier.");

    private static ApplicationParameterDescriptor StateParameter() =>
        IntegerParameter("state", "Forum moderation state.");

    private static ApplicationParameterDescriptor IntegerParameter(
        string name,
        string description) =>
        new(name, typeof(int), true, null, description);

    private static ApplicationParameterDescriptor StringParameter(
        string name,
        string description) =>
        new(name, typeof(string), true, null, description,
            new(MinLength: 0, MaxLength: ushort.MaxValue));

    private static ApplicationParameterDescriptor OptionalStringParameter(
        string name,
        string description) =>
        new(name, typeof(string), false, "", description,
            new(MinLength: 0, MaxLength: ushort.MaxValue));

    private static ApplicationParameterDescriptor OffsetParameter(string name) =>
        new(name, typeof(int), false, 0, "Zero-based hotel page start.", new(Minimum: 0));

    private static ApplicationParameterDescriptor CountParameter() =>
        new("max_count", typeof(int), false, 20, "Maximum hotel page entries.", new(Minimum: 1));

    private static ApplicationParameterDescriptor TimeoutParameter() =>
        new("timeout_milliseconds", typeof(int), false, 10000, "Caller wait budget.",
            new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor GenerationParameter() =>
        new("expected_session_generation", typeof(long?), false, null,
            "Optional active hotel-session generation.", new(Minimum: 1));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter() =>
        new("snapshot_revision", typeof(long?), false, null,
            "Optional immutable forum snapshot revision.", new(Minimum: 1));

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Forums.Stats, false),
        Observe(MessageKeys.Forums.List, false),
        Observe(MessageKeys.Forums.Threads, false),
        Observe(MessageKeys.Forums.Messages, false),
        Observe(MessageKeys.Forums.ThreadCreated, false),
        Observe(MessageKeys.Forums.MessageCreated, false),
        Observe(MessageKeys.Forums.ThreadUpdated, false),
        Observe(MessageKeys.Forums.MessageUpdated, false),
        Observe(MessageKeys.Forums.UnreadCount, false)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, MessageDirection.In, ApplicationMessageRole.Observe, required);

    private static ApplicationStateEffect ReadEffect() =>
        new(ApplicationStateKey.Forums, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect ChangeEffect() =>
        new(ApplicationStateKey.Forums, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);

    private static ApplicationToolHints RefreshHints() => new(true, false, true, true);
}
