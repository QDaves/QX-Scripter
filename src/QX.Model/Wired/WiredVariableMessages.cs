using Qx.Messages;

namespace Qx.Model.Wired;

// Operation codes shared by WiredSetObjectVariableValue and WiredSetUserPermanentVariable.
/// <summary>Contains the operation codes of <see cref="WiredSetObjectVariableValue"/> and <see cref="WiredSetUserPermanentVariable"/>.</summary>
public static class WiredVariableOperation
{
    /// <summary>Writes the value of an existing variable, code 0.</summary>
    public const int Write = 0;
    /// <summary>Creates the variable on the target, code 1.</summary>
    public const int Create = 1;
    /// <summary>Deletes the variable from the target, code 2.</summary>
    /// <remarks>The value sent with a delete is ignored.</remarks>
    public const int Delete = 2;
}

// Object inspection discriminator values, matching the response union and the outgoing request.
/// <summary>Contains the target codes of wired variables and of object variable inspection.</summary>
/// <remarks>The same codes are used by <see cref="WiredVariable.VariableTarget"/>, <see cref="WiredObjectInspectionData.Type"/> and <see cref="WiredGetVariablesForObject.Type"/>.</remarks>
public static class WiredVariableTarget
{
    /// <summary>A furni item, code 0.</summary>
    public const int Furni = 0;
    /// <summary>A room user, code 1.</summary>
    public const int User = 1;
    /// <summary>The merged target, code 2.</summary>
    public const int Merged = 2;
    /// <summary>The room as a whole, code -10.</summary>
    public const int Global = -10;
    /// <summary>The context target, code -20.</summary>
    public const int Context = -20;
}

// One persisted storage slot for a variable on an entity. Context-polymorphic: the leading
// variableId is present only when the caller asked for it (true in WiredUserPermanentVariables,
// false in WiredUserVariablesList) — decided by calling context, never by a wire tag. The two
// timestamps are 8-byte longs.
/// <summary>Represents the stored value of a variable on an entity, with its timestamps.</summary>
/// <remarks>Whether the variable id is on the wire depends on the message that carries the entry, not on a flag in the packet. The timestamps are 64 bit integers.</remarks>
/// <param name="IncludesVariableId">Whether the entry carries the variable id on the wire.</param>
/// <param name="VariableId">The id of the variable, or <see langword="null"/> when <paramref name="IncludesVariableId"/> is <see langword="false"/>.</param>
/// <param name="Value">The stored value.</param>
/// <param name="CreationTime">The time the value was created, as sent by the hotel.</param>
/// <param name="CreationTimeStr">The creation time as text formatted by the hotel.</param>
/// <param name="LastUpdateTime">The time the value was last updated, as sent by the hotel.</param>
/// <param name="LastUpdateTimeStr">The last update time as text formatted by the hotel.</param>
public sealed record WiredVariableStorageParameter(
    bool IncludesVariableId,
    string? VariableId,
    int Value,
    long CreationTime,
    string CreationTimeStr,
    long LastUpdateTime,
    string LastUpdateTimeStr) : IComposer
{
    /// <summary>Parses a storage entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <param name="includeVariableId">Whether the entry starts with the variable id.</param>
    /// <returns>The parsed storage entry.</returns>
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

    /// <summary>Composes the storage entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with a variable definition and every object that holds the variable.</summary>
/// <remarks>Received as the Flash <c>WiredAllVariableHolders</c> message, in answer to <see cref="WiredGetAllVariableHolders"/>.</remarks>
/// <param name="LeadingValue">The integer that starts the message. The client reads and discards it, and it is kept so the message composes to the same bytes.</param>
/// <param name="VariableInfoAndHolders">The variable definition with its holders and their values.</param>
public sealed record WiredAllVariableHolders(int LeadingValue, VariableInfoAndHolders VariableInfoAndHolders)
    : IParserComposer<WiredAllVariableHolders>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredAllVariableHolders Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredAllVariableHolders ParseFlash(in PacketReader p)
    {
        int leading = p.ReadInt();
        VariableInfoAndHolders info = p.Parse<VariableInfoAndHolders>();
        return new WiredAllVariableHolders(leading, info);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAllVariableHolders value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.VariableInfoAndHolders);
        WiredVariable.Validate(value.VariableInfoAndHolders.Variable, in p);
        ArgumentNullException.ThrowIfNull(value.VariableInfoAndHolders.Holders);
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
/// <summary>Represents a variable definition with its own hash, as sent in a variable difference chunk.</summary>
/// <param name="PerVariableHash">The hash of the variable definition, which is read before the definition.</param>
/// <param name="Variable">The variable definition.</param>
public sealed record WiredVariableWithHash(int PerVariableHash, WiredVariable Variable)
    : IParserComposer<WiredVariableWithHash>
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredVariableWithHash Parse(in PacketReader p)
    {
        int hash = p.ReadInt();
        WiredVariable variable = WiredVariable.Parse(p);
        return new WiredVariableWithHash(hash, variable);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(PerVariableHash);
        Variable.Compose(p);
    }
}

