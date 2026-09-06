namespace Qx.Interception;

internal sealed class EmptySubscription : IDisposable
{
    public static EmptySubscription Instance { get; } = new();

    public void Dispose()
    {
    }
}
