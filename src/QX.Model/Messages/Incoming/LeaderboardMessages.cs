using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>One place on a game leaderboard.</summary>
public sealed record LeaderboardEntry : IParserComposer<LeaderboardEntry>
{
    private string name = "";
    private string figure = "";
    private string gender = "";

    /// <param name="UserId">The player.</param>
    /// <param name="Score">Their score.</param>
    /// <param name="Rank">Their position, counted from one.</param>
    /// <param name="Name">Their name.</param>
    /// <param name="Figure">Their look.</param>
    /// <param name="Gender">Their gender.</param>
    public LeaderboardEntry(
        int UserId,
        int Score,
        int Rank,
        string Name,
        string Figure,
        string Gender)
    {
        this.UserId = UserId;
        this.Score = Score;
        this.Rank = Rank;
        this.Name = Name;
        this.Figure = Figure;
        this.Gender = Gender;
    }

    public int UserId { get; init; }

    public int Score { get; init; }

    public int Rank { get; init; }

    public string Name
    {
        get => name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Name));
            name = value;
        }
    }

    public string Figure
    {
        get => figure;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Figure));
            figure = value;
        }
    }

    public string Gender
    {
        get => gender;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Gender));
            gender = value;
        }
    }

    public void Deconstruct(
        out int UserId,
        out int Score,
        out int Rank,
        out string Name,
        out string Figure,
        out string Gender)
    {
        UserId = this.UserId;
        Score = this.Score;
        Rank = this.Rank;
        Name = this.Name;
        Figure = this.Figure;
        Gender = this.Gender;
    }

    public static LeaderboardEntry Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static LeaderboardEntry ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardEntry value = ParseWire(in p, 0, ref strings);
        LeaderboardWire.RequireEmpty(in p, nameof(LeaderboardEntry));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(LeaderboardEntry value, in PacketWriter p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardEntryWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }

    internal static LeaderboardEntry ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref LeaderboardStringBudget strings)
    {
        LeaderboardWire.RequireRemaining(
            in p,
            LeaderboardWire.EntryMinimumBytes,
            trailing_bytes,
            nameof(LeaderboardEntry));
        int user_id = p.ReadInt();
        int score = p.ReadInt();
        int rank = p.ReadInt();
        string entry_name = strings.Read(in p, nameof(Name), trailing_bytes);
        string entry_figure = strings.Read(in p, nameof(Figure), trailing_bytes);
        string entry_gender = strings.Read(in p, nameof(Gender), trailing_bytes);
        return new LeaderboardEntry(user_id, score, rank, entry_name, entry_figure, entry_gender);
    }

    internal static LeaderboardEntryWireSnapshot PrepareWire(
        LeaderboardEntry value,
        ref LeaderboardStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var snapshot = new LeaderboardEntryWireSnapshot(
            value.UserId,
            value.Score,
            value.Rank,
            value.Name,
            value.Figure,
            value.Gender);
        strings.Require(snapshot.Name, nameof(Name), in p);
        strings.Require(snapshot.Figure, nameof(Figure), in p);
        strings.Require(snapshot.Gender, nameof(Gender), in p);
        return snapshot;
    }

    internal static void WriteWire(LeaderboardEntryWireSnapshot value, in PacketWriter p)
    {
        p.WriteInt(value.UserId);
        p.WriteInt(value.Score);
        p.WriteInt(value.Rank);
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }
}

internal readonly record struct LeaderboardEntryWireSnapshot(
    int UserId,
    int Score,
    int Rank,
    string Name,
    string Figure,
    string Gender);

/// <summary>
/// A page of a game leaderboard.
/// </summary>
/// <remarks>
/// The hotel sends a window rather than the whole board, so the entries are a slice and
/// <see cref="TotalListSize"/> is how long the board really is. The ranks in the slice are absolute,
/// which is what makes paging possible: asking for the next page means asking from the last rank
/// held plus one.
/// </remarks>
public sealed record Leaderboard : IParserComposer<Leaderboard>
{
    private IReadOnlyList<LeaderboardEntry> entries =
        Array.AsReadOnly(Array.Empty<LeaderboardEntry>());

    /// <param name="Entries">The rows in this window.</param>
    /// <param name="TotalListSize">How many rows the whole board has.</param>
    /// <param name="GameTypeId">Which game the board belongs to.</param>
    public Leaderboard(
        IReadOnlyList<LeaderboardEntry> Entries,
        int TotalListSize,
        int GameTypeId)
    {
        this.Entries = Entries;
        this.TotalListSize = TotalListSize;
        this.GameTypeId = GameTypeId;
    }

