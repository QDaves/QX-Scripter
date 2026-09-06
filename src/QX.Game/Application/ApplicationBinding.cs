namespace Qx.Game.Application;

internal interface IApplicationBinding
{
    ApplicationDescriptor Descriptor { get; }
    Type? RequestType { get; }
    Type ResultType { get; }
    bool CanInvoke { get; }
    bool CanSubscribe { get; }
    ValueTask<object?> InvokeAsync(object? request, CancellationToken cancellation_token = default);
    IDisposable Subscribe(Action<object?> receiver);
}

internal sealed class ApplicationCallBinding<TRequest, TResult> : IApplicationBinding
{
    private readonly Func<TRequest, CancellationToken, ValueTask<TResult>> invoke;

    public ApplicationCallBinding(
        ApplicationDescriptor descriptor,
        Func<TRequest, CancellationToken, ValueTask<TResult>> invoke)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(invoke);
        if (descriptor.Kind is ApplicationMemberKind.Event ||
            descriptor.RequestType != typeof(TRequest) ||
            descriptor.ResultType != typeof(TResult))
        {
            throw new ArgumentException("The binding types do not match the application descriptor.", nameof(descriptor));
        }
        Descriptor = descriptor;
        this.invoke = invoke;
    }

    public ApplicationDescriptor Descriptor { get; }
    public Type RequestType => typeof(TRequest);
    Type? IApplicationBinding.RequestType => RequestType;
    public Type ResultType => typeof(TResult);
    public bool CanInvoke => true;
    public bool CanSubscribe => false;

    public async ValueTask<TResult> InvokeAsync(
        TRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        return await invoke(request, cancellation_token).ConfigureAwait(false);
    }

    async ValueTask<object?> IApplicationBinding.InvokeAsync(
        object? request,
        CancellationToken cancellation_token)
    {
        if (request is not TRequest typed_request)
        {
            throw new ArgumentException(
                $"Application member '{Descriptor.Id}' requires '{typeof(TRequest).FullName}'.",
                nameof(request));
        }
        return await InvokeAsync(typed_request, cancellation_token).ConfigureAwait(false);
    }

    IDisposable IApplicationBinding.Subscribe(Action<object?> receiver) =>
        throw new InvalidOperationException($"Application member '{Descriptor.Id}' is not an event.");
}

internal sealed class ApplicationEventBinding<TEvent> : IApplicationBinding
{
    private readonly Func<Action<TEvent>, IDisposable> subscribe;

    public ApplicationEventBinding(
        ApplicationDescriptor descriptor,
        Func<Action<TEvent>, IDisposable> subscribe)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(subscribe);
        if (descriptor.Kind is not ApplicationMemberKind.Event ||
            descriptor.RequestType is not null ||
            descriptor.ResultType != typeof(TEvent))
        {
            throw new ArgumentException("The binding type does not match the application descriptor.", nameof(descriptor));
        }
        Descriptor = descriptor;
        this.subscribe = subscribe;
    }

    public ApplicationDescriptor Descriptor { get; }
    public Type? RequestType => null;
    public Type ResultType => typeof(TEvent);
    public bool CanInvoke => false;
    public bool CanSubscribe => true;

    ValueTask<object?> IApplicationBinding.InvokeAsync(object? request, CancellationToken cancellation_token) =>
        ValueTask.FromException<object?>(
            new InvalidOperationException($"Application member '{Descriptor.Id}' is an event."));

    public IDisposable Subscribe(Action<TEvent> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return subscribe(receiver);
    }

    IDisposable IApplicationBinding.Subscribe(Action<object?> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return Subscribe(value => receiver(value));
    }
}
