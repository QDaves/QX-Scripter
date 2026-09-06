using System.Globalization;
using System.Text;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

internal interface IProfileOperations
{
    Task EnsureLoadedAsync(int timeout_milliseconds, CancellationToken cancellation_token);
    void UpdateFigure(string gender, string figure);
    void UpdateMotto(string motto);
}

internal interface IRemotePeopleOperations
{
    RemoteProfileOpenReceipt OpenProfile(
        RemoteProfileOpenRequest request,
        CancellationToken cancellation_token = default);
}

internal static class ProfileApplicationCollectionLimits
{
    internal const int MaximumEntries = 500;

    internal static void Validate(int count, string name)
    {
        if (count > MaximumEntries)
            throw new InvalidDataException($"The {name} collection exceeds the maximum of {MaximumEntries} entries.");
    }
}

internal sealed class ProfileApplication : IApplicationFeature, IProfileOperations
{
    private const int profile_commit_history_limit = 16;
    private const int wardrobe_snapshot_limit = 16;

    private readonly IConnection connection;
    private readonly GameState game;
    private readonly ProfileManager profile;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<ProfileChanged> changed;
    private readonly ApplicationEventSource<ProfileBlockUpdated> block_updated;
    private readonly ApplicationEventSource<ProfileIgnoreUpdated> ignore_updated;
    private readonly object profile_updates_sync = new();
    private readonly Dictionary<ProfileStateChangeKind, List<ProfileStateUpdate>> profile_updates = [];
    private readonly object wardrobe_sync = new();
    private readonly Dictionary<long, WardrobeSnapshotLease> wardrobe_snapshots = [];
    private long wardrobe_snapshot_revision;
    private int disposed;

    public ProfileApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        this.game = game;
        profile = game.Profile;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<ProfileChanged>(observer_error);
        block_updated = new ApplicationEventSource<ProfileBlockUpdated>(observer_error);
        ignore_updated = new ApplicationEventSource<ProfileIgnoreUpdated>(observer_error);

