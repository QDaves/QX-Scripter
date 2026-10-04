using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;

namespace Qx.Game.Application;

/// <summary>Defines the runtime that describes, invokes and subscribes to application members.</summary>
/// <remarks>
/// The member ids are listed in <see cref="ApplicationMemberIds"/>. Queries and operations are
/// invoked with their request type, and events are subscribed to. The generic overloads check
/// their type arguments against the member and throw <see cref="ArgumentException"/> at the call
/// when they do not fit.
/// </remarks>
public interface IApplicationRuntime
{
    /// <summary>Gets the metadata of every application member, ordered by id.</summary>
    IReadOnlyList<ApplicationDescriptor> Members { get; }
    /// <summary>Gets the metadata and current availability of an application member.</summary>
    /// <param name="id">The member id, such as <c>room.chat.talk</c>.</param>
    /// <returns>The member metadata and its availability in the active session.</returns>
    ApplicationMemberDescription Describe(string id);
    /// <summary>Invokes a query or operation and blocks until it completes.</summary>
    /// <typeparam name="TRequest">The request type of the member, or a base or derived type of it.</typeparam>
    /// <typeparam name="TResult">The result type of the member, or a base type of it.</typeparam>
    /// <param name="id">The member id, such as <c>room.chat.talk</c>.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the member.</returns>
    TResult Invoke<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>Invokes a query or operation.</summary>
    /// <typeparam name="TRequest">The request type of the member, or a base or derived type of it.</typeparam>
    /// <typeparam name="TResult">The result type of the member, or a base type of it.</typeparam>
    /// <param name="id">The member id, such as <c>room.chat.talk</c>.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes with the result of the member.</returns>
    ValueTask<TResult> InvokeAsync<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>Invokes a query or operation with an untyped request.</summary>
    /// <param name="id">The member id, such as <c>room.chat.talk</c>.</param>
    /// <param name="request">The request, which must be an instance of the request type of the member.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes with the result of the member.</returns>
    ValueTask<object?> InvokeAsync(
        string id,
        object? request,
        CancellationToken cancellationToken = default);
    /// <summary>Subscribes to an application event.</summary>
    /// <typeparam name="TEvent">The value type of the event, or a base type of it.</typeparam>
    /// <param name="id">The event id, such as <c>room.chat.received</c>.</param>
    /// <param name="receiver">The action that receives each published value.</param>
    /// <returns>An object that ends the subscription when disposed.</returns>
    IDisposable Subscribe<TEvent>(string id, Action<TEvent> receiver);
    /// <summary>Subscribes to an application event with an untyped receiver.</summary>
    /// <param name="id">The event id, such as <c>room.chat.received</c>.</param>
    /// <param name="receiver">The action that receives each published value.</param>
    /// <returns>An object that ends the subscription when disposed.</returns>
    IDisposable Subscribe(string id, Action<object?> receiver);
}

/// <summary>Provides the application members of a game session: the queries, operations and events that scripts, the command line and the MCP server call.</summary>
/// <remarks>
/// An invocation first checks that the member is a query or operation, and the generic overloads
/// check their type arguments against the member, so a wrong call throws before the availability
/// check and before anything is sent. The invocation then checks the availability of the member
/// and throws <see cref="ApplicationUnavailableException"/> when a required state or message is
/// missing. Subscriptions do not check availability.
/// </remarks>
public sealed class ApplicationRuntime : IApplicationRuntime, IDisposable
{
    private readonly IReadOnlyList<IApplicationFeature> features;
    private readonly ApplicationCatalog catalog;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private int disposed;

