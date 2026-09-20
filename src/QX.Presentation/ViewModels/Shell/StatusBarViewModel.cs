using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Shell;

public sealed partial class StatusBarViewModel : ViewModelBase
{
    readonly INotificationService _notifications;
    readonly TimeProvider _time;

    public StatusBarViewModel(ISessionStatusService status, IClipboardService clipboard, INotificationService notifications, IUiDispatcher dispatcher, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        Current = status.Current;
        CopyRoomId = Own(new CopyAction(clipboard, dispatcher, time, RoomIdText, ReportCopyFailure));
        CopyRoomId.PropertyChanged += OnCopyChanged;
        Own(time.CreateTimer(_ => dispatcher.Post(RefreshActivity), null, SessionStatusService.ActivityInterval, SessionStatusService.ActivityInterval));
        status.Changed += OnStatusChanged;
        Own(() =>
        {
            status.Changed -= OnStatusChanged;
            CopyRoomId.PropertyChanged -= OnCopyChanged;
        });
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(GEarthIcon),
        nameof(IsGEarthMuted),
        nameof(GEarthTooltip),
        nameof(McpIcon),
        nameof(IsMcpMuted),
        nameof(McpTooltip),
        nameof(HasClient),
        nameof(ClientLabel),
        nameof(ClientDetail),
        nameof(ClientTooltip),
        nameof(HasUser),
        nameof(UserName),
        nameof(UserTooltip),
        nameof(HasRoom),
        nameof(RoomName),
        nameof(RoomDetail),
        nameof(RoomTooltip),
        nameof(UserCountText),
        nameof(BotCountText),
        nameof(PetCountText),
        nameof(FurniCountText))]
    public partial SessionStatus Current { get; private set; }

    public CopyAction CopyRoomId { get; }

    public IAsyncRelayCommand CopyRoomIdCommand => CopyRoomId.CopyCommand;

    public string GEarthLabel => "G-Earth";

    public IconKind GEarthIcon => Current.IsGEarthConnected ? IconKind.Connection : IconKind.ConnectionOff;

    public bool IsGEarthMuted => !Current.IsGEarthConnected;

    public string GEarthTooltip => Current.IsGEarthConnected
        ? $"Connected to G-Earth on port {Current.GEarthPort}."
        : $"Waiting for G-Earth on port {Current.GEarthPort}.";

    public string McpLabel => "MCP";

    public IconKind McpIcon => Current.IsMcpRunning ? IconKind.Connection : IconKind.ConnectionOff;

    public bool IsMcpMuted => !Current.IsMcpRunning;

    public string McpTooltip => RelativeTime.McpTooltip(Current, _time.GetUtcNow());

    public bool HasClient => Current.IsGameConnected && Current.ClientName.Length > 0;

    public string ClientLabel => Current.ClientName;

    public string ClientDetail => Current.HotelVersion;

    public string ClientTooltip => Current.ClientTooltip;

    public bool HasUser => !string.IsNullOrEmpty(Current.UserName);

    public string UserName => Current.UserName ?? "";

    public string UserTooltip => HasUser ? $"Signed in as {UserName}." : "";

    public bool HasRoom => Current.IsInRoom;

    public string RoomName => Current.RoomName;

    public string RoomDetail => CopyRoomId.Copied ? "Copied" : Current.RoomId > 0 ? "#" + Current.RoomId.ToString(CultureInfo.CurrentCulture) : "";

    public string RoomTooltip => Current.RoomTooltip;

    public string UserCountText => Count(Current.UserCount);

    public string BotCountText => Count(Current.BotCount);

    public string PetCountText => Count(Current.PetCount);

    public string FurniCountText => Count(Current.FurniCount);

    static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    string? RoomIdText() => Current.RoomId > 0 ? Current.RoomId.ToString(CultureInfo.InvariantCulture) : null;

    void ReportCopyFailure(string message)
    {
        Diag.Error($"Copy failed: {message}", "ui");
        _notifications.Show(message, NoticeSeverity.Error);
    }

    void RefreshActivity() => OnPropertyChanged(nameof(McpTooltip));

    void OnStatusChanged(SessionStatus status) => Current = status;

    void OnCopyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CopyAction.Copied))
            OnPropertyChanged(nameof(RoomDetail));
    }
}
