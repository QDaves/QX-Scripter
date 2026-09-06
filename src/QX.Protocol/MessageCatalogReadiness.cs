namespace Qx.Protocol;

public interface IMessageCatalogReadiness
{
    Task WaitUntilReadyAsync(CancellationToken cancellation_token = default);
}
