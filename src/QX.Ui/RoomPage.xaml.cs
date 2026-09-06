using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;

namespace Qx.Ui;

/// <summary>
/// Everything about the room the session is in, on one page.
/// </summary>
/// <remarks>
/// One entry in the sidebar, not four. Who is here, who has been here, who may not come back and
/// what is standing in the room are all questions about the same room, and four buttons that each
/// opened the same panel at a different scroll position made them look like four features.
/// </remarks>
public partial class RoomPage : GamePage
{
    private IReadOnlyList<RoomEntry> _avatars = [];
    private IReadOnlyList<RoomEntry> _visitors = [];
    private IReadOnlyList<RoomEntry> _bans = [];
    private IReadOnlyList<RoomEntry> _furni = [];
    private IReadOnlyList<FurniStack> _stacks = [];
    private readonly ObservableCollection<RoomEntry> _user_rows = [];
    private readonly ObservableCollection<RoomEntry> _visitor_rows = [];
    private DispatcherTimer? _settle;
    private Area? _area;
    private bool _loadingBans;
    private IDisposable? _moderation_changes;
    private RoomModerationStateView? _moderation_state;
    private long _moderation_binding;
    private long _queued_moderation_binding;
    private int _furni_menu_request;

    public RoomPage()
    {
        InitializeComponent();
        UsersList.ItemsSource = _user_rows;
        VisitorsList.ItemsSource = _visitor_rows;

        // The same menu on both views. Declared once, on the list, because a menu declared in the
        // resources would have no names to bind its items to.
        FurniGrid.ContextMenu = FurniList.ContextMenu;
    }

    private GameState? _game => Game;

    /// <summary>Whether a filter is holding text, which decides what Escape means.</summary>
    public override bool IsSearching =>
        UsersFilter.Text.Length > 0 ||
        FurniFilter.Text.Length > 0 ||
        VisitorsFilter.Text.Length > 0 ||
        BansFilter.Text.Length > 0;

    public override void Opened() => Open(RoomSection.Info);

    /// <summary>Brings the page up on one tab.</summary>
    public void Open(RoomSection section)
    {
        Tabs.SelectedItem = section switch
        {
            RoomSection.Users => UsersTab,
            RoomSection.Visitors => VisitorsTab,
            RoomSection.Bans => BansTab,
            RoomSection.Furni => FurniTab,
            _ => InfoTab
        };

        Refresh();
        PostOnUi(FocusFilter, DispatcherPriority.Input);
    }

    private void FocusFilter()
    {
        if (ReferenceEquals(Tabs.SelectedItem, UsersTab))
            UsersFilter.Focus();
        else if (ReferenceEquals(Tabs.SelectedItem, FurniTab))
            FurniFilter.Focus();
        else if (ReferenceEquals(Tabs.SelectedItem, VisitorsTab))
            VisitorsFilter.Focus();
        else if (ReferenceEquals(Tabs.SelectedItem, BansTab))
            BansFilter.Focus();
    }

    /// <summary>
    /// Reads the room again.
    /// </summary>
    /// <remarks>
    /// Everything is taken in one pass rather than per tab. A room raises dozens of events a second
    /// while it loads, and reading each tab on its own would leave the counts in the headers
    /// disagreeing with the lists under them.
    /// </remarks>
    public override void Refresh()
    {
        if (_game is null || !_game.Room.IsInRoom)
        {
            _avatars = [];
            _visitors = [];
            _bans = [];
            _moderation_state = null;
            _furni = [];
            _stacks = [];
            OutsideNotice.Visibility = Visibility.Visible;
            Subheading.Text = "Not in a room.";
            Apply();
            return;
        }

        OutsideNotice.Visibility = Visibility.Collapsed;
        _avatars = RoomContents.Avatars(_game);
        _visitors = RoomContents.Visitors(_game);
        if (Application is { } application)
        {
            try
            {
                _moderation_state = ReadModerationState(application);
                _bans = _moderation_state.RoomReady
                    ? RoomContents.Bans(_moderation_state)
                    : [];
            }
            catch
            {
                _moderation_state = null;
                _bans = [];
            }
        }
        else
        {
            _moderation_state = null;
            _bans = [];
        }
        _furni = RoomContents.Furni(_game);
        _stacks = RoomContents.FurniByKind(_game);

        Subheading.Text = _game.Room.Name is { Length: > 0 } name
            ? $"In {name}."
            : "In this room.";

        FillInfo(_game);
        Apply();
    }

