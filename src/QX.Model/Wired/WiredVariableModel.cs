using Qx.Messages;

namespace Qx.Model.Wired;

// A WiredContext entry — one of seven tagged variable structures. Compose mirrors the exact read.
/// <summary>Defines a value carried by an entry of a <see cref="WiredContext"/>.</summary>
public interface IWiredContextEntry
{
    /// <summary>Composes the value into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    void Compose(in PacketWriter p);
}

internal delegate T WiredVariableReader<T>(in PacketReader p, bool execution_context);

internal static class WiredVariableWire
{
    public static T ReadWhole<T>(in PacketReader p, WiredVariableReader<T> reader)
    {
        int start = p.Pos;
        try
        {
            T value = reader(in p, true);
            if (p.Available == 0)
                return value;
        }
        catch (Exception e) when (e is IndexOutOfRangeException or OverflowException
            or InvalidDataException or InvalidOperationException or ArgumentOutOfRangeException)
        {
        }
        p.Pos = start;
        try
        {
            T value = reader(in p, false);
            WiredWire.RequireEmpty(in p, nameof(WiredVariable));
            return value;
        }
        catch
        {
            p.Pos = start;
            throw;
        }
    }
}

// §7.8 — the leaf variable descriptor, appears throughout the context tree.
/// <summary>Represents the definition of a wired variable.</summary>
public sealed class WiredVariable : IParserComposer<WiredVariable>, IWiredContextEntry
{
    /// <summary>Gets the id of the variable.</summary>
    public string VariableId { get; init; } = "";
    /// <summary>Gets the variable type code.</summary>
    public int VariableType { get; init; }
    /// <summary>Gets the name of the variable, empty when it has none.</summary>
    public string VariableName { get; init; } = "";
    /// <summary>Gets the availability type code.</summary>
    /// <remarks>Values below 100 are stored, and 10, 11 and 20 are persisted.</remarks>
    public int AvailabilityType { get; init; }
    /// <summary>Gets the target code of the variable, one of the <see cref="WiredVariableTarget"/> values.</summary>
    public int VariableTarget { get; init; }
    /// <summary>Gets whether the variable is always available.</summary>
    public bool AlwaysAvailable { get; init; }
    /// <summary>Gets whether the variable can be created on and deleted from its holders.</summary>
    public bool CanCreateAndDelete { get; init; }
    /// <summary>Gets whether the variable carries a value.</summary>
    public bool HasValue { get; init; }
    /// <summary>Gets whether the value can be written.</summary>
    public bool CanWriteValue { get; init; }
    /// <summary>Gets whether changes to the variable can be intercepted.</summary>
    public bool CanInterceptChanges { get; init; }
    /// <summary>Gets whether the variable is invisible.</summary>
    public bool IsInvisible { get; init; }
    /// <summary>Gets whether the variable requires an execution context, or <see langword="null"/> when the packet uses the older layout.</summary>
    public bool? RequiresExecutionContext { get; init; }
    /// <summary>Gets whether the creation time of the variable can be read.</summary>
    public bool CanReadCreationTime { get; init; }
    /// <summary>Gets whether the last update time of the variable can be read.</summary>
    public bool CanReadLastUpdateTime { get; init; }
    // null = presence flag was false (no bytes); non-null (even empty) = flag true.
    /// <summary>Gets the text connector entries as key and text pairs, or <see langword="null"/> when the variable has no text connector.</summary>
    /// <remarks>An empty list means the variable has a text connector without entries.</remarks>
    public IReadOnlyList<KeyValuePair<Id, string>>? TextConnector { get; init; }
    /// <summary>Gets the text connector fallback text, or <see langword="null"/> when it has none.</summary>
    public string? TextConnectorFallback { get; init; }

    /// <summary>Gets whether the variable has a text connector.</summary>
    public bool HasTextConnector => TextConnector is not null;
    /// <summary>Gets whether the variable is stored, which is the case when <see cref="AvailabilityType"/> is below 100.</summary>
    public bool IsStored => AvailabilityType < 100;
    /// <summary>Gets whether the variable is persisted, which is the case when <see cref="AvailabilityType"/> is 10, 11 or 20.</summary>
    public bool IsPersisted => AvailabilityType is 10 or 11 or 20;
    /// <summary>Gets <see cref="VariableTarget"/> as a <see cref="WiredTarget"/> value.</summary>
    public WiredTarget Target => (WiredTarget)VariableTarget;
    /// <summary>Gets the display name of the variable, which is <see cref="VariableName"/>, or <see cref="VariableId"/> when the name is empty.</summary>
    public string Name => VariableName.Length > 0 ? VariableName : VariableId;
    /// <summary>Gets whether the value cannot be written.</summary>
    public bool IsReadOnly => !CanWriteValue;

