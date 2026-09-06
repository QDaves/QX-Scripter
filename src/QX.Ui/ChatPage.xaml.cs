using System.Windows;
using System.Windows.Controls;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Ui;

public partial class ChatPage : GamePage
{
    private const int Limit = 2000;
    private const int PageSize = 500;
    private const int PagesPerDrain = 4;

    private readonly List<ActivityRow> activity = [];
    private readonly List<TimedRow> chat = [];
    private readonly HashSet<long> chat_sequences = [];
    private ChatBinding? chat_binding;
    private long activity_sequence;
    private long chat_cursor;
    private long hidden_chat_sequence;

    public ChatPage() => InitializeComponent();

    public override bool IsSearching => Filter.Text.Length > 0;

    protected override void Attach(GameState game)
    {
        game.Room.Entered += OnEntered;
        game.Room.RoomDataUpdated += OnRoomData;
        game.Room.AvatarsAdded += OnArrived;
        game.Room.AvatarRemoved += OnLeft;
    }

    protected override void Detach(GameState game)
    {
        game.Room.Entered -= OnEntered;
        game.Room.RoomDataUpdated -= OnRoomData;
        game.Room.AvatarsAdded -= OnArrived;
        game.Room.AvatarRemoved -= OnLeft;
    }

    protected override void AttachApplication(IApplicationRuntime application)
    {
        hidden_chat_sequence = 0;
        chat_cursor = 0;
        chat.Clear();
        chat_sequences.Clear();
        var binding = new ChatBinding(application);
        chat_binding = binding;
        binding.Subscription = application.Subscribe<RoomChatEntry>(
            ApplicationMemberIds.RoomChatReceived,
            _ => ScheduleChatDrain(binding));
        SeedChat(application, out bool more);
        if (more)
            ScheduleChatDrain(binding, signal: false);
        RefreshIfVisible();
    }

    protected override void DetachApplication(IApplicationRuntime application)
    {
        ChatBinding? binding = chat_binding;
        chat_binding = null;
        binding?.Dispose();
    }

    private void OnEntered()
    {
        GameState? game = Game;
        if (game is null)
            return;
        var snapshot = game.Room.Capture(room => (
            room.Generation,
            room.RoomId,
            room.Name,
            room.OwnerName,
            received_at_utc: DateTimeOffset.Now));
        OnUi(() =>
        {
            if (!ReferenceEquals(Game, game))
                return;
            var current = game.Room.Capture(room => (
                room.Generation,
                room.RoomId,
                room.Name,
                room.OwnerName));
            bool same_room = current.Generation == snapshot.Generation &&
                current.RoomId == snapshot.RoomId;
            AddActivity(
                EntryRow(
                    same_room ? current.Name : snapshot.Name,
                    same_room ? current.OwnerName : snapshot.OwnerName,
                    snapshot.received_at_utc),
                snapshot.received_at_utc,
                room_generation: snapshot.Generation,
                room_id: snapshot.RoomId);
        });
    }

    private void OnRoomData(RoomData data)
    {
        GameState? game = Game;
        if (game is null)
            return;
        var scope = game.Room.Capture(room => (
            generation: room.Generation,
            room_id: room.RoomId));
        if (scope.room_id != (long)data.Id)
            return;
        string name = data.Name;
        string owner_name = data.OwnerName;
        OnUi(() =>
        {
            if (!ReferenceEquals(Game, game))
                return;
            int index = activity.FindLastIndex(value =>
                value.RoomGeneration == scope.generation &&
                value.RoomId == scope.room_id);
            if (index < 0)
                return;
            ActivityRow current = activity[index];
            activity[index] = current with
            {
                Row = EntryRow(name, owner_name, current.ReceivedAtUtc)
            };
            RefreshIfVisible();
        });
    }

