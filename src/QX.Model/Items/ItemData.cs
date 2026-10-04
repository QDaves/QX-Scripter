using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a furni's payload, which carries its state and any extra data.</summary>
/// <remarks>
/// <see cref="Parse"/> returns the subclass that matches <see cref="Type"/>, such as
/// <see cref="LegacyData"/> or <see cref="MapData"/>.
/// </remarks>
public abstract class ItemData : IParserComposer<ItemData>
{
    /// <summary>Gets the shape of the payload.</summary>
    public ItemDataType Type { get; }
    /// <summary>Gets or sets the modifier bits sent above the type byte.</summary>
    public ItemDataFlags Flags { get; set; }
    /// <summary>Gets whether <see cref="Flags"/> marks the item as part of a numbered limited series.</summary>
    public bool IsLimitedRare => Flags.HasFlag(ItemDataFlags.IsLimitedRare);
    /// <summary>Gets or sets the item's serial number within its limited series, or 0 when it is not limited.</summary>
    public int UniqueSerialNumber { get; set; }
    /// <summary>Gets or sets the number of items in the limited series, or 0 when it is not limited.</summary>
    public int UniqueSeriesSize { get; set; }
    /// <summary>Gets or sets additional limited-edition data.</summary>
    /// <remarks>Packets do not carry it, so it stays empty unless assigned.</remarks>
    public string UniqueLimitedData { get; set; } = "";
    /// <summary>Gets or sets the payload's leading string, empty for shapes that carry none.</summary>
    /// <remarks>
    /// Only the <see cref="LegacyData"/>, <see cref="VoteResultData"/>, <see cref="HighScoreData"/>
    /// and <see cref="CrackableFurniData"/> shapes read and write it.
    /// </remarks>
    public string Value { get; set; } = "";

    /// <summary>Gets <see cref="Value"/> as a state number.</summary>
    /// <remarks>
    /// <c>C</c>, <c>FALSE</c> and <c>OFF</c> give 0, <c>O</c>, <c>TRUE</c> and <c>ON</c> give 1, an
    /// integer gives its own value and anything else gives -1. The text match is case-sensitive.
    /// </remarks>
    public int State => Value switch
    {
        "C" or "FALSE" or "OFF" => 0,
        "O" or "TRUE" or "ON" => 1,
        _ => int.TryParse(Value, out int state) ? state : -1
    };

    /// <summary>Initializes a new instance of the <see cref="ItemData"/> class.</summary>
    /// <param name="type">The shape of the payload.</param>
    protected ItemData(ItemDataType type) => Type = type;

    /// <summary>Reads the shape-specific part of the payload.</summary>
    /// <param name="p">The packet to read from, positioned after the type integer.</param>
    protected abstract void ReadData(in PacketReader p);
    /// <summary>Writes the shape-specific part of the payload.</summary>
    /// <param name="p">The packet to write to, positioned after the type integer.</param>
    protected abstract void WriteData(in PacketWriter p);

    /// <summary>Reads the payload in the Flash layout, which by default is <see cref="ReadData"/>.</summary>
    /// <param name="p">The packet to read from, positioned after the type integer.</param>
    protected virtual void ReadFlashData(in PacketReader p) => ReadData(in p);
    /// <summary>Writes the payload in the Flash layout, which by default is <see cref="WriteData"/>.</summary>
    /// <param name="p">The packet to write to, positioned after the type integer.</param>
    protected virtual void WriteFlashData(in PacketWriter p) => WriteData(in p);

    /// <summary>
    /// Reads <see cref="UniqueSerialNumber"/> and <see cref="UniqueSeriesSize"/> when
    /// <see cref="IsLimitedRare"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="p">The packet to read from.</param>
    protected void ReadFlashRare(in PacketReader p)
    {
        if (IsLimitedRare)
        {
            UniqueSerialNumber = p.ReadInt();
            UniqueSeriesSize = p.ReadInt();
        }
    }