    public IReadOnlyList<LeaderboardEntry> Entries
    {
        get => entries;
        init => entries = LeaderboardWire.FreezeReferences(value, nameof(Entries));
    }

    public int TotalListSize { get; init; }

    public int GameTypeId { get; init; }

    public void Deconstruct(
        out IReadOnlyList<LeaderboardEntry> Entries,
        out int TotalListSize,
        out int GameTypeId)
    {
        Entries = this.Entries;
        TotalListSize = this.TotalListSize;
        GameTypeId = this.GameTypeId;
    }

    /// <summary>The best rank in this window, or zero when it is empty.</summary>
    public int FirstRank => Entries.Count > 0 ? Entries[0].Rank : 0;

    /// <summary>The worst rank in this window, or zero when it is empty.</summary>
    public int LastRank => Entries.Count > 0 ? Entries[^1].Rank : 0;

    /// <summary>Whether there are rows above this window.</summary>
    public bool HasMoreAbove => Entries.Count > 0 && FirstRank > 1;

    /// <summary>Whether there are rows below this window.</summary>
    public bool HasMoreBelow => Entries.Count > 0 && LastRank < TotalListSize;

    public static Leaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static Leaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard value = ParseWire(in p, 0, ref strings);
        LeaderboardWire.RequireEmpty(in p, nameof(Leaderboard));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(Leaderboard value, in PacketWriter p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }

    internal static Leaderboard ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref LeaderboardStringBudget strings)
    {
        int footer_bytes = checked(sizeof(int) * 2 + trailing_bytes);
        int count = LeaderboardWire.ReadCount(
            in p,
            LeaderboardWire.EntryMinimumBytes,
            footer_bytes,
            nameof(Entries));
        var values = new LeaderboardEntry[count];
        for (int index = 0; index < values.Length; index++)
        {
            int sibling_bytes = checked(
                (values.Length - index - 1) * LeaderboardWire.EntryMinimumBytes + footer_bytes);
            values[index] = LeaderboardEntry.ParseWire(in p, sibling_bytes, ref strings);
        }
        LeaderboardWire.RequireRemaining(
            in p,
            sizeof(int) * 2,
            trailing_bytes,
            nameof(Leaderboard));
        return new Leaderboard(values, p.ReadInt(), p.ReadInt());
    }

    internal static LeaderboardWireSnapshot PrepareWire(
        Leaderboard value,
        ref LeaderboardStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        IReadOnlyList<LeaderboardEntry> source = value.Entries;
        int count = LeaderboardWire.RequireCount(source.Count, nameof(Entries));
        var values = new LeaderboardEntryWireSnapshot[count];
        for (int index = 0; index < values.Length; index++)
            values[index] = LeaderboardEntry.PrepareWire(source[index], ref strings, in p);
        return new LeaderboardWireSnapshot(values, value.TotalListSize, value.GameTypeId);
    }

    internal static void WriteWire(LeaderboardWireSnapshot value, in PacketWriter p)
    {
        p.WriteInt(value.Entries.Length);
        foreach (LeaderboardEntryWireSnapshot entry in value.Entries)
            LeaderboardEntry.WriteWire(entry, in p);
        p.WriteInt(value.TotalListSize);
        p.WriteInt(value.GameTypeId);
    }
}

internal readonly record struct LeaderboardWireSnapshot(
    LeaderboardEntryWireSnapshot[] Entries,
    int TotalListSize,
    int GameTypeId);

/// <summary>The all-time board covering everyone.</summary>
public sealed record TotalLeaderboard : IParserComposer<TotalLeaderboard>
{
    private Leaderboard board = null!;

    /// <param name="Board">The window.</param>
    public TotalLeaderboard(Leaderboard Board)
    {
        this.Board = Board;
    }

    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    public void Deconstruct(out Leaderboard Board)
    {
        Board = this.Board;
    }

    public static TotalLeaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TotalLeaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        var value = new TotalLeaderboard(Leaderboard.ParseWire(in p, 0, ref strings));
        LeaderboardWire.RequireEmpty(in p, nameof(TotalLeaderboard));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TotalLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        Leaderboard.WriteWire(board, in p);
    }
}

/// <summary>The all-time board covering the local user's friends.</summary>
public sealed record FriendsLeaderboard : IParserComposer<FriendsLeaderboard>
{
    private Leaderboard board = null!;

    /// <param name="Board">The window.</param>
    public FriendsLeaderboard(Leaderboard Board)
    {
        this.Board = Board;
    }

    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    public void Deconstruct(out Leaderboard Board)
    {
        Board = this.Board;
    }

