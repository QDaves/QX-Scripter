using Qx.Game.Application;
using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    private CancellationToken ScopedToken(CancellationToken cancellation_token) =>
        cancellation_token.CanBeCanceled ? cancellation_token : Ct;

    private sealed class ScopedInterceptor(IInterceptor inner, ScriptGlobals globals) : IInterceptor
    {
        private readonly Dictionary<Delegate, Stack<IDisposable>> _handlers = [];

        public IMessageResolver Messages => inner.Messages;

        public bool IsConnected => inner.IsConnected;

        public Session? Session => inner.Session;

        public event Action<Session>? Connected
        {
            add => Add(value, handler => inner.Connected += handler, handler => inner.Connected -= handler);
            remove => Remove(value);
        }

        public event Action? Disconnected
        {
            add => Add(value, handler => inner.Disconnected += handler, handler => inner.Disconnected -= handler);
            remove => Remove(value);
        }

        public event Action<Intercept>? Intercepted
        {
            add => Add(value, handler => inner.Intercepted += handler, handler => inner.Intercepted -= handler);
            remove => Remove(value);
        }

        public Task WaitForCatalogBuildAsync(CancellationToken cancellationToken) =>
            inner.WaitForCatalogBuildAsync(globals.ScopedToken(cancellationToken));

        public InterceptorSessionCatalog CaptureSessionCatalog() => inner.CaptureSessionCatalog();

        public void Send(IPacket packet) => inner.Send(packet);

        public void Send(IPacket packet, Session? expectedSession) => inner.Send(packet, expectedSession);

        public void Send(
            IPacket packet,
            Session? expectedSession,
            SessionCatalogBinding? expectedCatalog) =>
            inner.Send(packet, expectedSession, expectedCatalog);

        public void Send(
            IPacket packet,
            Session? expectedSession,
            SessionCatalogBinding? expectedCatalog,
            Action? dispatchGuard) =>
            inner.Send(packet, expectedSession, expectedCatalog, dispatchGuard);

        public IDisposable Intercept(Header header, Action<Intercept> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            return globals.Track(inner.Intercept(header, globals.Guarded(callback)));
        }

        public IDisposable Intercept(Identifier identifier, Action<Intercept> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            return globals.Track(inner.Intercept(identifier, globals.Guarded(callback)));
        }

        public IDisposable Intercept(MessageKey key, Action<Intercept> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            return globals.Track(inner.Intercept(key, globals.Guarded(callback)));
        }

        private void Add(Action? handler, Action<Action> add, Action<Action> remove)
        {
            if (handler is not null)
                Keep(handler, globals.Subscribe(handler, add, remove));
        }

        private void Add<T>(Action<T>? handler, Action<Action<T>> add, Action<Action<T>> remove)
        {
            if (handler is not null)
                Keep(handler, globals.Subscribe(handler, add, remove));
        }

        private void Keep(Delegate handler, IDisposable subscription)
        {
            lock (_handlers)
            {
                if (!_handlers.TryGetValue(handler, out Stack<IDisposable>? subscriptions))
                    _handlers.Add(handler, subscriptions = new Stack<IDisposable>());
                subscriptions.Push(subscription);
            }
        }

        private void Remove(Delegate? handler)
        {
            if (handler is null)
                return;
            IDisposable subscription;
            lock (_handlers)
            {
                if (!_handlers.TryGetValue(handler, out Stack<IDisposable>? subscriptions))
                    return;
                subscription = subscriptions.Pop();
                if (subscriptions.Count == 0)
                    _handlers.Remove(handler);
            }
            subscription.Dispose();
        }
    }

    private sealed class ScopedApplication(IApplicationRuntime inner, ScriptGlobals globals) : IApplicationRuntime
    {
        public IReadOnlyList<ApplicationDescriptor> Members => inner.Members;

        public ApplicationMemberDescription Describe(string id) => inner.Describe(id);

        public TResult Invoke<TRequest, TResult>(
            string id,
            TRequest request,
            CancellationToken cancellationToken) =>
            inner.Invoke<TRequest, TResult>(id, request, globals.ScopedToken(cancellationToken));

        public ValueTask<TResult> InvokeAsync<TRequest, TResult>(
            string id,
            TRequest request,
            CancellationToken cancellationToken) =>
            inner.InvokeAsync<TRequest, TResult>(id, request, globals.ScopedToken(cancellationToken));

        public ValueTask<object?> InvokeAsync(
            string id,
            object? request,
            CancellationToken cancellationToken) =>
            inner.InvokeAsync(id, request, globals.ScopedToken(cancellationToken));

        public IDisposable Subscribe<TEvent>(string id, Action<TEvent> receiver)
        {
            ArgumentNullException.ThrowIfNull(receiver);
            return globals.Track(inner.Subscribe<TEvent>(id, globals.Guarded(receiver)));
        }

        public IDisposable Subscribe(string id, Action<object?> receiver)
        {
            ArgumentNullException.ThrowIfNull(receiver);
            return globals.Track(inner.Subscribe(id, globals.Guarded(receiver)));
        }
    }
}