    /// <summary>
    /// Writes <see cref="UniqueSerialNumber"/> and <see cref="UniqueSeriesSize"/> when
    /// <see cref="IsLimitedRare"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="p">The packet to write to.</param>
    protected void WriteFlashRare(in PacketWriter p)
    {
        if (IsLimitedRare)
        {
            p.WriteInt(UniqueSerialNumber);
            p.WriteInt(UniqueSeriesSize);
        }
    }

    /// <summary>Writes the payload to a packet, starting with the type and flags integer.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ItemData value, in PacketWriter p)
    {
        value.WriteType(in p);
        value.WriteFlashData(in p);
    }

    private void WriteType(in PacketWriter p) =>
        p.WriteInt(((int)Type & 0xFF) | ((int)Flags << 8));

    /// <summary>Reads a furni payload from a packet.</summary>
    /// <remarks>
    /// The low byte of the leading integer picks the subclass and the higher bits become
    /// <see cref="Flags"/>.
    /// </remarks>
    /// <param name="p">The packet to read from.</param>
    /// <returns>The payload, as the subclass that matches its type.</returns>
    /// <exception cref="Exception">Thrown when the payload type is not recognized.</exception>
    public static ItemData Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ItemData ParseFlash(in PacketReader p)
    {
        ItemData data = Create(in p);
        data.ReadFlashData(in p);
        return data;
    }

    private static ItemData Create(in PacketReader p)
    {
        int value = p.ReadInt();
        var type = (ItemDataType)(value & 0xFF);
        var flags = (ItemDataFlags)(value >> 8);

        ItemData data = type switch
        {
            ItemDataType.Legacy => new LegacyData(),
            ItemDataType.Map => new MapData(),
            ItemDataType.StringArray => new StringArrayData(),
            ItemDataType.VoteResult => new VoteResultData(),
            ItemDataType.Empty => new EmptyItemData(),
            ItemDataType.IntArray => new IntArrayData(),
            ItemDataType.HighScore => new HighScoreData(),
            ItemDataType.CrackableFurni => new CrackableFurniData(),
            _ => throw new Exception($"Unknown item data type: {type}.")
        };

        data.Flags = flags;
        return data;
    }
}

/// <summary>Represents a furni payload that holds a single string in <see cref="ItemData.Value"/>.</summary>
/// <remarks>This is the payload most furni carry.</remarks>
public sealed class LegacyData() : ItemData(ItemDataType.Legacy)
{
    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p) => Value = p.ReadString();

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p) => p.WriteString(Value);

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }
}

/// <summary>Represents a furni payload that holds a string-to-string map.</summary>
public sealed class MapData() : ItemData(ItemDataType.Map)
{
    /// <summary>Gets the map entries.</summary>
    public Dictionary<string, string> Entries { get; } = [];

    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p)
    {
        int n = InventoryWire.RequireCount(
            p.ReadLength(),
            p.Available,
            sizeof(short) * 2,
            nameof(Entries));
        for (int i = 0; i < n; i++)
            Entries[p.ReadString()] = p.ReadString();
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p)
    {
        p.WriteLength((Length)Entries.Count);
        foreach ((string key, string value) in Entries)
        {
            p.WriteString(key);
            p.WriteString(value);
        }
    }

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }
}

/// <summary>Represents a furni payload that holds a list of strings.</summary>
public sealed class StringArrayData() : ItemData(ItemDataType.StringArray)
{
    /// <summary>Gets the strings in the payload.</summary>
    public List<string> Values { get; } = [];

    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p)
    {
        int count = p.ReadInt();
        count = InventoryWire.RequireCount(count, p.Available, sizeof(short), nameof(Values));
        for (int index = 0; index < count; index++)
            Values.Add(p.ReadString());
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p)
    {
        p.WriteStringArray(Values);
    }

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }
}

/// <summary>Represents a furni payload that holds a string in <see cref="ItemData.Value"/> and a vote tally.</summary>
public sealed class VoteResultData() : ItemData(ItemDataType.VoteResult)
{
    /// <summary>Gets or sets the vote tally.</summary>
    public int Result { get; set; }

    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p)
    {
        Value = p.ReadString();
        Result = p.ReadInt();
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p)
    {
        p.WriteString(Value);
        p.WriteInt(Result);
    }

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }
}

