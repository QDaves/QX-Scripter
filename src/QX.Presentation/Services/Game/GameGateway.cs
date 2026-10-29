using Qx.Game.Api;
using Qx.Game.Data;
using Qx.Game;
using Qx.Interception.GEarth;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Game;

public sealed class GameGateway : IGameGateway, IAlwaysOn, IDisposable
{
    public const int StableReadAttempts = 3;

    readonly DesktopRuntime _runtime;
    readonly HotelContext _hotel;
    readonly CoalescingSignal _session;

    public GameGateway(DesktopRuntime runtime, HotelContext hotel, IUiDispatcher dispatcher)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _hotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
        _session = new CoalescingSignal(dispatcher, () => SessionChanged?.Invoke());
        _runtime.Extension.Connected += OnConnected;
        _runtime.Extension.Disconnected += OnDisconnected;
        _runtime.Extension.InterceptorConnected += OnInterceptorChanged;
        _runtime.Extension.InterceptorDisconnected += OnInterceptorChanged;
        _session.Raise();
    }

    public GameState Game => _runtime.Game;

    public IGameApi Api => _runtime.Api;

    public GEarthExtension Extension => _runtime.Extension;

    public bool IsHotelConnected => _runtime.Extension.IsConnected;

    public event Action? SessionChanged;

    public MemberGate Gate(string memberId)
    {
        ApiAvailability availability = _runtime.Api.Describe(memberId).Availability;
        return availability.Available ? MemberGate.Open : new MemberGate(false, Explain(availability));
    }

    public async ValueTask<TResult> QueryAsync<TRequest, TResult>(string memberId, TRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _runtime.Api.InvokeAsync<TRequest, TResult>(memberId, request, cancellationToken);
        }
        catch (GameNotReadyException error)
        {
            throw new GameUnavailableException(memberId, Explain(error.Availability), error);
        }
    }

    public async Task<TResult> ReadStableAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> read, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await read(cancellationToken);
            }
            catch (InvalidOperationException error) when (error is not GameNotReadyException && attempt < StableReadAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    public async Task<TResult> InvokeAsync<TRequest, TResult>(string memberId, TRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _runtime.Api.InvokeAsync<TRequest, TResult>(memberId, request, cancellationToken);
        }
        catch (GameNotReadyException error)
        {
            throw new GameUnavailableException(memberId, Explain(error.Availability), error);
        }
    }

    public IDisposable Subscribe<TEvent>(string memberId, Action<TEvent> receiver) =>
        _runtime.Api.Subscribe(memberId, receiver);

    public IDisposable SubscribeSignal(string memberId, CoalescingSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return _runtime.Api.Subscribe(memberId, (object? _) => signal.Raise());
    }

    public void Dispose()
    {
        _runtime.Extension.Connected -= OnConnected;
        _runtime.Extension.Disconnected -= OnDisconnected;
        _runtime.Extension.InterceptorConnected -= OnInterceptorChanged;
        _runtime.Extension.InterceptorDisconnected -= OnInterceptorChanged;
    }

    void OnConnected(Qx.Interception.Session session)
    {
        _hotel.Use(GameData.WebHostFor(session.Host));
        _session.Raise();
    }

    void OnDisconnected()
    {
        _hotel.Use(null);
        _session.Raise();
    }

    void OnInterceptorChanged() => _session.Raise();

    internal static string Explain(ApiAvailability availability)
    {
        if (availability.MissingStates.Contains(GameRequirement.HotelConnected))
            return "Connect to the hotel through G-Earth first.";
        if (availability.MissingStates.Contains(GameRequirement.RoomReady) ||
            availability.MissingStates.Contains(GameRequirement.RoomActive))
            return "Enter a room first.";
        if (availability.MissingStates.Contains(GameRequirement.ProfileLoaded))
            return "Your profile has not arrived from the hotel yet.";
        if (availability.MissingStates.Count > 0)
            return "The hotel has not sent what this needs yet.";
        return availability.Connected
            ? "Not supported by the connected client build."
            : "Not available right now.";
    }
}
