using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

internal sealed class NavigatorApplication : IApplicationFeature
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly NavigatorManager navigator;
    private readonly ApplicationMessageDispatcher messages;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<NavigatorChanged> changed;
    private readonly ApplicationEventSource<NavigatorSearchReceived> search_received;
    private int disposed;

    public NavigatorApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher messages,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        this.game = game;
        navigator = game.Navigator;
        this.messages = messages;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<NavigatorChanged>(observer_error);
        search_received = new ApplicationEventSource<NavigatorSearchReceived>(observer_error);

        try
        {
            Bindings = Array.AsReadOnly<IApplicationBinding>(
            [
                new ApplicationCallBinding<NavigatorStateRequest, NavigatorState>(
                    NavigatorApplicationDescriptors.State,
                    (request, _) => ValueTask.FromResult(ReadState(request))),
                new ApplicationCallBinding<NavigatorRefreshRequest, NavigatorState>(
                    NavigatorApplicationDescriptors.MetadataRefresh,
                    RefreshMetadata),
                new ApplicationCallBinding<NavigatorRefreshRequest, NavigatorState>(
                    NavigatorApplicationDescriptors.FlatCategoriesRefresh,
                    RefreshFlatCategories),
                new ApplicationCallBinding<NavigatorViewSearchInput, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchView,
                    SearchView),
                new ApplicationCallBinding<NavigatorTextSearchInput, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchText,
                    SearchText),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyRooms,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyRooms,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyFavourites,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyFavouriteRooms,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyRoomRights,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyRoomRights,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyHistory,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyRoomHistory,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyFrequentHistory,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyFrequentRoomHistory,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyFriendsRooms,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyFriendsRooms,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchFriendsPresent,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.RoomsWhereFriendsAre,
                        token)),
                new ApplicationCallBinding<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchMyGuildBases,
                    (request, token) => SearchEmpty(
                        request,
                        MessageContracts.Navigator.Search.MyGuildBases,
                        token)),
                new ApplicationCallBinding<NavigatorPopularSearchInput, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchPopular,
                    SearchPopular),
                new ApplicationCallBinding<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchHighestScore,
                    (request, token) => SearchAd(
                        request,
                        MessageContracts.Navigator.Search.HighestScoring,
                        token)),
                new ApplicationCallBinding<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                    NavigatorApplicationDescriptors.SearchGuildBases,
                    (request, token) => SearchAd(
                        request,
                        MessageContracts.Navigator.Search.GuildBases,
                        token)),
                new ApplicationCallBinding<NavigatorSavedSearchAddInput, NavigatorOperationResult>(
                    NavigatorApplicationDescriptors.SavedSearchAdd,
                    AddSavedSearch),
                new ApplicationCallBinding<NavigatorSavedSearchDeleteInput, NavigatorOperationResult>(
                    NavigatorApplicationDescriptors.SavedSearchDelete,
                    DeleteSavedSearch),
                new ApplicationCallBinding<NavigatorCategoryInput, NavigatorOperationResult>(
                    NavigatorApplicationDescriptors.CategoryCollapse,
                    CollapseCategory),
                new ApplicationCallBinding<NavigatorCategoryInput, NavigatorOperationResult>(
                    NavigatorApplicationDescriptors.CategoryExpand,
                    ExpandCategory),
                new ApplicationCallBinding<NavigatorRoomCreateInput, NavigatorRoomOperationResult>(
                    NavigatorApplicationDescriptors.RoomCreate,
                    CreateRoom),
                new ApplicationCallBinding<NavigatorRoomDeleteInput, NavigatorRoomOperationResult>(
                    NavigatorApplicationDescriptors.RoomDelete,
                    DeleteRoom),
                new ApplicationCallBinding<NavigatorHomeRoomSetInput, NavigatorRoomOperationResult>(
                    NavigatorApplicationDescriptors.HomeRoomSet,
                    SetHomeRoom),
                new ApplicationEventBinding<NavigatorChanged>(
                    NavigatorApplicationDescriptors.Changed,
                    changed.Subscribe),
                new ApplicationEventBinding<NavigatorSearchReceived>(
                    NavigatorApplicationDescriptors.SearchReceived,
                    search_received.Subscribe)
            ]);

            navigator.StateChanged += OnStateChanged;
            navigator.SearchReceived += OnSearchReceived;
        }
        catch
        {
            changed.Dispose();
            search_received.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public NavigatorState ReadState(NavigatorStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        return navigator.State;
    }

    public async ValueTask<NavigatorState> RefreshMetadata(
        NavigatorRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateRefresh(request);
        Session session = RequireSession(cancellation_token);
        using var commit = new NavigatorStateCommit(
            navigator,
            connection,
            session,
            NavigatorStateChangeKind.Metadata);
        NavigatorMetaData response = await game.Requests.RequestAsync(
            MessageContracts.Navigator.State.MetadataRequest,
            new NavigatorMetadataRequest(),
            MessageContracts.Navigator.State.Metadata,
            session,
            match: _ => ReferenceEquals(connection.Session, session),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2).ConfigureAwait(false);
        await commit.WaitAsync(
            state => MetadataMatches(state, response),
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        NavigatorState state = RequireCurrentState(session);
        if (!state.MetadataLoaded)
            throw new InvalidOperationException("Navigator metadata was received without updating navigator state.");
        return state;
    }

    public async ValueTask<NavigatorState> RefreshFlatCategories(
        NavigatorRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateRefresh(request);
        Session session = RequireSession(cancellation_token);
        using var commit = new NavigatorStateCommit(
            navigator,
            connection,
            session,
            NavigatorStateChangeKind.FlatCategories);
        UserFlatCats response = await game.Requests.RequestAsync(
            MessageContracts.Navigator.State.FlatCategoriesRequest,
            new FlatCategoriesRequest(),
            MessageContracts.Navigator.State.FlatCategories,
            session,
            match: _ => ReferenceEquals(connection.Session, session),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2).ConfigureAwait(false);
        await commit.WaitAsync(
            state => FlatCategoriesMatch(state, response),
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        NavigatorState state = RequireCurrentState(session);
        if (!state.FlatCategoriesLoaded)
            throw new InvalidOperationException("Room categories were received without updating navigator state.");
        return state;
    }

    public ValueTask<NavigatorSearchSnapshot> SearchView(
        NavigatorViewSearchInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        RequiredText(request.SearchCode, nameof(request.SearchCode));
        ValidateText(request.Filter, nameof(request.Filter));
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        return Search(
            MessageContracts.Navigator.Search.View,
            new NavigatorViewSearchRequest(request.SearchCode, request.Filter),
            request.TimeoutMilliseconds,
            session,
            result =>
                result.SearchCode == request.SearchCode &&
                result.Filter == request.Filter,
            cancellation_token);
    }

    public ValueTask<NavigatorSearchSnapshot> SearchText(
        NavigatorTextSearchInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.Text, nameof(request.Text));
        if (!Enum.IsDefined(request.Field))
            throw new ArgumentOutOfRangeException(nameof(request.Field));
        ValidateTimeout(request.TimeoutMilliseconds);
        string filter = NavigatorManager.FilterText(request.Field, request.Text);
        ValidateText(filter, nameof(request.Text));
        Session session = RequireSession(cancellation_token);
        return Search(
            MessageContracts.Navigator.Search.Text,
            new NavigatorTextSearchRequest(filter),
            MessageContracts.Navigator.Search.LegacyResult,
            request.TimeoutMilliseconds,
            session,
            result => result.Filter == filter,
            cancellation_token);
    }

    public ValueTask<NavigatorSearchSnapshot> SearchEmpty(
        NavigatorSearchRequest request,
        MessageContract<NavigatorEmptySearchRequest> contract,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(contract);
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        return Search(
            contract,
            new NavigatorEmptySearchRequest(),
            request.TimeoutMilliseconds,
            session,
            null,
            cancellation_token);
    }

    public ValueTask<NavigatorSearchSnapshot> SearchPopular(
        NavigatorPopularSearchInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.Tag, nameof(request.Tag));
        if (request.AdIndex < -1)
            throw new ArgumentOutOfRangeException(nameof(request.AdIndex));
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        return Search(
            MessageContracts.Navigator.Search.Popular,
            new NavigatorTagSearchRequest(request.Tag, request.AdIndex),
            request.TimeoutMilliseconds,
            session,
            null,
            cancellation_token);
    }

    public ValueTask<NavigatorSearchSnapshot> SearchAd(
        NavigatorAdSearchInput request,
        MessageContract<NavigatorAdSearchRequest> contract,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(contract);
        if (request.AdIndex < -1)
            throw new ArgumentOutOfRangeException(nameof(request.AdIndex));
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        return Search(
            contract,
            new NavigatorAdSearchRequest(request.AdIndex),
            request.TimeoutMilliseconds,
            session,
            null,
            cancellation_token);
    }

    public ValueTask<NavigatorOperationResult> AddSavedSearch(
        NavigatorSavedSearchAddInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        RequiredText(request.SearchCode, nameof(request.SearchCode));
        ValidateText(request.Filter, nameof(request.Filter));
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Navigator.Personalization.SavedSearchAdd,
            new AddSavedSearchRequest(request.SearchCode, request.Filter),
            session,
            cancellation_token);
        return OperationResult(session, request.SearchCode, request.Filter);
    }

    public ValueTask<NavigatorOperationResult> DeleteSavedSearch(
        NavigatorSavedSearchDeleteInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (request.SavedSearchId < 0)
            throw new ArgumentOutOfRangeException(nameof(request.SavedSearchId));
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Navigator.Personalization.SavedSearchDelete,
            new DeleteSavedSearchRequest(request.SavedSearchId),
            session,
            cancellation_token);
        return OperationResult(session, saved_search_id: request.SavedSearchId);
    }

    public ValueTask<NavigatorOperationResult> CollapseCategory(
        NavigatorCategoryInput request,
        CancellationToken cancellation_token) =>
        UpdateCategory(
            request,
            MessageContracts.Navigator.Personalization.CollapsedCategoryAdd,
            static code => new AddCollapsedCategoryRequest(code),
            cancellation_token);

    public ValueTask<NavigatorOperationResult> ExpandCategory(
        NavigatorCategoryInput request,
        CancellationToken cancellation_token) =>
        UpdateCategory(
            request,
            MessageContracts.Navigator.Personalization.CollapsedCategoryRemove,
            static code => new RemoveCollapsedCategoryRequest(code),
            cancellation_token);

    public ValueTask<NavigatorRoomOperationResult> CreateRoom(
        NavigatorRoomCreateInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        RequiredText(request.Name, nameof(request.Name));
        ValidateText(request.Description, nameof(request.Description));
        RequiredText(request.Model, nameof(request.Model));
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Navigator.RoomCreate,
            new CreateRoomRequest(
                request.Name,
                request.Description,
                request.Model,
                request.Category,
                request.MaxVisitors,
                request.TradeMode),
            session,
            cancellation_token,
            () => RequireDispatch(session, cancellation_token));
        return RoomOperationResult(session);
    }

    public ValueTask<NavigatorRoomOperationResult> DeleteRoom(
        NavigatorRoomDeleteInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Navigator.RoomDelete,
            new DeleteRoomRequest(request.RoomId),
            session,
            cancellation_token,
            () => RequireDispatch(session, cancellation_token));
        return RoomOperationResult(session, request.RoomId);
    }

    public ValueTask<NavigatorRoomOperationResult> SetHomeRoom(
        NavigatorHomeRoomSetInput request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Navigator.HomeRoomUpdate,
            new SetHomeRoomRequest(request.RoomId),
            session,
            cancellation_token,
            () => RequireDispatch(session, cancellation_token));
        return RoomOperationResult(session, request.RoomId);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        navigator.StateChanged -= OnStateChanged;
        navigator.SearchReceived -= OnSearchReceived;
        changed.Dispose();
        search_received.Dispose();
    }

    private async ValueTask<NavigatorSearchSnapshot> Search<TRequest>(
        MessageContract<TRequest> request_contract,
        TRequest request,
        int timeout_milliseconds,
        Session session,
        Func<NavigatorSearchResult, bool>? match,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest> => await Search(
            request_contract,
            request,
            MessageContracts.Navigator.Search.Result,
            timeout_milliseconds,
            session,
            match,
            cancellation_token).ConfigureAwait(false);

    private async ValueTask<NavigatorSearchSnapshot> Search<TRequest>(
        MessageContract<TRequest> request_contract,
        TRequest request,
        MessageContract<NavigatorSearchResult> result_contract,
        int timeout_milliseconds,
        Session session,
        Func<NavigatorSearchResult, bool>? match,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
    {
        NavigatorSearchResult result = await game.Requests.RequestAsync(
            request_contract,
            request,
            result_contract,
            session,
            match: response =>
                ReferenceEquals(connection.Session, session) &&
                (match?.Invoke(response) ?? true),
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2).ConfigureAwait(false);
        RequireCurrentState(session);
        return NavigatorManager.Snapshot(result);
    }

    private ValueTask<NavigatorOperationResult> UpdateCategory<TRequest>(
        NavigatorCategoryInput request,
        MessageContract<TRequest> contract,
        Func<string, TRequest> create,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(create);
        RequiredText(request.SearchCode, nameof(request.SearchCode));
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(contract, create(request.SearchCode), session, cancellation_token);
        return OperationResult(session, request.SearchCode);
    }

    private Session RequireSession(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        return connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
    }

    private void RequireDispatch(Session session, CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed before navigator dispatch.");
    }

    private NavigatorState RequireCurrentState(Session session)
    {
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed during the navigator request.");
        return navigator.State;
    }

    private ValueTask<NavigatorOperationResult> OperationResult(
        Session session,
        string? search_code = null,
        string? filter = null,
        int? saved_search_id = null) => ValueTask.FromResult(new NavigatorOperationResult(
            session.Client,
            time_provider.GetUtcNow(),
            search_code,
            filter,
            saved_search_id));

    private ValueTask<NavigatorRoomOperationResult> RoomOperationResult(
        Session session,
        Id? room_id = null) => ValueTask.FromResult(new NavigatorRoomOperationResult(
            session.Client,
            time_provider.GetUtcNow(),
            room_id));

    private void OnStateChanged(NavigatorStateChangeKind kind, NavigatorState state) =>
        changed.Publish(new NavigatorChanged(
            (NavigatorChangeKind)kind,
            time_provider.GetUtcNow(),
            state));

    private void OnSearchReceived(
        NavigatorSearchSnapshot result,
        long generation,
        long revision) => search_received.Publish(new NavigatorSearchReceived(
            generation,
            revision,
            time_provider.GetUtcNow(),
            result));

    private static void ValidateRefresh(NavigatorRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void RequiredText(string value, string parameter_name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The value cannot be empty.", parameter_name);
        ValidateText(value, parameter_name);
    }

    private static void ValidateText(string value, string parameter_name)
    {
        ArgumentNullException.ThrowIfNull(value, parameter_name);
        if (System.Text.Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("The UTF-8 value is too long.", parameter_name);
    }

    private static bool MetadataMatches(NavigatorState state, NavigatorMetaData response)
    {
        if (!state.MetadataLoaded || state.Categories.Count != response.Categories.Count)
            return false;
        for (int category_index = 0; category_index < state.Categories.Count; category_index++)
        {
            NavigatorCategorySnapshot category = state.Categories[category_index];
            NavigatorCategory expected = response.Categories[category_index];
            if (category.SearchCode != expected.SearchCode ||
                category.QuickLinks.Count != expected.QuickLinks.Count)
            {
                return false;
            }
            for (int search_index = 0; search_index < category.QuickLinks.Count; search_index++)
            {
                NavigatorSearchEntrySnapshot search = category.QuickLinks[search_index];
                NavigatorSearch expected_search = expected.QuickLinks[search_index];
                if (search.Id != expected_search.Id ||
                    search.SearchCode != expected_search.SearchCode ||
                    search.Filter != expected_search.Filter ||
                    search.Localization != expected_search.Localization)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool FlatCategoriesMatch(NavigatorState state, UserFlatCats response)
    {
        if (!state.FlatCategoriesLoaded || state.FlatCategories.Count != response.Categories.Count)
            return false;
        for (int index = 0; index < state.FlatCategories.Count; index++)
        {
            NavigatorFlatCategorySnapshot category = state.FlatCategories[index];
            FlatCategory expected = response.Categories[index];
            if (category.NodeId != expected.NodeId ||
                category.Name != expected.Name ||
                category.Visible != expected.Visible ||
                category.Automatic != expected.Automatic ||
                category.AutomaticCategoryKey != expected.AutomaticCategoryKey ||
                category.GlobalCategoryKey != expected.GlobalCategoryKey ||
                category.StaffOnly != expected.StaffOnly)
            {
                return false;
            }
        }
        return true;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private sealed class NavigatorStateCommit : IDisposable
    {
        private readonly object sync = new();
        private readonly NavigatorManager navigator;
        private readonly IConnection connection;
        private readonly Session session;
        private readonly NavigatorStateChangeKind kind;
        private readonly List<NavigatorState> candidates = [];
        private readonly TaskCompletionSource<NavigatorState> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<NavigatorState, bool>? match;
        private bool disposed;

        public NavigatorStateCommit(
            NavigatorManager navigator,
            IConnection connection,
            Session session,
            NavigatorStateChangeKind kind)
        {
            this.navigator = navigator;
            this.connection = connection;
            this.session = session;
            this.kind = kind;
            navigator.StateChanged += OnStateChanged;
        }

        public Task<NavigatorState> WaitAsync(
            Func<NavigatorState, bool> predicate,
            int timeout_milliseconds,
            CancellationToken cancellation_token)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                match = predicate;
                NavigatorState? candidate = candidates.LastOrDefault(predicate);
                if (candidate is not null)
                    completion.TrySetResult(candidate);
                candidates.Clear();
            }
            return completion.Task.WaitAsync(
                TimeSpan.FromMilliseconds(timeout_milliseconds),
                cancellation_token);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                candidates.Clear();
            }
            navigator.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(NavigatorStateChangeKind changed_kind, NavigatorState state)
        {
            if (changed_kind != kind || !ReferenceEquals(connection.Session, session))
                return;
            lock (sync)
            {
                if (disposed)
                    return;
                if (match is null)
                    candidates.Add(state);
                else if (match(state))
                    completion.TrySetResult(state);
            }
        }
    }
}
