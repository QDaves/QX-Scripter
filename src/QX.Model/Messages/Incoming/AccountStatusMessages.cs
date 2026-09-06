using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record BlockUserUpdate(int Result, Id UserId) : IParserComposer<BlockUserUpdate>
{
    public static BlockUserUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BlockUserUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static BlockUserUpdate ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BlockUserUpdate value, in PacketWriter p)
    {
        int user_id = AccountWire.FlashId(value.UserId);
        p.WriteInt(value.Result);
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(BlockUserUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Result);
        p.WriteLong(value.UserId);
    }
}

public sealed record BlockList : IParserComposer<BlockList>
{
    private IReadOnlyList<Id> _user_ids = Array.Empty<Id>();

    public BlockList(IReadOnlyList<Id> user_ids) => UserIds = user_ids;

    public IReadOnlyList<Id> UserIds
    {
        get => _user_ids;
        init => _user_ids = AccountWire.FreezeValues(value, nameof(UserIds));
    }

    public static BlockList Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BlockList ParseFlash(in PacketReader p) =>
        new(AccountWire.ReadFlashIds(in p, nameof(UserIds)));

    private static BlockList ParseUnity(in PacketReader p) =>
        new(AccountWire.ReadUnityIds(in p, nameof(UserIds)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BlockList value, in PacketWriter p) =>
        AccountWire.WriteFlashIds(in p, value.UserIds, nameof(UserIds));

    private static void ComposeUnity(BlockList value, in PacketWriter p) =>
        AccountWire.WriteUnityIds(in p, value.UserIds, nameof(UserIds));
}

public sealed record IgnoreUserResult(int Result, Id UserId) : IParserComposer<IgnoreUserResult>
{
    public static IgnoreUserResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static IgnoreUserResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static IgnoreUserResult ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(IgnoreUserResult value, in PacketWriter p)
    {
        int user_id = AccountWire.FlashId(value.UserId);
        p.WriteInt(value.Result);
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(IgnoreUserResult value, in PacketWriter p)
    {
        p.WriteInt(value.Result);
        p.WriteLong(value.UserId);
    }
}

public sealed record RequestIgnoreList : IParserComposer<RequestIgnoreList>
{
    private IReadOnlyList<Id> _user_ids = Array.Empty<Id>();

    public RequestIgnoreList(IReadOnlyList<Id> user_ids) => UserIds = user_ids;

    public IReadOnlyList<Id> UserIds
    {
        get => _user_ids;
        init => _user_ids = AccountWire.FreezeValues(value, nameof(UserIds));
    }

    public static RequestIgnoreList Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RequestIgnoreList ParseFlash(in PacketReader p) =>
        new(AccountWire.ReadFlashIds(in p, nameof(UserIds)));

    private static RequestIgnoreList ParseUnity(in PacketReader p) =>
        new(AccountWire.ReadUnityIds(in p, nameof(UserIds)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RequestIgnoreList value, in PacketWriter p) =>
        AccountWire.WriteFlashIds(in p, value.UserIds, nameof(UserIds));

    private static void ComposeUnity(RequestIgnoreList value, in PacketWriter p) =>
        AccountWire.WriteUnityIds(in p, value.UserIds, nameof(UserIds));
}

public sealed record FigureSetIdAdded(int FigureSetId) : IParserComposer<FigureSetIdAdded>
{
    public static FigureSetIdAdded Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FigureSetIdAdded ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FigureSetIdAdded value, in PacketWriter p) =>
        p.WriteInt(value.FigureSetId);
}

public sealed record FigureSetIdRemoved(int FigureSetId) : IParserComposer<FigureSetIdRemoved>
{
    public static FigureSetIdRemoved Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FigureSetIdRemoved ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FigureSetIdRemoved value, in PacketWriter p) =>
        p.WriteInt(value.FigureSetId);
}

public readonly record struct FigureSetEntry(int FigureSetId, int Metadata)
    : IParserComposer<FigureSetEntry>
{
    public static FigureSetEntry Parse(in PacketReader p) =>
        ModernWireClients.ParseUnity(in p, ParseUnity);

    private static FigureSetEntry ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeUnity(this, in p, ComposeUnity);

    private static void ComposeUnity(FigureSetEntry value, in PacketWriter p)
    {
        p.WriteInt(value.FigureSetId);
        p.WriteInt(value.Metadata);
    }
}

public sealed record FigureSetIds : IParserComposer<FigureSetIds>
{
    private IReadOnlyList<FigureSetEntry> _entries = Array.Empty<FigureSetEntry>();
    private IReadOnlyList<string> _bound_furniture_names = Array.Empty<string>();