/// <summary>Represents a furni payload with no data beyond the limited-rare fields.</summary>
public sealed class EmptyItemData() : ItemData(ItemDataType.Empty)
{
    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p) { }
    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p) { }
    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p) => ReadFlashRare(in p);
    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p) => WriteFlashRare(in p);
}

/// <summary>Represents a furni payload that holds a list of integers.</summary>
public sealed class IntArrayData() : ItemData(ItemDataType.IntArray)
{
    /// <summary>Gets the integers in the payload.</summary>
    public List<int> Values { get; } = [];

    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p)
    {
        int count = p.ReadInt();
        count = InventoryWire.RequireCount(count, p.Available, sizeof(int), nameof(Values));
        for (int index = 0; index < count; index++)
            Values.Add(p.ReadInt());
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p)
    {
        p.WriteIntArray(Values);
    }

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }
}

/// <summary>Represents a game furni's high-score table.</summary>
/// <remarks>This payload has no limited-rare fields in the Flash layout.</remarks>
public sealed class HighScoreData() : ItemData(ItemDataType.HighScore)
{
    /// <summary>Gets or sets how the table scores entries, as the hotel numbers it.</summary>
    public int ScoreType { get; set; }
    /// <summary>Gets or sets when the table is cleared, as the hotel numbers it.</summary>
    public int ClearType { get; set; }
    /// <summary>Gets the table's entries in the order the hotel sent them.</summary>
    public List<HighScore> Scores { get; } = [];

    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p)
    {
        Value = p.ReadString();
        ScoreType = p.ReadInt();
        ClearType = p.ReadInt();
        int count = p.ReadInt();
        count = InventoryWire.RequireCount(count, p.Available, sizeof(int) * 2, nameof(Scores));
        for (int index = 0; index < count; index++)
            Scores.Add(p.Parse<HighScore>());
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p)
    {
        p.WriteString(Value);
        p.WriteInt(ScoreType);
        p.WriteInt(ClearType);
        p.ComposeArray(Scores);
    }

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p) => ReadData(in p);

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p) => WriteData(in p);
}

/// <summary>Represents one entry of a high-score table.</summary>
public sealed class HighScore : IParserComposer<HighScore>
{
    /// <summary>Gets or sets the score.</summary>
    public int Score { get; set; }
    /// <summary>Gets or sets the names of the users who share the score.</summary>
    public List<string> Names { get; set; } = [];

    /// <summary>Reads a high-score entry from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static HighScore Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static HighScore ParseFlash(in PacketReader p)
    {
        int score = p.ReadInt();
        int count = InventoryWire.RequireCount(
            p.ReadInt(),
            p.Available,
            sizeof(short),
            nameof(Names));
        var names = new string[count];
        for (int index = 0; index < names.Length; index++)
            names[index] = p.ReadString();
        return new HighScore { Score = score, Names = [.. names] };
    }

    /// <summary>Writes the high-score entry to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(HighScore value, in PacketWriter p) => value.ComposeScore(in p);

    private void ComposeScore(in PacketWriter p)
    {
        p.WriteInt(Score);
        p.WriteStringArray(Names);
    }
}

/// <summary>Represents a crackable furni's payload with its hit count.</summary>
public sealed class CrackableFurniData() : ItemData(ItemDataType.CrackableFurni)
{
    /// <summary>Gets or sets how many hits the furni has taken.</summary>
    public int Hits { get; set; }
    /// <summary>Gets or sets how many hits the furni needs in total to crack.</summary>
    public int Target { get; set; }

    /// <inheritdoc/>
    protected override void ReadData(in PacketReader p)
    {
        Value = p.ReadString();
        Hits = p.ReadInt();
        Target = p.ReadInt();
    }

    /// <inheritdoc/>
    protected override void WriteData(in PacketWriter p)
    {
        p.WriteString(Value);
        p.WriteInt(Hits);
        p.WriteInt(Target);
    }

    /// <inheritdoc/>
    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    /// <inheritdoc/>
    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }
}