/// <summary>Received with one chunk of the differences between the client's variable cache and the room's variables.</summary>
/// <remarks>Received as the Flash <c>WiredAllVariablesDiffs</c> message, in answer to <see cref="WiredGetAllVariablesDiffs"/>.</remarks>
/// <param name="AllVariablesHash">The hash of all variables in the room.</param>
/// <param name="IsLastChunk">Whether the chunk is the last one of the answer.</param>
/// <param name="RemovedVariables">The ids of the variables that no longer exist.</param>
/// <param name="AddedOrUpdated">The variables that were added or changed, with their hashes.</param>
public sealed record WiredAllVariablesDiffs(
    int AllVariablesHash,
    bool IsLastChunk,
    IReadOnlyList<string> RemovedVariables,
    IReadOnlyList<WiredVariableWithHash> AddedOrUpdated) : IParserComposer<WiredAllVariablesDiffs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredAllVariablesDiffs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAllVariablesDiffs value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RemovedVariables);
        ArgumentNullException.ThrowIfNull(value.AddedOrUpdated);
        foreach (string variable_id in value.RemovedVariables)
            WiredWire.RequireString(variable_id, nameof(RemovedVariables), in p);
        foreach (WiredVariableWithHash item in value.AddedOrUpdated)
        {
            ArgumentNullException.ThrowIfNull(item);
            WiredVariable.Validate(item.Variable, in p);
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

/// <summary>Received with the hash of all variables in the room.</summary>
/// <remarks>Received as the Flash <c>WiredAllVariablesHash</c> message, in answer to <see cref="WiredGetAllVariablesHash"/>. A changed hash means the variable definitions changed.</remarks>
/// <param name="AllVariablesHash">The hash of all variables in the room.</param>
public sealed record WiredAllVariablesHash(int AllVariablesHash) : IParserComposer<WiredAllVariablesHash>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredAllVariablesHash Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredAllVariablesHash ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAllVariablesHash value, in PacketWriter p) =>
        p.WriteInt(value.AllVariablesHash);
}

/// <summary>Represents one entity that owns a permanent variable, as listed in a <see cref="WiredUserVariablesPage"/>.</summary>
/// <param name="EntityType">The hotel entity type code of the owner.</param>
/// <param name="EntityId">The id of the entity.</param>
/// <param name="EntityName">The name of the entity.</param>
/// <param name="Storage">The stored value, without the variable id.</param>
public sealed record WiredUserVariablesElement(
    int EntityType, int EntityId, string EntityName, WiredVariableStorageParameter Storage)
    : IParserComposer<WiredUserVariablesElement>
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredUserVariablesElement Parse(in PacketReader p)
    {
        int entityType = p.ReadInt();
        int entityId = p.ReadInt();
        string entityName = p.ReadString();
        WiredVariableStorageParameter storage = WiredVariableStorageParameter.Parse(p, false);
        return new WiredUserVariablesElement(entityType, entityId, entityName, storage);
    }

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        WiredVariableMessagesWire.validate_storage(Storage, false, in p);
        p.WriteInt(EntityType);
        p.WriteInt(EntityId);
        p.WriteString(EntityName);
        Storage.Compose(p);
    }
}

/// <summary>Represents one page of the entities that own a permanent variable.</summary>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="TotalEntries">The total number of owners.</param>
/// <param name="CurrentPage">The number of the page.</param>
/// <param name="Amount">The page size the hotel reports.</param>
/// <param name="Elements">The owners on the page.</param>
/// <param name="UserTypeFilter">The entity type filter the page was built with.</param>
/// <param name="SortTypeFilter">The sort order the page was built with.</param>
public sealed record WiredUserVariablesPage(
    string VariableId,
    int TotalEntries,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredUserVariablesElement> Elements,
    int UserTypeFilter,
    int SortTypeFilter) : IParserComposer<WiredUserVariablesPage>
{
    /// <summary>Parses the page from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the page into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(this, in p);
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

/// <summary>Received with one page of the entities that own a permanent variable.</summary>
/// <remarks>Received as the Flash <c>WiredUserVariablesList</c> message, in answer to <see cref="WiredGetVariableOwnersPage"/>.</remarks>
/// <param name="Page">The page of owners.</param>
public sealed record WiredUserVariablesList(WiredUserVariablesPage Page)
    : IParserComposer<WiredUserVariablesList>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredUserVariablesList Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredUserVariablesList ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredUserVariablesPage>());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUserVariablesList value, in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(value.Page, in p);
        p.Compose(value.Page);
    }
}

