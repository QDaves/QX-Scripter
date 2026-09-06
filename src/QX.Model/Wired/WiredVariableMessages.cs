using Qx.Messages;

namespace Qx.Model.Wired;

// Operation codes shared by the two variable set/create/delete composers (689 / 625).
public static class WiredVariableOperation
{
    public const int Write = 0;
    public const int Create = 1;
    public const int Delete = 2;
}

// Object inspection discriminator values, matching the response union and the outgoing request.
public static class WiredVariableTarget
{
    public const int Furni = 0;
    public const int User = 1;
    public const int Merged = 2;
    public const int Global = -10;
    public const int Context = -20;
}

// One persisted storage slot for a variable on an entity. Context-polymorphic: the leading
// variableId is present only when the caller asked for it (true in 1557, false in 749) — decided
// by calling context, never by a wire tag. The two timestamps are 8-byte longs.
public sealed record WiredVariableStorageParameter(
    bool IncludesVariableId,
    string? VariableId,
    int Value,
    long CreationTime,
    string CreationTimeStr,
    long LastUpdateTime,
    string LastUpdateTimeStr) : IComposer
{
    public static WiredVariableStorageParameter Parse(in PacketReader p, bool includeVariableId)
    {
        string? variableId = includeVariableId ? p.ReadString() : null;
        int value = p.ReadInt();
        long creationTime = p.ReadLong();
        string creationTimeStr = p.ReadString();
        long lastUpdateTime = p.ReadLong();
        string lastUpdateTimeStr = p.ReadString();
        return new WiredVariableStorageParameter(
            includeVariableId, variableId, value,
            creationTime, creationTimeStr, lastUpdateTime, lastUpdateTimeStr);
    }

    public void Compose(in PacketWriter p)
    {
        if (IncludesVariableId)
            p.WriteString(VariableId ?? "");
        p.WriteInt(Value);
        p.WriteLong(CreationTime);
        p.WriteString(CreationTimeStr);
        p.WriteLong(LastUpdateTime);
        p.WriteString(LastUpdateTimeStr);
    }
}

// Leading throwaway int (read + discarded by the client) is retained so recompose is byte-exact.
public sealed record WiredAllVariableHolders(int LeadingValue, VariableInfoAndHolders VariableInfoAndHolders)
    : IParserComposer<WiredAllVariableHolders>
{
    public static WiredAllVariableHolders Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredAllVariableHolders ParseFlash(in PacketReader p)
    {
        int leading = p.ReadInt();
        VariableInfoAndHolders info = p.Parse<VariableInfoAndHolders>();
        return new WiredAllVariableHolders(leading, info);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAllVariableHolders value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.VariableInfoAndHolders);
        WiredVariable.Validate(value.VariableInfoAndHolders.Variable, in p, false);
        ArgumentNullException.ThrowIfNull(value.VariableInfoAndHolders.Holders);
        WiredWire.RequireUnityCount(
            value.VariableInfoAndHolders.Holders.Count,
            nameof(value.VariableInfoAndHolders.Holders));
        foreach (ObjectIdAndValuePair holder in value.VariableInfoAndHolders.Holders)
        {
            _ = WiredWire.FlashId(holder.ObjectId);
            _ = checked((int)holder.Value);
        }
        p.WriteInt(value.LeadingValue);
        p.Compose(value.VariableInfoAndHolders);
    }
}

// perVariableHash is read BEFORE its WiredVariable, then stored map[variable]=hash; kept as an
// ordered pair list so the chunk recomposes byte-for-byte. Chunked via IsLastChunk.
public sealed record WiredVariableWithHash(int PerVariableHash, WiredVariable Variable)
    : IParserComposer<WiredVariableWithHash>
{
    public static WiredVariableWithHash Parse(in PacketReader p)
    {
        int hash = p.ReadInt();
        WiredVariable variable = WiredVariable.Parse(p);
        return new WiredVariableWithHash(hash, variable);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(PerVariableHash);
        Variable.Compose(p);
    }
}

