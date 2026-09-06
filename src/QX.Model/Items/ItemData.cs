using Qx.Messages;

namespace Qx.Model;

public abstract class ItemData : IParserComposer<ItemData>
{
    public ItemDataType Type { get; }
    public ItemDataFlags Flags { get; set; }
    public bool IsLimitedRare => Flags.HasFlag(ItemDataFlags.IsLimitedRare);
    public int UniqueSerialNumber { get; set; }
    public int UniqueSeriesSize { get; set; }
    public string UniqueLimitedData { get; set; } = "";
    public string Value { get; set; } = "";

    public int State => Value switch
    {
        "C" or "FALSE" or "OFF" => 0,
        "O" or "TRUE" or "ON" => 1,
        _ => int.TryParse(Value, out int state) ? state : -1
    };

    protected ItemData(ItemDataType type) => Type = type;

    protected abstract void ReadData(in PacketReader p);
    protected abstract void WriteData(in PacketWriter p);

    protected virtual void ReadFlashData(in PacketReader p) => ReadData(in p);
    protected virtual void ReadUnityData(in PacketReader p) => ReadData(in p);
    protected virtual void WriteFlashData(in PacketWriter p) => WriteData(in p);
    protected virtual void WriteUnityData(in PacketWriter p) => WriteData(in p);

    protected void ReadFlashRare(in PacketReader p)
    {
        if (IsLimitedRare)
        {
            UniqueSerialNumber = p.ReadInt();
            UniqueSeriesSize = p.ReadInt();
        }
    }

    protected void ReadUnityRare(in PacketReader p)
    {
        ReadFlashRare(in p);
        if (IsLimitedRare)
            UniqueLimitedData = p.ReadString();
    }

    protected void WriteFlashRare(in PacketWriter p)
    {
        if (IsLimitedRare)
        {
            p.WriteInt(UniqueSerialNumber);
            p.WriteInt(UniqueSeriesSize);
        }
    }

    protected void WriteUnityRare(in PacketWriter p)
    {
        WriteFlashRare(in p);
        if (IsLimitedRare)
            p.WriteString(UniqueLimitedData);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ItemData value, in PacketWriter p)
    {
        value.WriteType(in p);
        value.WriteFlashData(in p);
    }

    private static void ComposeUnity(ItemData value, in PacketWriter p)
    {
        value.WriteType(in p);
        value.WriteUnityData(in p);
    }

    private void WriteType(in PacketWriter p) =>
        p.WriteInt(((int)Type & 0xFF) | ((int)Flags << 8));

    public static ItemData Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ItemData ParseFlash(in PacketReader p)
    {
        ItemData data = Create(in p);
        data.ReadFlashData(in p);
        return data;
    }

    private static ItemData ParseUnity(in PacketReader p)
    {
        ItemData data = Create(in p);
        data.ReadUnityData(in p);
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

public sealed class LegacyData() : ItemData(ItemDataType.Legacy)
{
    protected override void ReadData(in PacketReader p) => Value = p.ReadString();

    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteData(in PacketWriter p) => p.WriteString(Value);

    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}

public sealed class MapData() : ItemData(ItemDataType.Map)
{
    public Dictionary<string, string> Entries { get; } = [];

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

    protected override void WriteData(in PacketWriter p)
    {
        p.WriteLength((Length)Entries.Count);
        foreach ((string key, string value) in Entries)
        {
            p.WriteString(key);
            p.WriteString(value);
        }
    }

    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}

public sealed class StringArrayData() : ItemData(ItemDataType.StringArray)
{
    public List<string> Values { get; } = [];

    protected override void ReadData(in PacketReader p)
    {
        int count = p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => p.ReadLength(),
            _ => throw new UnsupportedClientException(p.Client)
        };
        count = InventoryWire.RequireCount(count, p.Available, sizeof(short), nameof(Values));
        for (int index = 0; index < count; index++)
            Values.Add(p.ReadString());
    }

    protected override void WriteData(in PacketWriter p)
    {
        p.WriteStringArray(Values);
    }

    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}

public sealed class VoteResultData() : ItemData(ItemDataType.VoteResult)
{
    public int Result { get; set; }

    protected override void ReadData(in PacketReader p)
    {
        Value = p.ReadString();
        Result = p.ReadInt();
    }