// Owner trio (ownerId/ownerName/ownerFigure) present only when entityType != 1. Storage params
// here carry the variableId (param2 = true).
/// <summary>Represents the permanent variables stored on one entity.</summary>
/// <remarks>The owner fields are on the wire only when <paramref name="EntityType"/> is not 1.</remarks>
/// <param name="EntityType">The hotel entity type code.</param>
/// <param name="EntityId">The id of the entity.</param>
/// <param name="EntityName">The name of the entity.</param>
/// <param name="EntityFigure">The figure of the entity.</param>
/// <param name="OwnerId">The id of the owner, or 0 when <paramref name="EntityType"/> is 1.</param>
/// <param name="OwnerName">The name of the owner, or <see langword="null"/> when <paramref name="EntityType"/> is 1.</param>
/// <param name="OwnerFigure">The figure of the owner, or <see langword="null"/> when <paramref name="EntityType"/> is 1.</param>
/// <param name="VariableStorage">The stored variables, each with its variable id.</param>
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
    /// <summary>Gets whether the entry carries owner fields, which is the case when <see cref="EntityType"/> is not 1.</summary>
    public bool HasOwner => EntityType != 1;

    /// <summary>Parses the list from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the list into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(this, in p);
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

/// <summary>Received with the permanent variables stored on one entity.</summary>
/// <remarks>Received as the Flash <c>WiredUserPermanentVariables</c> message, in answer to <see cref="WiredGetUserPermanentVariables"/>.</remarks>
/// <param name="List">The entity and its stored variables.</param>
public sealed record WiredUserPermanentVariables(WiredUserPermanentVariablesList List)
    : IParserComposer<WiredUserPermanentVariables>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredUserPermanentVariables Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredUserPermanentVariables ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredUserPermanentVariablesList>());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUserPermanentVariables value, in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(value.List, in p);
        p.Compose(value.List);
    }
}

// Three-way union on type: only type 0 (object) reads objectId + the trailing configuredInWireds
// vector; type 1 (user) reads userIndex; type -10 (global) reads neither id nor trailing vector.
/// <summary>Represents the variable values held by a furni item, a room user or the room.</summary>
/// <remarks>The object id and the configured wired list are on the wire only for <see cref="WiredVariableTarget.Furni"/>, and the user index only for <see cref="WiredVariableTarget.User"/>.</remarks>
/// <param name="Type">The target code, one of the <see cref="WiredVariableTarget"/> values.</param>
/// <param name="ObjectId">The id of the furni item, or 0 for other targets.</param>
/// <param name="UserIndex">The room index of the user, or 0 for other targets.</param>
/// <param name="VariableValues">The variable ids with their values.</param>
/// <param name="ConfiguredInWireds">The ids of the wired furni whose configuration includes the furni item, or <see langword="null"/> for other targets.</param>
public sealed record WiredObjectInspectionData(
    int Type,
    int ObjectId,
    int UserIndex,
    IReadOnlyList<KeyValuePair<string, int>> VariableValues,
    IReadOnlyList<int>? ConfiguredInWireds) : IParserComposer<WiredObjectInspectionData>
{
    /// <summary>Parses the inspection data from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the inspection data into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>Received with the variable values held by a furni item, a room user or the room.</summary>
/// <remarks>Received as the Flash <c>WiredVariablesForObject</c> message, in answer to <see cref="WiredGetVariablesForObject"/>.</remarks>
/// <param name="Data">The inspected target and its values.</param>
public sealed record WiredVariablesForObject(WiredObjectInspectionData Data)
    : IParserComposer<WiredVariablesForObject>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredVariablesForObject Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredVariablesForObject ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredObjectInspectionData>());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredVariablesForObject value, in PacketWriter p)
    {
        WiredVariableMessagesWire.Validate(value.Data, in p);
        p.Compose(value.Data);
    }
}

