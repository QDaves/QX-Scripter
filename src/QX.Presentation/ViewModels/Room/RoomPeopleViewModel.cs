using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public abstract partial class RoomPeopleViewModel : ViewModelBase
{
    public static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(150);

    static readonly string[] _moderation_members =
    [
        ApplicationMemberIds.RoomModerationMute,
        ApplicationMemberIds.RoomModerationKick,
        ApplicationMemberIds.RoomModerationBan,
        ApplicationMemberIds.RoomModerationBounce
    ];

    readonly KeyedRows<string, PersonRowViewModel> _all;
    readonly Debouncer _refilter;
    RoomSnapshot _snapshot = RoomSnapshot.Empty;

    protected RoomPeopleViewModel(RoomPageContext context, IEqualityComparer<string> comparer)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(comparer);
        _all = new KeyedRows<string, PersonRowViewModel>(comparer);
        _refilter = Own(new Debouncer(context.Dispatcher, context.Time, FilterDelay, ApplyFilter));
        Selection.Changed += OnSelectionChanged;
        Rows.Applied += OnRowsApplied;
        Own(() =>
        {
            Selection.Changed -= OnSelectionChanged;
            Rows.Applied -= OnRowsApplied;
        });
    }

    public RoomPageContext Context { get; }

    public FilteredRows<PersonRowViewModel> Rows { get; } = new();

    public SelectionList<PersonRowViewModel> Selection { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public ViewState State { get; } = new();

    protected RoomSnapshot Snapshot => _snapshot;

    protected bool IsAway => _snapshot.Presence is not RoomPresence.Inside;

    protected KeyedRows<string, PersonRowViewModel> All => _all;

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; protected set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindCommand))]
    public partial bool CanFind { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TradeCommand))]
    public partial bool CanTrade { get; private set; }

    [ObservableProperty]
    public partial string? TradeReason { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddFriendCommand))]
    public partial bool CanFriend { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenProfileCommand))]
    public partial bool CanProfile { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOutfitCommand))]
    public partial bool CanSaveOutfit { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MimicCommand))]
    public partial bool CanMimic { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyFieldCommand))]
    public partial bool CanCopy { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MuteCommand), nameof(KickCommand), nameof(BanCommand), nameof(BounceCommand))]
    public partial bool CanModerate { get; private set; }

    [ObservableProperty]
    public partial string? ModerateReason { get; private set; }

    public void Show(RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        Fill(snapshot);
        ApplyFilter();
    }

    public bool ClearFilter()
    {
        if (Filter.Length == 0)
            return false;
        Filter = "";
        return true;
    }

    [RelayCommand]
    public void RefreshMenu()
    {
        PersonRowViewModel? row = Selection.Count == 1 ? Selection.First : null;
        bool self = row is not null && Context.Self is { } mine && mine == row.UserId;
        bool is_user = row is not null && row.Kind is RoomPersonKind.User;
        bool in_room = row?.Person is not null;
        CanFind = in_room && !self;
        CanFriend = row is not null && is_user && !self;
        CanProfile = row is not null && is_user && !self;
        CanSaveOutfit = row?.Person is User { Figure.Length: > 0 };
        CanMimic = in_room;
        CanCopy = row is not null;
        (bool trade, string? trade_reason) = TradeGate(row, self);
        CanTrade = trade;
        TradeReason = trade_reason;
        (bool moderate, string? moderate_reason) = ModerationGate(row, self);
        CanModerate = moderate;
        ModerateReason = moderate_reason;
    }

    [RelayCommand(CanExecute = nameof(CanFind))]
    void Find()
    {
        if (Selection.First?.Person is not { } avatar)
            return;
        try
        {
            Context.Gateway.Game.People.Find(avatar);
            Context.Say($"Put a bubble over {avatar.Name}.");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Context.Fail("Could not point them out", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanFriend))]
    async Task AddFriendAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        try
        {
            await Context.Gateway.InvokeAsync<FriendRequestSendRequest, FriendOperationResult>(
                ApplicationMemberIds.FriendRequestSend,
                new FriendRequestSendRequest(row.Name),
                cancellation_token);
            Context.Say($"Friend request sent to {row.Name}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Context.Fail($"Could not ask {row.Name} to be friends", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanTrade))]
    async Task TradeAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row || TradeTarget(row) is not { } target)
            return;
        try
        {
            TradeStateView trade = await Context.Gateway
                .QueryAsync<TradeStateRequest, TradeStateView>(
                    ApplicationMemberIds.TradeState,
                    new TradeStateRequest(),
                    cancellation_token);
            await Context.Gateway.InvokeAsync<TradeOpenRequest, TradeDispatchResult>(
                ApplicationMemberIds.TradeOpen,
                new TradeOpenRequest(
                    target.Index,
                    trade.SessionGeneration,
                    trade.Revision,
                    trade.LatestEpoch,
                    trade.RoomGeneration,
                    target.Id),
                cancellation_token);
            Context.Say($"Trade opened with {row.Name}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Context.Fail("Could not open trade", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanProfile))]
    void OpenProfile()
    {
        if (Selection.First is not { } row)
            return;
        try
        {
            Context.Gateway.Game.People.OpenProfile(row.UserId);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Context.Fail($"Could not open the profile of {row.Name}", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMimic))]
    void Mimic(string? part)
    {
        if (Selection.First?.Person is not { } avatar)
            return;
        MimicParts wanted = part switch
        {
            "figure" => MimicParts.Figure,
            "motto" => MimicParts.Motto,
            "dance" => MimicParts.Dance,
            "sign" => MimicParts.Sign,
            "effect" => MimicParts.Effect,
            "direction" => MimicParts.Direction,
            "walk" => MimicParts.Walk,
            _ => MimicParts.Appearance | MimicParts.Walk
        };
        try
        {
            MimicParts done = Context.Gateway.Game.Mimic.Copy(avatar, wanted);
            if (done == MimicParts.None)
                Context.Warn($"{avatar.Name} has nothing there to copy.");
            else
                Context.Say($"Copied {RoomText.Copied(done)} from {avatar.Name}.");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Context.Fail($"Could not copy anything from {avatar.Name}", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOutfit))]
    void SaveOutfit()
    {
        if (Selection.First?.Person is not User user || user.Figure.Length == 0)
            return;
        string gender = user.Gender is Gender.Female ? "F" : "M";
        bool kept = Context.Outfits.Add(new SavedOutfit(user.Figure, gender, user.Name));
        if (kept)
            Context.Say($"Kept {user.Name}'s outfit in your wardrobe.");
        else
            Context.Note("You are already keeping that outfit.");
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    async Task CopyFieldAsync(string? field, CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        (string text, string done) = field switch
        {
            "id" => (row.IdText, "User id copied."),
            "motto" => (row.Motto, "Motto copied."),
            "figure" => (row.Figure, "Figure copied."),
            _ => (row.Name, "Name copied.")
        };
        await Context.CopyAsync(text, done, cancellation_token);
    }

    [RelayCommand]
    async Task CopySelectionAsync(CancellationToken cancellation_token)
    {
        if (Selection.Count == 0)
            return;
        string text = string.Join(
            Environment.NewLine,
            Selection.Items.Select(row => string.Join('\t', Columns(row))));
        int rows = Selection.Count;
        await Context.CopyAsync(text, rows == 1 ? "Copied 1 row." : $"Copied {rows} rows.", cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanModerate))]
    async Task MuteAsync(string? minutes, CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row || !int.TryParse(minutes, CultureInfo.InvariantCulture, out int span))
            return;
        await ModerateAsync(
            row,
            span == 0 ? $"Unmuted {row.Name}." : $"Muted {row.Name} for {minutes} minutes.",
            $"Could not mute {row.Name}",
            (state, token) => Context.Gateway.InvokeAsync<RoomModerationMuteRequest, RoomModerationDispatchResult>(
                ApplicationMemberIds.RoomModerationMute,
                new RoomModerationMuteRequest(
                    row.UserId,
                    span,
                    state.SessionGeneration,
                    state.RoomId,
                    row.RoomGeneration,
                    row.Index),
                token),
            cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanModerate))]
    async Task KickAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        await ModerateAsync(
            row,
            $"Kicked {row.Name}.",
            $"Could not kick {row.Name}",
            (state, token) => Context.Gateway.InvokeAsync<RoomModerationTargetRequest, RoomModerationDispatchResult>(
                ApplicationMemberIds.RoomModerationKick,
                new RoomModerationTargetRequest(
                    row.UserId,
                    state.SessionGeneration,
                    state.RoomId,
                    row.RoomGeneration,
                    row.Index),
                token),
            cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanModerate))]
    async Task BanAsync(string? length, CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        (BanLength span, string word) = length switch
        {
            "day" => (BanLength.Day, "for a day"),
            "perm" => (BanLength.Permanent, "permanently"),
            _ => (BanLength.Hour, "for an hour")
        };
        bool sure = await Context.Dialogs.ConfirmAsync(
            $"Ban {row.Name}?",
            $"They are banned from this room {word} and put out of it now.",
            "Ban",
            DialogTone.Destructive,
            cancellation_token: cancellation_token);
        if (!sure)
            return;
        await ModerateAsync(
            row,
            $"Banned {row.Name} {word}.",
            $"Could not ban {row.Name}",
            (state, token) => Context.Gateway.InvokeAsync<RoomModerationBanRequest, RoomModerationDispatchResult>(
                ApplicationMemberIds.RoomModerationBan,
                new RoomModerationBanRequest(
                    row.UserId,
                    span,
                    state.SessionGeneration,
                    state.RoomId,
                    row.RoomGeneration,
                    row.Index),
                token),
            cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanModerate))]
    async Task BounceAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        await ModerateAsync(
            row,
            $"Bounced {row.Name}.",
            $"Could not bounce {row.Name}",
            (state, token) => Context.Gateway.InvokeAsync<RoomModerationTargetRequest, RoomModerationDispatchResult>(
                ApplicationMemberIds.RoomModerationBounce,
                new RoomModerationTargetRequest(
                    row.UserId,
                    state.SessionGeneration,
                    state.RoomId,
                    row.RoomGeneration,
                    row.Index),
                token),
            cancellation_token);
    }

    protected abstract void Fill(RoomSnapshot snapshot);

    protected abstract IComparer<PersonRowViewModel> Order { get; }

    protected abstract void Recount();

    protected abstract IEnumerable<string> Columns(PersonRowViewModel row);

    protected void ShowNoMatches() =>
        State.ShowEmpty(IconKind.Search, "No matches", $"Nothing matches “{Filter.Trim()}”.", ClearFiltersCommand, "Clear filters");

    protected void ApplyFilter()
    {
        string term = Filter.Trim();
        Rows.ApplyAsync(
            _all.Rows,
            row => Keep(row, term),
            Order,
            Context.Lifetime).Observe("ui");
    }

    [RelayCommand]
    void ClearFilters() => ClearFilter();

    static bool Keep(PersonRowViewModel row, string term)
    {
        if (term.Length == 0)
            return true;
        PersonSearch search = row.Search;
        return search.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            search.Detail.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }

    void OnSelectionChanged() => RefreshMenu();

    void OnRowsApplied()
    {
        Selection.Prune(Rows.Visible);
        Recount();
    }

    partial void OnFilterChanged(string value) => _refilter.Trigger();

    async Task ModerateAsync(
        PersonRowViewModel row,
        string done,
        string trouble,
        Func<RoomModerationStateView, CancellationToken, Task<RoomModerationDispatchResult>> send,
        CancellationToken cancellation_token)
    {
        try
        {
            RoomModerationStateView state = await TargetAsync(row, cancellation_token);
            await send(state, cancellation_token);
            Context.Say(done);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Context.Fail(trouble, error);
        }
    }

    async Task<RoomModerationStateView> TargetAsync(PersonRowViewModel row, CancellationToken cancellation_token)
    {
        if (row.Person is not User user || row.Index < 0 || user.Id != row.UserId || user.Index != row.Index)
            throw new InvalidOperationException("The selected row is not a current room user.");
        RoomModerationStateView state = await Context.Gateway
            .QueryAsync<RoomModerationStateRequest, RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationState,
                new RoomModerationStateRequest(),
                cancellation_token);
        if (!state.RoomReady || state.RoomId <= 0 || state.RoomGeneration != row.RoomGeneration)
            throw new InvalidOperationException("The selected user is no longer in the current room.");
        return state;
    }

    (bool Available, string? Reason) TradeGate(PersonRowViewModel? row, bool self)
    {
        if (row is null || self)
            return (false, null);
        if (TradeTarget(row) is null)
            return (false, "Only somebody standing in this room can be traded with.");
        MemberGate gate = Context.Gateway.Gate(ApplicationMemberIds.TradeOpen);
        return (gate.Available, gate.Available ? null : gate.Reason);
    }

    (bool Available, string? Reason) ModerationGate(PersonRowViewModel? row, bool self)
    {
        if (row is null || self)
            return (false, null);
        if (!_snapshot.Identity.IsOwner && !_snapshot.Identity.HasRights)
            return (false, "Only the room owner or somebody with rights can moderate here.");
        if (row.Person is not User user || row.Index < 0 || user.Id != row.UserId || user.Index != row.Index)
            return (false, "The selected row is not a current room user.");
        foreach (string member in _moderation_members)
        {
            MemberGate gate = Context.Gateway.Gate(member);
            if (!gate.Available)
                return (false, gate.Reason);
        }
        return (true, null);
    }

    User? TradeTarget(PersonRowViewModel row)
    {
        if (row.Person is not User)
            return null;
        int index = row.Index;
        Id wanted = row.UserId;
        return Context.Gateway.Game.Room.Capture(room =>
            room.IsReady && room.AvatarByIndex(index) is User current && current.Id == wanted
                ? current
                : null);
    }
}