    /// <summary>Parses the variable from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredVariable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredVariable ParseFlash(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static WiredVariable Read(in PacketReader p, bool execution_context) =>
        Read(in p, p.ReadString(), p.ReadInt(), execution_context);

    private static WiredVariable Read(
        in PacketReader p,
        string variable_id,
        int variable_type,
        bool execution_context)
    {
        string variableName = p.ReadString();
        int availabilityType = p.ReadInt();
        int variableTarget = p.ReadInt();
        bool alwaysAvailable = p.ReadBool();
        bool canCreateAndDelete = p.ReadBool();
        bool hasValue = p.ReadBool();
        bool canWriteValue = p.ReadBool();
        bool canInterceptChanges = p.ReadBool();
        bool isInvisible = p.ReadBool();
        bool? requiresExecutionContext = execution_context ? p.ReadBool() : null;
        bool canReadCreationTime = p.ReadBool();
        bool canReadLastUpdateTime = p.ReadBool();

        List<KeyValuePair<Id, string>>? connector = null;
        string? fallback = null;
        if (p.ReadBool())
        {
            int m = p.ReadLength();
            WiredWire.RequireBoundedCount(m, p.Available, 6, nameof(TextConnector));
            connector = new List<KeyValuePair<Id, string>>(m);
            for (int i = 0; i < m; i++)
                connector.Add(new KeyValuePair<Id, string>(p.ReadId(), p.ReadString()));
            if (execution_context && p.ReadBool())
                fallback = p.ReadString();
        }

        return new WiredVariable
        {
            VariableId = variable_id,
            VariableType = variable_type,
            VariableName = variableName,
            AvailabilityType = availabilityType,
            VariableTarget = variableTarget,
            AlwaysAvailable = alwaysAvailable,
            CanCreateAndDelete = canCreateAndDelete,
            HasValue = hasValue,
            CanWriteValue = canWriteValue,
            CanInterceptChanges = canInterceptChanges,
            IsInvisible = isInvisible,
            RequiresExecutionContext = requiresExecutionContext,
            CanReadCreationTime = canReadCreationTime,
            CanReadLastUpdateTime = canReadLastUpdateTime,
            TextConnector = connector,
            TextConnectorFallback = fallback
        };
    }

    /// <summary>Composes the variable into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredVariable value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.VariableId);
        p.WriteInt(value.VariableType);
        WriteCommon(value, in p);
    }

    private static void WriteCommon(WiredVariable value, in PacketWriter p)
    {
        p.WriteString(value.VariableName);
        p.WriteInt(value.AvailabilityType);
        p.WriteInt(value.VariableTarget);
        p.WriteBool(value.AlwaysAvailable);
        p.WriteBool(value.CanCreateAndDelete);
        p.WriteBool(value.HasValue);
        p.WriteBool(value.CanWriteValue);
        p.WriteBool(value.CanInterceptChanges);
        p.WriteBool(value.IsInvisible);
        if (value.RequiresExecutionContext is { } requires_execution_context)
            p.WriteBool(requires_execution_context);
        p.WriteBool(value.CanReadCreationTime);
        p.WriteBool(value.CanReadLastUpdateTime);
        p.WriteBool(value.TextConnector is not null);
        if (value.TextConnector is not null)
        {
            p.WriteLength((Length)value.TextConnector.Count);
            foreach (KeyValuePair<Id, string> kv in value.TextConnector)
            {
                p.WriteId(kv.Key);
                p.WriteString(kv.Value);
            }
            if (value.RequiresExecutionContext is not null)
            {
                p.WriteBool(value.TextConnectorFallback is not null);
                if (value.TextConnectorFallback is not null)
                    p.WriteString(value.TextConnectorFallback);
            }
        }
    }

    internal static void Validate(WiredVariable value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(value.VariableName, nameof(VariableName), in p);
        {
            WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        }

        if (value.TextConnectorFallback is not null)
        {
            if (value.TextConnector is null || value.RequiresExecutionContext is null)
                throw new InvalidDataException(
                    $"{nameof(TextConnectorFallback)} needs a text connector and {nameof(RequiresExecutionContext)}.");
            WiredWire.RequireString(value.TextConnectorFallback, nameof(TextConnectorFallback), in p);
        }

        if (value.TextConnector is null)
            return;
        foreach (KeyValuePair<Id, string> connector in value.TextConnector)
        {
            _ = WiredWire.FlashId(connector.Key);
            WiredWire.RequireString(connector.Value, nameof(TextConnector), in p);
        }
    }
}

