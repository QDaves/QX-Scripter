using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;

namespace Qx.Game.Application;

public interface IApplicationRuntime
{
    IReadOnlyList<ApplicationDescriptor> Members { get; }
    ApplicationMemberDescription Describe(string id);
    TResult Invoke<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellation_token = default);
    ValueTask<TResult> InvokeAsync<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellation_token = default);
    ValueTask<object?> InvokeAsync(
        string id,
        object? request,
        CancellationToken cancellation_token = default);
    IDisposable Subscribe<TEvent>(string id, Action<TEvent> receiver);
    IDisposable Subscribe(string id, Action<object?> receiver);
}

public sealed class ApplicationRuntime : IApplicationRuntime, IDisposable
{
    private readonly IReadOnlyList<IApplicationFeature> features;
    private readonly ApplicationCatalog catalog;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private int disposed;

    public ApplicationRuntime(
        IInterceptor interceptor,
        GameState game,
        MessageContractCatalog contracts,
        TimeProvider? time_provider = null)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(contracts);
        if (!ReferenceEquals(interceptor.Messages.Registry, contracts.Registry))
            throw new ArgumentException("The application runtime requires the interceptor's contract registry.", nameof(contracts));
        TimeProvider clock = time_provider ?? TimeProvider.System;
        Availability = new ApplicationAvailabilityResolver(interceptor, game, contracts);
        message_dispatcher = new ApplicationMessageDispatcher();

        var created_features = new List<IApplicationFeature>();
        try
        {
            message_dispatcher.Attach(interceptor);
            created_features.Add(
                new RoomChatApplication(
                    interceptor,
                    game,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new RoomAvatarApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new RoomItemApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new RoomLifecycleApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new RoomControlApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new RoomPeopleControlApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new RoomModerationApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new RoomSettingsApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new RoomReadsApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new ProfileApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new RemotePeopleApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock));
            created_features.Add(
                new GroupReadsApplication(
                    interceptor,
                    game,
                    clock));
            created_features.Add(
                new CatalogApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new CraftingApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new AchievementApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new EarningApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new DailyTaskApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new QuestApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new ForumApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new LeaderboardApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new HabbiconApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new GiftApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new SubscriptionApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new WalletApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new InventoryApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new PollApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new RoomPlacementApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new TradeApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new GroupMembershipApplication(
                    interceptor,
                    message_dispatcher,
                    clock));
            created_features.Add(
                new NavigatorApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new MarketplaceApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new FriendsApplication(
                    interceptor,
                    game,
                    clock,
                    ReportObserverError));
            created_features.Add(
                new WiredApplication(
                    interceptor,
                    game,
                    message_dispatcher,
                    clock,
                    ReportObserverError));
            features = Array.AsReadOnly(created_features.ToArray());
            catalog = new ApplicationCatalog(
                features.SelectMany(feature => feature.Bindings));
        }
        catch
        {
            dispose_features(created_features);
            message_dispatcher.Dispose();
            throw;
        }
    }

    private ApplicationAvailabilityResolver Availability { get; }
    public IReadOnlyList<ApplicationDescriptor> Members
    {
        get
        {
            ThrowIfDisposed();
            return catalog.Descriptors;
        }
    }
    public event Action<Exception>? ObserverFailed;

    public ApplicationMemberDescription Describe(string id)
    {
        ThrowIfDisposed();
        ApplicationDescriptor descriptor = catalog.Describe(id);
        return new ApplicationMemberDescription(descriptor, Availability.Read(descriptor));
    }

    public ValueTask<object?> InvokeAsync(
        string id,
        object? request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ApplicationDescriptor descriptor = catalog.Describe(id);
        ApplicationAvailability availability = Availability.Read(descriptor);
        if (!availability.Available)
            throw new ApplicationUnavailableException(descriptor.Id, availability);
        return catalog.InvokeAsync(id, request, cancellation_token);
    }

    public TResult Invoke<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellation_token = default)
    {
        object? result = InvokeAsync(id, request, cancellation_token).AsTask().GetAwaiter().GetResult();
        return result is TResult typed_result
            ? typed_result
            : throw new InvalidOperationException(
                $"Application member '{id}' returned an unexpected result type.");
    }

    public async ValueTask<TResult> InvokeAsync<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellation_token = default)
    {
        object? result = await InvokeAsync(id, request, cancellation_token).ConfigureAwait(false);
        return result is TResult typed_result
            ? typed_result
            : throw new InvalidOperationException(
                $"Application member '{id}' returned an unexpected result type.");
    }

    public IDisposable Subscribe<TEvent>(string id, Action<TEvent> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return Subscribe(id, value =>
        {
            if (value is not TEvent typed_value)
            {
                throw new InvalidOperationException(
                    $"Application member '{id}' published an unexpected event type.");
            }
            receiver(typed_value);
        });
    }

    public IDisposable Subscribe(string id, Action<object?> receiver)
    {
        ThrowIfDisposed();
        return catalog.Subscribe(id, receiver);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        Exception[] errors = dispose_features(features);
        try
        {
            message_dispatcher.Dispose();
        }
        catch (Exception error)
        {
            errors = [.. errors, error];
        }
        ObserverFailed = null;
        if (errors.Length != 0)
            throw new AggregateException("One or more application features failed to dispose.", errors);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private void ReportObserverError(Exception error)
    {
        Action<Exception>? listeners = ObserverFailed;
        if (listeners is null)
            return;
        foreach (Action<Exception> listener in listeners.GetInvocationList().Cast<Action<Exception>>())
        {
            try
            {
                listener(error);
            }
            catch
            {
            }
        }
    }

    private static Exception[] dispose_features(
        IReadOnlyList<IApplicationFeature> feature_list)
    {
        List<Exception>? errors = null;
        for (int index = feature_list.Count - 1; index >= 0; index--)
        {
            try
            {
                feature_list[index].Dispose();
            }
            catch (Exception error)
            {
                (errors ??= []).Add(error);
            }
        }
        return errors?.ToArray() ?? [];
    }
}

internal sealed class ApplicationMessageDispatcher : GameStateManager
{
    protected override void OnAttach()
    {
    }

    public void Dispatch<T>(
        MessageContract<T> contract,
        T message,
        Session session,
        CancellationToken cancellation_token,
        Action? dispatch_guard = null)
        where T : IParserComposer<T> =>
        SendMessage(contract, message, session, cancellation_token, dispatch_guard);
}

public sealed class ApplicationUnavailableException(
    string member_id,
    ApplicationAvailability availability) : InvalidOperationException(
        $"Application member '{member_id}' is unavailable for the active session.")
{
    public string MemberId { get; } = member_id;
    public ApplicationAvailability Availability { get; } = availability;
}
