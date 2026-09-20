using Qx.Game;
using Qx.Game.Application;
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
    }

    public GameState Game => _runtime.Game;

    public IApplicationRuntime Application => _runtime.Application;

    public GEarthExtension Extension => _runtime.Extension;

    public Qx.ClientType Client => _runtime.Extension.Session?.Client ?? Qx.ClientType.None;

    public bool IsHotelConnected => _runtime.Extension.IsConnected;

    public event Action? SessionChanged;

    public MemberGate Gate(string member_id)
    {
        ApplicationAvailability availability = _runtime.Application.Describe(member_id).Availability;
        return availability.Available ? MemberGate.Open : new MemberGate(false, Explain(availability));
    }

    public async ValueTask<TResult> QueryAsync<TRequest, TResult>(string member_id, TRequest request, CancellationToken cancellation_token = default)
    {
        try
        {
            return await _runtime.Application.InvokeAsync<TRequest, TResult>(member_id, request, cancellation_token);
        }
        catch (ApplicationUnavailableException error)
        {
            throw new GameUnavailableException(member_id, Explain(error.Availability), error);
        }
    }

    public async Task<TResult> ReadStableAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> read, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await read(cancellation_token);
            }
            catch (InvalidOperationException error) when (error is not ApplicationUnavailableException && attempt < StableReadAttempts)
            {
                cancellation_token.ThrowIfCancellationRequested();
            }
        }
    }

    public async Task<TResult> InvokeAsync<TRequest, TResult>(string member_id, TRequest request, CancellationToken cancellation_token = default)
    {
        try
        {
            return await _runtime.Application.InvokeAsync<TRequest, TResult>(member_id, request, cancellation_token);
        }
        catch (ApplicationUnavailableException error)
        {
            throw new GameUnavailableException(member_id, Explain(error.Availability), error);
        }
    }

    public IDisposable Subscribe<TEvent>(string member_id, Action<TEvent> receiver) =>
        _runtime.Application.Subscribe(member_id, receiver);

    public IDisposable SubscribeSignal(string member_id, CoalescingSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return _runtime.Application.Subscribe(member_id, (object? _) => signal.Raise());
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

    internal static string Explain(ApplicationAvailability availability)
    {
        if (availability.MissingStates.Contains(ApplicationStateKey.HotelConnected))
            return "Connect to the hotel through G-Earth first.";
        if (availability.MissingStates.Contains(ApplicationStateKey.RoomReady) ||
            availability.MissingStates.Contains(ApplicationStateKey.RoomActive))
            return "Enter a room first.";
        if (availability.MissingStates.Contains(ApplicationStateKey.ProfileLoaded))
            return "Your profile has not arrived from the hotel yet.";
        if (availability.MissingStates.Count > 0)
            return "The hotel has not sent what this needs yet.";
        return availability.Client == Qx.ClientType.None
            ? "Not available right now."
            : $"Not supported on the {availability.Client} client.";
    }
}
