using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Application;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomViewModel : PageViewModel
{
    readonly RoomPageContext _context;
    readonly IRoomSnapshotSource _snapshots;
    readonly IGameGateway _gateway;
    readonly CoalescingSignal _session;
    RoomPresence _presence = RoomPresence.Outside;

    public RoomViewModel(
        IGameGateway gateway,
        IRoomSnapshotSource snapshots,
        HotelContext hotel,
        IImageService images,
        IOutfitStore outfits,
        IClipboardService clipboard,
        ILauncherService launcher,
        IDialogService dialogs,
        INotificationService notifications,
        IFurniDirections directions,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Room)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        Notices = Own(new NoticeLine(dispatcher, time));
        _context = new RoomPageContext(
            gateway,
            hotel,
            images,
            outfits,
            clipboard,
            launcher,
            dialogs,
            notifications,
            directions,
            dispatcher,
            time,
            Notices);
        _session = new CoalescingSignal(dispatcher, OnSession);
        Own(gateway.SubscribeSignal(ApplicationMemberIds.ProfileChanged, _session));
        Info = Own(new RoomInfoViewModel(_context));
        Users = Own(new RoomUsersViewModel(_context));
        Visitors = Own(new RoomVisitorsViewModel(_context));
        Bans = Own(new RoomBansViewModel(_context));
        Furni = Own(new RoomFurniViewModel(_context));
        Tabs =
        [
            new RoomTabViewModel(RoomSection.Info, "Info", IconKind.Info),
            new RoomTabViewModel(RoomSection.Users, "Users", IconKind.Users),
            new RoomTabViewModel(RoomSection.Visitors, "Visitors", IconKind.Visitors),
            new RoomTabViewModel(RoomSection.Bans, "Bans", IconKind.Ban),
            new RoomTabViewModel(RoomSection.Furni, "Furni", IconKind.Furni)
        ];
        SelectedTab = Tabs[0];
        Subtitle = "Not in a room.";
        State.ShowUnavailable(IconKind.Room, "Enter a room", "Enter a room to see who and what is in it.");
        _snapshots.Updated += OnSnapshot;
        _gateway.SessionChanged += _session.Raise;
        Users.PropertyChanged += OnSectionReported;
        Visitors.PropertyChanged += OnSectionReported;
        Bans.PropertyChanged += OnSectionReported;
        Furni.PropertyChanged += OnSectionReported;
        Own(() =>
        {
            _snapshots.Updated -= OnSnapshot;
            _gateway.SessionChanged -= _session.Raise;
            Users.PropertyChanged -= OnSectionReported;
            Visitors.PropertyChanged -= OnSectionReported;
            Bans.PropertyChanged -= OnSectionReported;
            Furni.PropertyChanged -= OnSectionReported;
        });
    }

    public NoticeLine Notices { get; }

    public RoomInfoViewModel Info { get; }

    public RoomUsersViewModel Users { get; }

    public RoomVisitorsViewModel Visitors { get; }

    public RoomBansViewModel Bans { get; }

    public RoomFurniViewModel Furni { get; }

    public IReadOnlyList<RoomTabViewModel> Tabs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInfo), nameof(IsUsers), nameof(IsVisitors), nameof(IsBans), nameof(IsFurni))]
    public partial RoomSection Section { get; private set; }

    [ObservableProperty]
    public partial RoomTabViewModel? SelectedTab { get; set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial OperationProgress? Progress { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoomId))]
    public partial string RoomIdText { get; private set; } = "";

    public bool HasRoomId => RoomIdText.Length > 0;

    public bool IsInfo => Section is RoomSection.Info;

    public bool IsUsers => Section is RoomSection.Users;

    public bool IsVisitors => Section is RoomSection.Visitors;

    public bool IsBans => Section is RoomSection.Bans;

    public bool IsFurni => Section is RoomSection.Furni;

    public bool IsInRoom => _presence is RoomPresence.Inside;

    public void ShowSection(RoomSection section)
    {
        Section = section;
        SelectedTab = Tabs.FirstOrDefault(tab => tab.Section == section);
    }

    public override bool TryClearSearch()
    {
        bool cleared = Users.ClearFilter();
        cleared |= Visitors.ClearFilter();
        cleared |= Bans.ClearFilter();
        cleared |= Furni.ClearFilter();
        return cleared;
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        _context.Lifetime = cancellation_token;
        _snapshots.Start();
        Furni.Start();
        Bans.Start();
        await IdentifyAsync(cancellation_token);
    }

    protected override void OnDeactivated()
    {
        Users.Selection.Select([]);
        Visitors.Selection.Select([]);
        _snapshots.Stop();
        Furni.Stop();
        Bans.Stop();
        _context.Lifetime = CancellationToken.None;
    }

    async Task IdentifyAsync(CancellationToken cancellation_token)
    {
        try
        {
            ProfileStateView? profile = await _gateway
                .QueryAsync<ProfileStateRequest, ProfileStateView?>(
                    ApplicationMemberIds.ProfileState,
                    new ProfileStateRequest(),
                    cancellation_token);
            _context.Self = profile?.Identity?.Id;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Self = null;
        }
        Users.RefreshMenu();
        Visitors.RefreshMenu();
        Furni.RefreshMenu();
    }

    void OnSession()
    {
        if (IsActive)
            IdentifyAsync(_context.Lifetime).Observe("ui");
    }

    void OnSnapshot(RoomSnapshot snapshot)
    {
        if (_presence is RoomPresence.Inside && snapshot.Presence is not RoomPresence.Inside)
            Furni.Release();
        _presence = snapshot.Presence;
        OnPropertyChanged(nameof(IsInRoom));
        Describe(snapshot);
        Info.Show(snapshot);
        Users.Show(snapshot);
        Visitors.Show(snapshot);
        Bans.Show(snapshot);
        Furni.Show(snapshot);
        Recount();
    }

    void Describe(RoomSnapshot snapshot)
    {
        RoomIdentity identity = snapshot.Identity;
        RoomIdText = identity.RoomId > 0 ? $"#{identity.RoomId}" : "";
        switch (snapshot.Presence)
        {
            case RoomPresence.Disconnected:
                Subtitle = "Not connected.";
                State.ShowUnavailable(IconKind.ConnectionOff, "Not connected", "Connect to the hotel through G-Earth first.");
                break;
            case RoomPresence.Entering:
                Subtitle = "Loading room…";
                State.ShowUnavailable(IconKind.Room, "Loading room…", "The hotel is still sending this room.");
                break;
            case RoomPresence.Inside:
                Subtitle = Sentence(identity);
                State.ShowReady();
                break;
            default:
                Subtitle = "Not in a room.";
                State.ShowUnavailable(IconKind.Room, "Enter a room", "Enter a room to see who and what is in it.");
                break;
        }
    }

    static string Sentence(RoomIdentity identity)
    {
        var parts = new List<string>(3)
        {
            identity.Name.Length > 0 ? identity.Name : "This room"
        };
        if (identity.OwnerName.Length > 0)
            parts.Add($"owned by {identity.OwnerName}");
        if (identity.MaxUserCount > 0)
            parts.Add($"{identity.UserCount} of {identity.MaxUserCount} people");
        return string.Join(" · ", parts);
    }

    void Recount()
    {
        Tabs[1].Count(Users.Rows.SourceCount);
        Tabs[2].Count(Visitors.Rows.SourceCount);
        Tabs[3].Count(Bans.Rows.SourceCount);
        Tabs[4].Count(Furni.Rows.SourceCount);
        CountText = Section switch
        {
            RoomSection.Users => Users.CountText,
            RoomSection.Visitors => Visitors.CountText,
            RoomSection.Bans => Bans.CountText,
            RoomSection.Furni => Furni.CountText,
            _ => ""
        };
        Progress = Furni.Progress;
    }

    void OnSectionReported(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoomFurniViewModel.CountText) or nameof(RoomFurniViewModel.Progress))
            Recount();
    }

    partial void OnSectionChanged(RoomSection value)
    {
        Users.Selection.Select([]);
        Visitors.Selection.Select([]);
        if (SelectedTab?.Section != value)
            SelectedTab = Tabs.FirstOrDefault(tab => tab.Section == value);
        if (value is RoomSection.Bans)
            Bans.Visit();
        Recount();
    }

    partial void OnSelectedTabChanged(RoomTabViewModel? value)
    {
        if (value is not null && Section != value.Section)
            Section = value.Section;
    }
}
