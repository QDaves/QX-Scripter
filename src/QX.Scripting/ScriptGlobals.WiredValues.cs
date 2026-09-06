using System.Collections;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

/// <summary>
/// The wired variables defined in a room, in the order the server first reported them, with
/// name-based lookup on top. A point-in-time snapshot: it does not update as the room changes.
/// </summary>
/// <param name="All">Every variable definition, in server order.</param>
public sealed record RoomVariables(IReadOnlyList<WiredVariableSnapshot> All) :
    IEnumerable<WiredVariableSnapshot>
{
    /// <summary>
    /// Finds a variable by display name, falling back to its id when no name matches.
    /// </summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <returns>The variable, or <see langword="null"/> when neither matches.</returns>
    public WiredVariableSnapshot? this[string name] => WiredLookup.Find(All, name);

    /// <summary>Finds a variable by its id string only, ignoring display names.</summary>
    /// <param name="variableId">The variable id.</param>
    /// <returns>The variable, or <see langword="null"/> when no variable has that id.</returns>
    public WiredVariableSnapshot? ById(string variableId) => WiredLookup.ById(All, variableId);

    /// <summary>Tries to find a variable by display name, falling back to its id.</summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="variable">Receives the variable, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when a variable was found.</returns>
    public bool TryGet(string name, out WiredVariableSnapshot? variable) =>
        (variable = this[name]) is not null;

    /// <summary>Filters the variables down to those bound to one kind of holder.</summary>
    /// <param name="target">The holder kind: furni, user, merged, global or context.</param>
    /// <returns>The matching variables, lazily evaluated over this snapshot.</returns>
    public IEnumerable<WiredVariableSnapshot> OfTarget(WiredTarget target) =>
        All.Where(variable => variable.VariableTarget == (int)target);

    /// <summary>How many variable definitions the snapshot holds.</summary>
    public int Count => All.Count;

    /// <summary>Enumerates every variable definition in server order.</summary>
    /// <returns>An enumerator over the snapshot.</returns>
    public IEnumerator<WiredVariableSnapshot> GetEnumerator() => All.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>One wired variable's value on a specific holder, paired with its definition.</summary>
/// <param name="Variable">
/// The variable's definition. When the room's definition list did not contain it, this is a
/// placeholder whose id and name are both the raw variable id from the wire.
/// </param>
/// <param name="Value">The integer value stored on the holder.</param>
public sealed record WiredValue(WiredVariableSnapshot Variable, int Value)
{
    /// <summary>The variable's display name, taken from its definition.</summary>
    public string Name => Variable.VariableName;
}

/// <summary>
/// A snapshot of every wired variable value held by one object, user or the global scope, resolved
/// against the room's variable definitions.
/// </summary>
/// <param name="Target">The holder kind that was inspected.</param>
/// <param name="ObjectId">
/// The holder's identifier as the server reported it: the furni id for a furni, the room index for
/// a user, and 0 for the global scope.
/// </param>
/// <param name="Values">The values, one entry per variable the holder carries.</param>
/// <param name="ConfiguredInWireds">
/// The ids of the wired boxes that reference these variables. The server only sends this for furni
/// inspections; it is empty for user and global inspections.
/// </param>
public sealed record WiredObjectValues(
    WiredTarget Target,
    int ObjectId,
    IReadOnlyList<WiredValue> Values,
    IReadOnlyList<int> ConfiguredInWireds) : IEnumerable<WiredValue>
{
    /// <summary>
    /// Looks up a value by variable display name, falling back to the variable id.
    /// </summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <returns>The value, or <see langword="null"/> when the holder carries no such variable.</returns>
    public int? this[string name] => WiredLookup.FindValue(Values, name)?.Value;

    /// <summary>How many values the holder carries.</summary>
    public int Count => Values.Count;

    /// <summary>Tries to look up a value by variable display name, falling back to the id.</summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="value">Receives the value, or 0 when the variable is absent.</param>
    /// <returns><see langword="true"/> when the holder carries the variable.</returns>
    public bool TryGet(string name, out int value)
    {
        int? found = this[name];
        value = found ?? 0;
        return found.HasValue;
    }

    /// <summary>Enumerates every value the holder carries.</summary>
    /// <returns>An enumerator over the snapshot.</returns>
    public IEnumerator<WiredValue> GetEnumerator() => Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class WiredLookup
{
    public static WiredVariableSnapshot? Find(
        IReadOnlyList<WiredVariableSnapshot> all,
        string name)
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i].VariableName == name) return all[i];
        for (int i = 0; i < all.Count; i++)
            if (all[i].VariableId == name) return all[i];
        return null;
    }

    public static WiredVariableSnapshot? ById(
        IReadOnlyList<WiredVariableSnapshot> all,
        string variableId)
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i].VariableId == variableId) return all[i];
        return null;
    }

    public static WiredValue? FindValue(IReadOnlyList<WiredValue> values, string name)
    {
        for (int i = 0; i < values.Count; i++)
            if (values[i].Variable.VariableName == name) return values[i];
        for (int i = 0; i < values.Count; i++)
            if (values[i].Variable.VariableId == name) return values[i];
        return null;
    }
}

