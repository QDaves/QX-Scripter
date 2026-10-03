using Qx.Game;
using Qx.Game.Application;
using Qx.Interception.GEarth;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Game;

public interface IGameGateway
{
    GameState Game { get; }

    IApplicationRuntime Application { get; }

    GEarthExtension Extension { get; }

    bool IsHotelConnected { get; }

    event Action? SessionChanged;

    MemberGate Gate(string memberId);

    ValueTask<TResult> QueryAsync<TRequest, TResult>(string memberId, TRequest request, CancellationToken cancellationToken = default);

    Task<TResult> ReadStableAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> read, CancellationToken cancellationToken = default);

    Task<TResult> InvokeAsync<TRequest, TResult>(string memberId, TRequest request, CancellationToken cancellationToken = default);

    IDisposable Subscribe<TEvent>(string memberId, Action<TEvent> receiver);

    IDisposable SubscribeSignal(string memberId, CoalescingSignal signal);
}