    public FigureSetIds(IReadOnlyList<FigureSetEntry> entries) : this(entries, []) { }

    public FigureSetIds(
        IReadOnlyList<FigureSetEntry> entries,
        IReadOnlyList<string> bound_furniture_names)
    {
        Entries = entries;
        BoundFurnitureNames = bound_furniture_names;
    }

    public IReadOnlyList<FigureSetEntry> Entries
    {
        get => _entries;
        init => _entries = AccountWire.FreezeValues(value, nameof(Entries));
    }

    public IReadOnlyList<string> BoundFurnitureNames
    {
        get => _bound_furniture_names;
        init => _bound_furniture_names = AccountWire.FreezeStrings(value, nameof(BoundFurnitureNames));
    }

    public static FigureSetIds Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FigureSetIds ParseFlash(in PacketReader p)
    {
        int[] ids = AccountWire.ReadFlashInts(in p, nameof(Entries));
        var entries = new FigureSetEntry[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            entries[i] = new FigureSetEntry(ids[i], 0);
        return new FigureSetIds(
            entries,
            AccountWire.ReadFlashStrings(in p, nameof(BoundFurnitureNames)));
    }

    private static FigureSetIds ParseUnity(in PacketReader p)
    {
        int count = AccountWire.ReadUnityCount(in p, p.Available, 8, nameof(Entries));
        var entries = new FigureSetEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = p.Parse<FigureSetEntry>();
        return new FigureSetIds(entries);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FigureSetIds value, in PacketWriter p)
    {
        var ids = new int[value.Entries.Count];
        for (int i = 0; i < ids.Length; i++)
        {
            FigureSetEntry entry = value.Entries[i];
            if (entry.Metadata != 0)
                throw new InvalidDataException("Flash figure-set snapshots cannot carry metadata.");
            ids[i] = entry.FigureSetId;
        }
        AccountWire.RequireStrings(value.BoundFurnitureNames, nameof(BoundFurnitureNames), in p);
        AccountWire.WriteFlashInts(in p, ids);
        AccountWire.WriteFlashStrings(in p, value.BoundFurnitureNames);
    }

    private static void ComposeUnity(FigureSetIds value, in PacketWriter p)
    {
        if (value.BoundFurnitureNames.Count != 0)
            throw new InvalidDataException("Unity figure-set snapshots cannot carry bound furniture names.");
        AccountWire.RequireUnityCount(value.Entries.Count, nameof(Entries));
        p.WriteLength((Length)value.Entries.Count);
        foreach (FigureSetEntry entry in value.Entries)
            p.Compose(entry);
    }
}

public sealed record SanctionType(string Name, int First, int Second) : IParserComposer<SanctionType>
{
    public static SanctionType Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SanctionType ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    private static SanctionType ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SanctionType value, in PacketWriter p)
    {
        AccountWire.RequireString(value.Name, nameof(Name), in p);
        p.WriteString(value.Name);
        p.WriteInt(value.First);
        p.WriteInt(value.Second);
    }