/// <summary>Received with the result of a permanent variable update.</summary>
/// <remarks>Received as the Flash <c>WiredSetUserPermanentVariableResult</c> message, in answer to <see cref="WiredSetUserPermanentVariable"/>.</remarks>
/// <param name="Success">Whether the update succeeded.</param>
public sealed record WiredSetUserPermanentVariableResult(bool Success)
    : IParserComposer<WiredSetUserPermanentVariableResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSetUserPermanentVariableResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSetUserPermanentVariableResult ParseFlash(in PacketReader p) =>
        new(p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSetUserPermanentVariableResult value, in PacketWriter p) =>
        p.WriteBool(value.Success);
}


/// <summary>Requests a variable definition and every object that holds the variable.</summary>
/// <remarks>Sent as the Flash <c>WiredGetAllVariableHolders</c> message. The hotel answers with <see cref="WiredAllVariableHolders"/>.</remarks>
/// <param name="VariableId">The id of the variable.</param>
public sealed record WiredGetAllVariableHolders(string VariableId)
    : IParserComposer<WiredGetAllVariableHolders>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetAllVariableHolders Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetAllVariableHolders ParseFlash(in PacketReader p) => new(p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetAllVariableHolders value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteString(value.VariableId);
    }
}

/// <summary>Sent when the user removes a variable from every furni item and user that holds it.</summary>
/// <remarks>Sent as the Flash <c>WiredDeleteAllVariableHolders</c> message. The hotel sends no acknowledgement.</remarks>
/// <param name="VariableId">The id of the variable.</param>
public sealed record WiredDeleteAllVariableHolders(string VariableId)
    : IParserComposer<WiredDeleteAllVariableHolders>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredDeleteAllVariableHolders Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredDeleteAllVariableHolders ParseFlash(in PacketReader p) => new(p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredDeleteAllVariableHolders value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteString(value.VariableId);
    }
}

// Uploads the client's whole {variableId -> perVariableHash} cache. Null cache -> count 0, which
// is exactly an empty list here.
/// <summary>Represents one variable in the client's variable cache, with its hash.</summary>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="Hash">The hash of the variable definition the client holds.</param>
public sealed record VariableHashEntry(string VariableId, int Hash) : IParserComposer<VariableHashEntry>
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableHashEntry Parse(in PacketReader p) => new(p.ReadString(), p.ReadInt());
    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) { p.WriteString(VariableId); p.WriteInt(Hash); }
}

/// <summary>Requests the differences between the client's variable cache and the room's variables.</summary>
/// <remarks>Sent as the Flash <c>WiredGetAllVariablesDiffs</c> message. The hotel answers with one or more <see cref="WiredAllVariablesDiffs"/> chunks.</remarks>
/// <param name="Cache">The variables the client knows with their hashes, or an empty list to receive every variable.</param>
public sealed record WiredGetAllVariablesDiffs(IReadOnlyList<VariableHashEntry> Cache)
    : IParserComposer<WiredGetAllVariablesDiffs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetAllVariablesDiffs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetAllVariablesDiffs ParseFlash(in PacketReader p)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 6, nameof(Cache));
        var items = new VariableHashEntry[n];
        for (int i = 0; i < n; i++)
            items[i] = p.Parse<VariableHashEntry>();
        return new WiredGetAllVariablesDiffs(items);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Requests the hash of all variables in the room.</summary>
/// <remarks>Sent as the Flash <c>WiredGetAllVariablesHash</c> message, which carries no fields. The hotel answers with <see cref="WiredAllVariablesHash"/>.</remarks>
public sealed record WiredGetAllVariablesHash() : IParserComposer<WiredGetAllVariablesHash>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetAllVariablesHash Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetAllVariablesHash ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetAllVariablesHash));
        return new WiredGetAllVariablesHash();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetAllVariablesHash value, in PacketWriter p) { }
}

/// <summary>Requests the permanent variables stored on one entity.</summary>
/// <remarks>Sent as the Flash <c>WiredGetUserPermanentVariables</c> message. The hotel answers with <see cref="WiredUserPermanentVariables"/>.</remarks>
/// <param name="EntityType">The hotel entity type code.</param>
/// <param name="EntityId">The id of the entity.</param>
public sealed record WiredGetUserPermanentVariables(int EntityType, int EntityId)
    : IParserComposer<WiredGetUserPermanentVariables>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetUserPermanentVariables Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetUserPermanentVariables ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetUserPermanentVariables value, in PacketWriter p)
    {
        p.WriteInt(value.EntityType);
        p.WriteInt(value.EntityId);
    }

}