public sealed record WiredAllVariablesDiffs(
    int AllVariablesHash,
    bool IsLastChunk,
    IReadOnlyList<string> RemovedVariables,
    IReadOnlyList<WiredVariableWithHash> AddedOrUpdated) : IParserComposer<WiredAllVariablesDiffs>
{
    public static WiredAllVariablesDiffs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredAllVariablesDiffs ParseFlash(in PacketReader p)
    {
        int hash = p.ReadInt();
        bool isLastChunk = p.ReadBool();

        int removedCount = p.ReadInt();
        WiredWire.RequireBoundedCount(
            removedCount,
            p.Available,
            2,
            nameof(RemovedVariables));
        var removed = new string[removedCount];
        for (int i = 0; i < removedCount; i++)
            removed[i] = p.ReadString();

        int addedCount = p.ReadInt();
        WiredWire.RequireBoundedCount(
            addedCount,
            p.Available,
            29,
            nameof(AddedOrUpdated));
        var added = new WiredVariableWithHash[addedCount];
        for (int i = 0; i < addedCount; i++)
            added[i] = p.Parse<WiredVariableWithHash>();

        return new WiredAllVariablesDiffs(hash, isLastChunk, removed, added);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAllVariablesDiffs value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RemovedVariables);
        ArgumentNullException.ThrowIfNull(value.AddedOrUpdated);
        foreach (string variable_id in value.RemovedVariables)
            WiredWire.RequireString(variable_id, nameof(RemovedVariables), in p);
        foreach (WiredVariableWithHash item in value.AddedOrUpdated)
        {
            ArgumentNullException.ThrowIfNull(item);
            WiredVariable.Validate(item.Variable, in p, false);
        }
        p.WriteInt(value.AllVariablesHash);
        p.WriteBool(value.IsLastChunk);

        p.WriteInt(value.RemovedVariables.Count);
        foreach (string id in value.RemovedVariables)
            p.WriteString(id);

        p.WriteInt(value.AddedOrUpdated.Count);
        foreach (WiredVariableWithHash item in value.AddedOrUpdated)
            p.Compose(item);
    }
}

public sealed record WiredAllVariablesHash(int AllVariablesHash) : IParserComposer<WiredAllVariablesHash>
{
    public static WiredAllVariablesHash Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredAllVariablesHash ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAllVariablesHash value, in PacketWriter p) =>
        p.WriteInt(value.AllVariablesHash);
}

public sealed record WiredUserVariablesElement(
    int EntityType, int EntityId, string EntityName, WiredVariableStorageParameter Storage)
    : IParserComposer<WiredUserVariablesElement>
{
    public static WiredUserVariablesElement Parse(in PacketReader p)
    {
        int entityType = p.ReadInt();
        int entityId = p.ReadInt();
        string entityName = p.ReadString();
        WiredVariableStorageParameter storage = WiredVariableStorageParameter.Parse(p, false);
        return new WiredUserVariablesElement(entityType, entityId, entityName, storage);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(EntityType);
        p.WriteInt(EntityId);
        p.WriteString(EntityName);
        Storage.Compose(p);
    }
}

public sealed record WiredUserVariablesPage(
    string VariableId,
    int TotalEntries,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredUserVariablesElement> Elements,
    int UserTypeFilter,
    int SortTypeFilter) : IParserComposer<WiredUserVariablesPage>
{
    public static WiredUserVariablesPage Parse(in PacketReader p)
    {
        string variableId = p.ReadString();
        int totalEntries = p.ReadInt();
        int currentPage = p.ReadInt();
        int amount = p.ReadInt();

        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 34, nameof(Elements));
        var elements = new WiredUserVariablesElement[n];
        for (int i = 0; i < n; i++)
            elements[i] = p.Parse<WiredUserVariablesElement>();

        int userTypeFilter = p.ReadInt();
        int sortTypeFilter = p.ReadInt();
        return new WiredUserVariablesPage(
            variableId, totalEntries, currentPage, amount, elements, userTypeFilter, sortTypeFilter);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteString(VariableId);
        p.WriteInt(TotalEntries);
        p.WriteInt(CurrentPage);
        p.WriteInt(Amount);

        p.WriteInt(Elements.Count);
        foreach (WiredUserVariablesElement e in Elements)
            p.Compose(e);

        p.WriteInt(UserTypeFilter);
        p.WriteInt(SortTypeFilter);
    }
}

