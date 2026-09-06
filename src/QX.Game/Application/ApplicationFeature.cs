namespace Qx.Game.Application;

internal interface IApplicationFeature : IDisposable
{
    IReadOnlyList<IApplicationBinding> Bindings { get; }
}
