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

    Qx.ClientType Client { get; }

    bool IsHotelConnected { get; }

    event Action? SessionChanged;

    MemberGate Gate(string member_id);

    ValueTask<TResult> QueryAsync<TRequest, TResult>(string member_id, TRequest request, CancellationToken cancellation_token = default);

    Task<TResult> ReadStableAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> read, CancellationToken cancellation_token = default);

    Task<TResult> InvokeAsync<TRequest, TResult>(string member_id, TRequest request, CancellationToken cancellation_token = default);

    IDisposable Subscribe<TEvent>(string member_id, Action<TEvent> receiver);

    IDisposable SubscribeSignal(string member_id, CoalescingSignal signal);
}
