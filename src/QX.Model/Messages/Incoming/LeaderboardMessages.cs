using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents one entry on a game leaderboard.</summary>
public sealed record LeaderboardEntry : IParserComposer<LeaderboardEntry>
{
    private string name = "";
    private string figure = "";
    private string gender = "";

    /// <summary>Initializes a new instance of the <see cref="LeaderboardEntry"/> class.</summary>
    /// <param name="userId">The player's user ID.</param>
    /// <param name="score">The player's score.</param>
    /// <param name="rank">The player's rank, counted from one.</param>
    /// <param name="name">The player's name.</param>
    /// <param name="figure">The player's figure string.</param>
    /// <param name="gender">The player's gender.</param>
    public LeaderboardEntry(
        int userId,
        int score,
        int rank,
        string name,
        string figure,
        string gender)
    {
        UserId = userId;
        Score = score;
        Rank = rank;
        Name = name;
        Figure = figure;
        Gender = gender;
    }

    /// <summary>Gets the player's user ID.</summary>
    public int UserId { get; init; }

    /// <summary>Gets the player's score.</summary>
    public int Score { get; init; }

    /// <summary>Gets the player's rank, counted from one.</summary>
    public int Rank { get; init; }

    /// <summary>Gets the player's name.</summary>
    public string Name
    {
        get => name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Name));
            name = value;
        }
    }

    /// <summary>Gets the player's figure string.</summary>
    public string Figure
    {
        get => figure;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Figure));
            figure = value;
        }
    }

    /// <summary>Gets the player's gender.</summary>
    public string Gender
    {
        get => gender;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Gender));
            gender = value;
        }
    }

    /// <summary>Deconstructs the entry into its values.</summary>
    /// <param name="userId">The player's user ID.</param>
    /// <param name="score">The player's score.</param>
    /// <param name="rank">The player's rank, counted from one.</param>
    /// <param name="name">The player's name.</param>
    /// <param name="figure">The player's figure string.</param>
    /// <param name="gender">The player's gender.</param>
    public void Deconstruct(
        out int userId,
        out int score,
        out int rank,
        out string name,
        out string figure,
        out string gender)
    {
        userId = UserId;
        score = Score;
        rank = Rank;
        name = Name;
        figure = Figure;
        gender = Gender;
    }

    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static LeaderboardEntry Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static LeaderboardEntry ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardEntry value = ParseWire(in p, 0, ref strings);
        LeaderboardWire.RequireEmpty(in p, nameof(LeaderboardEntry));
        return value;
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents one page of a game leaderboard.</summary>
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

    /// <summary>Initializes a new instance of the <see cref="Leaderboard"/> class.</summary>
    /// <param name="entries">The entries in this window.</param>
    /// <param name="totalListSize">The number of entries on the whole board.</param>
    /// <param name="gameTypeId">The ID of the game the board belongs to.</param>
    public Leaderboard(
        IReadOnlyList<LeaderboardEntry> entries,
        int totalListSize,
        int gameTypeId)
    {
        Entries = entries;
        TotalListSize = totalListSize;
        GameTypeId = gameTypeId;
    }

    /// <summary>Gets the entries in this window.</summary>
    public IReadOnlyList<LeaderboardEntry> Entries
    {
        get => entries;
        init => entries = LeaderboardWire.FreezeReferences(value, nameof(Entries));
    }

    /// <summary>Gets the number of entries on the whole board.</summary>
    public int TotalListSize { get; init; }

    /// <summary>Gets the ID of the game the board belongs to.</summary>
    public int GameTypeId { get; init; }

    /// <summary>Deconstructs the leaderboard into its values.</summary>
    /// <param name="entries">The entries in this window.</param>
    /// <param name="totalListSize">The number of entries on the whole board.</param>
    /// <param name="gameTypeId">The ID of the game the board belongs to.</param>
    public void Deconstruct(
        out IReadOnlyList<LeaderboardEntry> entries,
        out int totalListSize,
        out int gameTypeId)
    {
        entries = Entries;
        totalListSize = TotalListSize;
        gameTypeId = GameTypeId;
    }

    /// <summary>Gets the best rank in this window, or 0 when it is empty.</summary>
    public int FirstRank => Entries.Count > 0 ? Entries[0].Rank : 0;

    /// <summary>Gets the worst rank in this window, or 0 when it is empty.</summary>
    public int LastRank => Entries.Count > 0 ? Entries[^1].Rank : 0;

    /// <summary>Gets whether there are entries above this window.</summary>
    public bool HasMoreAbove => Entries.Count > 0 && FirstRank > 1;

    /// <summary>Gets whether there are entries below this window.</summary>
    public bool HasMoreBelow => Entries.Count > 0 && LastRank < TotalListSize;

    /// <summary>Parses the leaderboard from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static Leaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Leaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard value = ParseWire(in p, 0, ref strings);
        LeaderboardWire.RequireEmpty(in p, nameof(Leaderboard));
        return value;
    }

    /// <summary>Composes the leaderboard into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>Game2TotalLeaderboard</c> message, received with a page of the all-time leaderboard of all players.</summary>