    /// <summary>Initializes a new instance of the <see cref="ApplicationRuntime"/> class and creates every application feature.</summary>
    /// <param name="interceptor">The interceptor the members send and observe messages through.</param>
    /// <param name="game">The game state the members read and change.</param>
    /// <param name="contracts">The message contracts, which must use the message registry of <paramref name="interceptor"/>.</param>
    /// <param name="timeProvider">The time provider, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interceptor"/>, <paramref name="game"/> or <paramref name="contracts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="contracts"/> does not use the message registry of <paramref name="interceptor"/>.</exception>
    public ApplicationRuntime(
        IInterceptor interceptor,
        GameState game,
        MessageContractCatalog contracts,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(contracts);
        if (!ReferenceEquals(interceptor.Messages.Registry, contracts.Registry))
            throw new ArgumentException("The application runtime requires the interceptor's contract registry.", nameof(contracts));
        TimeProvider clock = timeProvider ?? TimeProvider.System;
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
                new RoomVariableFxApplication(
                    game,
                    ReportObserverError));
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
            message_dispatcher.Close();
            throw;
        }
    }

    private ApplicationAvailabilityResolver Availability { get; }
    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public IReadOnlyList<ApplicationDescriptor> Members
    {
        get
        {
            ThrowIfDisposed();
            return catalog.Descriptors;
        }
    }
    /// <summary>Occurs when an event receiver throws an exception.</summary>
    /// <remarks>The argument is the exception. Exceptions thrown by handlers of this event are ignored.</remarks>
    public event Action<Exception>? ObserverFailed;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no member has the id.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public ApplicationMemberDescription Describe(string id)
    {
        ThrowIfDisposed();
        ApplicationDescriptor descriptor = catalog.Describe(id);
        return new ApplicationMemberDescription(descriptor, Availability.Read(descriptor));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty or white space, or <paramref name="request"/> is not an instance of the request type of the member.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no member has the id.</exception>
    /// <exception cref="ApplicationUnavailableException">Thrown when the member is not available in the active session.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the member is an event.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public ValueTask<object?> InvokeAsync(
        string id,
        object? request,
        CancellationToken cancellationToken = default) =>
        invoke_member(invokable_descriptor(id), request, cancellationToken);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty or white space, <typeparamref name="TRequest"/> is not the request type of the member or a base or derived type of it, <typeparamref name="TResult"/> is not the result type of the member or a base type of it, or <paramref name="request"/> is not an instance of the request type of the member.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no member has the id.</exception>
    /// <exception cref="ApplicationUnavailableException">Thrown when the member is not available in the active session.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the member is an event.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public TResult Invoke<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<TRequest, TResult>(id, request, cancellationToken).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    /// <remarks>
    /// The id, the member kind and the type arguments are checked when the method is called, and a
    /// disposed runtime also throws at the call. Unavailability of the member, a request that is not
    /// an instance of its request type and failures of the member are reported through the returned
    /// task.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty or white space, <typeparamref name="TRequest"/> is not the request type of the member or a base or derived type of it, <typeparamref name="TResult"/> is not the result type of the member or a base type of it, or <paramref name="request"/> is not an instance of the request type of the member.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no member has the id.</exception>
    /// <exception cref="ApplicationUnavailableException">Thrown when the member is not available in the active session.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the member is an event.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public ValueTask<TResult> InvokeAsync<TRequest, TResult>(
        string id,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplicationDescriptor descriptor = invokable_descriptor(id);
        Type request_type = descriptor.RequestType!;
        bool request_fits = typeof(TRequest).IsAssignableFrom(request_type) ||
            request_type.IsAssignableFrom(typeof(TRequest));
        if (!request_fits || !typeof(TResult).IsAssignableFrom(descriptor.ResultType))
        {
            throw new ArgumentException(
                $"Application member '{id}' takes '{request_type}' and returns '{descriptor.ResultType}', not '{typeof(TRequest)}' and '{typeof(TResult)}'.",
                nameof(id));
        }
        return invoke_typed<TResult>(descriptor, request, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="receiver"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty or white space, or <typeparamref name="TEvent"/> is not the value type of the event or a base type of it.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no member has the id.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the member is not an event.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public IDisposable Subscribe<TEvent>(string id, Action<TEvent> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ThrowIfDisposed();
        ApplicationDescriptor descriptor = catalog.Describe(id);
        if (descriptor.Kind is not ApplicationMemberKind.Event)
            throw new InvalidOperationException($"Application member '{id}' is not an event.");
        if (!typeof(TEvent).IsAssignableFrom(descriptor.ResultType))
        {
            throw new ArgumentException(
                $"Application member '{id}' publishes '{descriptor.ResultType}', not '{typeof(TEvent)}'.",
                nameof(id));
        }
        return catalog.Subscribe(id, value => receiver((TEvent)value!));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="receiver"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no member has the id.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the member is not an event.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
    public IDisposable Subscribe(string id, Action<object?> receiver)
    {
        ThrowIfDisposed();
        return catalog.Subscribe(id, receiver);
    }

    /// <summary>Disposes every application feature in reverse creation order.</summary>
    /// <remarks>Calling the method again has no effect.</remarks>
    /// <exception cref="AggregateException">Thrown when one or more features fail to dispose.</exception>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        Exception[] errors = dispose_features(features);
        try
        {
            message_dispatcher.Close();
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

    private ApplicationDescriptor invokable_descriptor(string id)
    {
        ThrowIfDisposed();
        ApplicationDescriptor descriptor = catalog.Describe(id);
        if (descriptor.Kind is ApplicationMemberKind.Event)
            throw new InvalidOperationException($"Application member '{id}' is an event.");
        return descriptor;
    }

    private ValueTask<object?> invoke_member(
        ApplicationDescriptor descriptor,
        object? request,
        CancellationToken cancellation_token)
    {
        ApplicationAvailability availability = Availability.Read(descriptor);
        if (!availability.Available)
            throw new ApplicationUnavailableException(descriptor.Id, availability);
        return catalog.InvokeAsync(descriptor.Id, request, cancellation_token);
    }

    private async ValueTask<TResult> invoke_typed<TResult>(
        ApplicationDescriptor descriptor,
        object? request,
        CancellationToken cancellation_token) =>
        (TResult)(await invoke_member(descriptor, request, cancellation_token).ConfigureAwait(false))!;

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

/// <summary>Thrown when an application member is invoked while it is not available in the active session.</summary>
/// <remarks>
/// <see cref="Availability"/> names the missing states, the unresolved messages and the unavailable
/// wire capabilities.
/// </remarks>
/// <param name="memberId">The id of the member that was invoked.</param>
/// <param name="availability">The availability of the member when it was invoked.</param>
public sealed class ApplicationUnavailableException(
    string memberId,
    ApplicationAvailability availability) : InvalidOperationException(
        $"Application member '{memberId}' is unavailable for the active session.")
{
    /// <summary>Gets the id of the member that was invoked.</summary>
    public string MemberId { get; } = memberId;
    /// <summary>Gets the availability of the member when it was invoked.</summary>
    public ApplicationAvailability Availability { get; } = availability;
}