    private void Apply()
    {
        string users = UsersFilter.Text.Trim();
        IReadOnlyList<RoomEntry> visible =
        [
            .. _avatars.Where(row => row.Tag switch
            {
                "bot" => _showBots,
                "pet" => _showPets,
                _ => true
            })
        ];
        List<RoomEntry> people = Match(visible, users);
        UpdateRows(_user_rows, people, UsersList);
        UsersBadge.Text = Badge(people.Count);

        List<RoomEntry> visitors = Match(_visitors, VisitorsFilter.Text.Trim());
        UpdateRows(_visitor_rows, visitors, VisitorsList);
        VisitorsBadge.Text = Badge(_visitors.Count);
        int here = _visitors.Count(row => row.HasTag);
        VisitorsStatus.Text = _visitors.Count == 0
            ? "Nobody has come or gone since this room was opened. The hotel keeps no such list, so this one starts when you walk in."
            : $"{_visitors.Count:N0} seen, {here:N0} still here" +
              (visitors.Count == _visitors.Count ? "" : $" · {visitors.Count:N0} shown");

        List<RoomEntry> bans = Match(_bans, BansFilter.Text.Trim());
        BansList.ItemsSource = bans;
        BansStatus.Text = BanStatusText(bans.Count);

        // The area holds the list down to part of the floor before anything is typed, so a search
        // inside a captured area searches only what is inside it.
        IReadOnlyList<RoomEntry> scope = _area is { } area
            ? RoomContents.Within(_furni, area)
            : _furni;

        string furni = FurniFilter.Text.Trim();
        List<RoomEntry> items = Match(scope, furni);
        HashSet<Id> selected_items =
        [
            .. FurniList.SelectedItems
                .OfType<RoomEntry>()
                .Select(row => row.EntityId)
        ];
        HashSet<(ItemType Type, int Kind)> selected_stacks =
        [
            .. FurniGrid.SelectedItems
                .OfType<FurniStack>()
                .Select(stack => (stack.Type, stack.Kind))
        ];

        FurniList.SelectionChanged -= FurniSelectionChanged;
        FurniGrid.SelectionChanged -= FurniSelectionChanged;
        try
        {
            FurniList.ItemsSource = items;
            foreach (RoomEntry row in items.Where(row => selected_items.Contains(row.EntityId)))
                FurniList.SelectedItems.Add(row);

            List<FurniStack> stacks = StacksFor(scope, furni);
            FurniGrid.ItemsSource = stacks;
            foreach (FurniStack stack in stacks.Where(stack => selected_stacks.Contains((stack.Type, stack.Kind))))
                FurniGrid.SelectedItems.Add(stack);
        }
        finally
        {
            FurniList.SelectionChanged += FurniSelectionChanged;
            FurniGrid.SelectionChanged += FurniSelectionChanged;
        }
        FurniBadge.Text = Badge(items.Count);

        int hidden = _furni.Count(row => row.Item?.IsHidden == true);
        ShowHiddenButton.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowHiddenLabel.Text = $"Show {hidden}";

        ReportFurni(items.Count, FurniGrid.Items.Count);
    }

    private static void UpdateRows(
        ObservableCollection<RoomEntry> rows,
        IReadOnlyList<RoomEntry> updated,
        DataGrid grid)
    {
        HashSet<(Id Id, int Index, string Name)> selected =
        [
            .. grid.SelectedItems
                .OfType<RoomEntry>()
                .Select(Identity)
        ];

        for (int index = 0; index < updated.Count; index++)
        {
            RoomEntry next = updated[index];
            int existing = FindRow(rows, next, index);
            if (existing < 0)
            {
                rows.Insert(index, next);
                continue;
            }

            if (existing != index)
                rows.Move(existing, index);

            if (!SameRow(rows[index], next))
                rows[index] = next;
        }

        while (rows.Count > updated.Count)
            rows.RemoveAt(rows.Count - 1);

        foreach (RoomEntry row in rows.Where(row =>
                     selected.Contains(Identity(row)) &&
                     !grid.SelectedItems.Contains(row)))
        {
            grid.SelectedItems.Add(row);
        }
    }

    private static int FindRow(
        IReadOnlyList<RoomEntry> rows,
        RoomEntry expected,
        int start)
    {
        for (int index = start; index < rows.Count; index++)
        {
            if (Identity(rows[index]) == Identity(expected))
                return index;
        }

        return -1;
    }

    private static (Id Id, int Index, string Name) Identity(RoomEntry row) =>
        (row.EntityId, row.Index, row.Name);

    private static bool SameRow(RoomEntry left, RoomEntry right) =>
        left.EntityId == right.EntityId &&
        left.Index == right.Index &&
        left.RoomGeneration == right.RoomGeneration &&
        left.Name == right.Name &&
        left.Detail == right.Detail &&
        left.Position == right.Position &&
        left.Tag == right.Tag &&
        left.Fallback == right.Fallback &&
        left.IsIdle == right.IsIdle &&
        left.IsTrading == right.IsTrading &&
        left.ImageUrl == right.ImageUrl &&
        ReferenceEquals(left.Person, right.Person);

    private string BanStatusText(int shown)
    {
        if (_game is null)
            return "";
        if (_loadingBans)
            return "Asking the hotel…";
        if (_moderation_state?.Loaded != true)
            return "Not loaded. The hotel only sends the ban list when it is asked for, and only to someone with rights in the room.";
        if (_bans.Count == 0)
            return "Nobody is barred from this room.";

        return shown == _bans.Count
            ? $"{_bans.Count:N0} barred"
            : $"{shown:N0} of {_bans.Count:N0} barred";
    }