public sealed record TotalLeaderboard : IParserComposer<TotalLeaderboard>
{
    private Leaderboard board = null!;

    /// <summary>Initializes a new instance of the <see cref="TotalLeaderboard"/> class.</summary>
    /// <param name="board">The leaderboard page.</param>
    public TotalLeaderboard(Leaderboard board)
    {
        Board = board;
    }

    /// <summary>Gets the leaderboard page.</summary>
    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="board">The leaderboard page.</param>
    public void Deconstruct(out Leaderboard board)
    {
        board = Board;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TotalLeaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TotalLeaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        var value = new TotalLeaderboard(Leaderboard.ParseWire(in p, 0, ref strings));
        LeaderboardWire.RequireEmpty(in p, nameof(TotalLeaderboard));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TotalLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        Leaderboard.WriteWire(board, in p);
    }
}

/// <summary>Represents the <c>Game2FriendsLeaderboard</c> message, received with a page of the all-time leaderboard of the user's friends.</summary>
public sealed record FriendsLeaderboard : IParserComposer<FriendsLeaderboard>
{
    private Leaderboard board = null!;

    /// <summary>Initializes a new instance of the <see cref="FriendsLeaderboard"/> class.</summary>
    /// <param name="board">The leaderboard page.</param>
    public FriendsLeaderboard(Leaderboard board)
    {
        Board = board;
    }

    /// <summary>Gets the leaderboard page.</summary>
    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="board">The leaderboard page.</param>
    public void Deconstruct(out Leaderboard board)
    {
        board = Board;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FriendsLeaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendsLeaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        var value = new FriendsLeaderboard(Leaderboard.ParseWire(in p, 0, ref strings));
        LeaderboardWire.RequireEmpty(in p, nameof(FriendsLeaderboard));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendsLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        Leaderboard.WriteWire(board, in p);
    }
}

/// <summary>Represents the <c>Game2TotalGroupLeaderboard</c> message, received with a page of the all-time group leaderboard.</summary>
/// <remarks>The favorite group ID follows the leaderboard page on the wire.</remarks>
public sealed record TotalGroupLeaderboard : IParserComposer<TotalGroupLeaderboard>
{
    private Leaderboard board = null!;

    /// <summary>Initializes a new instance of the <see cref="TotalGroupLeaderboard"/> class.</summary>
    /// <param name="board">The leaderboard page.</param>
    /// <param name="favouriteGroupId">The ID of the group the user has marked as their favorite.</param>
    public TotalGroupLeaderboard(Leaderboard board, int favouriteGroupId)
    {
        Board = board;
        FavouriteGroupId = favouriteGroupId;
    }

