using Qx.Messages;
using Qx.Protocol;

namespace Qx.Interception;

public readonly record struct InterceptorSessionCatalog(
    Session? Session,
    SessionCatalogBinding? Catalog);

public interface IInterceptor : IConnection
{
    MessageManager Messages { get; }

    event Action<Intercept>? Intercepted;

    Task WaitForCatalogBuildAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    InterceptorSessionCatalog CaptureSessionCatalog();

    void Send(
        IPacket packet,
        Session? expected_session,
        SessionCatalogBinding? expected_catalog) =>
        Send(packet, expected_session, expected_catalog, null);

    void Send(
        IPacket packet,
        Session? expected_session,
        SessionCatalogBinding? expected_catalog,
        Action? dispatch_guard);

    IDisposable Intercept(Header header, Action<Intercept> callback);
    IDisposable Intercept(Identifier identifier, Action<Intercept> callback);
    IDisposable Intercept(MessageKey key, Action<Intercept> callback)
    {
        if (!Messages.TryGetHeader(key, out Header header))
            return EmptySubscription.Instance;
        return Intercept(header, callback);
    }
}