    private static RoomModerationStateView ReadModerationState(IApplicationRuntime application)
    {
        RoomModerationStateView first = application.Invoke<
            RoomModerationStateRequest,
            RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationState,
                new RoomModerationStateRequest(Limit: 500));
        return CompleteModerationState(application, first);
    }

    private static RoomModerationStateView CompleteModerationState(
        IApplicationRuntime application,
        RoomModerationStateView first)
    {
        if (first.BanList.Offset != 0)
            throw new InvalidOperationException("The room-ban snapshot did not start at offset zero.");
        var bans = new List<RoomBanView>(first.BanList.TotalBans);
        bans.AddRange(first.BanList.Bans);
        int? next_offset = first.BanList.NextOffset;
        while (next_offset is int offset)
        {
            RoomModerationStateView page = application.Invoke<
                RoomModerationStateRequest,
                RoomModerationStateView>(
                    ApplicationMemberIds.RoomModerationState,
                    new RoomModerationStateRequest(
                        offset,
                        500,
                        first.BanList.SnapshotRevision));
            if (page.SessionGeneration != first.SessionGeneration ||
                page.Revision != first.Revision ||
                page.RoomGeneration != first.RoomGeneration ||
                page.RoomId != first.RoomId ||
                page.BanList.SnapshotRevision != first.BanList.SnapshotRevision ||
                page.BanList.Offset != offset)
            {
                throw new InvalidOperationException("The room-ban snapshot changed between pages.");
            }
            bans.AddRange(page.BanList.Bans);
            if (page.BanList.NextOffset is int following && following <= offset)
                throw new InvalidOperationException("The room-ban snapshot returned an invalid continuation offset.");
            next_offset = page.BanList.NextOffset;
        }
        if (bans.Count != first.BanList.TotalBans)
            throw new InvalidOperationException("The room-ban snapshot ended before every entry was read.");
        return first with
        {
            BanList = first.BanList with
            {
                Offset = 0,
                NextOffset = null,
                Bans = Array.AsReadOnly(bans.ToArray())
            }
        };
    }

    /// <summary>
    /// Rebuilds the grid's kinds from whatever the list is currently showing.
    /// </summary>
    /// <remarks>
    /// Folded from the same rows rather than filtered off the whole room, so an area or a search
    /// narrows both views to the same thing and the counts on the tiles stay true to it.
    /// </remarks>
    private static List<FurniStack> StacksFor(IReadOnlyList<RoomEntry> scope, string term)
    {
        List<RoomEntry> rows = Match(scope, term);
        return
        [
            .. rows
                .Where(row => row.Item is not null)
                .GroupBy(row => (row.Item!.Type, row.Item!.Kind))
                .Select(group =>
                {
                    RoomEntry first = group.First();
                    return new FurniStack(first.ImageUrl)
                    {
                        Name = first.Name,
                        Count = group.Count(),
                        Identifier = first.Item!.Identifier ?? "",
                        Kind = first.Item!.Kind,
                        Type = first.Item!.Type,
                        Items = [.. group.Select(row => row.Item!)]
                    };
                })
                .OrderBy(stack => stack.Name, StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    private void ReportFurni(int items, int kinds)
    {
        if (_game?.RoomActions.Progress is { IsRunning: true } running)
        {
            FurniStatus.Text = running.ToString();
            CancelRun.Visibility = Visibility.Visible;
            return;
        }

        CancelRun.Visibility = Visibility.Collapsed;

        string counted = FurniGrid.Visibility == Visibility.Visible
            ? $"{kinds} {(kinds == 1 ? "kind" : "kinds")}, {items} in total"
            : $"{items} {(items == 1 ? "item" : "items")}";

        FurniStatus.Text = _area is { } area
            ? $"{counted} · area ({area.Origin.X}, {area.Origin.Y}) to " +
              $"({area.Origin.X + area.Width - 1}, {area.Origin.Y + area.Length - 1}) · {_furni.Count} in the room"
            : counted;
    }

    /// <summary>Zero reads as nothing at all; a badge showing "0" is noise.</summary>
    private static string Badge(int count) => count > 0 ? count.ToString() : "";

    private static List<RoomEntry> Match(IReadOnlyList<RoomEntry> source, string term) =>
        term.Length == 0
            ? [.. source]
            : [.. source.Where(entry =>
                entry.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                entry.Detail.Contains(term, StringComparison.CurrentCultureIgnoreCase))];

    private void FillInfo(GameState game)
    {
        RoomData? data = game.Room.Data;

        RoomName.Text = game.Room.Name is { Length: > 0 } name ? name : "This room";
        RoomOwner.Text = game.Room.OwnerName is { Length: > 0 } owner ? $"by {owner}" : "";
        RoomDescription.Text = game.Room.Description;
        RoomDescription.Visibility = game.Room.Description.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        string host = string.IsNullOrWhiteSpace(HabboImages.WebHost)
            ? "www.habbo.com"
            : HabboImages.WebHost;
        RoomLinkText.Text = $"{host}/room/{game.Room.RoomId}";
        RoomLink.IsEnabled = game.Room.RoomId > 0;
        CopyRoomLinkButton.IsEnabled = game.Room.RoomId > 0;

        var room = new List<Stat>
        {
            new("ROOM ID", game.Room.RoomId.ToString()),
            new("YOUR RIGHTS", game.Room.RightsAreKnown
                ? RightsText(game.Room.RightsLevel, game.Room.IsOwner)
                : "not known yet")
        };

        if (data is not null)
        {
            room.Add(new Stat("OWNER ID", data.OwnerId.ToString()));
            room.Add(new Stat("TRADING", data.TradeMode.ToString()));
            room.Add(new Stat("VISITORS", $"{data.UserCount} of {data.MaxUserCount}"));
            room.Add(new Stat("CATEGORY", data.Category.ToString()));
            room.Add(new Stat("SCORE", data.Score.ToString()));
            room.Add(new Stat("RANKING", data.Ranking.ToString()));
            room.Add(new Stat("PETS", data.AllowPets ? "allowed" : "not allowed"));
            room.Add(new Stat("OWNER SHOWN", data.ShowOwner ? "yes" : "no"));
            room.Add(new Stat("ENTRY AD", data.DisplayRoomEntryAd ? "shown" : "hidden"));
            room.Add(new Stat("OFFICIAL IMAGE", data.OfficialRoomPicRef is { Length: > 0 } ? "yes" : "no"));
            if (data.Tags.Count > 0)
                room.Add(new Stat("TAGS", string.Join(", ", data.Tags)));
            if (data.HasGroup && data.GroupName.Length > 0)
            {
                room.Add(new Stat("GROUP", data.GroupName));
                room.Add(new Stat("GROUP ID", data.GroupId.ToString()));
            }
            if (data.HasEvent && data.EventName.Length > 0)
            {
                room.Add(new Stat("EVENT", data.EventName));
                room.Add(new Stat("EVENT LEFT", $"{data.EventMinutesRemaining} min"));
            }
        }

        room.Add(new Stat("FLOOR ITEMS", game.Room.FloorItems.Count.ToString()));
        room.Add(new Stat("WALL ITEMS", game.Room.WallItems.Count.ToString()));
        room.Add(new Stat("CONTROLLERS", game.Room.Controllers.Count.ToString()));
        if (game.Room.EntryTile is { } entry)
            room.Add(new Stat("ENTRY TILE", $"{entry.X}, {entry.Y} · {entry.Direction}"));
        if (game.Room.VisualizationSettings is { } visual)
        {
            room.Add(new Stat("WALLS", visual.WallsHidden ? "hidden" : "shown"));
            room.Add(new Stat("WALL THICKNESS", visual.WallThickness.ToString()));
            room.Add(new Stat("FLOOR THICKNESS", visual.FloorThickness.ToString()));
        }
        if (game.Room.Details is { } details)
        {
            room.Add(new Stat("STAFF PICK", details.IsStaffPick ? "yes" : "no"));
            room.Add(new Stat("GROUP MEMBER", details.IsGroupMember ? "yes" : "no"));
            room.Add(new Stat("ROOM MUTED", details.IsRoomMuted ? "yes" : "no"));
            room.Add(new Stat("CAN MUTE", details.CanMute ? "yes" : "no"));
        }

        RoomFields.ItemsSource = room;

        RoomModerationSettings? moderation = game.Room.Details?.Moderation;
        RoomChatSettings? chat = game.Room.ChatSettings;
        AccessRule.Content = data?.DoorMode.ToString() ?? "not received";
        MuteRule.Content = new Stat("MUTE", moderation?.Mute.ToString() ?? "not received");
        KickRule.Content = new Stat("KICK", moderation?.Kick.ToString() ?? "not received");
        BanRule.Content = new Stat("BAN", moderation?.Ban.ToString() ?? "not received");
        FlowRule.Content = new Stat("FLOW", chat?.Flow.ToString() ?? "not received");
        BubbleRule.Content = new Stat("BUBBLE", chat?.BubbleWidth.ToString() ?? "not received");
        ScrollRule.Content = new Stat("SCROLL", chat?.ScrollSpeed.ToString() ?? "not received");
        HearingRule.Content = new Stat("HEARING", chat?.TalkHearingDistance.ToString() ?? "not received");
        FloodRule.Content = chat?.FloodProtection.ToString() ?? "not received";

        Observe(() => LoadThumbnailAsync(game.Room.RoomId, data?.OfficialRoomPicRef));
    }

    /// <summary>
    /// Fetches the room's picture from the navigator's thumbnail store.
    /// </summary>
    /// <remarks>
    /// It is not on the imaging host and not on the hotel — it sits in a bucket keyed by hotel
    /// identifier and room id, which is why nothing appeared while the page only knew about the
    /// other two. A room whose owner never took a picture has none, and the placeholder stays.
    /// </remarks>
    private async Task LoadThumbnailAsync(long roomId, string? officialPictureRef)
    {
        Thumbnail.Source = null;
        ThumbnailNote.Visibility = Visibility.Collapsed;

        if (roomId <= 0)
            return;

        long requested = roomId;

        // The official banner first, because when a room has one it is named in the room data and
        // is therefore certain. The camera shot is a guess at a key in a store that answers the
        // same way for "no picture" as it does for "wrong key".
        System.Windows.Media.ImageSource? image =
            await HabboImages.LoadAsync(HabboImages.OfficialRoomPictureUrl(officialPictureRef))
            ?? await HabboImages.LoadAsync(HabboImages.RoomThumbnailUrl(roomId));

        // The room may have changed while the fetch was in flight.
        if (_game?.Room.RoomId != requested)
            return;

        Thumbnail.Source = image;

        // Most rooms have no picture at all — only one whose owner took one with the camera does.
        // Saying so beats an empty frame that reads as something being broken.
        if (image is null)
        {
            ThumbnailNote.Visibility = Visibility.Visible;
            ThumbnailNote.Text = "no picture";
        }
    }

    /// <summary>One caption and its value, as the info blocks lay them out.</summary>
    public sealed record Stat(string Caption, string Value);

    private static string RightsText(int? level, bool owner) => owner
        ? "owner"
        : level switch
        {
            null => "not known yet",
            0 => "none",
            1 => "rights",
            2 => "group member",
            3 => "group admin",
            4 => "owner",
            5 => "moderator",
            _ => level.Value.ToString()
        };

    protected override void Attach(GameState game)
    {
        game.Room.Entered += Settle;
        game.Room.Ready += Settle;
        game.Room.Left += Settle;
        game.Room.RoomDataUpdated += OnRoomData;
        game.Room.AvatarsAdded += OnAvatars;
        game.Room.AvatarRemoved += OnAvatar;
        game.Room.AvatarUpdated += OnAvatar;
        game.Room.FloorItemsLoaded += Settle;
        game.Room.WallItemsLoaded += Settle;
        game.Room.FloorItemAdded += OnFloorItem;
        game.Room.FloorItemUpdated += OnFloorItem;
        game.Room.FloorItemRemoved += OnItemId;
        game.Room.WallItemAdded += OnWallItem;
        game.Room.WallItemUpdated += OnWallItem;
        game.Room.WallItemRemoved += OnItemId;
        game.Room.Left += OnLeftRoom;
        game.Visitors.Changed += Settle;
        game.RoomActions.Progressed += OnProgress;
        game.RoomActions.VisibilityChanged += OnVisibility;
    }

    protected override void Detach(GameState game)
    {
        game.Room.Entered -= Settle;
        game.Room.Ready -= Settle;
        game.Room.Left -= Settle;
        game.Room.RoomDataUpdated -= OnRoomData;
        game.Room.AvatarsAdded -= OnAvatars;
        game.Room.AvatarRemoved -= OnAvatar;
        game.Room.AvatarUpdated -= OnAvatar;
        game.Room.FloorItemsLoaded -= Settle;
        game.Room.WallItemsLoaded -= Settle;
        game.Room.FloorItemAdded -= OnFloorItem;
        game.Room.FloorItemUpdated -= OnFloorItem;
        game.Room.FloorItemRemoved -= OnItemId;
        game.Room.WallItemAdded -= OnWallItem;
        game.Room.WallItemUpdated -= OnWallItem;
        game.Room.WallItemRemoved -= OnItemId;
        game.Room.Left -= OnLeftRoom;
        game.Visitors.Changed -= Settle;
        game.RoomActions.Progressed -= OnProgress;
        game.RoomActions.VisibilityChanged -= OnVisibility;
    }

    protected override void AttachApplication(IApplicationRuntime application)
    {
        long binding = Interlocked.Increment(ref _moderation_binding);
        _moderation_changes = application.Subscribe<RoomModerationChanged>(
            ApplicationMemberIds.RoomModerationChanged,
            _ => QueueModerationRefresh(binding));
        QueueModerationRefresh(binding);
    }

    protected override void DetachApplication(IApplicationRuntime application)
    {
        Interlocked.Increment(ref _moderation_binding);
        Interlocked.Exchange(ref _queued_moderation_binding, 0);
        Interlocked.Exchange(ref _moderation_changes, null)?.Dispose();
        _moderation_state = null;
        _bans = [];
    }

    private void QueueModerationRefresh(long binding)
    {
        if (binding != Volatile.Read(ref _moderation_binding) ||
            Interlocked.CompareExchange(ref _queued_moderation_binding, binding, 0) != 0)
        {
            return;
        }
        PostOnUi(
            () =>
            {
                if (Interlocked.CompareExchange(
                        ref _queued_moderation_binding,
                        0,
                        binding) != binding ||
                    binding != Volatile.Read(ref _moderation_binding))
                {
                    return;
                }
                Settle();
            },
            DispatcherPriority.Background);
    }

    /// <summary>An area picked in one room means nothing in the next one.</summary>
    private void OnLeftRoom() => OnUi(() =>
    {
        _area = null;
        AreaToggle.IsChecked = false;
        AreaLabel.Text = "Area";
        Settle();
    });

    /// <summary>
    /// Reports a run as it goes.
    /// </summary>
    /// <remarks>
    /// Written straight to the line rather than through the redraw, because a run is a hundred
    /// steps and rebuilding every list a hundred times to change one sentence would make the run
    /// itself the slowest thing on screen.
    /// </remarks>
    private void OnProgress(FurniProgress progress) => OnUi(() =>
    {
        if (Visibility != Visibility.Visible)
            return;

        if (progress.IsRunning)
        {
            FurniStatus.Text = progress.ToString();
            CancelRun.Visibility = Visibility.Visible;
        }
        else
        {
            ReportFurni(FurniList.Items.Count, FurniGrid.Items.Count);
        }
    });

    private void OnVisibility(Furni item) => Settle();

    // Named rather than written as lambdas so the same delegate can be taken off again; a lambda
    // handed to -= is a different object and never unsubscribes.
    private void OnRoomData(RoomData data) => Settle();

    private void OnAvatars(IReadOnlyList<Avatar> avatars) => Settle();

    private void OnAvatar(Avatar avatar) => Settle();

    private void OnFloorItem(FloorItem item) => Settle();

    private void OnWallItem(WallItem item) => Settle();

    private void OnItemId(Id id) => Settle();

    /// <summary>
    /// Redraws once the room stops changing.
    /// </summary>
    /// <remarks>
    /// Entering a room delivers hundreds of items and avatars within a second, each raising its own
    /// event. Rebuilding the lists on every one of them would rebuild them hundreds of times and
    /// throw away every image request in flight. A short wait after the last change collapses that
    /// into one pass.
    /// </remarks>
    private void Settle() => OnUi(() =>
    {
        if (Visibility != Visibility.Visible)
            return;

        _settle ??= CreateSettleTimer();
        _settle.Stop();
        _settle.Start();
    });

    private DispatcherTimer CreateSettleTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Refresh();
        };
        return timer;
    }

    private void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs))
            return;

        e.Handled = true;
        PostOnUi(FocusFilter, DispatcherPriority.Input);

        // The ban list is the one thing in this room the hotel will not send unprompted, so opening
        // the tab is the ask. Once per room: a list that is already held is not asked for again.
        if (ReferenceEquals(Tabs.SelectedItem, BansTab) &&
            _game is { Room.IsInRoom: true } &&
            Application is not null &&
            _moderation_state?.Loaded != true &&
            !_loadingBans)
        {
            RefreshBans_Click(this, e);
        }
    }

    private void FilterChanged(object sender, TextChangedEventArgs e) => Apply();

    private void ShowFurniList(object sender, RoutedEventArgs e)
    {
        FurniListToggle.IsChecked = true;
        FurniGridToggle.IsChecked = false;
        FurniList.Visibility = Visibility.Visible;
        FurniGrid.Visibility = Visibility.Collapsed;
        Apply();
    }

    private void ShowFurniGrid(object sender, RoutedEventArgs e)
    {
        FurniListToggle.IsChecked = false;
        FurniGridToggle.IsChecked = true;
        FurniList.Visibility = Visibility.Collapsed;
        FurniGrid.Visibility = Visibility.Visible;
        Apply();
    }


    private void ClearVisitors(object sender, RoutedEventArgs e)
    {
        _game?.Visitors.Clear();
        Refresh();
    }


    private void RefreshBans_Click(object sender, RoutedEventArgs e) => Observe(RefreshBansAsync);

    private async Task RefreshBansAsync()
    {
        if (Application is not { } application || _loadingBans)
            return;

        RoomModerationStateView state;
        try
        {
            state = ReadModerationState(application);
            if (!state.RoomReady || state.RoomId <= 0)
                throw new InvalidOperationException("A ready room is required to read its ban list.");
        }
        catch (Exception error)
        {
            BansStatus.Text = $"Could not read the ban list: {error.Message}";
            return;
        }

        _loadingBans = true;
        RefreshBans.IsEnabled = false;
        Apply();

        string? failure = null;
        try
        {
            RoomModerationStateView first = await application.InvokeAsync<
                RoomModerationRefreshRequest,
                RoomModerationStateView>(
                    ApplicationMemberIds.RoomModerationRefresh,
                    new RoomModerationRefreshRequest(
                        500,
                        10000,
                        state.SessionGeneration,
                        state.RoomId,
                        state.RoomGeneration))
                .ConfigureAwait(true);
            RoomModerationStateView refreshed = CompleteModerationState(application, first);
            if (ReferenceEquals(Application, application))
            {
                _moderation_state = refreshed;
                _bans = RoomContents.Bans(refreshed);
            }
        }
        catch (Exception error)
        {
            failure = $"Could not read the ban list: {error.Message}";
        }
        finally
        {
            _loadingBans = false;
            RefreshBans.IsEnabled = true;
            if (ReferenceEquals(Application, application))
            {
                Apply();
                if (failure is not null)
                    BansStatus.Text = failure;
            }
        }
    }

    private void BansSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UnbanSelected.IsEnabled = BansList.SelectedItems.Count > 0;

    private void UnbanSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Application is not { } application)
            return;

        RoomModerationStateView? displayed = _moderation_state;
        RoomModerationStateView state;
        try
        {
            state = ReadModerationState(application);
            if (!state.RoomReady || state.RoomId <= 0)
                throw new InvalidOperationException("A ready room is required to unban a user.");
            if (displayed is null ||
                !displayed.Loaded ||
                !state.Loaded ||
                displayed.SessionGeneration != state.SessionGeneration ||
                displayed.RoomId != state.RoomId ||
                displayed.RoomGeneration != state.RoomGeneration ||
                displayed.BanList.SnapshotRevision != state.BanList.SnapshotRevision)
            {
                throw new InvalidOperationException("The displayed ban list is no longer current.");
            }
        }
        catch (Exception error)
        {
            BansStatus.Text = $"Could not unban the selection: {error.Message}";
            return;
        }
        long? snapshot_revision = displayed.BanList.SnapshotRevision;
        foreach (RoomEntry row in BansList.SelectedItems.OfType<RoomEntry>().ToArray())
        {
            try
            {
                if (row.RoomGeneration != state.RoomGeneration)
                    throw new InvalidOperationException("The selected ban belongs to an earlier room session.");
                application.Invoke<RoomModerationUnbanRequest, RoomModerationDispatchResult>(
                    ApplicationMemberIds.RoomModerationUnban,
                    new RoomModerationUnbanRequest(
                        row.EntityId,
                        state.RoomId,
                        state.SessionGeneration,
                        state.RoomGeneration,
                        snapshot_revision));
                snapshot_revision = null;
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Warn($"Could not unban {row.Name}: {error.Message}", "ui");
                BansStatus.Text = $"Could not unban {row.Name}: {error.Message}";
            }
        }
    }


    /// <summary>
    /// What the acts apply to.
    /// </summary>
    /// <remarks>
    /// From the list it is the rows selected. From the grid a tile is a kind rather than a piece,
    /// so it is every copy of every kind selected — which is the point of the grid.
    /// </remarks>
    private IReadOnlyList<Furni> Selection()
    {
        Furni[] selected = FurniGrid.Visibility == Visibility.Visible
            ? [.. FurniGrid.SelectedItems.OfType<FurniStack>().SelectMany(stack => stack.Items)]
            : [.. FurniList.SelectedItems.OfType<RoomEntry>().Select(row => row.Item).OfType<Furni>()];

        if (_game is not { } game)
            return [];

        return game.Room.Capture(room => selected
            .Select<Furni, Furni?>(item => item switch
            {
                FloorItem => room.FloorItem(item.Id),
                WallItem => room.WallItem(item.Id),
                _ => null
            })
            .OfType<Furni>()
            .ToArray());
    }

    private void FurniSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ReportFurni(FurniList.Items.Count, FurniGrid.Items.Count);

    /// <summary>
    /// Greys out what would not work before it is clicked.
    /// </summary>
    /// <remarks>
    /// The hotel answers a pickup of someone else's furni, a rotation of a picture on the wall or
    /// an eject by somebody who does not own the room by doing nothing at all. A menu that offers
    /// them anyway looks broken; one that shows them greyed says why without being asked.
    /// </remarks>
    private void FurniMenuOpening(object sender, ContextMenuEventArgs e)
    {
        IReadOnlyList<Furni> picked = Selection();
        if (picked.Count == 0 || _game is not { } game)
        {
            e.Handled = true;
            return;
        }

        bool busy = game.RoomActions.IsBusy;
        bool anyFloor = picked.Any(item => item is FloorItem);
        Id? self = SelfId();

        MenuHide.IsEnabled = picked.Any(item => !item.IsHidden);
        MenuShow.IsEnabled = picked.Any(item => item.IsHidden);
        MenuToggle.IsEnabled = !busy;
        MenuRotate.IsEnabled = false;
        MenuMove.IsEnabled = !busy && anyFloor;
        MenuPickup.IsEnabled = !busy && self is { } mine && picked.Any(item => item.OwnerId == mine);
        MenuEject.IsEnabled = !busy && game.Room.IsOwner &&
            self is { } owner && picked.Any(item => item.OwnerId != owner);

        MenuHide.Header = picked.Count > 1 ? $"Hide {picked.Count}" : "Hide";
        MenuShow.Header = picked.Count > 1 ? $"Show {picked.Count}" : "Show";
        MenuToggle.Header = picked.Count > 1 ? $"Toggle {picked.Count}" : "Toggle";
        MenuPickup.Header = picked.Count > 1 ? $"Pick up {picked.Count}" : "Pick up";
        MenuEject.Header = picked.Count > 1 ? $"Eject {picked.Count}" : "Eject";
        MenuCopyIds.Header = picked.Count > 1 ? $"Copy {picked.Count} ids" : "Copy id";

        int request = ++_furni_menu_request;
        SetRotationStatus(anyFloor ? "Loading…" : "Floor items only");
        if (!anyFloor)
            return;

        FloorItem[] floor_items = [.. picked.OfType<FloorItem>()];
        Observe(() => LoadRotationMenuAsync(game, floor_items, request));
    }

    private async Task LoadRotationMenuAsync(
        GameState game,
        IReadOnlyList<FloorItem> floor_items,
        int request)
    {
        IReadOnlyList<int>[] choices;
        try
        {
            choices = await Task.WhenAll(floor_items.Select(item =>
                game.GameData.Furni?.GetInfo(item) is { } info
                    ? FurniDirectionCatalog.GetAsync(info)
                    : Task.FromResult<IReadOnlyList<int>>([]))).ConfigureAwait(true);
        }
        catch (Exception error)
        {
            if (request == _furni_menu_request && ReferenceEquals(_game, game))
                SetRotationStatus("Could not load directions");
            Qx.Diagnostics.Diag.Warn($"Could not load furni directions: {error.Message}", "ui");
            return;
        }

        if (request != _furni_menu_request || !ReferenceEquals(_game, game))
            return;

        int[] directions = choices.Length == 0 || choices.Any(choice => choice.Count == 0)
            ? []
            : [.. choices
                .Skip(1)
                .Aggregate(choices[0].ToHashSet(), (common, choice) =>
                {
                    common.IntersectWith(choice);
                    return common;
                })
                .Where(direction => floor_items.Any(item => item.Direction != direction))
                .Order()];

        MenuRotate.Items.Clear();
        foreach (int direction in directions)
            MenuRotate.Items.Add(RotationItem(direction));

        if (directions.Length == 0)
            SetRotationStatus("No other direction");
        else
            MenuRotate.IsEnabled = !game.RoomActions.IsBusy;
    }

    private void OpenRoomLink(object sender, RoutedEventArgs e)
    {
        if (RoomLinkText.Text is not { Length: > 0 } address || !RoomLink.IsEnabled)
            return;

        try
        {
            Process.Start(new ProcessStartInfo($"https://{address}") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void CopyRoomLink(object sender, RoutedEventArgs e)
    {
        if (RoomLinkText.Text is not { Length: > 0 } address || !CopyRoomLinkButton.IsEnabled)
            return;

        try
        {
            Clipboard.SetText(address);
        }
        catch
        {
        }
    }

    private void SetRotationStatus(string text)
    {
        MenuRotate.Items.Clear();
        MenuRotate.Items.Add(new MenuItem { Header = text, IsEnabled = false });
    }

    private MenuItem RotationItem(int direction)
    {
        var arrow = new PackIcon
        {
            Kind = PackIconKind.ArrowUp,
            Width = 17,
            Height = 17,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new RotateTransform(direction * 45)
        };
        string name = direction switch
        {
            0 => "North",
            1 => "Northeast",
            2 => "East",
            3 => "Southeast",
            4 => "South",
            5 => "Southwest",
            6 => "West",
            _ => "Northwest"
        };
        var item = new MenuItem { Header = arrow, Tag = direction.ToString(), ToolTip = name };
        AutomationProperties.SetName(item, name);
        item.Click += RotateFurni;
        return item;
    }

    private void HideFurni(object sender, RoutedEventArgs e)
    {
        if (_game is not { } game)
            return;

        foreach (Furni item in Selection())
            game.RoomActions.Hide(item);
        Refresh();
    }

    private void ShowFurni(object sender, RoutedEventArgs e)
    {
        if (_game is not { } game)
            return;

        foreach (Furni item in Selection())
            game.RoomActions.Show(item);
        Refresh();
    }

    private void ShowHidden(object sender, RoutedEventArgs e)
    {
        _game?.RoomActions.ShowAll();
        Refresh();
    }

    private void ToggleFurni(object sender, RoutedEventArgs e) =>
        Run(actions => actions.ToggleAsync(Selection()));

    private void RotateFurni(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } || !int.TryParse(tag, out int direction))
            return;

        Run(actions => actions.RotateAsync(Selection(), direction));
    }

    private void MoveFurni(object sender, RoutedEventArgs e) =>
        Run(actions => actions.MoveAsync(Selection()));

    private void PickupFurni(object sender, RoutedEventArgs e) =>
        Run(actions => actions.PickupAsync(Selection()));

    private void EjectFurni(object sender, RoutedEventArgs e) =>
        Run(actions => actions.EjectAsync(Selection()));

    private void CopyFurniIds(object sender, RoutedEventArgs e)
    {
        string ids = string.Join(", ", Selection().Select(item => item.Id));
        if (ids.Length == 0)
            return;

        try
        {
            System.Windows.Clipboard.SetText(ids);
        }
        catch
        {
            // Another process can hold the clipboard open. Not worth interrupting anyone over.
        }
    }

    private void Run(Func<RoomActions, Task> work) => Observe(() => RunAsync(work));

    private async Task RunAsync(Func<RoomActions, Task> work)
    {
        if (_game is not { } game)
            return;

        try
        {
            await work(game.RoomActions).ConfigureAwait(true);
        }
        catch (Exception error)
        {
            FurniStatus.Text = error.Message;
            return;
        }

        Refresh();
    }

    private void CancelRun_Click(object sender, RoutedEventArgs e) => _game?.RoomActions.Cancel();

    /// <summary>
    /// Takes two clicks in the room and keeps only what stands between them.
    /// </summary>
    /// <remarks>
    /// Pressing it again while an area is held drops the area rather than starting another
    /// selection, so the same button both narrows and widens.
    /// </remarks>
    private void CaptureArea(object sender, RoutedEventArgs e) => Observe(CaptureAreaAsync);

    private async Task CaptureAreaAsync()
    {
        if (_game is not { } game)
            return;

        if (_area is not null)
        {
            _area = null;
            AreaToggle.IsChecked = false;
            AreaLabel.Text = "Area";
            Apply();
            return;
        }

        if (game.RoomActions.IsBusy)
        {
            game.RoomActions.Cancel();
            AreaToggle.IsChecked = false;
            AreaLabel.Text = "Area";
            return;
        }

        AreaToggle.IsChecked = true;
        AreaLabel.Text = "Pick…";

        Area? picked;
        try
        {
            picked = await game.RoomActions.SelectAreaAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            FurniStatus.Text = error.Message;
            AreaToggle.IsChecked = false;
            AreaLabel.Text = "Area";
            return;
        }

        _area = picked;
        AreaToggle.IsChecked = picked is not null;
        AreaLabel.Text = picked is null ? "Area" : "Clear";
        Apply();
    }
}
