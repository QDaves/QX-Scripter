using Qx.Messages;

namespace Qx.Model.Wired;

public sealed record WiredLeaderboardLocation(int TileX, int TileY, int OffsetX, int Altitude);

public sealed record WiredLeaderboardColumn(int Type, string Label, int Width);

public sealed record WiredLeaderboardChoice(string VariableId, string DisplayName);

public sealed record WiredLeaderboardDescriptor(int OverlayInstanceId, int QueryRevision, string Title,
    string Description, WiredLeaderboardLocation Location, WiredSortOrderOption SortMode,
    IReadOnlyList<WiredLeaderboardColumn> Columns, IReadOnlyList<WiredLeaderboardChoice> Choices,
    string DefaultVariableId, int PageSizeOption) : IParserComposer<WiredLeaderboardDescriptor>
{
    public int EntriesPerPage => PageSizeOption switch
    {
        0 => 10,
        1 => 15,
        2 => 20,
        _ => throw new InvalidOperationException($"Unknown leaderboard page size {PageSizeOption}.")
    };

    public static WiredLeaderboardDescriptor Parse(in PacketReader p)
    {
        int overlay = p.ReadInt(), revision = p.ReadInt();
        string title = p.ReadString(), description = p.ReadString();
        var location = new WiredLeaderboardLocation(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt());
        var sort = (WiredSortOrderOption)p.ReadInt();
        int column_count = p.ReadInt();
        WiredLeaderboardWire.Count(column_count, 1, 4, p.Available, 10);
        var columns = new WiredLeaderboardColumn[column_count];
        for (int index = 0; index < columns.Length; index++)
            columns[index] = new(p.ReadInt(), p.ReadString(), p.ReadInt());
        int choice_count = p.ReadInt();
        WiredLeaderboardWire.Count(choice_count, 0, 10, p.Available, 4);
        var choices = new WiredLeaderboardChoice[choice_count];
        for (int index = 0; index < choices.Length; index++) choices[index] = new(p.ReadString(), p.ReadString());
        return new(overlay, revision, title, description, location, sort, Array.AsReadOnly(columns),
            Array.AsReadOnly(choices), p.ReadString(), p.ReadInt());
    }

    public void Compose(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(Location);
        ArgumentNullException.ThrowIfNull(Columns);
        ArgumentNullException.ThrowIfNull(Choices);
        WiredLeaderboardWire.Count(Columns.Count, 1, 4);
        WiredLeaderboardWire.Count(Choices.Count, 0, 10);
        WiredWire.RequireString(Title, nameof(Title), in p);
        WiredWire.RequireString(Description, nameof(Description), in p);
        WiredWire.RequireString(DefaultVariableId, nameof(DefaultVariableId), in p);
        foreach (var column in Columns)
        {
            ArgumentNullException.ThrowIfNull(column);
            WiredWire.RequireString(column.Label, nameof(column.Label), in p);
        }
        foreach (var choice in Choices)
        {
            ArgumentNullException.ThrowIfNull(choice);
            WiredWire.RequireString(choice.VariableId, nameof(choice.VariableId), in p);
            WiredWire.RequireString(choice.DisplayName, nameof(choice.DisplayName), in p);
        }
        p.WriteInt(OverlayInstanceId);
        p.WriteInt(QueryRevision);
        p.WriteString(Title);
        p.WriteString(Description);
        p.WriteInt(Location.TileX);
        p.WriteInt(Location.TileY);
        p.WriteInt(Location.OffsetX);
        p.WriteInt(Location.Altitude);
        p.WriteInt((int)SortMode);
        p.WriteInt(Columns.Count);
        foreach (var column in Columns)
        {
            p.WriteInt(column.Type);
            p.WriteString(column.Label);
            p.WriteInt(column.Width);
        }
        p.WriteInt(Choices.Count);
        foreach (var choice in Choices)
        {
            p.WriteString(choice.VariableId);
            p.WriteString(choice.DisplayName);
        }
        p.WriteString(DefaultVariableId);
        p.WriteInt(PageSizeOption);
    }
}

public sealed record VariableLeaderboardSnapshot(IReadOnlyList<WiredLeaderboardDescriptor> Descriptors)
    : IParserComposer<VariableLeaderboardSnapshot>
{
    public static VariableLeaderboardSnapshot Parse(in PacketReader p)
    {
        int count = p.ReadInt();
        WiredLeaderboardWire.Count(count, 0, 4, p.Available, 46);
        var descriptors = new WiredLeaderboardDescriptor[count];
        for (int index = 0; index < count; index++) descriptors[index] = p.Parse<WiredLeaderboardDescriptor>();
        return new(Array.AsReadOnly(descriptors));
    }

    public void Compose(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(Descriptors);
        WiredLeaderboardWire.Count(Descriptors.Count, 0, 4);
        if (Descriptors.Any(value => value is null)) throw new ArgumentException("A leaderboard descriptor is null.");
        p.WriteInt(Descriptors.Count);
        foreach (var descriptor in Descriptors) p.Compose(descriptor);
    }
}