public sealed record WiredUserVariablesList(WiredUserVariablesPage Page)
    : IParserComposer<WiredUserVariablesList>
{
    public static WiredUserVariablesList Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredUserVariablesList ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredUserVariablesPage>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUserVariablesList value, in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(value.Page, in p);
        p.Compose(value.Page);
    }
}

// Owner trio (ownerId/ownerName/ownerFigure) present only when entityType != 1. Storage params
// here carry the variableId (param2 = true).
public sealed record WiredUserPermanentVariablesList(
    int EntityType,
    int EntityId,
    string EntityName,
    string EntityFigure,
    int OwnerId,
    string? OwnerName,
    string? OwnerFigure,
    IReadOnlyList<WiredVariableStorageParameter> VariableStorage)
    : IParserComposer<WiredUserPermanentVariablesList>
{
    public bool HasOwner => EntityType != 1;

    public static WiredUserPermanentVariablesList Parse(in PacketReader p)
    {
        int entityType = p.ReadInt();
        int entityId = p.ReadInt();
        string entityName = p.ReadString();
        string entityFigure = p.ReadString();

        int ownerId = 0;
        string? ownerName = null;
        string? ownerFigure = null;
        if (entityType != 1)
        {
            ownerId = p.ReadInt();
            ownerName = p.ReadString();
            ownerFigure = p.ReadString();
        }

        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 26, nameof(VariableStorage));
        var storage = new WiredVariableStorageParameter[n];
        for (int i = 0; i < n; i++)
            storage[i] = WiredVariableStorageParameter.Parse(p, true);

        return new WiredUserPermanentVariablesList(
            entityType, entityId, entityName, entityFigure, ownerId, ownerName, ownerFigure, storage);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(EntityType);
        p.WriteInt(EntityId);
        p.WriteString(EntityName);
        p.WriteString(EntityFigure);

        if (EntityType != 1)
        {
            p.WriteInt(OwnerId);
            p.WriteString(OwnerName ?? "");
            p.WriteString(OwnerFigure ?? "");
        }

        p.WriteInt(VariableStorage.Count);
        foreach (WiredVariableStorageParameter s in VariableStorage)
            s.Compose(p);
    }
}

public sealed record WiredUserPermanentVariables(WiredUserPermanentVariablesList List)
    : IParserComposer<WiredUserPermanentVariables>
{
    public static WiredUserPermanentVariables Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredUserPermanentVariables ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredUserPermanentVariablesList>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUserPermanentVariables value, in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(value.List, in p);
        p.Compose(value.List);
    }
}

// Three-way union on type: only type 0 (object) reads objectId + the trailing configuredInWireds
// vector; type 1 (user) reads userIndex; type -10 (global) reads neither id nor trailing vector.
public sealed record WiredObjectInspectionData(
    int Type,
    int ObjectId,
    int UserIndex,
    IReadOnlyList<KeyValuePair<string, int>> VariableValues,
    IReadOnlyList<int>? ConfiguredInWireds) : IParserComposer<WiredObjectInspectionData>
{
    public static WiredObjectInspectionData Parse(in PacketReader p)
    {
        int type = p.ReadInt();

        int objectId = 0;
        int userIndex = 0;
        if (type == WiredVariableTarget.Furni)
            objectId = p.ReadInt();
        else if (type == WiredVariableTarget.User)
            userIndex = p.ReadInt();

        int valueCount = p.ReadInt();
        WiredWire.RequireBoundedCount(valueCount, p.Available, 6, nameof(VariableValues));
        var values = new KeyValuePair<string, int>[valueCount];
        for (int i = 0; i < valueCount; i++)
        {
            string key = p.ReadString();
            int value = p.ReadInt();
            values[i] = new KeyValuePair<string, int>(key, value);
        }

        int[]? configured = null;
        if (type == WiredVariableTarget.Furni)
            configured = WiredIo.IntArray(p);

        return new WiredObjectInspectionData(type, objectId, userIndex, values, configured);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Type);

        if (Type == WiredVariableTarget.Furni)
            p.WriteInt(ObjectId);
        else if (Type == WiredVariableTarget.User)
            p.WriteInt(UserIndex);

        p.WriteInt(VariableValues.Count);
        foreach (KeyValuePair<string, int> kv in VariableValues)
        {
            p.WriteString(kv.Key);
            p.WriteInt(kv.Value);
        }

        if (Type == WiredVariableTarget.Furni)
            WiredIo.WriteIntArray(p, ConfiguredInWireds ?? []);
    }
}

