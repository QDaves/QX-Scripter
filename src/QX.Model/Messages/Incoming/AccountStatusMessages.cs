using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Represents the <c>BlockUserUpdate</c> message, received with the result of blocking or unblocking a user.
/// </summary>
/// <param name="Result">The result code sent by the server.</param>
/// <param name="UserId">The identifier of the user the result refers to.</param>
public sealed record BlockUserUpdate(int Result, Id UserId) : IParserComposer<BlockUserUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BlockUserUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BlockUserUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BlockUserUpdate value, in PacketWriter p)
    {
        int user_id = AccountWire.FlashId(value.UserId);
        p.WriteInt(value.Result);
        p.WriteInt(user_id);
    }
}

/// <summary>Represents the <c>BlockList</c> message, received with the users the local user has blocked.</summary>
public sealed record BlockList : IParserComposer<BlockList>
{
    private IReadOnlyList<Id> _user_ids = Array.Empty<Id>();

    /// <summary>Initializes a new instance of the <see cref="BlockList"/> record.</summary>
    /// <param name="userIds">The identifiers of the blocked users, copied into a read only list.</param>
    public BlockList(IReadOnlyList<Id> userIds) => UserIds = userIds;

    /// <summary>Gets the identifiers of the blocked users, as a read only copy.</summary>
    public IReadOnlyList<Id> UserIds
    {
        get => _user_ids;
        init => _user_ids = AccountWire.FreezeValues(value, nameof(UserIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BlockList Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BlockList ParseFlash(in PacketReader p) =>
        new(AccountWire.ReadFlashIds(in p, nameof(UserIds)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BlockList value, in PacketWriter p) =>
        AccountWire.WriteFlashIds(in p, value.UserIds, nameof(UserIds));
}

/// <summary>
/// Represents the <c>IgnoreResult</c> message, received with the result of ignoring or unignoring a user.
/// </summary>
/// <param name="Result">The result code sent by the server.</param>
/// <param name="UserId">The identifier of the user the result refers to.</param>
public sealed record IgnoreUserResult(int Result, Id UserId) : IParserComposer<IgnoreUserResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static IgnoreUserResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IgnoreUserResult ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IgnoreUserResult value, in PacketWriter p)
    {
        int user_id = AccountWire.FlashId(value.UserId);
        p.WriteInt(value.Result);
        p.WriteInt(user_id);
    }
}

/// <summary>Represents the <c>IgnoredUsers</c> message, received with the users the local user ignores.</summary>
public sealed record IgnoredUsers : IParserComposer<IgnoredUsers>
{
    private IReadOnlyList<Id> _user_ids = Array.Empty<Id>();

    /// <summary>Initializes a new instance of the <see cref="IgnoredUsers"/> record.</summary>
    /// <param name="userIds">The identifiers of the ignored users, copied into a read only list.</param>
    public IgnoredUsers(IReadOnlyList<Id> userIds) => UserIds = userIds;

    /// <summary>Gets the identifiers of the ignored users, as a read only copy.</summary>
    public IReadOnlyList<Id> UserIds
    {
        get => _user_ids;
        init => _user_ids = AccountWire.FreezeValues(value, nameof(UserIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static IgnoredUsers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IgnoredUsers ParseFlash(in PacketReader p) =>
        new(AccountWire.ReadFlashIds(in p, nameof(UserIds)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IgnoredUsers value, in PacketWriter p) =>
        AccountWire.WriteFlashIds(in p, value.UserIds, nameof(UserIds));
}

/// <summary>
/// Represents the <c>FigureSetIdAdded</c> message, received when the user gets a new wardrobe figure set.
/// </summary>
/// <param name="FigureSetId">The identifier of the added figure set.</param>
public sealed record FigureSetIdAdded(int FigureSetId) : IParserComposer<FigureSetIdAdded>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FigureSetIdAdded Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FigureSetIdAdded ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FigureSetIdAdded value, in PacketWriter p) =>
        p.WriteInt(value.FigureSetId);
}

/// <summary>
/// Represents the <c>FigureSetIdRemoved</c> message, received when the user loses a wardrobe figure set.
/// </summary>
/// <param name="FigureSetId">The identifier of the removed figure set.</param>
public sealed record FigureSetIdRemoved(int FigureSetId) : IParserComposer<FigureSetIdRemoved>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FigureSetIdRemoved Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FigureSetIdRemoved ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FigureSetIdRemoved value, in PacketWriter p) =>
        p.WriteInt(value.FigureSetId);
}

/// <summary>Represents a wardrobe figure set the user owns.</summary>
/// <param name="FigureSetId">The identifier of the figure set.</param>
/// <param name="Metadata">The metadata value of the figure set, always 0 because the message carries none.</param>
public readonly record struct FigureSetEntry(int FigureSetId, int Metadata);

/// <summary>Represents the <c>FigureSetIds</c> message, received with the wardrobe figure sets the user owns.</summary>
public sealed record FigureSetIds : IParserComposer<FigureSetIds>
{
    private IReadOnlyList<FigureSetEntry> _entries = Array.Empty<FigureSetEntry>();
    private IReadOnlyList<string> _bound_furniture_names = Array.Empty<string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="FigureSetIds"/> record with no bound furniture names.
    /// </summary>
    /// <param name="entries">The owned figure sets, copied into a read only list.</param>
    public FigureSetIds(IReadOnlyList<FigureSetEntry> entries) : this(entries, []) { }

    /// <summary>Initializes a new instance of the <see cref="FigureSetIds"/> record.</summary>
    /// <param name="entries">The owned figure sets, copied into a read only list.</param>
    /// <param name="boundFurnitureNames">The bound furniture names, copied into a read only list.</param>
    public FigureSetIds(
        IReadOnlyList<FigureSetEntry> entries,
        IReadOnlyList<string> boundFurnitureNames)
    {
        Entries = entries;
        BoundFurnitureNames = boundFurnitureNames;
    }

    /// <summary>Gets the owned figure sets, as a read only copy.</summary>
    public IReadOnlyList<FigureSetEntry> Entries
    {
        get => _entries;
        init => _entries = AccountWire.FreezeValues(value, nameof(Entries));
    }

    /// <summary>
    /// Gets the bound furniture names sent after the figure sets, as a read only copy.
    /// </summary>
    public IReadOnlyList<string> BoundFurnitureNames
    {
        get => _bound_furniture_names;
        init => _bound_furniture_names = AccountWire.FreezeStrings(value, nameof(BoundFurnitureNames));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FigureSetIds Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when an entry has a metadata value other than 0.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Represents the type of a sanction.</summary>
/// <param name="Name">The name of the sanction type.</param>
/// <param name="First">The first integer sent with the type.</param>
/// <param name="Second">The second integer sent with the type.</param>
public sealed record SanctionType(string Name, int First, int Second) : IParserComposer<SanctionType>
{
    /// <summary>Parses a sanction type from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SanctionType Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SanctionType ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the sanction type into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SanctionType value, in PacketWriter p)
    {
        AccountWire.RequireString(value.Name, nameof(Name), in p);
        p.WriteString(value.Name);
        p.WriteInt(value.First);
        p.WriteInt(value.Second);
    }
}

/// <summary>Represents a sanction recorded against the local user.</summary>
/// <param name="Type">The type of the sanction.</param>
/// <param name="Text">The text sent with the sanction.</param>
/// <param name="Flag">The boolean flag sent with the sanction.</param>
/// <param name="Value">The integer value sent with the sanction.</param>
/// <param name="NextType">The type of the next sanction.</param>
public sealed record Sanction(
    SanctionType Type,
    string Text,
    bool Flag,
    int Value,
    SanctionType NextType) : IParserComposer<Sanction>
{
    /// <summary>Parses a sanction from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static Sanction Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Sanction ParseFlash(in PacketReader p) =>
        new(
            p.Parse<SanctionType>(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadInt(),
            p.Parse<SanctionType>());

    /// <summary>Composes the sanction into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the sanctions recorded against the local user.</summary>
public sealed record MySanctionStatus : IParserComposer<MySanctionStatus>
{
    private IReadOnlyList<Sanction> _sanctions = Array.Empty<Sanction>();

    /// <summary>Initializes a new instance of the <see cref="MySanctionStatus"/> record.</summary>
    /// <param name="sanctions">The sanctions, copied into a read only list.</param>
    public MySanctionStatus(IReadOnlyList<Sanction> sanctions) => Sanctions = sanctions;

    /// <summary>Gets the sanctions, as a read only copy.</summary>
    public IReadOnlyList<Sanction> Sanctions
    {
        get => _sanctions;
        init => _sanctions = AccountWire.FreezeReferences(value, nameof(Sanctions));
    }

    /// <summary>Gets whether at least one sanction is recorded.</summary>
    public bool IsSanctioned => Sanctions.Count > 0;

    /// <summary>Parses the sanction status from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MySanctionStatus Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the sanction status into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MySanctionStatus value, in PacketWriter p)
    {
        foreach (Sanction sanction in value.Sanctions)
            Sanction.Validate(sanction, in p);
        p.WriteInt(value.Sanctions.Count);
        foreach (Sanction sanction in value.Sanctions)
            p.Compose(sanction);
    }
}

/// <summary>Specifies the layout of an account sanction status.</summary>
public enum AccountSanctionStatusKind
{
    /// <summary>A list of sanctions, the layout the Flash client uses.</summary>
    Sanctions
}

/// <summary>
/// Represents the <c>SanctionStatus</c> message, received with the sanctions recorded against the local user.
/// </summary>
public sealed record AccountSanctionStatus : IParserComposer<AccountSanctionStatus>
{
    /// <summary>Initializes a new instance of the <see cref="AccountSanctionStatus"/> record.</summary>
    /// <param name="sanctions">The sanctions recorded against the local user.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sanctions"/> is <see langword="null"/>.
    /// </exception>
    public AccountSanctionStatus(MySanctionStatus sanctions)
    {
        ArgumentNullException.ThrowIfNull(sanctions);
        Kind = AccountSanctionStatusKind.Sanctions;
        Sanctions = sanctions;
    }

    /// <summary>Gets the layout of the status, always <see cref="AccountSanctionStatusKind.Sanctions"/>.</summary>
    public AccountSanctionStatusKind Kind { get; }
    /// <summary>Gets the sanctions recorded against the local user.</summary>
    public MySanctionStatus? Sanctions { get; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AccountSanctionStatus Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AccountSanctionStatus ParseFlash(in PacketReader p) =>
        new(p.Parse<MySanctionStatus>());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AccountSanctionStatus value, in PacketWriter p)
    {
        if (value.Kind is not AccountSanctionStatusKind.Sanctions || value.Sanctions is null)
            throw new InvalidDataException("Flash sanction status requires the sanction-list variant.");
        value.Sanctions.Compose(in p);
    }
}

/// <summary>Represents the <c>FigureUpdate</c> message, received when the local user's figure changes.</summary>
/// <param name="Figure">The new figure string.</param>
/// <param name="Gender">The gender code sent with the figure.</param>
public sealed record FigureUpdate(string Figure, string Gender) : IParserComposer<FigureUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FigureUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FigureUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FigureUpdate value, in PacketWriter p)
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

/// <summary>
/// Represents the <c>ChangeUserNameResult</c> message, received with the result of a request to change the user name.
/// </summary>
public sealed record ChangeUserNameResult : IParserComposer<ChangeUserNameResult>
{
    private IReadOnlyList<string> _name_suggestions = Array.Empty<string>();

    /// <summary>Initializes a new instance of the <see cref="ChangeUserNameResult"/> record.</summary>
    /// <param name="resultCode">The result code sent by the server.</param>
    /// <param name="name">The name the result refers to.</param>
    /// <param name="nameSuggestions">The names suggested by the server, copied into a read only list.</param>
    public ChangeUserNameResult(
        int resultCode,
        string name,
        IReadOnlyList<string> nameSuggestions)
    {
        ResultCode = resultCode;
        Name = name;
        NameSuggestions = nameSuggestions;
    }

    /// <summary>The result code of a successful name change.</summary>
    public const int SuccessCode = 0;
    /// <summary>Gets the result code sent by the server.</summary>
    public int ResultCode { get; init; }
    /// <summary>Gets the name the result refers to.</summary>
    public string Name { get; init; }

    /// <summary>Gets the names suggested by the server, as a read only copy.</summary>
    public IReadOnlyList<string> NameSuggestions
    {
        get => _name_suggestions;
        init => _name_suggestions = AccountWire.FreezeStrings(value, nameof(NameSuggestions));
    }

    /// <summary>Gets whether the result code is <see cref="SuccessCode"/>.</summary>
    public bool Success => ResultCode == SuccessCode;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ChangeUserNameResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ChangeUserNameResult ParseFlash(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            AccountWire.ReadFlashStrings(in p, nameof(NameSuggestions)));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ChangeUserNameResult value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.ResultCode);
        p.WriteString(value.Name);
        AccountWire.WriteFlashStrings(in p, value.NameSuggestions);
    }

    private static void Validate(ChangeUserNameResult value, in PacketWriter p)
    {
        AccountWire.RequireString(value.Name, nameof(Name), in p);
        AccountWire.RequireStrings(value.NameSuggestions, nameof(NameSuggestions), in p);
    }
}

/// <summary>
/// Represents the <c>AccountSafetyLockStatusChange</c> message, received when the account safety lock status changes.
/// </summary>
/// <param name="Status">The raw status value, where 0 means the account is locked.</param>
public sealed record AccountSafetyLockStatusChange(int Status)
    : IParserComposer<AccountSafetyLockStatusChange>
{
    /// <summary>Gets whether the account safety lock is on, which is when <see cref="Status"/> is 0.</summary>
    public bool IsLocked => Status == 0;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AccountSafetyLockStatusChange Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AccountSafetyLockStatusChange ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

    public static Id[] ReadFlashIds(in PacketReader p, string name)
    {
        int count = ReadFlashCount(in p, p.Available, sizeof(int), name);
        var values = new Id[count];
        for (int i = 0; i < count; i++)
            values[i] = p.ReadInt();
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
