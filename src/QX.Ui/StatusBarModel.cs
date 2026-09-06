using System.ComponentModel;
using Microsoft.VisualStudio.Threading;
using Qx.Game;
using Qx.Game.Application;
using Qx.Interception.GEarth;
using Qx.Model;

namespace Qx.Ui;

public sealed class StatusBarModel : INotifyPropertyChanged
{
    private static readonly string[] AllProps =
    [
        nameof(IsGEarthConnected), nameof(IsGameConnected), nameof(IsInRoom), nameof(UserCount),
        nameof(BotCount), nameof(PetCount), nameof(FloorItemCount), nameof(WallItemCount),
        nameof(FurniCount), nameof(HasUserData), nameof(UserName), nameof(HotelVersion),
        nameof(GameClient), nameof(GameClientTooltip),
        nameof(IsMcpRunning), nameof(HasMcpFailure), nameof(McpTooltip),
        nameof(RoomName), nameof(RoomId), nameof(RoomTooltip),
        nameof(RunningCount), nameof(HasRunning), nameof(RunningText)
    ];

    private bool _mcp_running;
    private int _mcp_port;
    private string _mcp_failure = "";
    private DateTime? _mcp_last_request;
    private int _running;

    private readonly GameState _game;
    private readonly IApplicationRuntime _application;
    private readonly GEarthExtension _extension;
    private readonly JoinableTaskFactory _ui_tasks;
    private readonly CancellationToken _lifetime_token;

    public StatusBarModel(
        GameState game,
        GEarthExtension extension,
        IApplicationRuntime application)
        : this(
            game,
            extension,
            application,
            UiTaskScope.ApplicationFactory,
            CancellationToken.None)
    {
    }

    public StatusBarModel(
        GameState game,
        GEarthExtension extension,
        IApplicationRuntime application,
        JoinableTaskFactory uiTasks,
        CancellationToken lifetimeToken)
    {
        _game = game;
        _extension = extension;
        _application = application;
        _ui_tasks = uiTasks;
        _lifetime_token = lifetimeToken;

        extension.InterceptorConnected += Refresh;
        extension.InterceptorDisconnected += Refresh;
        extension.Connected += _ => Refresh();
        extension.Disconnected += Refresh;

        _game.Room.Entered += Refresh;
        // The name arrives with the room data, which lands after entry: without this the status
        // bar showed "Room 12345" for the whole session.
        _game.Room.Ready += Refresh;
        _game.Room.Left += Refresh;
        _game.Room.AvatarsAdded += _ => Refresh();
        _game.Room.AvatarRemoved += _ => Refresh();
        _game.Room.FloorItemsLoaded += Refresh;
        _game.Room.WallItemsLoaded += Refresh;
        _game.Room.FloorItemAdded += _ => Refresh();
        _game.Room.FloorItemRemoved += _ => Refresh();
        _game.Room.WallItemAdded += _ => Refresh();
        _game.Room.WallItemRemoved += _ => Refresh();
        _application.Subscribe<ProfileChanged>(
            ApplicationMemberIds.ProfileChanged,
            _ => Refresh());
    }

    public bool IsGEarthConnected => _extension.IsInterceptorConnected;
    public bool IsGameConnected => _extension.IsConnected;
    public string HotelVersion => _extension.Session?.HotelVersion ?? "";

    /// <summary>The connected client, short enough to sit in the bar without crowding it.</summary>
    public string GameClient
    {
        get
        {
            ClientType? client = _extension.Session?.Client;
            return client switch
            {
                null or ClientType.None => "",
                ClientType.Flash => "Flash",
                ClientType.Unity => "Unity",
                _ => throw new UnsupportedClientException(client.Value)
            };
        }
    }

    /// <summary>The build behind <see cref="GameClient"/>, which only matters on demand.</summary>
    public string GameClientTooltip =>
        HotelVersion.Length == 0 ? GameClient : $"{GameClient} build {HotelVersion}";
    public bool IsInRoom => _game.Room.IsInRoom;
    public int UserCount => _game.Room.Avatars.Count(a => a is User);
    public int BotCount => _game.Room.Avatars.Count(a => a is Bot);
    public int PetCount => _game.Room.Avatars.Count(a => a is Pet);
    public int FloorItemCount => _game.Room.FloorItems.Count;
    public int WallItemCount => _game.Room.WallItems.Count;
    public int FurniCount => FloorItemCount + WallItemCount;
    public bool HasUserData => Profile.Identity is not null;
    public string UserName => Profile.Identity?.Name ?? "";

    private ProfileStateView Profile =>
        _application.Invoke<ProfileStateRequest, ProfileStateView>(
            ApplicationMemberIds.ProfileState,
            new ProfileStateRequest(),
            _lifetime_token);

    /// <summary>
    /// The room's name, or its id while the name has not arrived.
    /// </summary>
    /// <remarks>
    /// The counts beside this were always here, but the name and the id are what a user actually
    /// copies out of a session, and neither was anywhere in the window.
    /// </remarks>
    public string RoomName => _game.Room.Name is { Length: > 0 } name
        ? name
        : _game.Room.RoomId > 0 ? $"Room {_game.Room.RoomId}" : "";

    public long RoomId => _game.Room.RoomId;

    public string RoomTooltip => _game.Room.RoomId > 0
        ? $"{RoomName}, id {_game.Room.RoomId}. Click to copy the id."
        : "Not in a room.";

    /// <summary>How many scripts are alive, which is what the panic key acts on.</summary>
    public int RunningCount => _running;
    public bool HasRunning => _running > 0;
    public string RunningText => _running == 1 ? "1 running" : $"{_running} running";

    /// <summary>Reported by the host whenever a run starts or ends.</summary>
    public void SetRunning(int running)
    {
        if (_running == running)
            return;
        _running = running;
        Refresh();
    }

    public bool IsMcpRunning => _mcp_running;

    /// <summary>Whether the server tried to start and could not, which is worth a colour of its own.</summary>
    public bool HasMcpFailure => !_mcp_running && _mcp_failure.Length > 0;

    /// <summary>Told by the host how recently a client called, so the bar can say whether one is there.</summary>
    public void SetMcpActivity(DateTime? lastRequestUtc)
    {
        if (_mcp_last_request == lastRequestUtc)
            return;
        _mcp_last_request = lastRequestUtc;
        Refresh();
    }

    public string McpTooltip => _mcp_running
        ? $"MCP server listening on 127.0.0.1:{_mcp_port}. {ClientActivity} Settings has the client URL."
        : _mcp_failure.Length > 0
            ? _mcp_failure
            : "MCP server is not running.";

    private string ClientActivity => _mcp_last_request is { } last
        ? $"A client last called {HomeView.Ago(last.ToLocalTime())}."
        : "No client has called yet.";

    /// <summary>
    /// Reported by the host once the server has tried to start. The failure is worth a permanent
    /// place: it was previously written to a script's output, which at startup may not exist yet.
    /// </summary>
    public void SetMcp(bool running, int port, string failure = "")
    {
        _mcp_running = running;
        _mcp_port = port;
        _mcp_failure = failure;
        Refresh();
    }

    public void Refresh()
    {
        if (_ui_tasks.Context.IsOnMainThread)
        {
            RaisePropertyChanged();
            return;
        }

        _ui_tasks.RunAsync(RefreshAsync).Task.Forget();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await _ui_tasks.SwitchToMainThreadAsync(_lifetime_token);
            RaisePropertyChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            Qx.Diagnostics.Diag.Error(error.ToString(), "status");
        }
    }

    private void RaisePropertyChanged()
    {
        foreach (string property in AllProps)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