public sealed record WiredVariablesForObject(WiredObjectInspectionData Data)
    : IParserComposer<WiredVariablesForObject>
{
    public static WiredVariablesForObject Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredVariablesForObject ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredObjectInspectionData>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredVariablesForObject value, in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(value.Data, in p);
        p.Compose(value.Data);
    }
}

public sealed record WiredSetUserPermanentVariableResult(bool Success)
    : IParserComposer<WiredSetUserPermanentVariableResult>
{
    public static WiredSetUserPermanentVariableResult Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredSetUserPermanentVariableResult ParseFlash(in PacketReader p) =>
        new(p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSetUserPermanentVariableResult value, in PacketWriter p) =>
        p.WriteBool(value.Success);
}


public sealed record WiredGetAllVariableHolders(string VariableId)
    : IParserComposer<WiredGetAllVariableHolders>
{
    public static WiredGetAllVariableHolders Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetAllVariableHolders ParseFlash(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetAllVariableHolders value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteString(value.VariableId);
    }
}

// Uploads the client's whole {variableId -> perVariableHash} cache. Null cache -> count 0, which
// is exactly an empty list here.
public sealed record VariableHashEntry(string VariableId, int Hash) : IParserComposer<VariableHashEntry>
{
    public static VariableHashEntry Parse(in PacketReader p) => new(p.ReadString(), p.ReadInt());
    public void Compose(in PacketWriter p) { p.WriteString(VariableId); p.WriteInt(Hash); }
}

public sealed record WiredGetAllVariablesDiffs(IReadOnlyList<VariableHashEntry> Cache)
    : IParserComposer<WiredGetAllVariablesDiffs>
{
    public static WiredGetAllVariablesDiffs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetAllVariablesDiffs ParseFlash(in PacketReader p)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 6, nameof(Cache));
        var items = new VariableHashEntry[n];
        for (int i = 0; i < n; i++)
            items[i] = p.Parse<VariableHashEntry>();
        return new WiredGetAllVariablesDiffs(items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetAllVariablesDiffs value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Cache);
        foreach (VariableHashEntry entry in value.Cache)
        {
            ArgumentNullException.ThrowIfNull(entry);
            WiredWire.RequireString(entry.VariableId, nameof(VariableHashEntry.VariableId), in p);
        }
        p.WriteInt(value.Cache.Count);
        foreach (VariableHashEntry e in value.Cache)
            p.Compose(e);
    }
}

public sealed record WiredGetAllVariablesHash() : IParserComposer<WiredGetAllVariablesHash>
{
    public static WiredGetAllVariablesHash Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetAllVariablesHash ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetAllVariablesHash));
        return new WiredGetAllVariablesHash();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetAllVariablesHash value, in PacketWriter p) { }
}

public sealed record WiredGetUserPermanentVariables(int EntityType, int EntityId)
    : IParserComposer<WiredGetUserPermanentVariables>
{
    public static WiredGetUserPermanentVariables Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetUserPermanentVariables ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetUserPermanentVariables value, in PacketWriter p)
    {
        p.WriteInt(value.EntityType);
        p.WriteInt(value.EntityId);
    }

}