    /// <summary>Gets the leaderboard page.</summary>
    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    /// <summary>Gets the ID of the group the user has marked as their favorite.</summary>
    public int FavouriteGroupId { get; init; }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="board">The leaderboard page.</param>
    /// <param name="favouriteGroupId">The ID of the group the user has marked as their favorite.</param>
    public void Deconstruct(out Leaderboard board, out int favouriteGroupId)
    {
        board = Board;
        favouriteGroupId = FavouriteGroupId;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TotalGroupLeaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TotalGroupLeaderboard ParseFlash(in PacketReader p)
    {
        var strings = LeaderboardWire.NewStringBudget();
        Leaderboard board = Leaderboard.ParseWire(in p, sizeof(int), ref strings);
        LeaderboardWire.RequireRemaining(in p, sizeof(int), 0, nameof(TotalGroupLeaderboard));
        var value = new TotalGroupLeaderboard(board, p.ReadInt());
        LeaderboardWire.RequireEmpty(in p, nameof(TotalGroupLeaderboard));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TotalGroupLeaderboard value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = LeaderboardWire.NewStringBudget();
        LeaderboardWireSnapshot board = Leaderboard.PrepareWire(value.Board, ref strings, in p);
        Leaderboard.WriteWire(board, in p);
        p.WriteInt(value.FavouriteGroupId);
    }
}

/// <summary>Represents the week header a weekly leaderboard sends in front of its page.</summary>
/// <remarks>
/// Weekly boards are addressed by an offset back from the current week rather than by date, and
/// <see cref="MaxOffset"/> is how far back the hotel keeps them.
/// </remarks>
/// <param name="Year">The year the page covers.</param>
/// <param name="Week">The week number the page covers.</param>
/// <param name="MaxOffset">The oldest week that can be requested, counted back from the current week.</param>
/// <param name="CurrentOffset">The number of weeks this page is back from the current week, 0 for the current week.</param>
/// <param name="MinutesUntilReset">The time in minutes until the current week ends.</param>
public sealed record WeeklyLeaderboardPeriod(
    int Year,
    int Week,
    int MaxOffset,
    int CurrentOffset,
    int MinutesUntilReset) : IParserComposer<WeeklyLeaderboardPeriod>
{
    /// <summary>Gets whether this page covers the current week.</summary>
    public bool IsCurrentWeek => CurrentOffset == 0;

    /// <summary>Parses the period from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WeeklyLeaderboardPeriod Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WeeklyLeaderboardPeriod ParseFlash(in PacketReader p)
    {
        WeeklyLeaderboardPeriod value = ParseWire(in p, 0);
        LeaderboardWire.RequireEmpty(in p, nameof(WeeklyLeaderboardPeriod));
        return value;
    }

    /// <summary>Composes the period into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>Game2WeeklyLeaderboard</c> message, received with a page of the weekly leaderboard of all players.</summary>
public sealed record WeeklyLeaderboard : IParserComposer<WeeklyLeaderboard>
{
    private WeeklyLeaderboardPeriod period = null!;
    private Leaderboard board = null!;

    /// <summary>Initializes a new instance of the <see cref="WeeklyLeaderboard"/> class.</summary>
    /// <param name="period">The week the page covers.</param>
    /// <param name="board">The leaderboard page.</param>
    public WeeklyLeaderboard(WeeklyLeaderboardPeriod period, Leaderboard board)
    {
        Period = period;
        Board = board;
    }

    /// <summary>Gets the week the page covers.</summary>
    public WeeklyLeaderboardPeriod Period
    {
        get => period;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Period));
            period = value;
        }
    }

    /// <summary>Gets the leaderboard page.</summary>
    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="period">The week the page covers.</param>
    /// <param name="board">The leaderboard page.</param>
    public void Deconstruct(out WeeklyLeaderboardPeriod period, out Leaderboard board)
    {
        period = Period;
        board = Board;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WeeklyLeaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>Game2WeeklyFriendsLeaderboard</c> message, received with a page of the weekly leaderboard of the user's friends.</summary>
public sealed record WeeklyFriendsLeaderboard : IParserComposer<WeeklyFriendsLeaderboard>
{
    private WeeklyLeaderboardPeriod period = null!;
    private Leaderboard board = null!;

    /// <summary>Initializes a new instance of the <see cref="WeeklyFriendsLeaderboard"/> class.</summary>
    /// <param name="period">The week the page covers.</param>
    /// <param name="board">The leaderboard page.</param>
    public WeeklyFriendsLeaderboard(WeeklyLeaderboardPeriod period, Leaderboard board)
    {
        Period = period;
        Board = board;
    }

    /// <summary>Gets the week the page covers.</summary>
    public WeeklyLeaderboardPeriod Period
    {
        get => period;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Period));
            period = value;
        }
    }

    /// <summary>Gets the leaderboard page.</summary>
    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="period">The week the page covers.</param>
    /// <param name="board">The leaderboard page.</param>
    public void Deconstruct(out WeeklyLeaderboardPeriod period, out Leaderboard board)
    {
        period = Period;
        board = Board;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WeeklyFriendsLeaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>Game2WeeklyGroupLeaderboard</c> message, received with a page of the weekly group leaderboard.</summary>
/// <remarks>
/// The favorite group trails the window rather than sitting in the header, which is the one place
/// the group variants differ from the plain ones.
/// </remarks>
public sealed record WeeklyGroupLeaderboard : IParserComposer<WeeklyGroupLeaderboard>
{
    private WeeklyLeaderboardPeriod period = null!;
    private Leaderboard board = null!;

    /// <summary>Initializes a new instance of the <see cref="WeeklyGroupLeaderboard"/> class.</summary>
    /// <param name="period">The week the page covers.</param>
    /// <param name="board">The leaderboard page.</param>
    /// <param name="favouriteGroupId">The ID of the group the user has marked as their favorite.</param>
    public WeeklyGroupLeaderboard(
        WeeklyLeaderboardPeriod period,
        Leaderboard board,
        int favouriteGroupId)
    {
        Period = period;
        Board = board;
        FavouriteGroupId = favouriteGroupId;
    }

    /// <summary>Gets the week the page covers.</summary>
    public WeeklyLeaderboardPeriod Period
    {
        get => period;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Period));
            period = value;
        }
    }

    /// <summary>Gets the leaderboard page.</summary>
    public Leaderboard Board
    {
        get => board;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Board));
            board = value;
        }
    }

    /// <summary>Gets the ID of the group the user has marked as their favorite.</summary>
    public int FavouriteGroupId { get; init; }

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="period">The week the page covers.</param>
    /// <param name="board">The leaderboard page.</param>
    /// <param name="favouriteGroupId">The ID of the group the user has marked as their favorite.</param>
    public void Deconstruct(
        out WeeklyLeaderboardPeriod period,
        out Leaderboard board,
        out int favouriteGroupId)
    {
        period = Period;
        board = Board;
        favouriteGroupId = FavouriteGroupId;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WeeklyGroupLeaderboard Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
