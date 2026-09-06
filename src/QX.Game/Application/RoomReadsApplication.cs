using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

internal sealed class RoomReadsApplication : IApplicationFeature
{
    private readonly object lifecycle_sync = new();
    private readonly CancellationTokenSource feature_lifetime = new();
    private readonly IConnection connection;
    private readonly ProfileManager profile;
    private readonly RequestBroker requests;
    private readonly TimeProvider time_provider;
    private int active_invocations;
    private bool dispose_completed;
    private int disposed;

    public RoomReadsApplication(
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
            new ApplicationCallBinding<RoomDataReadRequest, RoomDataReadResult>(
                RoomReadsApplicationDescriptors.DataGet,
                GetRoomData),
            new ApplicationCallBinding<RoomRightsReadRequest, RoomRightsReadResult>(
                RoomReadsApplicationDescriptors.RightsList,
                GetRoomRights),
            new ApplicationCallBinding<PetInfoReadRequest, PetInfoReadResult>(
                RoomReadsApplicationDescriptors.PetInfoGet,
                GetPetInfo),
            new ApplicationCallBinding<StickyReadRequest, StickyReadResult>(
                RoomReadsApplicationDescriptors.StickyGet,
                GetSticky),
            new ApplicationCallBinding<RoomAdInfoReadRequest, RoomAdInfoReadResult>(
                RoomReadsApplicationDescriptors.RoomAdInfoGet,
                GetRoomAdInfo)
        ]);
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public ValueTask<RoomDataReadResult> GetRoomData(
        RoomDataReadRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetRoomDataCore(request, token));

    public ValueTask<RoomRightsReadResult> GetRoomRights(
        RoomRightsReadRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetRoomRightsCore(request, token));

    public ValueTask<PetInfoReadResult> GetPetInfo(
        PetInfoReadRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetPetInfoCore(request, token));

    public ValueTask<StickyReadResult> GetSticky(
        StickyReadRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetStickyCore(request, token));

    public ValueTask<RoomAdInfoReadResult> GetRoomAdInfo(
        RoomAdInfoReadRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, token => GetRoomAdInfoCore(request, token));

    private async ValueTask<RoomDataReadResult> GetRoomDataCore(
        RoomDataReadRequest request,
        CancellationToken cancellation_token)
    {
        Validate(request.RoomId, request.TimeoutMilliseconds, request.ExpectedSessionGeneration);
        RoomReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.RoomId);
        GuestRoomResult response = await requests.RequestAsync(
            MessageContracts.Room.SnapshotRequest,
            new GetGuestRoomRequest(request.RoomId, false, false),
            MessageContracts.Room.Snapshot,
            scope.Session,
            match: value =>
                value.Data is { } data &&
                data.Id == request.RoomId &&
                ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => RequireDispatch(scope, cancellation_token)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        RoomDataView room = Snapshot(response.Data);
        RequireScope(scope);
        return new RoomDataReadResult(
            scope.Session.Client,
            received_at_utc,
            scope.Generation,
            request.RoomId,
            1,
            room);
    }

    private async ValueTask<RoomRightsReadResult> GetRoomRightsCore(
        RoomRightsReadRequest request,
        CancellationToken cancellation_token)
    {
        Validate(request.RoomId, request.TimeoutMilliseconds, request.ExpectedSessionGeneration);
        RoomReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.RoomId);
        RightsList response = await requests.RequestAsync(
            MessageContracts.Room.Authority.ControllersRequest,
            new GetFlatControllersRequest(request.RoomId),
            MessageContracts.Room.Authority.ControllersSnapshot,
            scope.Session,
            match: value => value.RoomId == request.RoomId && ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => RequireDispatch(scope, cancellation_token)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        IReadOnlyList<IdName> users = Freeze(response.Users);
        RequireScope(scope);
        return new RoomRightsReadResult(
            scope.Session.Client,
            received_at_utc,
            scope.Generation,
            response.RoomId,
            1,
            users);
    }

    private async ValueTask<PetInfoReadResult> GetPetInfoCore(
        PetInfoReadRequest request,
        CancellationToken cancellation_token)
    {
        Validate(request.PetId, request.TimeoutMilliseconds, request.ExpectedSessionGeneration);
        RoomReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.PetId);
        PetInfo response = await requests.RequestAsync(
            MessageContracts.Room.Occupants.Pet.InfoRequest,
            new GetPetInfoRequest(request.PetId),
            MessageContracts.Room.Occupants.Pet.Info,
            scope.Session,
            match: value => value.Id == request.PetId && ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => RequireDispatch(scope, cancellation_token)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        PetInfoView pet = Snapshot(response);
        RequireScope(scope);
        return new PetInfoReadResult(
            scope.Session.Client,
            received_at_utc,
            scope.Generation,
            request.PetId,
            1,
            pet);
    }

    private async ValueTask<StickyReadResult> GetStickyCore(
        StickyReadRequest request,
        CancellationToken cancellation_token)
    {
        Validate(request.ItemId, request.TimeoutMilliseconds, request.ExpectedSessionGeneration);
        RoomReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        ValidateWireId(scope.Session.Client, request.ItemId);
        Sticky response = await requests.RequestAsync(
            MessageContracts.Room.WallItem.StickyDataRequest,
            new GetStickyDataRequest(request.ItemId),
            MessageContracts.Room.WallItem.StickyData,
            scope.Session,
            match: value => value.Id == request.ItemId && ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => RequireDispatch(scope, cancellation_token)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        RequireScope(scope);
        return new StickyReadResult(
            scope.Session.Client,
            received_at_utc,
            scope.Generation,
            response.Id,
            1,
            response.Color,
            response.Text);
    }

    private async ValueTask<RoomAdInfoReadResult> GetRoomAdInfoCore(
        RoomAdInfoReadRequest request,
        CancellationToken cancellation_token)
    {
        Validate(request.TimeoutMilliseconds, request.ExpectedSessionGeneration);
        RoomReadScope scope = CaptureScope(request.ExpectedSessionGeneration, cancellation_token);
        RoomAdPurchaseInfo response = await requests.RequestAsync(
            MessageContracts.Catalog.RoomAdInfoRequest,
            new GetRoomAdPurchaseInfo(),
            MessageContracts.Catalog.RoomAdInfo,
            scope.Session,
            match: _ => ScopeActive(scope),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => RequireDispatch(scope, cancellation_token)).ConfigureAwait(false);
        DateTimeOffset received_at_utc = time_provider.GetUtcNow();
        IReadOnlyList<RoomAdRoomView> rooms = Snapshot(response.Rooms);
        RequireScope(scope);
        return new RoomAdInfoReadResult(
            scope.Session.Client,
            received_at_utc,
            scope.Generation,
            1,
            response.IsVip,
            rooms);
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
        using RoomReadInvocation active = EnterInvocation(cancellation_token);
        try
        {
            return await invocation(active.Token).ConfigureAwait(false);
        }
        catch (Exception) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (feature_lifetime.IsCancellationRequested)
        {
            throw Disposed();
        }
    }

    private RoomReadInvocation EnterInvocation(CancellationToken cancellation_token)
    {
        lock (lifecycle_sync)
        {
            ThrowIfDisposed();
            active_invocations = checked(active_invocations + 1);
        }
        try
        {
            return new RoomReadInvocation(
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
                throw new InvalidOperationException("The room-read invocation count became negative.");
            if (active_invocations == 0)
                Monitor.PulseAll(lifecycle_sync);
        }
    }

    private RoomReadScope CaptureScope(
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
        return new RoomReadScope(session, state.Generation);
    }

    private bool ScopeActive(RoomReadScope scope)
    {
        ProfileState state = profile.State;
        return Volatile.Read(ref disposed) == 0 &&
            ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(state.Session, scope.Session) &&
            state.Generation == scope.Generation;
    }

    private void RequireScope(RoomReadScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeActive(scope))
            throw new InvalidOperationException("The hotel session changed during the room-read operation.");
    }

    private void RequireDispatch(RoomReadScope scope, CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        RequireScope(scope);
    }

    private static RoomDataView Snapshot(RoomData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Name);
        ArgumentNullException.ThrowIfNull(value.OwnerName);
        ArgumentNullException.ThrowIfNull(value.Description);
        ArgumentNullException.ThrowIfNull(value.GroupName);
        ArgumentNullException.ThrowIfNull(value.GroupBadge);
        ArgumentNullException.ThrowIfNull(value.EventName);
        ArgumentNullException.ThrowIfNull(value.EventDescription);
        return new RoomDataView(
            value.Id,
            value.Name,
            value.OwnerId,
            value.OwnerName,
            value.DoorMode,
            value.UserCount,
            value.MaxUserCount,
            value.Description,
            value.TradeMode,
            value.Score,
            value.Ranking,
            value.Category,
            value.Tags,
            value.OfficialRoomPicRef,
            value.HasGroup,
            value.GroupId,
            value.GroupName,
            value.GroupBadge,
            value.HasEvent,
            value.EventName,
            value.EventDescription,
            value.EventMinutesRemaining,
            value.ShowOwner,
            value.AllowPets,
            value.DisplayRoomEntryAd);
    }

    private static PetInfoView Snapshot(PetInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Name);
        ArgumentNullException.ThrowIfNull(value.OwnerName);
        return new PetInfoView(
            value.Id,
            value.Name,
            value.Level,
            value.MaxLevel,
            value.Experience,
            value.MaxExperience,
            value.Energy,
            value.MaxEnergy,
            value.Happiness,
            value.MaxHappiness,
            value.Scratches,
            value.OwnerId,
            value.Age,
            value.OwnerName,
            value.BreedId,
            value.HasFreeSaddle,
            value.IsRiding,
            value.SkillThresholds,
            value.AccessRights,
            value.CanBreed,
            value.CanHarvest,
            value.CanRevive,
            value.RarityLevel,
            value.MaxWellbeingSeconds,
            value.RemainingWellbeingSeconds,
            value.RemainingGrowingSeconds,
            value.HasBreedingPermission);
    }

    private static IReadOnlyList<IdName> Freeze(IReadOnlyList<IdName> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }

    private static IReadOnlyList<RoomAdRoomView> Snapshot(IReadOnlyList<RoomAdRoom> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > ushort.MaxValue)
            throw new InvalidDataException("The room advertisement list exceeds the supported limit.");
        var rooms = new RoomAdRoomView[values.Count];
        for (int index = 0; index < rooms.Length; index++)
        {
            RoomAdRoom value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.RoomName);
            rooms[index] = new RoomAdRoomView(value.RoomId, value.RoomName, value.HasControllers);
        }
        return Array.AsReadOnly(rooms);
    }

    private static void Validate(Id room_id, int timeout_milliseconds, long? generation)
    {
        if ((long)room_id <= 0)
            throw new ArgumentOutOfRangeException(nameof(room_id));
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
    }

    private static void Validate(int timeout_milliseconds, long? generation)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
    }

    private static void ValidateWireId(ClientType client, Id room_id)
    {
        if (client is ClientType.Flash && (long)room_id > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(room_id));
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static ObjectDisposedException Disposed() => new(nameof(RoomReadsApplication));

    private readonly record struct RoomReadScope(Session Session, long Generation);

    private sealed class RoomReadInvocation(
        RoomReadsApplication owner,
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