        try
        {
            Bindings = Array.AsReadOnly<IApplicationBinding>(
            [
                new ApplicationCallBinding<ProfileStateRequest, ProfileStateView>(
                    ProfileApplicationDescriptors.State,
                    (request, _) => ValueTask.FromResult(ReadState(request))),
                new ApplicationCallBinding<ProfileRefreshRequest, ProfileStateView>(
                    ProfileApplicationDescriptors.Refresh,
                    Refresh),
                new ApplicationCallBinding<ProfileIdPageRequest, ProfileIdPage>(
                    ProfileApplicationDescriptors.BlocksList,
                    (request, _) => ValueTask.FromResult(BlockedUsers(request))),
                new ApplicationCallBinding<ProfileIdRefreshRequest, ProfileIdPage>(
                    ProfileApplicationDescriptors.BlocksRefresh,
                    RefreshBlockedUsers),
                new ApplicationCallBinding<ProfileUserRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.BlockAdd,
                    BlockUser),
                new ApplicationCallBinding<ProfileUserRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.BlockRemove,
                    UnblockUser),
                new ApplicationCallBinding<ProfileIdPageRequest, ProfileIdPage>(
                    ProfileApplicationDescriptors.IgnoresList,
                    (request, _) => ValueTask.FromResult(IgnoredUsers(request))),
                new ApplicationCallBinding<ProfileIdRefreshRequest, ProfileIdPage>(
                    ProfileApplicationDescriptors.IgnoresRefresh,
                    RefreshIgnoredUsers),
                new ApplicationCallBinding<ProfileUserRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.IgnoreAddById,
                    IgnoreUserById),
                new ApplicationCallBinding<ProfileUserNameRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.IgnoreAddByName,
                    IgnoreUserByName),
                new ApplicationCallBinding<ProfileIgnoreRemoveRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.IgnoreRemove,
                    UnignoreUser),
                new ApplicationCallBinding<ProfileFigureSetsRequest, ProfileFigureSetsPage>(
                    ProfileApplicationDescriptors.FigureSetsList,
                    (request, _) => ValueTask.FromResult(FigureSets(request))),
                new ApplicationCallBinding<ProfileSanctionsRequest, ProfileSanctionsPage>(
                    ProfileApplicationDescriptors.SanctionsList,
                    (request, _) => ValueTask.FromResult(Sanctions(request))),
                new ApplicationCallBinding<ProfileSanctionsRefreshRequest, ProfileSanctionsPage>(
                    ProfileApplicationDescriptors.SanctionsRefresh,
                    RefreshSanctions),
                new ApplicationCallBinding<ProfileWardrobeRequest, ProfileWardrobePage>(
                    ProfileApplicationDescriptors.WardrobeGet,
                    Wardrobe),
                new ApplicationCallBinding<ProfileMottoSetRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.MottoSet,
                    SetMotto),
                new ApplicationCallBinding<ProfileFigureSetRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.FigureSet,
                    SetFigure),
                new ApplicationCallBinding<ProfileOutfitSaveRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.OutfitSave,
                    SaveOutfit),
                new ApplicationCallBinding<ProfileFavoriteGroupRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.FavoriteGroupSelect,
                    SelectFavoriteGroup),
                new ApplicationCallBinding<ProfileFavoriteGroupRequest, ProfileDispatchResult>(
                    ProfileApplicationDescriptors.FavoriteGroupDeselect,
                    DeselectFavoriteGroup),
                new ApplicationEventBinding<ProfileChanged>(
                    ProfileApplicationDescriptors.Changed,
                    changed.Subscribe),
                new ApplicationEventBinding<ProfileBlockUpdated>(
                    ProfileApplicationDescriptors.BlockUpdated,
                    block_updated.Subscribe),
                new ApplicationEventBinding<ProfileIgnoreUpdated>(
                    ProfileApplicationDescriptors.IgnoreUpdated,
                    ignore_updated.Subscribe)
            ]);
            profile.StateChanged += OnStateChanged;
            game.BindProfileOperations(this);
        }
        catch
        {
            profile.StateChanged -= OnStateChanged;
            game.UnbindProfileOperations(this);
            changed.Dispose();
            block_updated.Dispose();
            ignore_updated.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public ProfileStateView ReadState(ProfileStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        return StateView(profile.State);
    }

    public async ValueTask<ProfileStateView> Refresh(
        ProfileRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        ProfileState state = await RefreshProfile(
            request.TimeoutMilliseconds,
            cancellation_token,
            true).ConfigureAwait(false);
        return StateView(state);
    }

    public ProfileIdPage BlockedUsers(ProfileIdPageRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ProfileState state = profile.State;
        return IdPage(
            state.BlockListLoaded,
            state,
            state.BlockedUserIds,
            request.Offset,
            request.Limit);
    }

    public async ValueTask<ProfileIdPage> RefreshBlockedUsers(
        ProfileIdRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateRefresh(request);
        ProfileState state = await RefreshBlockList(
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        return IdPage(
            state.BlockListLoaded,
            state,
            state.BlockedUserIds,
            request.Offset,
            request.Limit);
    }

    public ValueTask<ProfileDispatchResult> BlockUser(
        ProfileUserRequest request,
        CancellationToken cancellation_token)
    {
        RequireUser(request);
        return Dispatch(
            MessageContracts.Users.Block.Add,
            new BlockUserRequest(request.UserId),
            cancellation_token,
            request.UserId);
    }

    public ValueTask<ProfileDispatchResult> UnblockUser(
        ProfileUserRequest request,
        CancellationToken cancellation_token)
    {
        RequireUser(request);
        return Dispatch(
            MessageContracts.Users.Block.Remove,
            new UnblockUserRequest(request.UserId),
            cancellation_token,
            request.UserId);
    }

    public ProfileIdPage IgnoredUsers(ProfileIdPageRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ProfileState state = profile.State;
        return IdPage(
            state.IgnoreListLoaded,
            state,
            state.IgnoredUserIds,
            request.Offset,
            request.Limit);
    }

    public async ValueTask<ProfileIdPage> RefreshIgnoredUsers(
        ProfileIdRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateRefresh(request);
        ProfileState state = await RefreshIgnoreList(
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        return IdPage(
            state.IgnoreListLoaded,
            state,
            state.IgnoredUserIds,
            request.Offset,
            request.Limit);
    }

    public ValueTask<ProfileDispatchResult> IgnoreUserById(
        ProfileUserRequest request,
        CancellationToken cancellation_token)
    {
        RequireUser(request);
        return Dispatch(
            MessageContracts.Users.Ignore.AddByIdRequest,
            new IgnoreUserByIdRequest(request.UserId),
            cancellation_token,
            request.UserId);
    }

    public ValueTask<ProfileDispatchResult> IgnoreUserByName(
        ProfileUserNameRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.UserName, nameof(request.UserName), false);
        return Dispatch(
            MessageContracts.Users.Ignore.AddByNameRequest,
            new IgnoreUserByNameRequest(request.UserName),
            cancellation_token,
            target_name: request.UserName);
    }

    public ValueTask<ProfileDispatchResult> UnignoreUser(
        ProfileIgnoreRemoveRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.Identity, nameof(request.Identity), false);
        switch (request.Kind)
        {
            case ProfileIdentityKind.Id:
                if (!long.TryParse(
                        request.Identity,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long raw_id))
                {
                    throw new ArgumentException(
                        "The id identity must be a positive decimal integer.",
                        nameof(request.Identity));
                }
                Id user_id = raw_id;
                ValidateId(user_id, nameof(request.Identity));
                return Dispatch(
                    MessageContracts.Users.Ignore.Remove,
                    new UnignoreUserRequest(user_id),
                    cancellation_token,
                    user_id);
            case ProfileIdentityKind.Name:
                return Dispatch(
                    MessageContracts.Users.Ignore.Remove,
                    new UnignoreUserRequest(request.Identity),
                    cancellation_token,
                    target_name: request.Identity);
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind));
        }
    }

    public ProfileFigureSetsPage FigureSets(ProfileFigureSetsRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ProfileState state = profile.State;
        IReadOnlyList<FigureSetEntry> figures = Slice(
            state.FigureSets,
            request.Offset,
            request.Limit);
        IReadOnlyList<string> names = Slice(
            state.BoundFurnitureNames,
            request.Offset,
            request.Limit);
        return new ProfileFigureSetsPage(
            state.FigureSetsLoaded,
            state.Generation,
            state.Revision,
            state.FigureSets.Count,
            request.Offset,
            NextOffset(request.Offset, figures.Count, state.FigureSets.Count),
            figures,
            state.BoundFurnitureNames.Count,
            NextOffset(request.Offset, names.Count, state.BoundFurnitureNames.Count),
            names);
    }

    public ProfileSanctionsPage Sanctions(ProfileSanctionsRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        return SanctionsPage(profile.State, request.Offset, request.Limit);
    }

    public async ValueTask<ProfileSanctionsPage> RefreshSanctions(
        ProfileSanctionsRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        ProfileState state = await RefreshSanctionStatus(
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        return SanctionsPage(state, request.Offset, request.Limit);
    }

    public async ValueTask<ProfileWardrobePage> Wardrobe(
        ProfileWardrobeRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        ProfileOperationScope scope = CaptureScope(cancellation_token);
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        if (request.SnapshotRevision is null && request.Offset != 0)
            throw new ArgumentException(
                "A new wardrobe snapshot must be requested from offset zero.",
                nameof(request));

        WardrobeSnapshotLease snapshot;
        if (request.SnapshotRevision is long snapshot_revision)
        {
            snapshot = WardrobeSnapshot(scope, snapshot_revision);
        }
        else
        {
            Qx.Model.Messages.Incoming.Wardrobe wardrobe = await requests.RequestAsync(
                MessageContracts.Wardrobe.Request,
                new WardrobeRequest(),
                MessageContracts.Wardrobe.Snapshot,
                scope.Session,
                match: _ => ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: true,
                cancellation_token: cancellation_token,
                max_attempts: 2,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            snapshot = StoreWardrobeSnapshot(scope, wardrobe);
        }
        RequireScope(scope);
        IReadOnlyList<WardrobeOutfit> outfits = Slice(
            snapshot.Outfits,
            request.Offset,
            request.Limit);
        return new ProfileWardrobePage(
            snapshot.Client,
            snapshot.Generation,
            snapshot.ProfileRevision,
            snapshot.Revision,
            snapshot.State,
            snapshot.Outfits.Count,
            request.Offset,
            NextOffset(request.Offset, outfits.Count, snapshot.Outfits.Count),
            outfits);
    }

    public ValueTask<ProfileDispatchResult> SetMotto(
        ProfileMottoSetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.Motto, nameof(request.Motto), true);
        return Dispatch(
            MessageContracts.Users.MottoUpdate,
            new MottoUpdateRequest(request.Motto),
            cancellation_token);
    }

    public ValueTask<ProfileDispatchResult> SetFigure(
        ProfileFigureSetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        string gender = NormalizeGender(request.Gender);
        ValidateText(request.Figure, nameof(request.Figure), false);
        return Dispatch(
            MessageContracts.Wardrobe.FigureUpdate,
            new FigureUpdateRequest(gender, request.Figure),
            cancellation_token);
    }

    public ValueTask<ProfileDispatchResult> SaveOutfit(
        ProfileOutfitSaveRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.SlotId);
        string gender = NormalizeGender(request.Gender);
        ValidateText(request.Figure, nameof(request.Figure), false);
        return Dispatch(
            MessageContracts.Wardrobe.OutfitSave,
            new SaveWardrobeOutfitRequest(request.SlotId, request.Figure, gender),
            cancellation_token,
            slot_id: request.SlotId);
    }

    public ValueTask<ProfileDispatchResult> SelectFavoriteGroup(
        ProfileFavoriteGroupRequest request,
        CancellationToken cancellation_token)
    {
        RequireGroup(request);
        return Dispatch(
            MessageContracts.Users.FavoriteGroup.Select,
            new SelectFavoriteGroupRequest(request.GroupId),
            cancellation_token,
            request.GroupId);
    }

    public ValueTask<ProfileDispatchResult> DeselectFavoriteGroup(
        ProfileFavoriteGroupRequest request,
        CancellationToken cancellation_token)
    {
        RequireGroup(request);
        return Dispatch(
            MessageContracts.Users.FavoriteGroup.Deselect,
            new DeselectFavoriteGroupRequest(request.GroupId),
            cancellation_token,
            request.GroupId);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        profile.StateChanged -= OnStateChanged;
        game.UnbindProfileOperations(this);
        lock (profile_updates_sync)
            profile_updates.Clear();
        lock (wardrobe_sync)
            wardrobe_snapshots.Clear();
        changed.Dispose();
        block_updated.Dispose();
        ignore_updated.Dispose();
    }

    async Task IProfileOperations.EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateTimeout(timeout_milliseconds);
        if (profile.State.Loaded)
            return;
        await RefreshProfile(timeout_milliseconds, cancellation_token).ConfigureAwait(false);
    }

    void IProfileOperations.UpdateFigure(string gender, string figure) =>
        SetFigure(
            new ProfileFigureSetRequest(gender, figure),
            CancellationToken.None).GetAwaiter().GetResult();

    void IProfileOperations.UpdateMotto(string motto) =>
        SetMotto(
            new ProfileMottoSetRequest(motto),
            CancellationToken.None).GetAwaiter().GetResult();

    private async Task<ProfileState> RefreshProfile(
        int timeout_milliseconds,
        CancellationToken cancellation_token,
        bool force = false)
    {
        if (!force && profile.State.Loaded)
            return profile.State;
        return await Refresh(
            ProfileStateChangeKind.Identity,
            MessageContracts.Users.ProfileRequest,
            new ProfileRequest(),
            MessageContracts.Users.ProfileSnapshot,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
    }

    private Task<ProfileState> RefreshBlockList(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => Refresh(
            ProfileStateChangeKind.BlockList,
            MessageContracts.Users.Block.ListRequest,
            new BlockListRequest(),
            MessageContracts.Users.Block.ListSnapshot,
            timeout_milliseconds,
            cancellation_token);

    private Task<ProfileState> RefreshIgnoreList(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => Refresh(
            ProfileStateChangeKind.IgnoreList,
            MessageContracts.Users.Ignore.ListRequest,
            new IgnoreListRequest(),
            MessageContracts.Users.Ignore.ListSnapshot,
            timeout_milliseconds,
            cancellation_token);

    private Task<ProfileState> RefreshSanctionStatus(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => Refresh(
            ProfileStateChangeKind.Sanctions,
            MessageContracts.Users.Sanctions.Request,
            new SanctionStatusRequest(),
            MessageContracts.Users.Sanctions.Snapshot,
            timeout_milliseconds,
            cancellation_token);

    private async Task<ProfileState> Refresh<TRequest, TResponse>(
        ProfileStateChangeKind kind,
        MessageContract<TRequest> outgoing_contract,
        TRequest request,
        MessageContract<TResponse> incoming_contract,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
        where TRequest : Qx.Messages.IParserComposer<TRequest>
        where TResponse : Qx.Messages.IParserComposer<TResponse>
    {
        ProfileOperationScope scope = CaptureScope(cancellation_token);
        long attempt_baseline_revision = -1;
        ProfileState? accepted_state = null;
        await requests.RequestAsync(
            outgoing_contract,
            request,
            incoming_contract,
            scope.Session,
            match: response =>
            {
                if (!ScopeActive(scope))
                    return false;
                ProfileStateUpdate? update = FindProfileCommit(
                    kind,
                    scope.Generation,
                    Volatile.Read(ref attempt_baseline_revision),
                    response);
                if (update is null)
                    return false;
                Volatile.Write(ref accepted_state, update.State);
                return true;
            },
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                ProfileState current = profile.State;
                Volatile.Write(ref attempt_baseline_revision, current.Revision);
                Volatile.Write(ref accepted_state, null);
            }).ConfigureAwait(false);
        RequireScope(scope);
        return Volatile.Read(ref accepted_state)
            ?? throw new InvalidOperationException(
                "The accepted profile response was not committed by the passive state manager.");
    }

    private ValueTask<ProfileDispatchResult> Dispatch<T>(
        MessageContract<T> contract,
        T message,
        CancellationToken cancellation_token,
        Id? target_id = null,
        string? target_name = null,
        int? slot_id = null)
        where T : Qx.Messages.IParserComposer<T>
    {
        ThrowIfDisposed();
        ProfileOperationScope scope = CaptureScope(cancellation_token);
        message_dispatcher.Dispatch(
            contract,
            message,
            scope.Session,
            cancellation_token,
            () => RequireScope(scope));
        RequireScope(scope);
        return ValueTask.FromResult(new ProfileDispatchResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.Generation,
            scope.Revision,
            target_id,
            target_name,
            slot_id));
    }

    private ProfileOperationScope CaptureScope(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        ProfileState state = profile.State;
        Session session = state.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The profile state is not bound to the active hotel session.");
        return new ProfileOperationScope(session, state.Generation, state.Revision);
    }

    private bool ScopeActive(ProfileOperationScope scope)
    {
        ProfileState state = profile.State;
        return ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(state.Session, scope.Session) &&
            state.Generation == scope.Generation;
    }

    private void RequireScope(ProfileOperationScope scope)
    {
        ThrowIfDisposed();
        if (!ReferenceEquals(connection.Session, scope.Session))
            throw new InvalidOperationException("The hotel session changed during the profile operation.");
        ProfileState state = profile.State;
        if (!ReferenceEquals(state.Session, scope.Session) || state.Generation != scope.Generation)
            throw new InvalidOperationException("The profile generation changed during the operation.");
    }

    private void OnStateChanged(ProfileStateUpdate update)
    {
        lock (profile_updates_sync)
        {
            if (update.Kind == ProfileStateChangeKind.Reset)
                profile_updates.Clear();
            if (!profile_updates.TryGetValue(
                    update.Kind,
                    out List<ProfileStateUpdate>? updates))
            {
                updates = [];
                profile_updates.Add(update.Kind, updates);
            }
            updates.Add(update);
            if (updates.Count > profile_commit_history_limit)
                updates.RemoveRange(0, updates.Count - profile_commit_history_limit);
        }
        if (update.Kind == ProfileStateChangeKind.Reset)
        {
            lock (wardrobe_sync)
                wardrobe_snapshots.Clear();
        }
        DateTimeOffset now = time_provider.GetUtcNow();
        ProfileStateView view = StateView(update.State);
        changed.Publish(new ProfileChanged(ChangeKind(update.Kind), now, view));
        if (update.Value is BlockUserUpdate block_result)
        {
            block_updated.Publish(new ProfileBlockUpdated(
                update.State.Generation,
                update.State.Revision,
                now,
                block_result));
        }
        if (update.Value is IgnoreUserResult ignore_result)
        {
            ignore_updated.Publish(new ProfileIgnoreUpdated(
                update.State.Generation,
                update.State.Revision,
                now,
                ignore_result));
        }
    }

    private ProfileStateUpdate? FindProfileCommit<TResponse>(
        ProfileStateChangeKind kind,
        long generation,
        long baseline_revision,
        TResponse response)
    {
        lock (profile_updates_sync)
        {
            if (!profile_updates.TryGetValue(kind, out List<ProfileStateUpdate>? updates))
                return null;
            for (int index = updates.Count - 1; index >= 0; index--)
            {
                ProfileStateUpdate update = updates[index];
                if (update.State.Generation == generation &&
                    update.State.Revision > baseline_revision &&
                    ResponseEquals(update.Value, response))
                {
                    return update;
                }
            }
            return null;
        }
    }

    private static bool ResponseEquals<TResponse>(object? committed, TResponse response)
    {
        if (committed is not TResponse value)
            return false;
        if (value is UserData left_profile && response is UserData right_profile)
            return LocalProfileSnapshot.From(left_profile) == LocalProfileSnapshot.From(right_profile);
        if (value is BlockList left_blocks && response is BlockList right_blocks)
            return left_blocks.UserIds.SequenceEqual(right_blocks.UserIds);
        if (value is RequestIgnoreList left_ignores && response is RequestIgnoreList right_ignores)
            return left_ignores.UserIds.SequenceEqual(right_ignores.UserIds);
        if (value is FigureSetIds left_sets && response is FigureSetIds right_sets)
        {
            return left_sets.Entries.SequenceEqual(right_sets.Entries) &&
                left_sets.BoundFurnitureNames.SequenceEqual(right_sets.BoundFurnitureNames);
        }
        if (value is AccountSanctionStatus left_sanctions &&
            response is AccountSanctionStatus right_sanctions)
        {
            if (left_sanctions.Kind != right_sanctions.Kind)
                return false;
            return left_sanctions.Kind switch
            {
                AccountSanctionStatusKind.Sanctions =>
                    left_sanctions.Sanctions is not null &&
                    right_sanctions.Sanctions is not null &&
                    left_sanctions.Sanctions.Sanctions.SequenceEqual(
                        right_sanctions.Sanctions.Sanctions),
                AccountSanctionStatusKind.CallForHelp =>
                    left_sanctions.CallForHelp == right_sanctions.CallForHelp,
                _ => false
            };
        }
        return EqualityComparer<TResponse>.Default.Equals(value, response);
    }

    private ProfileStateView StateView(ProfileState state)
    {
        Session? active_session = connection.Session;
        bool connected = state.Session is not null &&
            ReferenceEquals(state.Session, active_session);
        return new ProfileStateView(
            state.Generation,
            state.Revision,
            connected,
            connected ? state.Session!.Client : null,
            state.Identity is null ? null : Identity(state.Identity),
            state.BlockListLoaded,
            state.BlockedUserIds.Count,
            state.IgnoreListLoaded,
            state.IgnoredUserIds.Count,
            state.FigureSetsLoaded,
            state.FigureSets.Count,
            state.BoundFurnitureNames.Count,
            state.SanctionsLoaded,
            state.Sanctions?.Kind);
    }

    private static ProfileIdentitySnapshot Identity(LocalProfileSnapshot value) => new(
        value.Id,
        value.Name,
        value.Figure,
        value.Gender,
        value.Motto,
        value.RealName,
        value.DirectMail,
        value.RespectTotal,
        value.RespectLeft,
        value.PetRespectLeft,
        value.StreamPublishingAllowed,
        value.LastAccessDate,
        value.IsNameChangeable,
        value.IsSafetyLocked,
        value.IsTradeLocked,
        value.NameColor,
        value.RespectReplenishesLeft,
        value.MaxRespectPerDay,
        value.TrailingFields);

    private WardrobeSnapshotLease StoreWardrobeSnapshot(
        ProfileOperationScope scope,
        Qx.Model.Messages.Incoming.Wardrobe wardrobe)
    {
        long revision = Interlocked.Increment(ref wardrobe_snapshot_revision);
        if (revision <= 0)
            throw new InvalidOperationException("The wardrobe snapshot revision space is exhausted.");
        var snapshot = new WardrobeSnapshotLease(
            scope.Session,
            scope.Session.Client,
            scope.Generation,
            scope.Revision,
            revision,
            wardrobe.State,
            Array.AsReadOnly(wardrobe.Outfits.ToArray()));
        lock (wardrobe_sync)
        {
            RequireScope(scope);
            wardrobe_snapshots.Add(revision, snapshot);
            while (wardrobe_snapshots.Count > wardrobe_snapshot_limit)
                wardrobe_snapshots.Remove(wardrobe_snapshots.Keys.Min());
        }
        return snapshot;
    }

    private WardrobeSnapshotLease WardrobeSnapshot(
        ProfileOperationScope scope,
        long revision)
    {
        lock (wardrobe_sync)
        {
            RequireScope(scope);
            if (!wardrobe_snapshots.TryGetValue(revision, out WardrobeSnapshotLease? snapshot) ||
                !ReferenceEquals(snapshot.Session, scope.Session) ||
                snapshot.Generation != scope.Generation)
            {
                throw new InvalidOperationException(
                    "The wardrobe snapshot is unavailable for the active session.");
            }
            return snapshot;
        }
    }

    private static ProfileIdPage IdPage(
        bool loaded,
        ProfileState state,
        IReadOnlyList<Id> values,
        int offset,
        int limit)
    {
        IReadOnlyList<Id> page = Slice(values, offset, limit);
        return new ProfileIdPage(
            loaded,
            state.Generation,
            state.Revision,
            values.Count,
            offset,
            NextOffset(offset, page.Count, values.Count),
            page);
    }

    private static ProfileSanctionsPage SanctionsPage(
        ProfileState state,
        int offset,
        int limit)
    {
        IReadOnlyList<Sanction> sanctions = state.Sanctions?.Sanctions?.Sanctions ?? [];
        IReadOnlyList<Sanction> page = Slice(sanctions, offset, limit);
        return new ProfileSanctionsPage(
            state.SanctionsLoaded,
            state.Generation,
            state.Revision,
            state.Sanctions?.Kind,
            sanctions.Count,
            offset,
            NextOffset(offset, page.Count, sanctions.Count),
            page,
            state.Sanctions?.CallForHelp);
    }

    private static IReadOnlyList<T> Slice<T>(
        IReadOnlyList<T> values,
        int offset,
        int limit)
    {
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }

    private void RequireUser(ProfileUserRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.UserId, nameof(request.UserId));
    }

    private void RequireGroup(ProfileFavoriteGroupRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.GroupId, nameof(request.GroupId));
    }

    private static string NormalizeGender(string value)
    {
        ValidateText(value, nameof(value), false);
        Gender gender = Genders.Parse(value);
        if (gender is Gender.None)
            throw new ArgumentException("Gender must be female, male or unisex.", nameof(value));
        return gender.ToClientString();
    }

    private static void ValidateId(Id value, string name)
    {
        if ((long)value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateText(string value, string name, bool allow_empty)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (!allow_empty && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The value cannot be empty.", name);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateRefresh(ProfileIdRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
    }

    private static void ValidatePaging(int offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > ProfileApplicationCollectionLimits.MaximumEntries)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static ProfileChangeKind ChangeKind(ProfileStateChangeKind kind) => kind switch
    {
        ProfileStateChangeKind.Identity => ProfileChangeKind.Identity,
        ProfileStateChangeKind.BlockList => ProfileChangeKind.BlockList,
        ProfileStateChangeKind.BlockResult => ProfileChangeKind.BlockResult,
        ProfileStateChangeKind.IgnoreList => ProfileChangeKind.IgnoreList,
        ProfileStateChangeKind.IgnoreResult => ProfileChangeKind.IgnoreResult,
        ProfileStateChangeKind.FigureSets => ProfileChangeKind.FigureSets,
        ProfileStateChangeKind.Sanctions => ProfileChangeKind.Sanctions,
        ProfileStateChangeKind.Reset => ProfileChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct ProfileOperationScope(
        Session Session,
        long Generation,
        long Revision);

    private sealed record WardrobeSnapshotLease(
        Session Session,
        ClientType Client,
        long Generation,
        long ProfileRevision,
        long Revision,
        int State,
        IReadOnlyList<WardrobeOutfit> Outfits);

}

internal sealed class GroupMembershipApplication : IApplicationFeature
{
    private readonly IConnection connection;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private int disposed;

    public GroupMembershipApplication(
        IConnection connection,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<GroupJoinRequest, GroupMembershipDispatchResult>(
                GroupMembershipApplicationDescriptors.Join,
                Join),
            new ApplicationCallBinding<GroupMemberKickRequest, GroupMembershipDispatchResult>(
                GroupMembershipApplicationDescriptors.Kick,
                Kick),
            new ApplicationCallBinding<GroupMemberRequest, GroupMembershipDispatchResult>(
                GroupMembershipApplicationDescriptors.Approve,
                Approve),
            new ApplicationCallBinding<GroupMemberRequest, GroupMembershipDispatchResult>(
                GroupMembershipApplicationDescriptors.Reject,
                Reject)
        ]);
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public ValueTask<GroupMembershipDispatchResult> Join(
        GroupJoinRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.GroupId, nameof(request.GroupId));
        return Dispatch(
            MessageContracts.Groups.Membership.Join,
            new JoinGroupRequest(request.GroupId),
            request.GroupId,
            null,
            null,
            cancellation_token);
    }

    public ValueTask<GroupMembershipDispatchResult> Kick(
        GroupMemberKickRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateIds(request.GroupId, request.UserId);
        return Dispatch(
            MessageContracts.Groups.Membership.Kick,
            new KickGroupMemberRequest(request.GroupId, request.UserId, request.BlockRejoin),
            request.GroupId,
            request.UserId,
            request.BlockRejoin,
            cancellation_token);
    }

    public ValueTask<GroupMembershipDispatchResult> Approve(
        GroupMemberRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateIds(request.GroupId, request.UserId);
        return Dispatch(
            MessageContracts.Groups.Membership.Approve,
            new ApproveGroupMemberRequest(request.GroupId, request.UserId),
            request.GroupId,
            request.UserId,
            null,
            cancellation_token);
    }

    public ValueTask<GroupMembershipDispatchResult> Reject(
        GroupMemberRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateIds(request.GroupId, request.UserId);
        return Dispatch(
            MessageContracts.Groups.Membership.Reject,
            new RejectGroupMemberRequest(request.GroupId, request.UserId),
            request.GroupId,
            request.UserId,
            null,
            cancellation_token);
    }

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);

    private ValueTask<GroupMembershipDispatchResult> Dispatch<T>(
        MessageContract<T> contract,
        T message,
        Id group_id,
        Id? user_id,
        bool? block_rejoin,
        CancellationToken cancellation_token)
        where T : Qx.Messages.IParserComposer<T>
    {
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        message_dispatcher.Dispatch(
            contract,
            message,
            session,
            cancellation_token,
            () => RequireSession(session));
        RequireSession(session);
        return ValueTask.FromResult(new GroupMembershipDispatchResult(
            session.Client,
            time_provider.GetUtcNow(),
            group_id,
            user_id,
            block_rejoin));
    }

    private void RequireSession(Session session)
    {
        ThrowIfDisposed();
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed before dispatch.");
    }

    private static void ValidateIds(Id group_id, Id user_id)
    {
        ValidateId(group_id, nameof(group_id));
        ValidateId(user_id, nameof(user_id));
    }

    private static void ValidateId(Id value, string name)
    {
        if ((long)value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
}

internal sealed class RemotePeopleApplication : IApplicationFeature, IRemotePeopleOperations
{
    private readonly object lifecycle_sync = new();
    private readonly CancellationTokenSource feature_lifetime = new();
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly ProfileManager profile;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private int active_invocations;
    private bool dispose_completed;
    private int disposed;

    public RemotePeopleApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        this.game = game;
        profile = game.Profile;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<RemoteProfileGetRequest, RemoteProfileResult>(
                RemotePeopleApplicationDescriptors.ProfileGet,
                GetProfile),
            new ApplicationCallBinding<RemoteRelationshipGetRequest, RemoteRelationshipResult>(
                RemotePeopleApplicationDescriptors.RelationshipGet,
                GetRelationship),
            new ApplicationCallBinding<RemoteBadgesGetRequest, RemoteBadgesResult>(
                RemotePeopleApplicationDescriptors.BadgesGet,
                GetBadges),
            new ApplicationCallBinding<RemoteProfileOpenRequest, RemoteProfileOpenReceipt>(
                RemotePeopleApplicationDescriptors.ProfileOpen,
                Open)
        ]);
        game.BindRemotePeopleOperations(this);
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public ValueTask<RemoteProfileResult> GetProfile(
        RemoteProfileGetRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetProfileCore(request, token));

    public ValueTask<RemoteRelationshipResult> GetRelationship(
        RemoteRelationshipGetRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetRelationshipCore(request, token));

    public ValueTask<RemoteBadgesResult> GetBadges(
        RemoteBadgesGetRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetBadgesCore(request, token));

    public ValueTask<RemoteProfileOpenReceipt> Open(
        RemoteProfileOpenRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => OpenCore(request, token));

    private async ValueTask<RemoteProfileResult> GetProfileCore(
        RemoteProfileGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.UserId, nameof(request.UserId));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        RemotePeopleScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.UserId, nameof(request.UserId));
        UserProfile response = await requests.RequestAsync(
            MessageContracts.Users.ExtendedProfileRequest,
            new ExtendedProfileRequest(request.UserId, false),
            MessageContracts.Users.ExtendedProfileSnapshot,
            scope.Session,
            match: value =>
                value.Id == request.UserId &&
                !value.OpenProfileWindow &&
                ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        RemoteProfileView result = ProfileView(response);
        RequireScope(scope);
        return new RemoteProfileResult(
            scope.Session.Client,
            scope.Generation,
            received_at_utc,
            result);
    }

    private async ValueTask<RemoteRelationshipResult> GetRelationshipCore(
        RemoteRelationshipGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.UserId, nameof(request.UserId));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        RemotePeopleScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.UserId, nameof(request.UserId));
        RelationshipStatus response = await requests.RequestAsync(
            MessageContracts.Users.Relationship.Request,
            new RelationshipStatusRequest(request.UserId),
            MessageContracts.Users.Relationship.Snapshot,
            scope.Session,
            match: value => value.UserId == request.UserId && ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        IReadOnlyList<RelationshipEntry> entries = Relationships(response.Entries);
        RequireScope(scope);
        return new RemoteRelationshipResult(
            scope.Session.Client,
            scope.Generation,
            received_at_utc,
            response.UserId,
            entries);
    }

    private async ValueTask<RemoteBadgesResult> GetBadgesCore(
        RemoteBadgesGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.UserId, nameof(request.UserId));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        RemotePeopleScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.UserId, nameof(request.UserId));
        UserBadges response = await requests.RequestAsync(
            MessageContracts.Badges.SelectedRequest,
            new SelectedBadgesRequest(request.UserId),
            MessageContracts.Badges.Selected,
            scope.Session,
            match: value => value.UserId == request.UserId && ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        IReadOnlyList<SelectedBadge> badges = Badges(response.Badges);
        RequireScope(scope);
        return new RemoteBadgesResult(
            scope.Session.Client,
            scope.Generation,
            received_at_utc,
            response.UserId,
            badges);
    }

    private ValueTask<RemoteProfileOpenReceipt> OpenCore(
        RemoteProfileOpenRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.UserId, nameof(request.UserId));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        RemotePeopleScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.UserId, nameof(request.UserId));
        message_dispatcher.Dispatch(
            MessageContracts.Users.ExtendedProfileRequest,
            new ExtendedProfileRequest(request.UserId, true),
            scope.Session,
            cancellation_token,
            () => RequireScope(scope));
        RequireScope(scope);
        return ValueTask.FromResult(new RemoteProfileOpenReceipt(
            scope.Session.Client,
            scope.Generation,
            time_provider.GetUtcNow(),
            request.UserId));
    }

    RemoteProfileOpenReceipt IRemotePeopleOperations.OpenProfile(
        RemoteProfileOpenRequest request,
        CancellationToken cancellation_token) =>
        Open(request, cancellation_token).GetAwaiter().GetResult();

    public void Dispose()
    {
        lock (lifecycle_sync)
        {
            if (disposed != 0)
            {
                while (!dispose_completed)
                    Monitor.Wait(lifecycle_sync);
                return;
            }
            Volatile.Write(ref disposed, 1);
        }
        feature_lifetime.Cancel();
        lock (lifecycle_sync)
        {
            while (active_invocations != 0)
                Monitor.Wait(lifecycle_sync);
        }
        try
        {
            game.UnbindRemotePeopleOperations(this);
            feature_lifetime.Dispose();
        }
        finally
        {
            lock (lifecycle_sync)
            {
                dispose_completed = true;
                Monitor.PulseAll(lifecycle_sync);
            }
        }
    }

    private async ValueTask<TResult> Invoke<TResult>(
        CancellationToken cancellation_token,
        Func<CancellationToken, ValueTask<TResult>> invocation)
    {
        using RemotePeopleInvocation active = EnterInvocation(cancellation_token);
        try
        {
            return await invocation(active.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            feature_lifetime.IsCancellationRequested &&
            !cancellation_token.IsCancellationRequested)
        {
            throw Disposed();
        }
    }

    private RemotePeopleInvocation EnterInvocation(CancellationToken cancellation_token)
    {
        lock (lifecycle_sync)
        {
            ThrowIfDisposed();
            active_invocations = checked(active_invocations + 1);
        }
        try
        {
            return new RemotePeopleInvocation(
                this,
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellation_token,
                    feature_lifetime.Token));
        }
        catch
        {
            ExitInvocation();
            throw;
        }
    }

    private void ExitInvocation()
    {
        lock (lifecycle_sync)
        {
            active_invocations--;
            if (active_invocations < 0)
                throw new InvalidOperationException("The remote-people invocation count became negative.");
            if (active_invocations == 0)
                Monitor.PulseAll(lifecycle_sync);
        }
    }

    private RemotePeopleScope CaptureScope(
        long? expected_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        ProfileState state = profile.State;
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(state.Session, session))
            throw new InvalidOperationException("The profile state is not bound to the active hotel session.");
        if (expected_generation is long generation && generation != state.Generation)
            throw new InvalidOperationException("The expected hotel-session generation is no longer active.");
        return new RemotePeopleScope(session, state.Generation);
    }

    private bool ScopeActive(RemotePeopleScope scope)
    {
        ProfileState state = profile.State;
        return Volatile.Read(ref disposed) == 0 &&
            ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(state.Session, scope.Session) &&
            state.Generation == scope.Generation;
    }

    private void RequireScope(RemotePeopleScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeActive(scope))
            throw new InvalidOperationException("The hotel session changed during the remote-people operation.");
    }

    private static RemoteProfileView ProfileView(UserProfile value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ProfileApplicationCollectionLimits.Validate(value.Groups.Count, "profile group");
        ProfileApplicationCollectionLimits.Validate(value.BadgeRarities.Count, "profile badge-rarity");
        ProfileApplicationCollectionLimits.Validate(value.OldNames.Count, "profile old-name");
        var groups = new ProfileGroup[value.Groups.Count];
        for (int index = 0; index < groups.Length; index++)
        {
            ProfileGroup group = value.Groups[index];
            groups[index] = new ProfileGroup(
                group.Id,
                group.Name,
                group.BadgeCode,
                group.PrimaryColor,
                group.SecondaryColor,
                group.IsFavourite,
                group.OwnerId,
                group.HasForum);
        }
        return new RemoteProfileView(
            value.Id,
            value.Name,
            value.Figure,
            value.Motto,
            value.Created,
            value.AchievementScore,
            value.FriendCount,
            value.IsFriend,
            value.IsFriendRequestSent,
            value.OnlineStatus,
            Array.AsReadOnly(groups),
            value.LastAccessSeconds,
            value.OpenProfileWindow,
            value.IsHidden,
            value.Level,
            value.SubscriptionLevel,
            value.StarGems,
            value.AllowFriendRequests,
            value.HasFriendRequestsPending,
            value.TotalBadges,
            value.AchievementLevel,
            Array.AsReadOnly(value.BadgeRarities.ToArray()),
            value.TotalBadgesRank,
            value.NameColor,
            Array.AsReadOnly(value.OldNames.ToArray()));
    }

    private static IReadOnlyList<RelationshipEntry> Relationships(
        IReadOnlyList<RelationshipEntry> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ProfileApplicationCollectionLimits.Validate(values.Count, "relationship");
        var entries = new RelationshipEntry[values.Count];
        for (int index = 0; index < entries.Length; index++)
        {
            RelationshipEntry value = values[index];
            entries[index] = new RelationshipEntry(
                value.Type,
                value.FriendCount,
                value.RandomFriendId,
                value.RandomFriendName,
                value.RandomFriendFigure);
        }
        return Array.AsReadOnly(entries);
    }

    private static IReadOnlyList<SelectedBadge> Badges(IReadOnlyList<SelectedBadge> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ProfileApplicationCollectionLimits.Validate(values.Count, "selected-badge");
        var badges = new SelectedBadge[values.Count];
        for (int index = 0; index < badges.Length; index++)
        {
            SelectedBadge value = values[index];
            badges[index] = new SelectedBadge(
                value.Slot,
                value.Code,
                value.OwnerCount,
                value.RarityId,
                value.HasRarityData);
        }
        return Array.AsReadOnly(badges);
    }

    private static void ValidateId(Id value, string name)
    {
        if ((long)value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateWireId(ClientType client, Id value, string name)
    {
        ValidateId(value, name);
        if (client is ClientType.Flash && (long)value > int.MaxValue)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateGeneration(long? generation, string name)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static ObjectDisposedException Disposed() => new(nameof(RemotePeopleApplication));

    private readonly record struct RemotePeopleScope(Session Session, long Generation);

    private sealed class RemotePeopleInvocation(
        RemotePeopleApplication owner,
        CancellationTokenSource cancellation) : IDisposable
    {
        private int disposed;

        public CancellationToken Token => cancellation.Token;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            cancellation.Dispose();
            owner.ExitInvocation();
        }
    }
}

internal sealed class GroupReadsApplication : IApplicationFeature
{
    private const int membership_snapshot_limit = 16;
    private const int membership_snapshot_entry_limit = ushort.MaxValue;

    private readonly object lifecycle_sync = new();
    private readonly CancellationTokenSource feature_lifetime = new();
    private readonly IConnection connection;
    private readonly ProfileManager profile;
    private readonly RequestBroker requests;
    private readonly TimeProvider time_provider;
    private readonly object membership_sync = new();
    private readonly Dictionary<long, GroupMembershipSnapshotLease> membership_snapshots = [];
    private long membership_snapshot_revision;
    private int active_invocations;
    private bool dispose_completed;
    private int disposed;

    public GroupReadsApplication(
        IConnection connection,
        GameState game,
        TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        profile = game.Profile;
        requests = game.Requests;
        this.time_provider = time_provider;
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<GroupDetailsGetRequest, GroupDetailsResult>(
                GroupReadsApplicationDescriptors.DetailsGet,
                GetDetails),
            new ApplicationCallBinding<GroupMembersPageRequest, GroupMembersPage>(
                GroupReadsApplicationDescriptors.MembersPage,
                GetMembersPage),
            new ApplicationCallBinding<GroupMembershipsGetRequest, GroupMembershipsPage>(
                GroupReadsApplicationDescriptors.MembershipsGet,
                GetMemberships)
        ]);
        profile.StateChanged += OnProfileStateChanged;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public ValueTask<GroupDetailsResult> GetDetails(
        GroupDetailsGetRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetDetailsCore(request, token));

    public ValueTask<GroupMembersPage> GetMembersPage(
        GroupMembersPageRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetMembersPageCore(request, token));

    public ValueTask<GroupMembershipsPage> GetMemberships(
        GroupMembershipsGetRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetMembershipsCore(request, token));

    private async ValueTask<GroupDetailsResult> GetDetailsCore(
        GroupDetailsGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.GroupId, nameof(request.GroupId));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        GroupReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.GroupId, nameof(request.GroupId));
        GroupData response = await requests.RequestAsync(
            MessageContracts.Groups.Details.Request,
            new GroupDetailsRequest(request.GroupId, false),
            MessageContracts.Groups.Details.Snapshot,
            scope.Session,
            match: value =>
                value.Id == request.GroupId &&
                !value.OpenDetails &&
                ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        GroupData details = GroupDetails(response);
        RequireScope(scope);
        return new GroupDetailsResult(
            scope.Session.Client,
            scope.Generation,
            received_at_utc,
            details);
    }

    private async ValueTask<GroupMembersPage> GetMembersPageCore(
        GroupMembersPageRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.GroupId, nameof(request.GroupId));
        ArgumentOutOfRangeException.ThrowIfNegative(request.PageIndex);
        ValidateFilter(request.UserNameFilter);
        if (!Enum.IsDefined(request.SearchType))
            throw new ArgumentOutOfRangeException(nameof(request.SearchType));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        GroupReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.GroupId, nameof(request.GroupId));
        if (scope.Session.Client is ClientType.Unity &&
            request.SearchType is not GuildMemberSearchType.All)
        {
            throw new NotSupportedException("Unity group-member reads support only the All search type.");
        }
        GuildMembers response = await requests.RequestAsync(
            MessageContracts.Groups.Members.Request,
            new GetGuildMembersRequest(
                request.GroupId,
                request.PageIndex,
                request.UserNameFilter,
                request.SearchType),
            MessageContracts.Groups.Members.Snapshot,
            scope.Session,
            match: value => Matches(request, scope, value),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        ValidateMembersPage(response);
        IReadOnlyList<GuildMember> entries = Members(response.Entries);
        RequireScope(scope);
        return new GroupMembersPage(
            scope.Session.Client,
            scope.Generation,
            received_at_utc,
            response.GroupId,
            response.GroupName,
            response.BaseRoomId,
            response.BadgeCode,
            response.TotalEntries,
            entries,
            response.IsAllowedToManage,
            response.PageSize,
            response.PageIndex,
            response.SearchType,
            response.UserNameFilter);
    }

    private async ValueTask<GroupMembershipsPage> GetMembershipsCore(
        GroupMembershipsGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Offset, request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        if (request.SnapshotRevision is null && request.Offset != 0)
        {
            throw new ArgumentException(
                "A new membership snapshot must be requested from offset zero.",
                nameof(request));
        }
        GroupReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        GroupMembershipSnapshotLease snapshot;
        if (request.SnapshotRevision is long revision)
        {
            snapshot = MembershipSnapshot(scope, revision);
        }
        else
        {
            GuildMemberships response = await requests.RequestAsync(
                MessageContracts.Groups.Memberships.Request,
                new GuildMembershipsRequest(),
                MessageContracts.Groups.Memberships.Snapshot,
                scope.Session,
                match: _ => ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: cancellation_token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            DateTimeOffset received_at_utc = time_provider.GetUtcNow();
            snapshot = StoreMembershipSnapshot(scope, response, received_at_utc);
        }
        RequireScope(scope);
        if (request.Offset > snapshot.Memberships.Count)
            throw new ArgumentOutOfRangeException(nameof(request.Offset));
        IReadOnlyList<GuildMembership> memberships = Slice(
            snapshot.Memberships,
            request.Offset,
            request.Limit);
        return new GroupMembershipsPage(
            snapshot.Client,
            snapshot.Generation,
            snapshot.ReceivedAtUtc,
            snapshot.Revision,
            snapshot.Memberships.Count,
            request.Offset,
            NextOffset(request.Offset, memberships.Count, snapshot.Memberships.Count),
            memberships);
    }

    public void Dispose()
    {
        lock (lifecycle_sync)
        {
            if (disposed != 0)
            {
                while (!dispose_completed)
                    Monitor.Wait(lifecycle_sync);
                return;
            }
            Volatile.Write(ref disposed, 1);
        }
        feature_lifetime.Cancel();
        lock (lifecycle_sync)
        {
            while (active_invocations != 0)
                Monitor.Wait(lifecycle_sync);
        }
        try
        {
            profile.StateChanged -= OnProfileStateChanged;
            lock (membership_sync)
                membership_snapshots.Clear();
            feature_lifetime.Dispose();
        }
        finally
        {
            lock (lifecycle_sync)
            {
                dispose_completed = true;
                Monitor.PulseAll(lifecycle_sync);
            }
        }
    }

    private async ValueTask<TResult> Invoke<TResult>(
        CancellationToken cancellation_token,
        Func<CancellationToken, ValueTask<TResult>> invocation)
    {
        using GroupReadInvocation active = EnterInvocation(cancellation_token);
        try
        {
            return await invocation(active.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            feature_lifetime.IsCancellationRequested &&
            !cancellation_token.IsCancellationRequested)
        {
            throw Disposed();
        }
    }

    private GroupReadInvocation EnterInvocation(CancellationToken cancellation_token)
    {
        lock (lifecycle_sync)
        {
            ThrowIfDisposed();
            active_invocations = checked(active_invocations + 1);
        }
        try
        {
            return new GroupReadInvocation(
                this,
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellation_token,
                    feature_lifetime.Token));
        }
        catch
        {
            ExitInvocation();
            throw;
        }
    }

    private void ExitInvocation()
    {
        lock (lifecycle_sync)
        {
            active_invocations--;
            if (active_invocations < 0)
                throw new InvalidOperationException("The group-read invocation count became negative.");
            if (active_invocations == 0)
                Monitor.PulseAll(lifecycle_sync);
        }
    }

    private GroupReadScope CaptureScope(
        long? expected_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        ProfileState state = profile.State;
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(state.Session, session))
            throw new InvalidOperationException("The profile state is not bound to the active hotel session.");
        if (expected_generation is long generation && generation != state.Generation)
            throw new InvalidOperationException("The expected hotel-session generation is no longer active.");
        return new GroupReadScope(session, state.Generation);
    }

    private bool ScopeActive(GroupReadScope scope)
    {
        ProfileState state = profile.State;
        return Volatile.Read(ref disposed) == 0 &&
            ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(state.Session, scope.Session) &&
            state.Generation == scope.Generation;
    }

    private void RequireScope(GroupReadScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeActive(scope))
            throw new InvalidOperationException("The hotel session changed during the group-read operation.");
    }

    private bool Matches(
        GroupMembersPageRequest request,
        GroupReadScope scope,
        GuildMembers response)
    {
        GuildMemberSearchType? expected_search_type = scope.Session.Client is ClientType.Flash
            ? request.SearchType
            : null;
        return ScopeActive(scope) &&
            response.GroupId == request.GroupId &&
            response.PageIndex == request.PageIndex &&
            string.Equals(response.UserNameFilter, request.UserNameFilter, StringComparison.Ordinal) &&
            response.SearchType == expected_search_type;
    }

    private GroupMembershipSnapshotLease StoreMembershipSnapshot(
        GroupReadScope scope,
        GuildMemberships response,
        DateTimeOffset received_at_utc)
    {
        if (response.Items.Count > membership_snapshot_entry_limit)
            throw new InvalidDataException("The group-membership snapshot exceeds the bounded entry limit.");
        IReadOnlyList<GuildMembership> memberships = Memberships(response.Items);
        RequireScope(scope);
        long revision = Interlocked.Increment(ref membership_snapshot_revision);
        if (revision <= 0)
            throw new InvalidOperationException("The group-membership snapshot revision space is exhausted.");
        var snapshot = new GroupMembershipSnapshotLease(
            scope.Session,
            scope.Session.Client,
            scope.Generation,
            received_at_utc,
            revision,
            memberships);
        lock (membership_sync)
        {
            RequireScope(scope);
            foreach (long stale_revision in membership_snapshots
                .Where(entry =>
                    !ReferenceEquals(entry.Value.Session, scope.Session) ||
                    entry.Value.Generation != scope.Generation)
                .Select(entry => entry.Key)
                .ToArray())
            {
                membership_snapshots.Remove(stale_revision);
            }
            membership_snapshots.Add(revision, snapshot);
            while (membership_snapshots.Count > membership_snapshot_limit)
                membership_snapshots.Remove(membership_snapshots.Keys.Min());
        }
        return snapshot;
    }

    private GroupMembershipSnapshotLease MembershipSnapshot(
        GroupReadScope scope,
        long revision)
    {
        lock (membership_sync)
        {
            RequireScope(scope);
            if (!membership_snapshots.TryGetValue(revision, out GroupMembershipSnapshotLease? snapshot) ||
                !ReferenceEquals(snapshot.Session, scope.Session) ||
                snapshot.Generation != scope.Generation)
            {
                throw new InvalidOperationException(
                    "The group-membership snapshot is unavailable for the active session.");
            }
            return snapshot;
        }
    }

    private void OnProfileStateChanged(ProfileStateUpdate update)
    {
        if (update.Kind is not ProfileStateChangeKind.Reset)
            return;
        lock (membership_sync)
            membership_snapshots.Clear();
    }

    private static void ValidateMembersPage(GuildMembers value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.TotalEntries < 0)
            throw new InvalidDataException("The group-member total cannot be negative.");
        if (value.PageSize < 0)
            throw new InvalidDataException("The group-member page size cannot be negative.");
        if (value.PageSize > ProfileApplicationCollectionLimits.MaximumEntries)
            throw new InvalidDataException("The group-member page size exceeds the bounded collection limit.");
        if (value.PageIndex < 0)
            throw new InvalidDataException("The group-member page index cannot be negative.");
        ProfileApplicationCollectionLimits.Validate(value.Entries.Count, "group-member page");
        if (value.Entries.Count > value.TotalEntries)
            throw new InvalidDataException("The group-member page exceeds the reported total.");
        if (value.Entries.Count > value.PageSize)
            throw new InvalidDataException("The group-member page exceeds the reported page size.");
        long total_pages = value.PageSize == 0
            ? 1
            : Math.Max(1L, ((long)value.TotalEntries + value.PageSize - 1) / value.PageSize);
        if (value.PageIndex >= total_pages)
            throw new InvalidDataException("The group-member page index exceeds the reported page count.");
        if (value.TotalEntries == 0)
        {
            if (value.Entries.Count != 0)
                throw new InvalidDataException("An empty group-member result cannot contain entries.");
            return;
        }
        if (value.PageSize == 0 || value.Entries.Count == 0)
            throw new InvalidDataException("A non-empty group-member result requires a non-empty page.");
        long page_offset = checked((long)value.PageIndex * value.PageSize);
        long remaining = value.TotalEntries - page_offset;
        if (remaining <= 0 || value.Entries.Count > remaining)
            throw new InvalidDataException("The group-member page exceeds its remaining result range.");
    }

    private static GroupData GroupDetails(GroupData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new GroupData(
            value.Id,
            value.IsGuild,
            value.Type,
            value.Name,
            value.Description,
            value.BadgeCode,
            value.RoomId,
            value.RoomName,
            value.MemberStatus,
            value.MemberCount,
            value.IsFavourite,
            value.Created,
            value.IsOwner,
            value.IsAdmin,
            value.OwnerName,
            value.OpenDetails,
            value.MembersCanDecorate,
            value.PendingMemberCount,
            value.HasBoard,
            value.UnityExtensionId);
    }

    private static IReadOnlyList<GuildMember> Members(IReadOnlyList<GuildMember> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var members = new GuildMember[values.Count];
        for (int index = 0; index < members.Length; index++)
        {
            GuildMember value = values[index];
            members[index] = new GuildMember(
                value.Type,
                value.Id,
                value.Name,
                value.Figure,
                value.MemberSince);
        }
        return Array.AsReadOnly(members);
    }

    private static IReadOnlyList<GuildMembership> Memberships(
        IReadOnlyList<GuildMembership> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var memberships = new GuildMembership[values.Count];
        for (int index = 0; index < memberships.Length; index++)
        {
            GuildMembership value = values[index];
            memberships[index] = new GuildMembership(
                value.Id,
                value.Name,
                value.BadgeCode,
                value.PrimaryColor,
                value.SecondaryColor,
                value.IsFavorite,
                value.OwnerId,
                value.HasForum);
        }
        return Array.AsReadOnly(memberships);
    }

    private static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> values, int offset, int limit)
    {
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }

    private static void ValidatePaging(int offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > ProfileApplicationCollectionLimits.MaximumEntries)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateFilter(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void ValidateId(Id value, string name)
    {
        if ((long)value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateWireId(ClientType client, Id value, string name)
    {
        ValidateId(value, name);
        if (client is ClientType.Flash && (long)value > int.MaxValue)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateGeneration(long? generation, string name)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static ObjectDisposedException Disposed() => new(nameof(GroupReadsApplication));

    private readonly record struct GroupReadScope(Session Session, long Generation);

    private sealed record GroupMembershipSnapshotLease(
        Session Session,
        ClientType Client,
        long Generation,
        DateTimeOffset ReceivedAtUtc,
        long Revision,
        IReadOnlyList<GuildMembership> Memberships);

    private sealed class GroupReadInvocation(
        GroupReadsApplication owner,
        CancellationTokenSource cancellation) : IDisposable
    {
        private int disposed;

        public CancellationToken Token => cancellation.Token;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            cancellation.Dispose();
            owner.ExitInvocation();
        }
    }
}