    private static GameRow EntryRow(
        string name,
        string owner_name,
        DateTimeOffset received_at_utc)
    {
        string room_name = name is { Length: > 0 } ? name : "room";
        return new GameRow
        {
            Name = owner_name is { Length: > 0 } owner
                ? $"Entered {room_name} · owned by {owner}"
                : $"Entered {room_name}",
            Trailing = received_at_utc.LocalDateTime.ToString("HH:mm")
        };
    }

    private void OnArrived(IReadOnlyList<Avatar> avatars)
    {
        GameState? game = Game;
        if (game is null || !game.Room.Capture(room => room.IsReady))
            return;
        DateTimeOffset received_at_utc = DateTimeOffset.Now;
        (Id Id, string Name)[] users = avatars
            .OfType<User>()
            .Select(user => (user.Id, user.Name))
            .ToArray();
        OnUi(() =>
        {
            if (!ReferenceEquals(Game, game))
                return;
            foreach ((Id id, string name) in users)
            {
                AddActivity(new GameRow
                {
                    Name = $"{name} came in",
                    Trailing = received_at_utc.LocalDateTime.ToString("HH:mm"),
                    Key = id
                }, received_at_utc, refresh: false);
            }
            if (users.Length > 0)
                Refresh();
        });
    }

    private void OnLeft(Avatar avatar)
    {
        GameState? game = Game;
        if (game is null || avatar is not User user)
            return;
        Id id = user.Id;
        string name = user.Name;
        DateTimeOffset received_at_utc = DateTimeOffset.Now;
        OnUi(() =>
        {
            if (!ReferenceEquals(Game, game))
                return;
            AddActivity(new GameRow
            {
                Name = $"{name} left",
                Trailing = received_at_utc.LocalDateTime.ToString("HH:mm"),
                Key = id
            }, received_at_utc);
        });
    }

    private void AddActivity(
        GameRow row,
        DateTimeOffset received_at_utc,
        bool refresh = true,
        long room_generation = 0,
        long room_id = 0)
    {
        activity.Add(new ActivityRow(
            ++activity_sequence,
            received_at_utc,
            row,
            room_generation,
            room_id));
        if (activity.Count > Limit)
            activity.RemoveRange(0, activity.Count - Limit);
        if (refresh)
            Refresh();
    }