// §7.7
/// <summary>Represents the value of a wired variable held by a furni item or a user.</summary>
/// <param name="ObjectId">The id of the furni item or user that holds the value, written as a 32 bit integer.</param>
/// <param name="Value">The variable value, written as a 32 bit integer.</param>
public readonly record struct ObjectIdAndValuePair(Id ObjectId, long Value) : IParserComposer<ObjectIdAndValuePair>
{
    /// <summary>Parses the pair from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ObjectIdAndValuePair Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ObjectIdAndValuePair ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the pair into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ObjectIdAndValuePair value, in PacketWriter p)
    {
        int object_id = WiredWire.FlashId(value.ObjectId);
        int stored_value = checked((int)value.Value);
        p.WriteInt(object_id);
        p.WriteInt(stored_value);
    }
}

// §7.1 — tag 0. Only a hash on the wire; the variable list is synchronised client-side (not transmitted).
/// <summary>Represents the hash of all variables in the room, carried by context tag 0.</summary>
/// <remarks>Only the hash is sent. The variable list itself is kept in sync by the variable messages.</remarks>
/// <param name="Hash">The hash of all variables in the room.</param>
public sealed record AllVariablesInRoom(int Hash) : IParserComposer<AllVariablesInRoom>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AllVariablesInRoom Parse(in PacketReader p) => new(p.ReadInt());
    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => p.WriteInt(Hash);
}

// §7.2 — tags 1 (furni) and 2 (user).
/// <summary>Represents a furni or user variable with the objects that hold it, carried by context tags 1 and 2.</summary>
/// <param name="Variable">The variable definition.</param>
/// <param name="Holders">The furni items or users that hold the variable, with their values.</param>
public sealed record VariableInfoAndHolders(WiredVariable Variable, IReadOnlyList<ObjectIdAndValuePair> Holders)
    : IParserComposer<VariableInfoAndHolders>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableInfoAndHolders Parse(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static VariableInfoAndHolders Read(in PacketReader p, bool execution_context)
    {
        WiredVariable variable = WiredVariable.Read(p, execution_context);
        int n = p.ReadLength();
        WiredWire.RequireBoundedCount(n, p.Available, 8, nameof(Holders));
        var holders = new ObjectIdAndValuePair[n];
        for (int i = 0; i < n; i++)
            holders[i] = p.Parse<ObjectIdAndValuePair>();
        return new VariableInfoAndHolders(variable, holders);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        variable_compose(p);
        p.WriteLength((Length)Holders.Count);
        foreach (ObjectIdAndValuePair h in Holders)
            p.Compose(h);
    }

    private void variable_compose(in PacketWriter p) => Variable.Compose(p);
}

// §7.3 — tag 3.
/// <summary>Represents a global variable with its value, carried by context tag 3.</summary>
/// <param name="Variable">The variable definition.</param>
/// <param name="Value">The value of the global variable, written as a 32 bit integer.</param>
public sealed record VariableInfoAndValue(WiredVariable Variable, long Value)
    : IParserComposer<VariableInfoAndValue>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableInfoAndValue Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static VariableInfoAndValue ParseFlash(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static VariableInfoAndValue Read(in PacketReader p, bool execution_context)
    {
        WiredVariable variable = WiredVariable.Read(p, execution_context);
        return new VariableInfoAndValue(variable, p.ReadInt());
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(VariableInfoAndValue value, in PacketWriter p)
    {
        int stored_value = checked((int)value.Value);
        value.Variable.Compose(in p);
        p.WriteInt(stored_value);
    }
}

// §7.4
/// <summary>Represents a wired variable shared from another room.</summary>
/// <param name="RoomId">The id of the room that shares the variable.</param>
/// <param name="RoomName">The name of the room that shares the variable.</param>
/// <param name="WiredVariable">The variable definition.</param>
public sealed record SharedVariable(Id RoomId, string RoomName, WiredVariable WiredVariable)
    : IParserComposer<SharedVariable>
{
    /// <summary>Parses the shared variable from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SharedVariable Parse(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static SharedVariable Read(in PacketReader p, bool execution_context)
    {
        Id roomId = p.ReadId();
        string roomName = p.ReadString();
        WiredVariable variable = WiredVariable.Read(p, execution_context);
        return new SharedVariable(roomId, roomName, variable);
    }

    /// <summary>Composes the shared variable into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteId(RoomId);
        p.WriteString(RoomName);
        WiredVariable.Compose(p);
    }
}

