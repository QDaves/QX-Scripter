using Qx.Messages;

namespace Qx.Model.Wired;

// A WiredContext entry — one of seven tagged variable structures. Compose mirrors the exact read.
public interface IWiredContextEntry
{
    void Compose(in PacketWriter p);
}

// §7.8 — the leaf variable descriptor, appears throughout the context tree.
public sealed class WiredVariable : IParserComposer<WiredVariable>, IWiredContextEntry
{
    public string VariableId { get; init; } = "";
    public int VariableType { get; init; }
    public string VariableName { get; init; } = "";
    public int AvailabilityType { get; init; }
    public int VariableTarget { get; init; }
    public bool AlwaysAvailable { get; init; }
    public bool CanCreateAndDelete { get; init; }
    public bool HasValue { get; init; }
    public bool CanWriteValue { get; init; }
    public bool CanInterceptChanges { get; init; }
    public bool IsInvisible { get; init; }
    public bool CanReadCreationTime { get; init; }
    public bool CanReadLastUpdateTime { get; init; }
    // null = presence flag was false (no bytes); non-null (even empty) = flag true.
    public IReadOnlyList<KeyValuePair<Id, string>>? TextConnector { get; init; }

    public bool HasTextConnector => TextConnector is not null;
    public bool IsStored => AvailabilityType < 100;
    public bool IsPersisted => AvailabilityType is 10 or 11 or 20;
    public WiredTarget Target => (WiredTarget)VariableTarget;
    public string Name => VariableName.Length > 0 ? VariableName : VariableId;
    public bool IsReadOnly => !CanWriteValue;

    public static WiredVariable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredVariable ParseFlash(in PacketReader p) =>
        Read(in p, p.ReadString(), p.ReadInt());

    private static WiredVariable Read(
        in PacketReader p,
        string variable_id,
        int variable_type)
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
        bool canReadCreationTime = p.ReadBool();
        bool canReadLastUpdateTime = p.ReadBool();

        List<KeyValuePair<Id, string>>? connector = null;
        if (p.ReadBool())
        {
            int m = p.ReadLength();
            connector = new List<KeyValuePair<Id, string>>(m);
            for (int i = 0; i < m; i++)
                connector.Add(new KeyValuePair<Id, string>(p.ReadId(), p.ReadString()));
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
            CanReadCreationTime = canReadCreationTime,
            CanReadLastUpdateTime = canReadLastUpdateTime,
            TextConnector = connector
        };
    }

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
        }
    }

    internal static void Validate(WiredVariable value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(value.VariableName, nameof(VariableName), in p);
        {
            WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
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
public readonly record struct ObjectIdAndValuePair(Id ObjectId, long Value) : IParserComposer<ObjectIdAndValuePair>
{
    public static ObjectIdAndValuePair Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ObjectIdAndValuePair ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

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
public sealed record AllVariablesInRoom(int Hash) : IParserComposer<AllVariablesInRoom>, IWiredContextEntry
{
    public static AllVariablesInRoom Parse(in PacketReader p) => new(p.ReadInt());
    public void Compose(in PacketWriter p) => p.WriteInt(Hash);
}

// §7.2 — tags 1 (furni) and 2 (user).
public sealed record VariableInfoAndHolders(WiredVariable Variable, IReadOnlyList<ObjectIdAndValuePair> Holders)
    : IParserComposer<VariableInfoAndHolders>, IWiredContextEntry
{
    public static VariableInfoAndHolders Parse(in PacketReader p)
    {
        WiredVariable variable = WiredVariable.Parse(p);
        int n = p.ReadLength();
        var holders = new ObjectIdAndValuePair[n];
        for (int i = 0; i < n; i++)
            holders[i] = p.Parse<ObjectIdAndValuePair>();
        return new VariableInfoAndHolders(variable, holders);
    }

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
public sealed record VariableInfoAndValue(WiredVariable Variable, long Value)
    : IParserComposer<VariableInfoAndValue>, IWiredContextEntry
{
    public static VariableInfoAndValue Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static VariableInfoAndValue ParseFlash(in PacketReader p)
    {
        WiredVariable variable = WiredVariable.Parse(p);
        return new VariableInfoAndValue(variable, p.ReadInt());
    }

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
public sealed record SharedVariable(Id RoomId, string RoomName, WiredVariable WiredVariable)
    : IParserComposer<SharedVariable>
{
    public static SharedVariable Parse(in PacketReader p)
    {
        Id roomId = p.ReadId();
        string roomName = p.ReadString();
        WiredVariable variable = WiredVariable.Parse(p);
        return new SharedVariable(roomId, roomName, variable);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteId(RoomId);
        p.WriteString(RoomName);
        WiredVariable.Compose(p);
    }
}

// §7.4 — tag 4.
public sealed record SharedVariableList(IReadOnlyList<SharedVariable> SharedVariables)
    : IParserComposer<SharedVariableList>, IWiredContextEntry
{
    public static SharedVariableList Parse(in PacketReader p)
    {
        int n = p.ReadLength();
        var items = new SharedVariable[n];
        for (int i = 0; i < n; i++)
            items[i] = p.Parse<SharedVariable>();
        return new SharedVariableList(items);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)SharedVariables.Count);
        foreach (SharedVariable s in SharedVariables)
            p.Compose(s);
    }
}

// §7.5 — tag 5 (via createFromMessage).
public sealed record VariableList(IReadOnlyList<WiredVariable> Variables)
    : IParserComposer<VariableList>, IWiredContextEntry
{
    public static VariableList Parse(in PacketReader p)
    {
        int n = p.ReadLength();
        var items = new WiredVariable[n];
        for (int i = 0; i < n; i++)
            items[i] = WiredVariable.Parse(p);
        return new VariableList(items);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)Variables.Count);
        foreach (WiredVariable v in Variables)
            v.Compose(p);
    }
}

// §7.6
public sealed record SharedGlobalPlaceholder(Id RoomId, string RoomName, string PlaceholderName)
    : IParserComposer<SharedGlobalPlaceholder>
{
    public static SharedGlobalPlaceholder Parse(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteId(RoomId);
        p.WriteString(RoomName);
        p.WriteString(PlaceholderName);
    }
}

// §7.6 — tag 6.
public sealed record SharedGlobalPlaceholderList(IReadOnlyList<SharedGlobalPlaceholder> SharedPlaceholders)
    : IParserComposer<SharedGlobalPlaceholderList>, IWiredContextEntry
{
    public static SharedGlobalPlaceholderList Parse(in PacketReader p)
    {
        int n = p.ReadLength();
        var items = new SharedGlobalPlaceholder[n];
        for (int i = 0; i < n; i++)
            items[i] = p.Parse<SharedGlobalPlaceholder>();
        return new SharedGlobalPlaceholderList(items);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)SharedPlaceholders.Count);
        foreach (SharedGlobalPlaceholder s in SharedPlaceholders)
            p.Compose(s);
    }
}