/// <summary>Requests one page of the entities that own a permanent variable.</summary>
/// <remarks>Sent as the Flash <c>WiredGetVariableOwnersPage</c> message. The hotel answers with <see cref="WiredUserVariablesList"/>.</remarks>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="Page">The one based page number.</param>
/// <param name="PageSize">The number of owners per page.</param>
/// <param name="SortTypeFilter">The sort order of the list. The game client starts with 0.</param>
/// <param name="UserTypeFilter">The entity type filter of the list. The game client starts with -1 for all types.</param>
public sealed record WiredGetVariableOwnersPage(
    string VariableId, int Page, int PageSize, int SortTypeFilter, int UserTypeFilter)
    : IParserComposer<WiredGetVariableOwnersPage>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetVariableOwnersPage Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetVariableOwnersPage ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetVariableOwnersPage value, in PacketWriter p)
    {
        WiredWire.RequireString(value.VariableId, nameof(VariableId), in p);
        p.WriteString(value.VariableId);
        p.WriteInt(value.Page);
        p.WriteInt(value.PageSize);
        p.WriteInt(value.SortTypeFilter);
        p.WriteInt(value.UserTypeFilter);
    }

}

/// <summary>Requests the variable values held by a furni item, a room user or the room.</summary>
/// <remarks>Sent as the Flash <c>WiredGetVariablesForObject</c> message. The hotel answers with <see cref="WiredVariablesForObject"/>.</remarks>
/// <param name="Type">The target code, one of the <see cref="WiredVariableTarget"/> values.</param>
/// <param name="ObjectId">The furni id, the room index of the user, or 0 for the room.</param>
public sealed record WiredGetVariablesForObject(int Type, int ObjectId)
    : IParserComposer<WiredGetVariablesForObject>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetVariablesForObject Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetVariablesForObject ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetVariablesForObject value, in PacketWriter p)
    {
        p.WriteInt(value.Type);
        p.WriteInt(value.ObjectId);
    }
}

/// <summary>Sent when the user writes, creates or deletes a variable on a furni item, a room user or the room.</summary>
/// <remarks>Sent as the Flash <c>WiredSetObjectVariableValue</c> message. The hotel sends no acknowledgement, and it drops the request when the user lacks wired rights or the variable does not allow the operation.</remarks>
/// <param name="VariableTarget">The target code, one of the <see cref="WiredVariableTarget"/> values.</param>
/// <param name="ObjectId">The furni id, the room index of the user, or 0 for the room.</param>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="Value">The value to write, ignored by a delete.</param>
/// <param name="Operation">The operation code, one of the <see cref="WiredVariableOperation"/> values.</param>
public sealed record WiredSetObjectVariableValue(
    int VariableTarget, int ObjectId, string VariableId, int Value, int Operation)
    : IParserComposer<WiredSetObjectVariableValue>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSetObjectVariableValue Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSetObjectVariableValue ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Sent when the user writes, creates or deletes a permanent variable on an entity.</summary>
/// <remarks>Sent as the Flash <c>WiredSetUserPermanentVariable</c> message. The hotel answers with <see cref="WiredSetUserPermanentVariableResult"/>.</remarks>
/// <param name="EntityType">The hotel entity type code.</param>
/// <param name="EntityId">The id of the entity.</param>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="Value">The value to write, ignored by a delete.</param>
/// <param name="Operation">The operation code, one of the <see cref="WiredVariableOperation"/> values.</param>
public sealed record WiredSetUserPermanentVariable(
    int EntityType, int EntityId, string VariableId, int Value, int Operation)
    : IParserComposer<WiredSetUserPermanentVariable>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSetUserPermanentVariable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSetUserPermanentVariable ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
            validate_storage(element.Storage, false, in p);
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
            validate_storage(storage, true, in p);
        }
    }

    public static void Validate(WiredObjectInspectionData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.VariableValues);
        foreach (KeyValuePair<string, int> variable in value.VariableValues)
            WiredWire.RequireString(variable.Key, nameof(value.VariableValues), in p);
    }

    internal static void validate_storage(WiredVariableStorageParameter value, bool includes_variable_id, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IncludesVariableId != includes_variable_id)
            throw new InvalidDataException("The stored variable layout does not match its containing message.");
        if (value.IncludesVariableId)
            WiredWire.RequireString(value.VariableId ?? "", nameof(value.VariableId), in p);
        WiredWire.RequireString(value.CreationTimeStr, nameof(value.CreationTimeStr), in p);
        WiredWire.RequireString(value.LastUpdateTimeStr, nameof(value.LastUpdateTimeStr), in p);
    }
}