// §7.4 — tag 4.
/// <summary>Represents the variables other rooms share, carried by context tag 4.</summary>
/// <param name="SharedVariables">The shared variables.</param>
public sealed record SharedVariableList(IReadOnlyList<SharedVariable> SharedVariables)
    : IParserComposer<SharedVariableList>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SharedVariableList Parse(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static SharedVariableList Read(in PacketReader p, bool execution_context)
    {
        int n = p.ReadLength();
        WiredWire.RequireBoundedCount(n, p.Available, 31, nameof(SharedVariables));
        var items = new SharedVariable[n];
        for (int i = 0; i < n; i++)
            items[i] = SharedVariable.Read(p, execution_context);
        return new SharedVariableList(items);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)SharedVariables.Count);
        foreach (SharedVariable s in SharedVariables)
            p.Compose(s);
    }
}

// §7.5 — tag 5 (via createFromMessage).
/// <summary>Represents a list of variable definitions, carried by context tag 5.</summary>
/// <param name="Variables">The variable definitions.</param>
public sealed record VariableList(IReadOnlyList<WiredVariable> Variables)
    : IParserComposer<VariableList>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableList Parse(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static VariableList Read(in PacketReader p, bool execution_context)
    {
        int n = p.ReadLength();
        WiredWire.RequireBoundedCount(n, p.Available, 25, nameof(Variables));
        var items = new WiredVariable[n];
        for (int i = 0; i < n; i++)
            items[i] = WiredVariable.Read(p, execution_context);
        return new VariableList(items);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)Variables.Count);
        foreach (WiredVariable v in Variables)
            v.Compose(p);
    }
}

// §7.6
/// <summary>Represents a wired placeholder shared from another room.</summary>
/// <param name="RoomId">The id of the room that shares the placeholder.</param>
/// <param name="RoomName">The name of the room that shares the placeholder.</param>
/// <param name="PlaceholderName">The name of the placeholder.</param>
public sealed record SharedGlobalPlaceholder(Id RoomId, string RoomName, string PlaceholderName)
    : IParserComposer<SharedGlobalPlaceholder>
{
    /// <summary>Parses the shared placeholder from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SharedGlobalPlaceholder Parse(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadString());

    /// <summary>Composes the shared placeholder into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteId(RoomId);
        p.WriteString(RoomName);
        p.WriteString(PlaceholderName);
    }
}

// §7.6 — tag 6.
/// <summary>Represents the placeholders other rooms share, carried by context tag 6.</summary>
/// <param name="SharedPlaceholders">The shared placeholders.</param>
public sealed record SharedGlobalPlaceholderList(IReadOnlyList<SharedGlobalPlaceholder> SharedPlaceholders)
    : IParserComposer<SharedGlobalPlaceholderList>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SharedGlobalPlaceholderList Parse(in PacketReader p)
    {
        int n = p.ReadLength();
        var items = new SharedGlobalPlaceholder[n];
        for (int i = 0; i < n; i++)
            items[i] = p.Parse<SharedGlobalPlaceholder>();
        return new SharedGlobalPlaceholderList(items);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)SharedPlaceholders.Count);
        foreach (SharedGlobalPlaceholder s in SharedPlaceholders)
            p.Compose(s);
    }
}