// §6 — the tagged-union variables container inlined into every wired config.
public sealed record WiredContextEntry(int Tag, IWiredContextEntry Value);

public sealed record WiredContext(IReadOnlyList<WiredContextEntry> Entries) : IParserComposer<WiredContext>
{
    public static WiredContext Empty { get; } = new([]);

    public const int TagRoomVariables = 0;
    public const int TagFurniVariableInfo = 1;
    public const int TagUserVariableInfo = 2;
    public const int TagGlobalVariableInfo = 3;
    public const int TagReferenceVariables = 4;
    public const int TagRulesetVariables = 5;
    public const int TagReferencePlaceholders = 6;

    public AllVariablesInRoom? RoomVariables => Last<AllVariablesInRoom>(TagRoomVariables);
    public VariableInfoAndHolders? FurniVariableInfo => Last<VariableInfoAndHolders>(TagFurniVariableInfo);
    public VariableInfoAndHolders? UserVariableInfo => Last<VariableInfoAndHolders>(TagUserVariableInfo);
    public VariableInfoAndValue? GlobalVariableInfo => Last<VariableInfoAndValue>(TagGlobalVariableInfo);
    public SharedVariableList? ReferenceVariables => Last<SharedVariableList>(TagReferenceVariables);
    public VariableList? RulesetVariables => Last<VariableList>(TagRulesetVariables);
    public SharedGlobalPlaceholderList? ReferencePlaceholders => Last<SharedGlobalPlaceholderList>(TagReferencePlaceholders);

    private T? Last<T>(int tag) where T : class
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
            if (Entries[i].Tag == tag && Entries[i].Value is T typed)
                return typed;
        return null;
    }

    public static WiredContext Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredContext ParseFlash(in PacketReader p) => Read(in p);

    private static WiredContext Read(in PacketReader p)
    {
        int count = p.ReadLength();
        var entries = new WiredContextEntry[count];
        for (int i = 0; i < count; i++)
        {
            int tag = p.ReadInt();
            IWiredContextEntry value = tag switch
            {
                TagRoomVariables => AllVariablesInRoom.Parse(p),
                TagFurniVariableInfo or TagUserVariableInfo => VariableInfoAndHolders.Parse(p),
                TagGlobalVariableInfo => VariableInfoAndValue.Parse(p),
                TagReferenceVariables => SharedVariableList.Parse(p),
                TagRulesetVariables => VariableList.Parse(p),
                TagReferencePlaceholders => SharedGlobalPlaceholderList.Parse(p),
                _ => throw new InvalidOperationException($"Unknown WiredContext tag {tag} — stream would desync.")
            };
            entries[i] = new WiredContextEntry(tag, value);
        }
        return new WiredContext(entries);
    }

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
