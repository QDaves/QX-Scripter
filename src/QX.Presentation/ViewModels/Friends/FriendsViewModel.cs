using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Friends;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Friends;

public sealed partial class FriendsViewModel : PageViewModel
{
    public const string AskingTheHotel = "Asking the hotel…";
    public const string NoFriends = "No friends yet.";
    public const string NothingYet = "Nothing yet. Reload to ask the hotel.";
    public const string NoMatches = "Nothing matches";
    public const string ClipboardUnreachable = "Could not reach the clipboard.";
    public const string NoFigure = "The hotel has not said what they are wearing.";
    public const string OutfitAlreadyKept = "You are already keeping that outfit.";

    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan RoomInterval = TimeSpan.FromMilliseconds(250);

    readonly IGameGateway _gateway;
    readonly HotelContext _hotel;
    readonly IOutfitStore _outfits;
    readonly IClipboardService _clipboard;
    readonly IDialogService _dialogs;
    readonly AppLifetime _lifetime;
    readonly KeyedRows<long, FriendRow> _all = new();
    readonly SerialOperation _reads = new();
    readonly Debouncer _search;
    readonly CoalescingSignal _friends_changed;
    readonly ThrottledSignal _room_changed;
    bool _loaded;
    bool _asking;

    public FriendsViewModel(
        IGameGateway gateway,
        HotelContext hotel,
        IOutfitStore outfits,
        IClipboardService clipboard,
        IDialogService dialogs,
        AppLifetime lifetime,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Friends)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _hotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
        _outfits = outfits ?? throw new ArgumentNullException(nameof(outfits));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        Notices = Own(new NoticeLine(dispatcher, time));
        _search = Own(new Debouncer(dispatcher, time, SearchDelay, ApplyFilter));
        _friends_changed = new CoalescingSignal(dispatcher, OnFriendsChanged);
        _room_changed = Own(new ThrottledSignal(dispatcher, time, RoomInterval, RefreshHere));
        Own(gateway.SubscribeSignal(ApplicationMemberIds.FriendsChanged, _friends_changed));
        RoomManager room = gateway.Game.Room;
        Rows.Applied += OnRowsApplied;
        Selection.Changed += RefreshMenu;
        gateway.SessionChanged += OnSessionChanged;
        room.Entered += OnRoomChanged;
        room.Left += OnRoomChanged;
        room.AvatarsAdded += OnAvatarsAdded;
        room.AvatarRemoved += OnAvatarRemoved;
        Own(() =>
        {
            Rows.Applied -= OnRowsApplied;
            Selection.Changed -= RefreshMenu;
            gateway.SessionChanged -= OnSessionChanged;
            room.Entered -= OnRoomChanged;
            room.Left -= OnRoomChanged;
            room.AvatarsAdded -= OnAvatarsAdded;
            room.AvatarRemoved -= OnAvatarRemoved;
        });
        CountText = FriendsText.Shown(0, 0);
        Subtitle = FriendsText.Summary(0, 0);
        RefreshMenu();
        ShowState();
    }

    public FilteredRows<FriendRow> Rows { get; } = new();

    public SelectionList<FriendRow> Selection { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public NoticeLine Notices { get; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool OnlineOnly { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySelectionCommand))]
    public partial bool HasSelection { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WhisperCommand), nameof(OpenProfileCommand), nameof(CopyFieldCommand))]
    public partial bool HasOneSelected { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FollowCommand))]
    public partial bool CanFollow { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindCommand))]
    public partial bool CanFind { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddOutfitCommand))]
    public partial bool CanAddOutfit { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool CanRemove { get; private set; }

    [ObservableProperty]
    public partial string FollowTip { get; private set; } = "";

    [ObservableProperty]
    public partial string FindTip { get; private set; } = "";

    [ObservableProperty]
    public partial string AddOutfitTip { get; private set; } = "";

    [ObservableProperty]
    public partial string RemoveTip { get; private set; } = "";

    public override bool TryClearSearch()
    {
        if (SearchText.Length == 0 && !OnlineOnly)
            return false;
        ClearFilters();
        return true;
    }

    public void RefreshMenu()
    {
        if (_lifetime.IsStopping)
            return;
        FriendRow? one = Selection.HasOne ? Selection.First : null;
        MemberGate follow = _gateway.Gate(ApplicationMemberIds.FriendFollow);
        MemberGate remove = _gateway.Gate(ApplicationMemberIds.FriendsRemove);
        HasSelection = Selection.HasAny;
        HasOneSelected = one is not null;
        CanFollow = one is { IsOnline: true } && follow.Available;
        FollowTip = one is null || CanFollow ? "" : one.IsOnline ? follow.Reason : $"{one.Name} is not online.";
        CanFind = one is { IsHere: true };
        FindTip = one is null || CanFind ? "" : $"{one.Name} is not in this room.";
        CanAddOutfit = one is { Figure.Length: > 0 };
        AddOutfitTip = one is null || CanAddOutfit ? "" : NoFigure;
        CanRemove = Selection.HasAny && remove.Available;
        RemoveTip = remove.Available ? "" : remove.Reason;
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        await RefreshAsync(cancellation_token);
        if (!_loaded)
            await AskAsync(null, cancellation_token);
    }

    partial void OnSearchTextChanged(string value) => _search.Trigger();

    partial void OnOnlineOnlyChanged(bool value) => ApplyFilter();

    [RelayCommand]
    void ClearFilters()
    {
        SearchText = "";
        OnlineOnly = false;
        _search.Cancel();
        ApplyFilter();
    }

    [RelayCommand(IncludeCancelCommand = true)]
    Task ReloadAsync(CancellationToken cancellation_token) => AskAsync(ReloadCancelCommand, cancellation_token);

    [RelayCommand(CanExecute = nameof(HasOneSelected))]
    async Task WhisperAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        if (await CopyAsync(FriendsText.WhisperPrefix(row.Name), cancellation_token))
            Notices.Show(NoticeSeverity.Info, $"Whisper to {row.Name} copied — paste it into the chat box.");
    }

    [RelayCommand(CanExecute = nameof(CanFollow))]
    async Task FollowAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        try
        {
            await _gateway.InvokeAsync<FriendFollowRequest, FriendOperationResult>(
                ApplicationMemberIds.FriendFollow,
                new FriendFollowRequest(row.Id),
                cancellation_token);
            Notices.Show(NoticeSeverity.Success, $"Following {row.Name}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not follow: {FailureText.Describe(error)}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanFind))]
    void Find()
    {
        if (Selection.First is not { } row)
            return;
        Avatar? found = _gateway.Game.Room.Capture(room => room.Avatars.FirstOrDefault(person =>
            person.Type == AvatarType.User && string.Equals(person.Name, row.Name, StringComparison.OrdinalIgnoreCase)));
        if (found is null)
        {
            Notices.Show(NoticeSeverity.Warning, $"{row.Name} is not in this room.");
            return;
        }
        try
        {
            _gateway.Game.People.Find(found);
        }
        catch (InvalidOperationException error)
        {
            Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
        }
    }

    [RelayCommand(CanExecute = nameof(HasOneSelected))]
    void OpenProfile()
    {
        if (Selection.First is not { } row)
            return;
        try
        {
            _gateway.Game.People.OpenProfile(row.Id);
        }
        catch (InvalidOperationException error)
        {
            Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddOutfit))]
    void AddOutfit()
    {
        if (Selection.First is not { } row)
            return;
        if (row.Figure is not { Length: > 0 } figure)
        {
            Notices.Show(NoticeSeverity.Warning, NoFigure);
            return;
        }
        bool kept = _outfits.Add(new SavedOutfit(figure, FriendsText.Gender(row.Gender), row.Name));
        Notices.Show(
            kept ? NoticeSeverity.Success : NoticeSeverity.Info,
            kept ? $"Kept {row.Name}'s outfit in your wardrobe." : OutfitAlreadyKept);
    }

    [RelayCommand(CanExecute = nameof(CanCopyField))]
    async Task CopyFieldAsync(FriendField field, CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        string text = field switch
        {
            FriendField.Id => row.IdText,
            FriendField.Motto => row.Motto,
            FriendField.Figure => row.Figure,
            _ => row.Name
        };
        if (await CopyAsync(text, cancellation_token))
            Notices.Show(NoticeSeverity.Info, "Copied.");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    async Task CopySelectionAsync(CancellationToken cancellation_token)
    {
        FriendRow[] picked = [.. Selection.Items];
        if (picked.Length == 0)
            return;
        string text = string.Join(
            Environment.NewLine,
            picked.Select(row => string.Join('\t', row.Name, row.Motto, row.LastSeen, row.IdText)));
        if (await CopyAsync(text, cancellation_token))
            Notices.Show(NoticeSeverity.Info, FriendsText.Copied(picked.Length));
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    async Task RemoveAsync(CancellationToken cancellation_token)
    {
        FriendRow[] picked = [.. Selection.Items];
        if (picked.Length == 0)
            return;
        bool confirmed = await _dialogs.ConfirmAsync(
            picked.Length == 1 ? "Remove friend?" : "Remove friends?",
            picked.Length == 1
                ? $"“{picked[0].Name}” will be removed from your friend list."
                : $"{picked.Length} friends will be removed from your friend list.",
            "Remove",
            DialogTone.Destructive,
            cancellation_token: cancellation_token);
        if (!confirmed || IsDisposed)
            return;
        try
        {
            await _gateway.InvokeAsync<FriendsRemoveRequest, FriendOperationResult>(
                ApplicationMemberIds.FriendsRemove,
                new FriendsRemoveRequest([.. picked.Select(row => (Id)row.Id)]),
                cancellation_token);
            Notices.Show(NoticeSeverity.Success, FriendsText.Removed(picked.Length));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not remove: {FailureText.Describe(error)}");
        }
    }

    bool CanCopyField(FriendField field) => HasOneSelected;

    async Task AskAsync(ICommand? cancel, CancellationToken cancellation_token)
    {
        MemberGate gate = _gateway.Gate(ApplicationMemberIds.FriendsRefresh);
        if (!gate.Available)
        {
            if (_all.Count > 0)
                Notices.Show(NoticeSeverity.Warning, gate.Reason);
            ShowState();
            return;
        }
        bool answered = false;
        _asking = true;
        State.ShowLoading(AskingTheHotel, cancel);
        try
        {
            await _gateway.InvokeAsync<FriendsRefreshRequest, FriendListPage>(
                ApplicationMemberIds.FriendsRefresh,
                new FriendsRefreshRequest(Limit: FriendListReader.PageSize),
                cancellation_token);
            answered = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not read the friend list: {FailureText.Describe(error)}");
        }
        finally
        {
            _asking = false;
        }
        if (answered)
        {
            await RefreshAsync(cancellation_token);
            return;
        }
        ShowState();
    }

    async Task RefreshAsync(CancellationToken cancellation_token)
    {
        OperationLease lease = await _reads.StartAsync(cancellation_token);
        try
        {
            FriendListPage page = await FriendListReader.ReadAsync(_gateway, lease.Token);
            if (_reads.IsCurrent(lease))
                Take(page);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            if (_reads.IsCurrent(lease))
            {
                Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
                ShowState();
            }
        }
        finally
        {
            _reads.Complete(lease);
        }
    }

    void Take(FriendListPage page)
    {
        _loaded = page.Loaded;
        HashSet<string> here = RoomNames();
        RowChanges changes = _all.Sync(
            page.Friends,
            friend => friend.Id,
            friend => new FriendRow(friend, here.Contains(friend.Name), Head(friend)),
            (row, friend) => row.Take(friend, here.Contains(friend.Name), Head(friend)));
        Subtitle = FriendsText.Summary(_all.Count, _all.Rows.Count(row => row.IsOnline));
        if (changes.Updated > 0)
            SortRefresh.Request();
        ApplyFilter();
        RefreshMenu();
    }

    void ApplyFilter()
    {
        var filter = new FriendFilter(SearchText.Trim(), OnlineOnly);
        Rows.ApplyAsync(_all.Rows, row => row.Matches(filter), FriendOrder.Default, ActivationToken).Observe("ui");
    }

    void OnRowsApplied()
    {
        CountText = FriendsText.Shown(Rows.Visible.Count, Rows.SourceCount);
        Selection.Prune(Rows.Visible);
        ShowState();
    }

    void ShowState()
    {
        if (_asking || _lifetime.IsStopping)
            return;
        if (_all.Count == 0)
        {
            MemberGate gate = _gateway.Gate(ApplicationMemberIds.FriendsRefresh);
            if (!gate.Available)
            {
                State.ShowUnavailable(Descriptor.Icon, gate.Reason);
                return;
            }
            if (_loaded)
                State.ShowEmpty(Descriptor.Icon, NoFriends);
            else
                State.ShowEmpty(Descriptor.Icon, NothingYet, "", ReloadCommand, "Reload");
            return;
        }
        if (Rows.Visible.Count == 0)
        {
            State.ShowEmpty(IconKind.Search, NoMatches, "", ClearFiltersCommand, "Clear filters");
            return;
        }
        State.ShowReady();
    }

    void RefreshHere()
    {
        if (!IsActive || _lifetime.IsStopping)
            return;
        HashSet<string> here = RoomNames();
        foreach (FriendRow row in _all.Rows)
            row.IsHere = here.Contains(row.Name);
        RefreshMenu();
    }

    HashSet<string> RoomNames() => _gateway.Game.Room.Capture(room =>
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Avatar person in room.Avatars)
        {
            if (person.Type == AvatarType.User)
                names.Add(person.Name);
        }
        return names;
    });

    string? Head(FriendSnapshot friend) =>
        friend.Figure is { Length: > 0 } figure
            ? HabboUrls.Head(figure, _hotel.WebHost)
            : HabboUrls.HeadForName(friend.Name, _hotel.WebHost);

    async Task<bool> CopyAsync(string text, CancellationToken cancellation_token)
    {
        if (text.Length == 0)
            return false;
        try
        {
            if (await _clipboard.TrySetTextAsync(text, cancellation_token))
                return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        Notices.Show(NoticeSeverity.Warning, ClipboardUnreachable);
        return false;
    }

    void OnFriendsChanged()
    {
        if (IsActive && !_lifetime.IsStopping)
            RefreshAsync(ActivationToken).Observe("ui");
    }

    void OnSessionChanged()
    {
        if (_lifetime.IsStopping)
            return;
        RefreshMenu();
        if (IsActive)
        {
            RefreshAsync(ActivationToken).Observe("ui");
            return;
        }
        ShowState();
    }

    void OnRoomChanged() => _room_changed.Raise();

    void OnAvatarsAdded(IReadOnlyList<Avatar> added) => _room_changed.Raise();

    void OnAvatarRemoved(Avatar removed) => _room_changed.Raise();

    static bool Reportable(Exception error) =>
        error is GameUnavailableException or InvalidOperationException or TimeoutException;

    sealed class FriendOrder : IComparer<FriendRow>
    {
        public static readonly FriendOrder Default = new();

        public int Compare(FriendRow? x, FriendRow? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return 1;
            if (y is null)
                return -1;
            if (x.IsOnline != y.IsOnline)
                return x.IsOnline ? -1 : 1;
            return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