// §7.10
/// <summary>Represents a variable a variable leaderboard can rank users by.</summary>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="TechnicalName">The technical name of the variable.</param>
/// <param name="Persistent">Whether the variable is persisted.</param>
/// <param name="HasValue">Whether the variable carries a value.</param>
/// <param name="ApplicableLevelUpCount">The number of level-up variables that apply to the variable.</param>
public sealed record VariableLeaderboardChoice(
    string VariableId,
    string TechnicalName,
    bool Persistent,
    bool HasValue,
    int ApplicableLevelUpCount) : IParserComposer<VariableLeaderboardChoice>
{
    /// <summary>Parses the choice from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableLeaderboardChoice Parse(in PacketReader p) =>
        new(p.ReadString(), p.ReadString(), p.ReadBool(), p.ReadBool(), p.ReadInt());

    /// <summary>Composes the choice into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteString(VariableId);
        p.WriteString(TechnicalName);
        p.WriteBool(Persistent);
        p.WriteBool(HasValue);
        p.WriteInt(ApplicableLevelUpCount);
    }
}

// §7.10 — tag 8.
/// <summary>Represents the state of a variable leaderboard add-on, carried by context tag 8.</summary>
/// <param name="OverlayInstanceId">The id of the published leaderboard overlay, or 0 when it is not published.</param>
/// <param name="TileX">The x coordinate of the tile the add-on stands on.</param>
/// <param name="TileY">The y coordinate of the tile the add-on stands on.</param>
/// <param name="TotalCandidateCount">The number of variables that could be ranked.</param>
/// <param name="Choices">The variables the leaderboard can rank by.</param>
public sealed record VariableLeaderboardContext(
    int OverlayInstanceId,
    int TileX,
    int TileY,
    int TotalCandidateCount,
    IReadOnlyList<VariableLeaderboardChoice> Choices)
    : IParserComposer<VariableLeaderboardContext>, IWiredContextEntry
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableLeaderboardContext Parse(in PacketReader p)
    {
        int overlayInstanceId = p.ReadInt();
        int tileX = p.ReadInt();
        int tileY = p.ReadInt();
        int totalCandidateCount = p.ReadInt();
        int n = p.ReadLength();
        WiredWire.RequireBoundedCount(n, p.Available, 10, nameof(Choices));
        var choices = new VariableLeaderboardChoice[n];
        for (int i = 0; i < n; i++)
            choices[i] = p.Parse<VariableLeaderboardChoice>();
        return new VariableLeaderboardContext(overlayInstanceId, tileX, tileY, totalCandidateCount, choices);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(OverlayInstanceId);
        p.WriteInt(TileX);
        p.WriteInt(TileY);
        p.WriteInt(TotalCandidateCount);
        p.WriteLength((Length)Choices.Count);
        foreach (VariableLeaderboardChoice choice in Choices)
            p.Compose(choice);
    }
}

// §6 — the tagged-union variables container inlined into every wired config.
/// <summary>Represents one tagged entry of a <see cref="WiredContext"/>.</summary>
/// <param name="Tag">The context tag, one of the tag constants of <see cref="WiredContext"/>.</param>
/// <param name="Value">The value the tag carries.</param>
public sealed record WiredContextEntry(int Tag, IWiredContextEntry Value);

public sealed record WiredTextPlaceholders(IReadOnlyList<string> Names, IReadOnlyList<string> CapturerNames)
    : IParserComposer<WiredTextPlaceholders>, IWiredContextEntry
{
    public WiredTextPlaceholders(IReadOnlyList<string> names) : this(names, []) { }

    public static WiredTextPlaceholders Parse(in PacketReader p) =>
        new(read_names(in p), read_names(in p));

    private static string[] read_names(in PacketReader p)
    {
        int count = p.ReadLength();
        WiredWire.RequireBoundedCount(count, p.Available, 2, nameof(Names));
        var names = new string[count];
        for (int index = 0; index < names.Length; index++) names[index] = p.ReadString();
        return names;
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteStringArray(Names);
        p.WriteStringArray(CapturerNames);
    }
}

/// <summary>Represents the variable context the hotel sends with a wired configuration.</summary>
/// <remarks>The context is a list of tagged entries. When a tag appears more than once, the typed properties return the last entry.</remarks>
/// <param name="Entries">The context entries in wire order.</param>
public sealed record WiredContext(IReadOnlyList<WiredContextEntry> Entries) : IParserComposer<WiredContext>
{
    /// <summary>Gets a context without entries.</summary>
    public static WiredContext Empty { get; } = new([]);

