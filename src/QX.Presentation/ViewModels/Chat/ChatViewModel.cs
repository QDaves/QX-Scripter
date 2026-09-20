using System.Collections;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Chat;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Chat;

public sealed partial class ChatViewModel : PageViewModel
{
    public static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(150);

    public const int Capacity = 2000;

    readonly IGameGateway _gateway;
    readonly HotelContext _hotel;
    readonly IDialogService _dialogs;
    readonly IFilePickerService _files;
    readonly TimeProvider _time;
    readonly ActivityLog _activity;
    readonly ChatFeed _feed;
    readonly ResettableCollection<ChatLineViewModel> _lines = [];
    readonly ResettableCollection<ChatLineViewModel> _filtered = [];
    readonly Debouncer _refilter;
    string _applied_filter = "";

    public ChatViewModel(
        IGameGateway gateway,
        HotelContext hotel,
        IDialogService dialogs,
        IFilePickerService files,
        ActivityLog activity,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Chat)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _hotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        Notices = Own(new NoticeLine(dispatcher, time));
        _refilter = Own(new Debouncer(dispatcher, time, FilterDelay, Refilter));
        _feed = Own(new ChatFeed(gateway, dispatcher));
        Visible = _lines;
        _feed.Added += OnChatArrived;
        _activity.Added += OnActivityAdded;
        _activity.Changed += OnActivityChanged;
        _activity.Cleared += OnActivityCleared;
        _gateway.SessionChanged += OnSessionChanged;
        Own(() =>
        {
            _feed.Added -= OnChatArrived;
            _activity.Added -= OnActivityAdded;
            _activity.Changed -= OnActivityChanged;
            _activity.Cleared -= OnActivityCleared;
            _gateway.SessionChanged -= OnSessionChanged;
        });
        foreach (ActivityEntry entry in _activity.Entries)
            Place(Line(entry));
        Refresh(0);
        _feed.Start();
    }

    public NoticeLine Notices { get; }

    [ObservableProperty]
    public partial IList Visible { get; private set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowUsers { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWhispers { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWired { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowPets { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowBots { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTrades { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowJoins { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowLeaves { get; set; } = true;

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending), nameof(PendingText))]
    public partial int PendingCount { get; private set; }

    [ObservableProperty]
    public partial bool IsFollowing { get; set; } = true;

    [ObservableProperty]
    public partial long ScrollToEndRequest { get; private set; }

    public bool HasPending => PendingCount > 0;

    public string PendingText => PendingCount == 1 ? "1 new line" : $"{PendingCount} new lines";

    public bool IsFiltering => _applied_filter.Length > 0 ||
        !ShowUsers || !ShowWhispers || !ShowWired || !ShowPets || !ShowBots || !ShowTrades || !ShowJoins || !ShowLeaves;

    public override bool TryClearSearch()
    {
        if (SearchText.Length == 0)
            return false;
        SearchText = "";
        _refilter.Flush();
        return true;
    }

    protected override Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        Refresh(0);
        if (IsFollowing)
            JumpToLatest();
        return Task.CompletedTask;
    }

    [RelayCommand]
    void ClearFilter()
    {
        SearchText = "";
        ShowUsers = ShowWhispers = ShowWired = ShowPets = ShowBots = ShowTrades = ShowJoins = ShowLeaves = true;
        Refilter();
    }

    [RelayCommand]
    void JumpToLatest()
    {
        PendingCount = 0;
        IsFollowing = true;
        ScrollToEndRequest++;
    }

    bool CanExport() => Visible.Count > 0;

    [RelayCommand(CanExecute = nameof(CanExport))]
    async Task ExportAsync(CancellationToken cancellation_token)
    {
        _refilter.Flush();
        if (Visible.Count == 0)
            return;
        string content = string.Join(Environment.NewLine, Visible.Cast<ChatLineViewModel>().Select(line =>
        {
            string timestamp = line.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string tag = line.HasTag ? $" [{line.Tag}]" : "";
            string message = line.IsMessage ? $": {line.Message}" : "";
            return $"[{timestamp}]{tag} {line.Name}{message}";
        }));
        string name = "qx-chat-" + _time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt";
        try
        {
            FilePickResult result = await _files.SaveTextAsync(name, content + Environment.NewLine, cancellation_token);
            if (result.Failure is { Length: > 0 } failure)
                Notices.Show(NoticeSeverity.Warning, failure);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
        }
    }

    [RelayCommand]
    async Task ClearAsync(CancellationToken cancellation_token)
    {
        bool confirmed = await _dialogs.ConfirmAsync(
            "Clear the chat log?",
            "Every line kept here is removed and cannot be brought back. Anything said from now on is logged again.",
            "Clear",
            DialogTone.Destructive,
            null,
            cancellation_token);
        if (!confirmed)
            return;
        try
        {
            await _feed.ClearAsync(cancellation_token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
        }
        _activity.Clear();
        _lines.ReplaceAll([]);
        _filtered.ReplaceAll([]);
        PendingCount = 0;
        IsFollowing = true;
        Refresh(0);
    }

    partial void OnSearchTextChanged(string value) => _refilter.Trigger();

    partial void OnShowUsersChanged(bool value) => Refilter();
    partial void OnShowWhispersChanged(bool value) => Refilter();
    partial void OnShowWiredChanged(bool value) => Refilter();
    partial void OnShowPetsChanged(bool value) => Refilter();
    partial void OnShowBotsChanged(bool value) => Refilter();
    partial void OnShowTradesChanged(bool value) => Refilter();
    partial void OnShowJoinsChanged(bool value) => Refilter();
    partial void OnShowLeavesChanged(bool value) => Refilter();

    partial void OnIsFollowingChanged(bool value)
    {
        if (value)
            PendingCount = 0;
    }

    void OnSessionChanged() => Refresh(0);

    void OnActivityAdded(ActivityEntry entry) => Refresh(Place(Line(entry)) ? 1 : 0);

    void OnActivityChanged(ActivityEntry entry)
    {
        for (int index = _lines.Count - 1; index >= 0; index--)
        {
            ChatLineViewModel line = _lines[index];
            if (line.Kind != ChatLineKind.Activity || line.Sequence != entry.Sequence)
                continue;
            line.Name = entry.Text;
            if (IsFiltering)
                Refilter();
            return;
        }
    }

    void OnActivityCleared()
    {
        for (int index = _lines.Count - 1; index >= 0; index--)
        {
            if (_lines[index].Kind != ChatLineKind.Activity)
                continue;
            _filtered.Remove(_lines[index]);
            _lines.RemoveAt(index);
        }
        Refresh(0);
    }

    void OnChatArrived(IReadOnlyList<RoomChatEntry> batch)
    {
        int shown = 0;
        foreach (RoomChatEntry entry in batch)
        {
            if (Place(Line(entry)))
                shown++;
        }
        Refresh(shown);
    }

    ChatLineViewModel Line(ActivityEntry entry) =>
        new(ChatLineKind.Activity, entry.Sequence, entry.At, entry.Text, category: entry.Kind switch
        {
            ActivityKind.Left => ChatCategory.Leaves,
            ActivityKind.Trade => ChatCategory.Trades,
            _ => ChatCategory.Joins
        });

    ChatLineViewModel Line(RoomChatEntry entry)
    {
        bool pictured = entry.SpeakerType is AvatarType.User or AvatarType.PublicBot or AvatarType.PrivateBot &&
            !string.IsNullOrWhiteSpace(entry.SpeakerFigure);
        string? head = pictured ? HabboUrls.Head(entry.SpeakerFigure, _hotel.WebHost) : null;
        return new ChatLineViewModel(
            ChatLineKind.Message,
            entry.Sequence,
            entry.ReceivedAtUtc,
            entry.SpeakerName is { Length: > 0 } name ? name : $"#{entry.SpeakerIndex}",
            entry.Chat.Message,
            entry.Chat.Type switch
            {
                ChatType.Shout => "shout",
                ChatType.Whisper when entry.Chat.BubbleStyle == 34 => "wired",
                ChatType.Whisper => "whisper",
                _ => ""
            },
            head is null ? null : new ImageRequest(head, false, IconKind.User),
            Category(entry));
    }

    static ChatCategory Category(RoomChatEntry entry)
    {
        ChatCategory speaker = entry.SpeakerType switch
        {
            AvatarType.Pet => ChatCategory.Pets,
            AvatarType.PublicBot or AvatarType.PrivateBot => ChatCategory.Bots,
            _ => 0
        };
        if (entry.Chat.Type == ChatType.Whisper)
            return speaker | (entry.Chat.BubbleStyle == 34 ? ChatCategory.Wired : ChatCategory.Whispers);
        return speaker == 0 ? ChatCategory.Users : speaker;
    }

    bool Matches(ChatLineViewModel line)
    {
        ChatCategory enabled = (ShowUsers ? ChatCategory.Users : 0) |
            (ShowWhispers ? ChatCategory.Whispers : 0) |
            (ShowWired ? ChatCategory.Wired : 0) |
            (ShowPets ? ChatCategory.Pets : 0) |
            (ShowBots ? ChatCategory.Bots : 0) |
            (ShowTrades ? ChatCategory.Trades : 0) |
            (ShowJoins ? ChatCategory.Joins : 0) |
            (ShowLeaves ? ChatCategory.Leaves : 0);
        return (line.Category & enabled) == line.Category && line.Matches(_applied_filter);
    }

    bool Place(ChatLineViewModel line)
    {
        int index = _lines.Count;
        while (index > 0 && _lines[index - 1].IsAfter(line))
            index--;
        _lines.Insert(index, line);
        bool appended = index == _lines.Count - 1;
        Trim();
        if (!IsFiltering)
            return appended;
        if (!appended)
        {
            Refilter();
            return false;
        }
        if (!Matches(line))
            return false;
        _filtered.Add(line);
        return true;
    }

    void Trim()
    {
        while (_lines.Count > Capacity)
        {
            ChatLineViewModel dropped = _lines[0];
            _lines.RemoveAt(0);
            if (IsFiltering)
                _filtered.Remove(dropped);
        }
    }

    void Refilter()
    {
        _applied_filter = SearchText.Trim();
        if (!IsFiltering)
        {
            _filtered.ReplaceAll([]);
            Visible = _lines;
        }
        else
        {
            _filtered.ReplaceAll(_lines.Where(Matches));
            Visible = _filtered;
        }
        Refresh(0);
        ScrollToEndRequest++;
    }

    void Refresh(int appended)
    {
        int total = _lines.Count;
        int shown = IsFiltering ? _filtered.Count : total;
        CountText = shown == total ? $"{shown:N0} shown" : $"{shown:N0} of {total:N0} shown";
        ExportCommand.NotifyCanExecuteChanged();
        Subtitle = total == 0 ? "" : $"{total:N0} {(total == 1 ? "line" : "lines")} kept, newest last";
        if (total == 0)
        {
            if (_gateway.IsHotelConnected)
                State.ShowEmpty(IconKind.Chat, "Nothing said yet.", "The log starts when QX does.");
            else
                State.ShowUnavailable(IconKind.ConnectionOff, "Not connected", "Connect through G-Earth to see room chat.");
        }
        else if (shown == 0)
        {
            string detail = _applied_filter.Length == 0
                ? "No lines match the selected filters."
                : $"Nothing matches “{_applied_filter}”.";
            State.ShowEmpty(IconKind.Search, "No matches", detail, ClearFilterCommand, "Clear filters");
        }
        else
        {
            State.ShowReady();
        }
        if (appended == 0)
            return;
        if (IsFollowing && _applied_filter.Length == 0 && IsActive)
            ScrollToEndRequest++;
        else
            PendingCount += appended;
    }
}