    public static FriendsLeaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FriendsLeaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        var value = new FriendsLeaderboard(Leaderboard.ParseWire(in p, 0, ref strings));
        LeaderboardWire.RequireEmpty(in p, nameof(FriendsLeaderboard));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendsLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        Leaderboard.WriteWire(board, in p);
    }
}

/// <summary>The all-time board covering groups.</summary>
public sealed record TotalGroupLeaderboard : IParserComposer<TotalGroupLeaderboard>
{
    private Leaderboard board = null!;

    /// <param name="Board">The window.</param>
    /// <param name="FavouriteGroupId">The group the local user has marked as their favourite.</param>
    public TotalGroupLeaderboard(Leaderboard Board, int FavouriteGroupId)
    {
        this.Board = Board;
        this.FavouriteGroupId = FavouriteGroupId;
    }

    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    public int FavouriteGroupId { get; init; }

    public void Deconstruct(out Leaderboard Board, out int FavouriteGroupId)
    {
        Board = this.Board;
        FavouriteGroupId = this.FavouriteGroupId;
    }

    public static TotalGroupLeaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TotalGroupLeaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard board = Leaderboard.ParseWire(in p, sizeof(int), ref strings);
        LeaderboardWire.RequireRemaining(in p, sizeof(int), 0, nameof(TotalGroupLeaderboard));
        var value = new TotalGroupLeaderboard(board, p.ReadInt());
        LeaderboardWire.RequireEmpty(in p, nameof(TotalGroupLeaderboard));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TotalGroupLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        Leaderboard.WriteWire(board, in p);
        p.WriteInt(value.FavouriteGroupId);
    }
}

/// <summary>
/// The header a weekly board carries in front of its window.
/// </summary>
/// <remarks>
/// Weekly boards are addressed by an offset back from the current week rather than by date, and
/// <see cref="MaxOffset"/> is how far back the hotel keeps them.
/// </remarks>
/// <param name="Year">The year the window covers.</param>
/// <param name="Week">The week number the window covers.</param>
/// <param name="MaxOffset">The oldest week that can be asked for, counted back from this one.</param>
/// <param name="CurrentOffset">How many weeks back this window is.</param>
/// <param name="MinutesUntilReset">How long until the running week ends.</param>
public sealed record WeeklyLeaderboardPeriod(
    int Year,
    int Week,
    int MaxOffset,
    int CurrentOffset,
    int MinutesUntilReset) : IParserComposer<WeeklyLeaderboardPeriod>
{
    /// <summary>Whether this window is the week currently running.</summary>
    public bool IsCurrentWeek => CurrentOffset == 0;

    public static WeeklyLeaderboardPeriod Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WeeklyLeaderboardPeriod ParseFlash(in PacketReader p)
    {
        WeeklyLeaderboardPeriod value = ParseWire(in p, 0);
        LeaderboardWire.RequireEmpty(in p, nameof(WeeklyLeaderboardPeriod));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WeeklyLeaderboardPeriod value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteWire(value, in p);
    }

    internal static WeeklyLeaderboardPeriod ParseWire(in PacketReader p, int trailing_bytes)
    {
        LeaderboardWire.RequireRemaining(
            in p,
            LeaderboardWire.PeriodBytes,
            trailing_bytes,
            nameof(WeeklyLeaderboardPeriod));
        return new WeeklyLeaderboardPeriod(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());
    }

    internal static void WriteWire(WeeklyLeaderboardPeriod value, in PacketWriter p)
    {
        p.WriteInt(value.Year);
        p.WriteInt(value.Week);
        p.WriteInt(value.MaxOffset);
        p.WriteInt(value.CurrentOffset);
        p.WriteInt(value.MinutesUntilReset);
    }
}

/// <summary>The weekly board covering everyone.</summary>
public sealed record WeeklyLeaderboard : IParserComposer<WeeklyLeaderboard>
{
    private WeeklyLeaderboardPeriod period = null!;
    private Leaderboard board = null!;

    /// <param name="Period">Which week the window covers.</param>
    /// <param name="Board">The window.</param>
    public WeeklyLeaderboard(WeeklyLeaderboardPeriod Period, Leaderboard Board)
    {
        this.Period = Period;
        this.Board = Board;
    }