    /// <summary>The tag of an <see cref="AllVariablesInRoom"/> entry.</summary>
    public const int TagRoomVariables = 0;
    /// <summary>The tag of a <see cref="VariableInfoAndHolders"/> entry for a furni variable.</summary>
    public const int TagFurniVariableInfo = 1;
    /// <summary>The tag of a <see cref="VariableInfoAndHolders"/> entry for a user variable.</summary>
    public const int TagUserVariableInfo = 2;
    /// <summary>The tag of a <see cref="VariableInfoAndValue"/> entry.</summary>
    public const int TagGlobalVariableInfo = 3;
    /// <summary>The tag of a <see cref="SharedVariableList"/> entry.</summary>
    public const int TagReferenceVariables = 4;
    /// <summary>The tag of a <see cref="VariableList"/> entry.</summary>
    public const int TagRulesetVariables = 5;
    /// <summary>The tag of a <see cref="SharedGlobalPlaceholderList"/> entry.</summary>
    public const int TagReferencePlaceholders = 6;
    /// <summary>The tag of a <see cref="WiredTextPlaceholders"/> entry.</summary>
    public const int TagTextPlaceholders = 7;
    /// <summary>The tag of a <see cref="VariableLeaderboardContext"/> entry.</summary>
    public const int TagVariableLeaderboard = 8;

    /// <summary>Gets the last room variables entry, or <see langword="null"/> when there is none.</summary>
    public AllVariablesInRoom? RoomVariables => Last<AllVariablesInRoom>(TagRoomVariables);
    /// <summary>Gets the last furni variable entry, or <see langword="null"/> when there is none.</summary>
    public VariableInfoAndHolders? FurniVariableInfo => Last<VariableInfoAndHolders>(TagFurniVariableInfo);
    /// <summary>Gets the last user variable entry, or <see langword="null"/> when there is none.</summary>
    public VariableInfoAndHolders? UserVariableInfo => Last<VariableInfoAndHolders>(TagUserVariableInfo);
    /// <summary>Gets the last global variable entry, or <see langword="null"/> when there is none.</summary>
    public VariableInfoAndValue? GlobalVariableInfo => Last<VariableInfoAndValue>(TagGlobalVariableInfo);
    /// <summary>Gets the last shared variables entry, or <see langword="null"/> when there is none.</summary>
    public SharedVariableList? ReferenceVariables => Last<SharedVariableList>(TagReferenceVariables);
    /// <summary>Gets the last variable definitions entry, or <see langword="null"/> when there is none.</summary>
    public VariableList? RulesetVariables => Last<VariableList>(TagRulesetVariables);
    /// <summary>Gets the last shared placeholders entry, or <see langword="null"/> when there is none.</summary>
    public SharedGlobalPlaceholderList? ReferencePlaceholders => Last<SharedGlobalPlaceholderList>(TagReferencePlaceholders);
    /// <summary>Gets the last text placeholders entry, or <see langword="null"/> when there is none.</summary>
    public WiredTextPlaceholders? TextPlaceholders => Last<WiredTextPlaceholders>(TagTextPlaceholders);
    /// <summary>Gets the last variable leaderboard entry, or <see langword="null"/> when there is none.</summary>
    public VariableLeaderboardContext? VariableLeaderboard => Last<VariableLeaderboardContext>(TagVariableLeaderboard);