public sealed record WiredLeaderboardRow(int Rank, int UserId, string Username, string Figure, string RawValue,
    long CreationTime, long LastUpdateTime, string? Level, string? TextConnection) : IParserComposer<WiredLeaderboardRow>
{
    public static WiredLeaderboardRow Parse(in PacketReader p)
    {
        int rank = p.ReadInt(), user = p.ReadInt();
        string username = p.ReadString(), figure = p.ReadString(), value = p.ReadString();
        long created = p.ReadLong(), updated = p.ReadLong();
        string? level = p.ReadBool() ? p.ReadString() : null;
        string? connection = p.ReadBool() ? p.ReadString() : null;
        return new(rank, user, username, figure, value, created, updated, level, connection);
    }

    public void Compose(in PacketWriter p)
    {
        WiredWire.RequireString(Username, nameof(Username), in p);
        WiredWire.RequireString(Figure, nameof(Figure), in p);
        WiredWire.RequireString(RawValue, nameof(RawValue), in p);
        if (Level is not null) WiredWire.RequireString(Level, nameof(Level), in p);
        if (TextConnection is not null) WiredWire.RequireString(TextConnection, nameof(TextConnection), in p);
        p.WriteInt(Rank);
        p.WriteInt(UserId);
        p.WriteString(Username);
        p.WriteString(Figure);
        p.WriteString(RawValue);
        p.WriteLong(CreationTime);
        p.WriteLong(LastUpdateTime);
        p.WriteBool(Level is not null);
        if (Level is not null) p.WriteString(Level);
        p.WriteBool(TextConnection is not null);
        if (TextConnection is not null) p.WriteString(TextConnection);
    }
}

public sealed record VariableLeaderboardChunkUpdate(int OverlayInstanceId, int QueryRevision,
    string SelectedVariableId, int ChunkIndex, int TotalCount, IReadOnlyList<WiredLeaderboardRow> Rows)
    : IParserComposer<VariableLeaderboardChunkUpdate>
{
    public static VariableLeaderboardChunkUpdate Parse(in PacketReader p)
    {
        int overlay = p.ReadInt(), revision = p.ReadInt();
        string variable = p.ReadString();
        int chunk = p.ReadInt(), total = p.ReadInt(), count = p.ReadInt();
        if (total < 0) throw new InvalidDataException("A leaderboard total count cannot be negative.");
        WiredLeaderboardWire.Count(count, 0, 50, p.Available, 32);
        var rows = new WiredLeaderboardRow[count];
        for (int index = 0; index < count; index++) rows[index] = p.Parse<WiredLeaderboardRow>();
        return new(overlay, revision, variable, chunk, total, Array.AsReadOnly(rows));
    }

    public void Compose(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(Rows);
        WiredLeaderboardWire.Count(Rows.Count, 0, 50);
        if (TotalCount < 0) throw new ArgumentOutOfRangeException(nameof(TotalCount));
        if (Rows.Any(value => value is null)) throw new ArgumentException("A leaderboard row is null.");
        WiredWire.RequireString(SelectedVariableId, nameof(SelectedVariableId), in p);
        p.WriteInt(OverlayInstanceId);
        p.WriteInt(QueryRevision);
        p.WriteString(SelectedVariableId);
        p.WriteInt(ChunkIndex);
        p.WriteInt(TotalCount);
        p.WriteInt(Rows.Count);
        foreach (var row in Rows) p.Compose(row);
    }
}

public sealed record VariableLeaderboardReplaceSubscription(int OverlayInstanceId, int QueryRevision,
    string SelectedVariableId, IReadOnlyList<int> ChunkIndices) : IParserComposer<VariableLeaderboardReplaceSubscription>
{
    public static VariableLeaderboardReplaceSubscription RoomOpen { get; } = new(0, 0, "0", Array.Empty<int>());

    public static VariableLeaderboardReplaceSubscription Parse(in PacketReader p)
    {
        int overlay = p.ReadInt(), revision = p.ReadInt();
        string variable = p.ReadString();
        int count = p.ReadInt();
        WiredLeaderboardWire.Count(count, 0, 2, p.Available, 4);
        var chunks = new int[count];
        for (int index = 0; index < chunks.Length; index++) chunks[index] = p.ReadInt();
        var value = new VariableLeaderboardReplaceSubscription(overlay, revision, variable, Array.AsReadOnly(chunks));
        value.Validate();
        return value;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ChunkIndices);
        WiredLeaderboardWire.Count(ChunkIndices.Count, 0, 2);
        if (ChunkIndices.Count == 0 && (OverlayInstanceId != 0 || QueryRevision != 0 || SelectedVariableId != "0"))
            throw new ArgumentException("Only the room-open request may omit chunk indices.");
        if (ChunkIndices.Any(index => index < 0) || ChunkIndices.Distinct().Count() != ChunkIndices.Count)
            throw new ArgumentException("Leaderboard chunk indices must be distinct and nonnegative.");
    }

    public void Compose(in PacketWriter p)
    {
        Validate();
        WiredWire.RequireString(SelectedVariableId, nameof(SelectedVariableId), in p);
        p.WriteInt(OverlayInstanceId);
        p.WriteInt(QueryRevision);
        p.WriteString(SelectedVariableId);
        p.WriteInt(ChunkIndices.Count);
        foreach (int index in ChunkIndices) p.WriteInt(index);
    }
}

internal static class WiredLeaderboardWire
{
    internal static void Count(int count, int minimum, int maximum, int available = int.MaxValue, int bytes = 1)
    {
        if (count < minimum || count > maximum) throw new InvalidDataException($"Leaderboard count {count} is outside {minimum}..{maximum}.");
        WiredWire.RequireBoundedCount(count, available, bytes, nameof(count));
    }
}