    public override void Refresh()
    {
        List<TimedRow> all = BuildRows();
        string term = Filter.Text.Trim();
        List<GameRow> rows = all
            .Select(value => value.Row)
            .Where(row => term.Length == 0 ||
                row.Detail.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                row.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        Rows.ItemsSource = rows;
        EmptyNotice.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (all.Count == 0)
            EmptyText.Text = "Nothing said yet. The log starts when QX does.";

        Subheading.Text = all.Count == 0
            ? ""
            : $"{all.Count:N0} {(all.Count == 1 ? "line" : "lines")} kept, newest last";
        Status.Text = rows.Count == all.Count
            ? $"{rows.Count:N0} shown"
            : $"{rows.Count:N0} of {all.Count:N0} shown";

        if (rows.Count > 0 && term.Length == 0)
        {
            PostOnUi(
                () => Rows.ScrollIntoView(rows[^1]),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }

    private List<TimedRow> BuildRows()
    {
        var rows = activity
            .Select(value => new TimedRow(value.ReceivedAtUtc, value.Sequence, value.Row))
            .ToList();
        rows.AddRange(chat);
        return rows
            .OrderBy(value => value.ReceivedAtUtc)
            .ThenBy(value => value.Sequence)
            .TakeLast(Limit)
            .ToList();
    }

    private bool SeedChat(IApplicationRuntime application, out bool more)
    {
        bool changed = false;
        more = false;
        for (int page_index = 0; page_index < PagesPerDrain; page_index++)
        {
            RoomChatHistoryPage page = application.Invoke<RoomChatHistoryRequest, RoomChatHistoryPage>(
                ApplicationMemberIds.RoomChatHistory,
                new RoomChatHistoryRequest(chat_cursor, PageSize));
            foreach (RoomChatEntry entry in page.Entries)
                changed |= AddChat(entry);
            long previous = chat_cursor;
            chat_cursor = page.Next;
            more = page.HasMore && chat_cursor > previous;
            if (!more)
                break;
        }
        return changed;
    }

    private bool AddChat(RoomChatEntry entry)
    {
        if (entry.Sequence <= hidden_chat_sequence || !chat_sequences.Add(entry.Sequence))
            return false;
        chat.Add(new TimedRow(entry.ReceivedAtUtc, entry.Sequence, ChatRow(entry)));
        while (chat.Count > Limit)
        {
            chat_sequences.Remove(chat[0].Sequence);
            chat.RemoveAt(0);
        }
        return true;
    }

    private void ScheduleChatDrain(ChatBinding binding, bool signal = true)
    {
        if (binding.IsDisposed)
            return;
        if (signal)
            Interlocked.Increment(ref binding.Signals);
        if (Interlocked.CompareExchange(ref binding.Scheduled, 1, 0) != 0)
            return;
        PostOnUi(
            () => DrainChat(binding),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void DrainChat(ChatBinding binding)
    {
        long observed = Volatile.Read(ref binding.Signals);
        bool more = false;
        try
        {
            if (!binding.IsDisposed &&
                ReferenceEquals(chat_binding, binding) &&
                SeedChat(binding.Application, out more) &&
                Visibility == Visibility.Visible)
            {
                Refresh();
            }
        }
        finally
        {
            Interlocked.Exchange(ref binding.Scheduled, 0);
            if (!binding.IsDisposed &&
                ReferenceEquals(chat_binding, binding) &&
                (more || observed != Volatile.Read(ref binding.Signals)))
            {
                ScheduleChatDrain(binding, signal: false);
            }
        }
    }

    private GameRow ChatRow(RoomChatEntry entry)
    {
        string? head = entry.SpeakerType is AvatarType.User or AvatarType.PublicBot or AvatarType.PrivateBot &&
            !string.IsNullOrWhiteSpace(entry.SpeakerFigure)
            ? HabboImages.HeadUrl(entry.SpeakerFigure)
            : null;
        return new GameRow(head)
        {
            Name = entry.SpeakerName is { Length: > 0 } name ? name : $"#{entry.SpeakerIndex}",
            Detail = entry.Chat.Message,
            Tag = entry.Chat.Type switch
            {
                ChatType.Shout => "shout",
                ChatType.Whisper => "whisper",
                _ => ""
            },
            Trailing = entry.ReceivedAtUtc.LocalDateTime.ToString("HH:mm"),
            Key = entry.SpeakerId ?? 0
        };
    }

    private void FilterChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void ClearLog(object sender, RoutedEventArgs e)
    {
        if (Application is not null)
        {
            hidden_chat_sequence = Application.Invoke<RoomChatHistoryRequest, RoomChatHistoryPage>(
                ApplicationMemberIds.RoomChatHistory,
                new RoomChatHistoryRequest(long.MaxValue, 1)).Latest;
            chat_cursor = hidden_chat_sequence;
        }
        chat.Clear();
        chat_sequences.Clear();
        activity.Clear();
        Refresh();
    }

    private sealed record ActivityRow(
        long Sequence,
        DateTimeOffset ReceivedAtUtc,
        GameRow Row,
        long RoomGeneration,
        long RoomId);
    private sealed record TimedRow(DateTimeOffset ReceivedAtUtc, long Sequence, GameRow Row);

    private sealed class ChatBinding(IApplicationRuntime application) : IDisposable
    {
        private int disposed;

        public IApplicationRuntime Application { get; } = application;
        public IDisposable? Subscription { get; set; }
        public long Signals;
        public int Scheduled;
        public bool IsDisposed => Volatile.Read(ref disposed) != 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            Subscription?.Dispose();
            Subscription = null;
        }
    }
}