public sealed record WiredGetVariableOwnersPage(
    string VariableId, int Page, int PageSize, int UserTypeFilter, int SortTypeFilter)
    : IParserComposer<WiredGetVariableOwnersPage>
{
    public static WiredGetVariableOwnersPage Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetVariableOwnersPage ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetVariableOwnersPage value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteString(value.VariableId);
        p.WriteInt(value.Page);
        p.WriteInt(value.PageSize);
        p.WriteInt(value.UserTypeFilter);
        p.WriteInt(value.SortTypeFilter);
    }

}

public sealed record WiredGetVariablesForObject(int Type, int ObjectId)
    : IParserComposer<WiredGetVariablesForObject>
{
    public static WiredGetVariablesForObject Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetVariablesForObject ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetVariablesForObject value, in PacketWriter p)
    {
        p.WriteInt(value.Type);
        p.WriteInt(value.ObjectId);
    }
}

public sealed record WiredSetObjectVariableValue(
    int VariableTarget, int ObjectId, string VariableId, int Value, int Operation)
    : IParserComposer<WiredSetObjectVariableValue>
{
    public static WiredSetObjectVariableValue Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredSetObjectVariableValue ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSetObjectVariableValue value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteInt(value.VariableTarget);
        p.WriteInt(value.ObjectId);
        p.WriteString(value.VariableId);
        p.WriteInt(value.Value);
        p.WriteInt(value.Operation);
    }
}

public sealed record WiredSetUserPermanentVariable(
    int EntityType, int EntityId, string VariableId, int Value, int Operation)
    : IParserComposer<WiredSetUserPermanentVariable>
{
    public static WiredSetUserPermanentVariable Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredSetUserPermanentVariable ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSetUserPermanentVariable value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteInt(value.EntityType);
        p.WriteInt(value.EntityId);
        p.WriteString(value.VariableId);
        p.WriteInt(value.Value);
        p.WriteInt(value.Operation);
    }

}

internal static class WiredVariableMessagesWire
{
    public static void Validate(WiredUserVariablesPage value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(value.VariableId, nameof(value.VariableId), in p);
        ArgumentNullException.ThrowIfNull(value.Elements);
        foreach (WiredUserVariablesElement element in value.Elements)
        {
            ArgumentNullException.ThrowIfNull(element);
            WiredWire.RequireString(element.EntityName, nameof(element.EntityName), in p);
            Validate(element.Storage, in p);
        }
    }

    public static void Validate(WiredUserPermanentVariablesList value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredWire.RequireString(value.EntityName, nameof(value.EntityName), in p);
        WiredWire.RequireString(value.EntityFigure, nameof(value.EntityFigure), in p);
        if (value.HasOwner)
        {
            WiredWire.RequireString(value.OwnerName ?? "", nameof(value.OwnerName), in p);
            WiredWire.RequireString(value.OwnerFigure ?? "", nameof(value.OwnerFigure), in p);
        }
        ArgumentNullException.ThrowIfNull(value.VariableStorage);
        foreach (WiredVariableStorageParameter storage in value.VariableStorage)
        {
            ArgumentNullException.ThrowIfNull(storage);
            Validate(storage, in p);
        }
    }

    public static void Validate(WiredObjectInspectionData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.VariableValues);
        foreach (KeyValuePair<string, int> variable in value.VariableValues)
            WiredWire.RequireString(variable.Key, nameof(value.VariableValues), in p);
        if (value.Type == WiredVariableTarget.Furni)
            WiredWire.RequireUnityCount(
                (value.ConfiguredInWireds ?? []).Count,
                nameof(value.ConfiguredInWireds));
    }

    private static void Validate(WiredVariableStorageParameter value, in PacketWriter p)
    {
        if (value.IncludesVariableId)
            WiredWire.RequireString(value.VariableId ?? "", nameof(value.VariableId), in p);
        WiredWire.RequireString(value.CreationTimeStr, nameof(value.CreationTimeStr), in p);
        WiredWire.RequireString(value.LastUpdateTimeStr, nameof(value.LastUpdateTimeStr), in p);
    }
}