    private static void ComposeUnity(SanctionType value, in PacketWriter p)
    {
        AccountWire.RequireString(value.Name, nameof(Name), in p);
        p.WriteString(value.Name);
        p.WriteInt(value.First);
        p.WriteInt(value.Second);
    }
}

public sealed record Sanction(
    SanctionType Type,
    string Text,
    bool Flag,
    int Value,
    SanctionType NextType) : IParserComposer<Sanction>
{
    public static Sanction Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static Sanction ParseFlash(in PacketReader p) =>
        new(
            p.Parse<SanctionType>(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadInt(),
            p.Parse<SanctionType>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(Sanction value, in PacketWriter p)
    {
        Validate(value, in p);
        p.Compose(value.Type);
        p.WriteString(value.Text);
        p.WriteBool(value.Flag);
        p.WriteInt(value.Value);
        p.Compose(value.NextType);
    }

    internal static void Validate(Sanction value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Type, nameof(Type));
        ArgumentNullException.ThrowIfNull(value.NextType, nameof(NextType));
        AccountWire.RequireString(value.Type.Name, nameof(Type), in p);
        AccountWire.RequireString(value.Text, nameof(Text), in p);
        AccountWire.RequireString(value.NextType.Name, nameof(NextType), in p);
    }
}

public sealed record MySanctionStatus : IParserComposer<MySanctionStatus>
{
    private IReadOnlyList<Sanction> _sanctions = Array.Empty<Sanction>();

    public MySanctionStatus(IReadOnlyList<Sanction> sanctions) => Sanctions = sanctions;

    public IReadOnlyList<Sanction> Sanctions
    {
        get => _sanctions;
        init => _sanctions = AccountWire.FreezeReferences(value, nameof(Sanctions));
    }

    public bool IsSanctioned => Sanctions.Count > 0;

    public static MySanctionStatus Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static MySanctionStatus ParseFlash(in PacketReader p)
    {
        const int minimum_sanction_bytes = 27;
        int count = AccountWire.ReadFlashCount(
            in p,
            p.Available,
            minimum_sanction_bytes,
            nameof(Sanctions));
        var sanctions = new Sanction[count];
        for (int i = 0; i < count; i++)
            sanctions[i] = p.Parse<Sanction>();
        return new MySanctionStatus(sanctions);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(MySanctionStatus value, in PacketWriter p)
    {
        foreach (Sanction sanction in value.Sanctions)
            Sanction.Validate(sanction, in p);
        p.WriteInt(value.Sanctions.Count);
        foreach (Sanction sanction in value.Sanctions)
            p.Compose(sanction);
    }
}

public sealed record CfhSanctionStatus(
    bool FirstFlag,
    bool SecondFlag,
    SanctionType CurrentType,
    string FirstText,
    string SecondText,
    int Value,
    SanctionType NextType,
    bool ThirdFlag,
    string ThirdText) : IParserComposer<CfhSanctionStatus>
{
    public static CfhSanctionStatus Parse(in PacketReader p) =>
        ModernWireClients.ParseUnity(in p, ParseUnity);

    private static CfhSanctionStatus ParseUnity(in PacketReader p) =>
        new(
            p.ReadBool(),
            p.ReadBool(),
            p.Parse<SanctionType>(),
            p.ReadString(),
            p.ReadString(),
            p.ReadInt(),
            p.Parse<SanctionType>(),
            p.ReadBool(),
            p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeUnity(this, in p, ComposeUnity);

    private static void ComposeUnity(CfhSanctionStatus value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteBool(value.FirstFlag);
        p.WriteBool(value.SecondFlag);
        p.Compose(value.CurrentType);
        p.WriteString(value.FirstText);
        p.WriteString(value.SecondText);
        p.WriteInt(value.Value);
        p.Compose(value.NextType);
        p.WriteBool(value.ThirdFlag);
        p.WriteString(value.ThirdText);
    }

    internal static void Validate(CfhSanctionStatus value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.CurrentType, nameof(CurrentType));
        ArgumentNullException.ThrowIfNull(value.NextType, nameof(NextType));
        AccountWire.RequireString(value.CurrentType.Name, nameof(CurrentType), in p);
        AccountWire.RequireString(value.FirstText, nameof(FirstText), in p);
        AccountWire.RequireString(value.SecondText, nameof(SecondText), in p);
        AccountWire.RequireString(value.NextType.Name, nameof(NextType), in p);
        AccountWire.RequireString(value.ThirdText, nameof(ThirdText), in p);
    }
}

public enum AccountSanctionStatusKind
{
    Sanctions,
    CallForHelp
}

public sealed record AccountSanctionStatus : IParserComposer<AccountSanctionStatus>
{
    public AccountSanctionStatus(MySanctionStatus sanctions)
    {
        ArgumentNullException.ThrowIfNull(sanctions);
        Kind = AccountSanctionStatusKind.Sanctions;
        Sanctions = sanctions;
    }

    public AccountSanctionStatus(CfhSanctionStatus call_for_help)
    {
        ArgumentNullException.ThrowIfNull(call_for_help);
        Kind = AccountSanctionStatusKind.CallForHelp;
        CallForHelp = call_for_help;
    }

    public AccountSanctionStatusKind Kind { get; }
    public MySanctionStatus? Sanctions { get; }
    public CfhSanctionStatus? CallForHelp { get; }

    public static AccountSanctionStatus Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AccountSanctionStatus ParseFlash(in PacketReader p) =>
        new(p.Parse<MySanctionStatus>());

    private static AccountSanctionStatus ParseUnity(in PacketReader p) =>
        new(p.Parse<CfhSanctionStatus>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AccountSanctionStatus value, in PacketWriter p)
    {
        if (value.Kind is not AccountSanctionStatusKind.Sanctions || value.Sanctions is null)
            throw new InvalidDataException("Flash sanction status requires the sanction-list variant.");
        value.Sanctions.Compose(in p);
    }

    private static void ComposeUnity(AccountSanctionStatus value, in PacketWriter p)
    {
        if (value.Kind is not AccountSanctionStatusKind.CallForHelp || value.CallForHelp is null)
            throw new InvalidDataException("Unity sanction status requires the call-for-help variant.");
        value.CallForHelp.Compose(in p);
    }
}

public sealed record FigureUpdate(string Figure, string Gender) : IParserComposer<FigureUpdate>
{
    public static FigureUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FigureUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    private static FigureUpdate ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FigureUpdate value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }

    private static void ComposeUnity(FigureUpdate value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }

    private static void Validate(FigureUpdate value, in PacketWriter p)
    {
        AccountWire.RequireString(value.Figure, nameof(Figure), in p);
        AccountWire.RequireString(value.Gender, nameof(Gender), in p);
    }
}

public sealed record ChangeUserNameResult : IParserComposer<ChangeUserNameResult>
{
    private IReadOnlyList<string> _name_suggestions = Array.Empty<string>();

    public ChangeUserNameResult(
        int result_code,
        string name,
        IReadOnlyList<string> name_suggestions)
    {
        ResultCode = result_code;
        Name = name;
        NameSuggestions = name_suggestions;
    }

    public const int SuccessCode = 0;
    public int ResultCode { get; init; }
    public string Name { get; init; }

    public IReadOnlyList<string> NameSuggestions
    {
        get => _name_suggestions;
        init => _name_suggestions = AccountWire.FreezeStrings(value, nameof(NameSuggestions));
    }

    public bool Success => ResultCode == SuccessCode;

    public static ChangeUserNameResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ChangeUserNameResult ParseFlash(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            AccountWire.ReadFlashStrings(in p, nameof(NameSuggestions)));

    private static ChangeUserNameResult ParseUnity(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            AccountWire.ReadUnityStrings(in p, nameof(NameSuggestions)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ChangeUserNameResult value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.ResultCode);
        p.WriteString(value.Name);
        AccountWire.WriteFlashStrings(in p, value.NameSuggestions);
    }

    private static void ComposeUnity(ChangeUserNameResult value, in PacketWriter p)
    {
        Validate(value, in p);
        AccountWire.RequireUnityCount(value.NameSuggestions.Count, nameof(NameSuggestions));
        p.WriteInt(value.ResultCode);
        p.WriteString(value.Name);
        AccountWire.WriteUnityStrings(in p, value.NameSuggestions);
    }

    private static void Validate(ChangeUserNameResult value, in PacketWriter p)
    {
        AccountWire.RequireString(value.Name, nameof(Name), in p);
        AccountWire.RequireStrings(value.NameSuggestions, nameof(NameSuggestions), in p);
    }
}

public sealed record AccountSafetyLockStatusChange(int Status)
    : IParserComposer<AccountSafetyLockStatusChange>
{
    public bool IsLocked => Status == 0;

    public static AccountSafetyLockStatusChange Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static AccountSafetyLockStatusChange ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(AccountSafetyLockStatusChange value, in PacketWriter p) =>
        p.WriteInt(value.Status);
}

internal static class AccountWire
{
    public static int FlashId(Id value) => checked((int)(long)value);

    public static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    public static IReadOnlyList<T> FreezeReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        T[] copy = values.ToArray();
        foreach (T value in copy)
            ArgumentNullException.ThrowIfNull(value, name);
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<string> FreezeStrings(IReadOnlyList<string> values, string name) =>
        FreezeReferences(values, name);

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    public static void RequireStrings(
        IReadOnlyList<string> values,
        string name,
        in PacketWriter p)
    {
        foreach (string value in values)
            RequireString(value, name, in p);
    }

    public static int ReadFlashCount(
        in PacketReader p,
        int available,
        int minimum_bytes,
        string name) =>
        RequireBoundedCount(p.ReadInt(), available - sizeof(int), minimum_bytes, name);

    public static int ReadUnityCount(
        in PacketReader p,
        int available,
        int minimum_bytes,
        string name) =>
        RequireBoundedCount(p.ReadLength(), available - sizeof(short), minimum_bytes, name);

    public static Id[] ReadFlashIds(in PacketReader p, string name)
    {
        int count = ReadFlashCount(in p, p.Available, sizeof(int), name);
        var values = new Id[count];
        for (int i = 0; i < count; i++)
            values[i] = p.ReadInt();
        return values;
    }

    public static Id[] ReadUnityIds(in PacketReader p, string name)
    {
        int count = ReadUnityCount(in p, p.Available, sizeof(long), name);
        var values = new Id[count];
        for (int i = 0; i < count; i++)
            values[i] = p.ReadLong();
        return values;
    }

    public static int[] ReadFlashInts(in PacketReader p, string name)
    {
        int count = ReadFlashCount(in p, p.Available, sizeof(int), name);
        var values = new int[count];
        for (int i = 0; i < count; i++)
            values[i] = p.ReadInt();
        return values;
    }

    public static string[] ReadFlashStrings(in PacketReader p, string name)
    {
        int count = ReadFlashCount(in p, p.Available, sizeof(short), name);
        var values = new string[count];
        for (int i = 0; i < count; i++)
            values[i] = p.ReadString();
        return values;
    }

    public static string[] ReadUnityStrings(in PacketReader p, string name)
    {
        int count = ReadUnityCount(in p, p.Available, sizeof(short), name);
        var values = new string[count];
        for (int i = 0; i < count; i++)
            values[i] = p.ReadString();
        return values;
    }

    public static void WriteFlashIds(
        in PacketWriter p,
        IReadOnlyList<Id> values,
        string name)
    {
        var ids = new int[values.Count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = FlashId(values[i]);
        p.WriteInt(ids.Length);
        foreach (int id in ids)
            p.WriteInt(id);
    }

    public static void WriteUnityIds(
        in PacketWriter p,
        IReadOnlyList<Id> values,
        string name)
    {
        RequireUnityCount(values.Count, name);
        p.WriteLength((Length)values.Count);
        foreach (Id id in values)
            p.WriteLong(id);
    }

    public static void WriteFlashInts(in PacketWriter p, IReadOnlyList<int> values)
    {
        p.WriteInt(values.Count);
        foreach (int value in values)
            p.WriteInt(value);
    }

    public static void WriteFlashStrings(in PacketWriter p, IReadOnlyList<string> values)
    {
        p.WriteInt(values.Count);
        foreach (string value in values)
            p.WriteString(value);
    }

    public static void WriteUnityStrings(in PacketWriter p, IReadOnlyList<string> values)
    {
        p.WriteLength((Length)values.Count);
        foreach (string value in values)
            p.WriteString(value);
    }

    public static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
    }

    private static int RequireBoundedCount(
        int count,
        int available,
        int minimum_bytes,
        string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (available < 0 || count > available / minimum_bytes)
            throw new InvalidDataException($"{name} count {count} exceeds the remaining payload capacity.");
        return count;
    }

}