    public WeeklyLeaderboardPeriod Period
    {
        get => period;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Period));
            period = value;
        }
    }

    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    public void Deconstruct(out WeeklyLeaderboardPeriod Period, out Leaderboard Board)
    {
        Period = this.Period;
        Board = this.Board;
    }

    public static WeeklyLeaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WeeklyLeaderboard ParseFlash(in PacketReader p)
    {
        WeeklyLeaderboardPeriod period = WeeklyLeaderboardPeriod.ParseWire(
            in p,
            LeaderboardWire.BoardMinimumBytes);
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard board = Leaderboard.ParseWire(in p, 0, ref strings);
        LeaderboardWire.RequireEmpty(in p, nameof(WeeklyLeaderboard));
        return new WeeklyLeaderboard(period, board);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WeeklyLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WeeklyLeaderboardPeriod period = value.Period;
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        WeeklyLeaderboardPeriod.WriteWire(period, in p);
        Leaderboard.WriteWire(board, in p);
    }
}

/// <summary>The weekly board covering the local user's friends.</summary>
public sealed record WeeklyFriendsLeaderboard : IParserComposer<WeeklyFriendsLeaderboard>
{
    private WeeklyLeaderboardPeriod period = null!;
    private Leaderboard board = null!;

    /// <param name="Period">Which week the window covers.</param>
    /// <param name="Board">The window.</param>
    public WeeklyFriendsLeaderboard(WeeklyLeaderboardPeriod Period, Leaderboard Board)
    {
        this.Period = Period;
        this.Board = Board;
    }

    public WeeklyLeaderboardPeriod Period
    {
        get => period;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Period));
            period = value;
        }
    }

    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    public void Deconstruct(out WeeklyLeaderboardPeriod Period, out Leaderboard Board)
    {
        Period = this.Period;
        Board = this.Board;
    }

    public static WeeklyFriendsLeaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WeeklyFriendsLeaderboard ParseFlash(in PacketReader p)
    {
        WeeklyLeaderboardPeriod period = WeeklyLeaderboardPeriod.ParseWire(
            in p,
            LeaderboardWire.BoardMinimumBytes);
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard board = Leaderboard.ParseWire(in p, 0, ref strings);
        LeaderboardWire.RequireEmpty(in p, nameof(WeeklyFriendsLeaderboard));
        return new WeeklyFriendsLeaderboard(period, board);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WeeklyFriendsLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WeeklyLeaderboardPeriod period = value.Period;
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        WeeklyLeaderboardPeriod.WriteWire(period, in p);
        Leaderboard.WriteWire(board, in p);
    }
}

/// <summary>
/// The weekly board covering groups.
/// </summary>
/// <remarks>
/// The favourite group trails the window rather than sitting in the header, which is the one place
/// the group variants differ from the plain ones.
/// </remarks>
public sealed record WeeklyGroupLeaderboard : IParserComposer<WeeklyGroupLeaderboard>
{
    private WeeklyLeaderboardPeriod period = null!;
    private Leaderboard board = null!;

    /// <param name="Period">Which week the window covers.</param>
    /// <param name="Board">The window.</param>
    /// <param name="FavouriteGroupId">The group the local user has marked as their favourite.</param>
    public WeeklyGroupLeaderboard(
        WeeklyLeaderboardPeriod Period,
        Leaderboard Board,
        int FavouriteGroupId)
    {
        this.Period = Period;
        this.Board = Board;
        this.FavouriteGroupId = FavouriteGroupId;
    }

    public WeeklyLeaderboardPeriod Period
    {
        get => period;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Period));
            period = value;
        }
    }

    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    public int FavouriteGroupId { get; init; }

    public void Deconstruct(
        out WeeklyLeaderboardPeriod Period,
        out Leaderboard Board,
        out int FavouriteGroupId)
    {
        Period = this.Period;
        Board = this.Board;
        FavouriteGroupId = this.FavouriteGroupId;
    }

    public static WeeklyGroupLeaderboard Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WeeklyGroupLeaderboard ParseFlash(in PacketReader p)
    {
        WeeklyLeaderboardPeriod period = WeeklyLeaderboardPeriod.ParseWire(
            in p,
            checked(LeaderboardWire.BoardMinimumBytes + sizeof(int)));
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard board = Leaderboard.ParseWire(in p, sizeof(int), ref strings);
        LeaderboardWire.RequireRemaining(in p, sizeof(int), 0, nameof(WeeklyGroupLeaderboard));
        var value = new WeeklyGroupLeaderboard(period, board, p.ReadInt());
        LeaderboardWire.RequireEmpty(in p, nameof(WeeklyGroupLeaderboard));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WeeklyGroupLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WeeklyLeaderboardPeriod period = value.Period;
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        WeeklyLeaderboardPeriod.WriteWire(period, in p);
        Leaderboard.WriteWire(board, in p);
        p.WriteInt(value.FavouriteGroupId);
    }
}