    private T? Last<T>(int tag) where T : class
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
            if (Entries[i].Tag == tag && Entries[i].Value is T typed)
                return typed;
        return null;
    }

    /// <summary>Parses the context from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <exception cref="InvalidOperationException">Thrown when an entry has an unknown tag.</exception>
    public static WiredContext Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredContext ParseFlash(in PacketReader p) => WiredVariableWire.ReadWhole(in p, Read);

    internal static WiredContext Read(in PacketReader p, bool execution_context)
    {
        int count = p.ReadLength();
        WiredWire.RequireBoundedCount(count, p.Available, 4, nameof(Entries));
        var entries = new WiredContextEntry[count];
        for (int i = 0; i < count; i++)
        {
            int tag = p.ReadInt();
            IWiredContextEntry value = tag switch
            {
                TagRoomVariables => AllVariablesInRoom.Parse(p),
                TagFurniVariableInfo or TagUserVariableInfo => VariableInfoAndHolders.Read(p, execution_context),
                TagGlobalVariableInfo => VariableInfoAndValue.Read(p, execution_context),
                TagReferenceVariables => SharedVariableList.Read(p, execution_context),
                TagRulesetVariables => VariableList.Read(p, execution_context),
                TagReferencePlaceholders => SharedGlobalPlaceholderList.Parse(p),
                TagTextPlaceholders => WiredTextPlaceholders.Parse(p),
                TagVariableLeaderboard => VariableLeaderboardContext.Parse(p),
                _ => throw new InvalidOperationException($"Unknown WiredContext tag {tag} — stream would desync.")
            };
            entries[i] = new WiredContextEntry(tag, value);
        }
        return new WiredContext(entries);
    }

    /// <summary>Composes the context into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when an entry's value does not match its tag.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredContext value, in PacketWriter p) =>
        Write(value, in p);

    private static void Write(WiredContext value, in PacketWriter p)
    {
        value.Validate(in p);
        p.WriteLength((Length)value.Entries.Count);
        foreach (WiredContextEntry e in value.Entries)
        {
            p.WriteInt(e.Tag);
            e.Value.Compose(p);
        }
    }

    internal void Validate(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(Entries);
        foreach (WiredContextEntry entry in Entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            switch (entry.Tag, entry.Value)
            {
                case (TagRoomVariables, AllVariablesInRoom):
                    break;
                case (TagFurniVariableInfo or TagUserVariableInfo, VariableInfoAndHolders holders):
                    ValidateVariableInfoAndHolders(holders, in p);
                    break;
                case (TagGlobalVariableInfo, VariableInfoAndValue global):
                    WiredVariable.Validate(global.Variable, in p);
                    _ = checked((int)global.Value);
                    break;
                case (TagReferenceVariables, SharedVariableList shared):
                    ; foreach (SharedVariable variable in shared.SharedVariables)
                    {
                        ArgumentNullException.ThrowIfNull(variable);
                        _ = WiredWire.FlashId(variable.RoomId);
                        WiredWire.RequireString(variable.RoomName, nameof(variable.RoomName), in p);
                        WiredVariable.Validate(variable.WiredVariable, in p);
                    }
                    break;
                case (TagRulesetVariables, VariableList variables):
                    ; foreach (WiredVariable variable in variables.Variables)
                        WiredVariable.Validate(variable, in p);
                    break;
                case (TagReferencePlaceholders, SharedGlobalPlaceholderList placeholders):
                    ; foreach (SharedGlobalPlaceholder placeholder in placeholders.SharedPlaceholders)
                    {
                        ArgumentNullException.ThrowIfNull(placeholder);
                        _ = WiredWire.FlashId(placeholder.RoomId);
                        WiredWire.RequireString(placeholder.RoomName, nameof(placeholder.RoomName), in p);
                        WiredWire.RequireString(placeholder.PlaceholderName, nameof(placeholder.PlaceholderName), in p);
                    }
                    break;
                case (TagTextPlaceholders, WiredTextPlaceholders text):
                    ArgumentNullException.ThrowIfNull(text.Names);
                    ArgumentNullException.ThrowIfNull(text.CapturerNames);
                    foreach (string name in text.Names)
                        WiredWire.RequireString(name, nameof(text.Names), in p);
                    foreach (string name in text.CapturerNames)
                        WiredWire.RequireString(name, nameof(text.CapturerNames), in p);
                    break;
                case (TagVariableLeaderboard, VariableLeaderboardContext leaderboard):
                    ArgumentNullException.ThrowIfNull(leaderboard.Choices);
                    foreach (VariableLeaderboardChoice choice in leaderboard.Choices)
                    {
                        ArgumentNullException.ThrowIfNull(choice);
                        WiredWire.RequireString(choice.VariableId, nameof(choice.VariableId), in p);
                        WiredWire.RequireString(choice.TechnicalName, nameof(choice.TechnicalName), in p);
                    }
                    break;
                default:
                    throw new InvalidDataException($"Wired context tag {entry.Tag} has an incompatible value.");
            }
        }
    }

    private static void ValidateVariableInfoAndHolders(
        VariableInfoAndHolders value,
        in PacketWriter p)
    {
        WiredVariable.Validate(value.Variable, in p);
        ArgumentNullException.ThrowIfNull(value.Holders);
        foreach (ObjectIdAndValuePair holder in value.Holders)
        {
            _ = WiredWire.FlashId(holder.ObjectId);
            _ = checked((int)holder.Value);
        }
    }
}