    protected override void WriteData(in PacketWriter p)
    {
        p.WriteString(Value);
        p.WriteInt(Result);
    }

    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}

public sealed class EmptyItemData() : ItemData(ItemDataType.Empty)
{
    protected override void ReadData(in PacketReader p) { }
    protected override void WriteData(in PacketWriter p) { }
    protected override void ReadFlashData(in PacketReader p) => ReadFlashRare(in p);
    protected override void ReadUnityData(in PacketReader p) => ReadUnityRare(in p);
    protected override void WriteFlashData(in PacketWriter p) => WriteFlashRare(in p);
    protected override void WriteUnityData(in PacketWriter p) => WriteUnityRare(in p);
}

public sealed class IntArrayData() : ItemData(ItemDataType.IntArray)
{
    public List<int> Values { get; } = [];

    protected override void ReadData(in PacketReader p)
    {
        int count = p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => p.ReadLength(),
            _ => throw new UnsupportedClientException(p.Client)
        };
        count = InventoryWire.RequireCount(count, p.Available, sizeof(int), nameof(Values));
        for (int index = 0; index < count; index++)
            Values.Add(p.ReadInt());
    }

    protected override void WriteData(in PacketWriter p)
    {
        p.WriteIntArray(Values);
    }

    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}

/// <summary>
/// Score-table item data, stuff data format 6.
/// </summary>
/// <remarks>
/// This is the one format that carries no limited-rare tail on Flash. Every other format's
/// client-side class calls its base <c>initializeFromIncomingMessage</c>, which reads the serial
/// number and series size when the rare bit is set; the score-table class does not, so the tail is
/// absent even when the bit is set. Unity is left reading the tail because nothing establishes
/// its behaviour either way - the IL2CPP method bodies are empty and the combination does not
/// occur in observable traffic, since score furni are not limited rares.
/// </remarks>
public sealed class HighScoreData() : ItemData(ItemDataType.HighScore)
{
    public int ScoreType { get; set; }
    public int ClearType { get; set; }
    public List<HighScore> Scores { get; } = [];

    protected override void ReadData(in PacketReader p)
    {
        Value = p.ReadString();
        ScoreType = p.ReadInt();
        ClearType = p.ReadInt();
        int minimum_bytes;
        int count;
        switch (p.Client)
        {
            case ClientType.Flash:
                count = p.ReadInt();
                minimum_bytes = sizeof(int) * 2;
                break;
            case ClientType.Unity:
                count = p.ReadLength();
                minimum_bytes = sizeof(int) + sizeof(short);
                break;
            default:
                throw new UnsupportedClientException(p.Client);
        }
        count = InventoryWire.RequireCount(count, p.Available, minimum_bytes, nameof(Scores));
        for (int index = 0; index < count; index++)
            Scores.Add(p.Parse<HighScore>());
    }

    protected override void WriteData(in PacketWriter p)
    {
        p.WriteString(Value);
        p.WriteInt(ScoreType);
        p.WriteInt(ClearType);
        p.ComposeArray(Scores);
    }

    protected override void ReadFlashData(in PacketReader p) => ReadData(in p);

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteFlashData(in PacketWriter p) => WriteData(in p);

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}

public sealed class HighScore : IParserComposer<HighScore>
{
    public int Score { get; set; }
    public List<string> Names { get; set; } = [];

    public static HighScore Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static HighScore ParseUnity(in PacketReader p)
    {
        int score = p.ReadInt();
        int count = InventoryWire.RequireCount(
            p.ReadLength(),
            p.Available,
            sizeof(short),
            nameof(Names));
        var names = new string[count];
        for (int index = 0; index < names.Length; index++)
            names[index] = p.ReadString();
        return new HighScore { Score = score, Names = [.. names] };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HighScore value, in PacketWriter p) => value.ComposeScore(in p);

    private static void ComposeUnity(HighScore value, in PacketWriter p) => value.ComposeScore(in p);

    private void ComposeScore(in PacketWriter p)
    {
        p.WriteInt(Score);
        p.WriteStringArray(Names);
    }
}

public sealed class CrackableFurniData() : ItemData(ItemDataType.CrackableFurni)
{
    public int Hits { get; set; }
    public int Target { get; set; }

    protected override void ReadData(in PacketReader p)
    {
        Value = p.ReadString();
        Hits = p.ReadInt();
        Target = p.ReadInt();
    }

    protected override void WriteData(in PacketWriter p)
    {
        p.WriteString(Value);
        p.WriteInt(Hits);
        p.WriteInt(Target);
    }

    protected override void ReadFlashData(in PacketReader p)
    {
        ReadData(in p);
        ReadFlashRare(in p);
    }

    protected override void ReadUnityData(in PacketReader p)
    {
        ReadData(in p);
        ReadUnityRare(in p);
    }

    protected override void WriteFlashData(in PacketWriter p)
    {
        WriteData(in p);
        WriteFlashRare(in p);
    }

    protected override void WriteUnityData(in PacketWriter p)
    {
        WriteData(in p);
        WriteUnityRare(in p);
    }
}