/// <content>
/// The friendly layer over the raw wired variable protocol: it runs the hash/diff exchange to
/// completion, resolves variable ids to display names, and lets values be read and written by name.
/// <para>
/// Everything here is a live request, not cached state. Each call re-fetches the room's variable
/// definitions, so a loop that reads many values one by one is expensive — prefer fetching a
/// holder's values once and indexing into the result.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Fetches the room's complete wired variable definitions by running the diff exchange until
    /// the server marks the last chunk.
    /// </summary>
    /// <returns>
    /// Every variable definition, in the order the server first reported it. Empty when the room
    /// has no wired variables.
    /// </returns>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    public async Task<RoomVariables> GetRoomVariables(int timeoutMs = 10000) =>
        room_variables(await get_room_variables(timeoutMs));

    /// <summary>
    /// Fetches the room's variable definitions and returns the one matching a name. This is a
    /// definition, not a value — it says what the variable is, not what any holder has stored.
    /// </summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="timeoutMs">How long to wait for the definitions, in milliseconds.</param>
    /// <returns>The definition, or <see langword="null"/> when the room has no such variable.</returns>
    public async Task<WiredVariableSnapshot?> GetRoomVariable(
        string name,
        int timeoutMs = 10000) =>
        (await GetRoomVariables(timeoutMs))[name];

    /// <summary>
    /// Reads every wired variable value held by one object, taking the object id as a 32-bit value.
    /// </summary>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The holder's values, resolved against the room's variable definitions.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    public Task<WiredObjectValues> GetVariableValues(WiredTarget target, int objectId, int timeoutMs = 10000) =>
        GetVariableValues(target, (Id)(long)objectId, timeoutMs);

    /// <summary>
    /// Reads every wired variable value held by one object and resolves each id to its definition.
    /// This makes two round trips: one for the room's definitions and one for the holder's values.
    /// </summary>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>
    /// The holder's values. A value whose variable is not in the room's definition list still
    /// appears, carrying a placeholder definition whose name equals its id.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    public async Task<WiredObjectValues> GetVariableValues(WiredTarget target, Id objectId, int timeoutMs = 10000)
    {
        WiredVariableCollectionSnapshot definitions = await get_room_variables(timeoutMs);
        WiredVariablesObjectSnapshot data =
            await GetVariablesForObject((int)target, objectId, timeoutMs);
        if (definitions.Generation != data.Generation)
        {
            throw new RequestDisconnectedException(
                "wired variable definitions",
                "wired object variables");
        }

        RoomVariables defs = room_variables(definitions);
        var values = new List<WiredValue>(data.Values.Count);
        foreach (WiredVariableValueSnapshot value in data.Values)
        {
            WiredVariableSnapshot variable =
                defs.ById(value.VariableId) ?? missing_variable(value.VariableId, target);
            values.Add(new WiredValue(variable, value.Value));
        }

        return new WiredObjectValues(
            data.Target,
            data.ObjectId,
            values,
            data.ConfiguredInWireds);
    }

    /// <summary>Reads every wired variable value held by one furni.</summary>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The furni's values, plus the ids of the wireds that reference them.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    public Task<WiredObjectValues> GetFurniValues(Id furniId, int timeoutMs = 10000) =>
        GetVariableValues(WiredTarget.Furni, furniId, timeoutMs);

    /// <summary>Reads every wired variable value held by one user in the room.</summary>
    /// <param name="userIndex">The user's room index, not their account id.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The user's values.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    public Task<WiredObjectValues> GetUserValues(int userIndex, int timeoutMs = 10000) =>
        GetVariableValues(WiredTarget.User, userIndex, timeoutMs);

    /// <summary>Reads the room's global wired variable values.</summary>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The global values.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    public Task<WiredObjectValues> GetGlobalValues(int timeoutMs = 10000) =>
        GetVariableValues(WiredTarget.Global, 0, timeoutMs);

    /// <summary>Reads one wired variable value from a furni, by name.</summary>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The value, or <see langword="null"/> when the furni carries no such variable.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    /// <remarks>
    /// This re-fetches the room's definitions and the furni's whole value set. Reading several
    /// values from the same furni is much cheaper through the values snapshot.
    /// </remarks>
    public async Task<int?> GetFurniValue(Id furniId, string name, int timeoutMs = 10000) =>
        (await GetFurniValues(furniId, timeoutMs))[name];

    /// <summary>Reads one wired variable value from a user in the room, by name.</summary>
    /// <param name="userIndex">The user's room index.</param>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The value, or <see langword="null"/> when the user carries no such variable.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    public async Task<int?> GetUserValue(int userIndex, string name, int timeoutMs = 10000) =>
        (await GetUserValues(userIndex, timeoutMs))[name];

    /// <summary>Reads one global wired variable value, by name.</summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="timeoutMs">How long to wait for each underlying reply, in milliseconds.</param>
    /// <returns>The value, or <see langword="null"/> when the room has no such global variable.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">A reply did not arrive in time.</exception>
    public async Task<int?> GetGlobalValue(string name, int timeoutMs = 10000) =>
        (await GetGlobalValues(timeoutMs))[name];

    /// <summary>
    /// Writes a wired variable value on a furni, naming the variable rather than passing its id.
    /// The definitions are fetched first to resolve the name; the write itself is
    /// fire-and-forget, so the returned task completes as soon as the message is sent and says
    /// nothing about whether the server accepted it.
    /// </summary>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="value">The integer value to store.</param>
    /// <param name="timeoutMs">How long to wait for the definitions used to resolve the name.</param>
    /// <exception cref="InvalidOperationException">
    /// The room has no furni-target wired variable with that name or id.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">The definitions did not arrive in time.</exception>
    public async Task SetFurniValue(Id furniId, string name, int value, int timeoutMs = 10000)
    {
        WiredVariableSnapshot v =
            resolve_writable(await GetRoomVariables(timeoutMs), name, WiredTarget.Furni);
        SetFurniVariable(furniId, v.VariableId, value);
    }

    /// <summary>
    /// Writes a global wired variable value, naming the variable rather than passing its id. The
    /// write is fire-and-forget: the returned task completes once the message is sent, not once
    /// the server has applied it.
    /// </summary>
    /// <param name="name">The variable's display name, or its id string.</param>
    /// <param name="value">The integer value to store.</param>
    /// <param name="timeoutMs">How long to wait for the definitions used to resolve the name.</param>
    /// <exception cref="InvalidOperationException">
    /// The room has no global-target wired variable with that name or id.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">The definitions did not arrive in time.</exception>
    public async Task SetGlobalValue(string name, int value, int timeoutMs = 10000)
    {
        WiredVariableSnapshot v =
            resolve_writable(await GetRoomVariables(timeoutMs), name, WiredTarget.Global);
        SetGlobalVariable(v.VariableId, value);
    }

    private static WiredVariableSnapshot resolve_writable(
        RoomVariables vars,
        string name,
        WiredTarget target)
    {
        foreach (WiredVariableSnapshot v in vars.All)
            if (v.VariableTarget == (int)target && v.VariableName == name) return v;
        foreach (WiredVariableSnapshot v in vars.All)
            if (v.VariableTarget == (int)target && v.VariableId == name) return v;
        throw new InvalidOperationException($"No {target} wired variable named '{name}' in this room.");
    }

    public IDisposable WatchVariables(
        Action<WiredVariableCollectionSnapshot> onChange,
        int intervalMs = 1000) =>
        watch_variable_collections(onChange, intervalMs);

    private IDisposable watch_variable_collections(
        Action<WiredVariableCollectionSnapshot> on_change,
        int interval_ms)
    {
        ArgumentNullException.ThrowIfNull(on_change);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interval_ms);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        StartObservedTask(
            () => poll_variable_collections(on_change, interval_ms, cancellation.Token),
            cancellation.Token);
        return Track(new Unsubscriber(cancellation.Cancel));
    }

    private async Task poll_variable_collections(
        Action<WiredVariableCollectionSnapshot> on_change,
        int interval_ms,
        CancellationToken cancellation_token)
    {
        (long Generation, int Hash)? last_state = null;
        while (!cancellation_token.IsCancellationRequested)
        {
            WiredVariableCollectionSnapshot current =
                await Application.InvokeAsync<
                    WiredVariableListRequest,
                    WiredVariableCollectionSnapshot>(
                    ApplicationMemberIds.WiredVariablesList,
                    new WiredVariableListRequest(),
                    cancellation_token);
            var current_state = (current.Generation, current.AllVariablesHash);
            if (last_state != current_state)
            {
                last_state = current_state;
                on_change(current);
            }
            await Task.Delay(interval_ms, cancellation_token);
        }
    }

    private Task<WiredVariableCollectionSnapshot> get_room_variables(int timeout_ms) =>
        wired_call<WiredVariableListRequest, WiredVariableCollectionSnapshot>(
            ApplicationMemberIds.WiredVariablesList,
            new WiredVariableListRequest(TimeoutMilliseconds: timeout_ms));

    private static RoomVariables room_variables(WiredVariableCollectionSnapshot variables) =>
        new(variables.Variables.Select(entry => entry.Variable).ToArray());

    private static WiredVariableSnapshot missing_variable(
        string variable_id,
        WiredTarget target) => new(
        variable_id,
        0,
        variable_id,
        0,
        (int)target,
        false,
        false,
        true,
        false,
        false,
        false,
        false,
        false,
        null);
}
