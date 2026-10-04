# Writing scripts

A script is C# with top-level statements. The members of <xref:Qx.Scripting.ScriptGlobals> are in
scope without a prefix, and these namespaces are imported:

`System`, `System.Collections.Generic`, `System.Collections.Concurrent`, `System.Diagnostics`,
`System.Globalization`, `System.IO`, `System.Linq`, `System.Text`, `System.Text.RegularExpressions`,
`System.Threading`, `System.Threading.Tasks`, `Qx`, `Qx.Messages`, `Qx.Protocol`, `Qx.Interception`,
`Qx.Game`, `Qx.Game.Application`, `Qx.Model`, `Qx.Model.Figures`, `Qx.Model.Messages.Incoming`,
`Qx.Model.Messages.Outgoing`, `Qx.Model.Wired`, `Qx.Platform` and `Qx.Scripting`.

## Output

`Log` writes a line to the tab's output console.

```csharp
Log("started");
Log(RoomId);
```

## Waiting

`Delay` waits without blocking and stops waiting when the script is stopped.

```csharp
Talk("one");
await Delay(1000);
Talk("two");
```

## Loops

`Run` is `true` until the script is stopped. A loop that checks it ends cleanly:

```csharp
while (Run)
{
    Talk("still here");
    await Delay(5000);
}
```

Every `while`, `for`, `foreach` and `do` loop in a script also checks for a stop on each pass, so a
loop that forgets `Run` still ends when **Stop** is pressed.

## Ending a script

A script ends after its last statement. `Finish()` ends it early and counts as a normal end.

```csharp
if (SelfAvatar is null)
    Finish();
```

`await Wait()` keeps a script alive until it is stopped, for scripts that only react to events.

## Cancellation

`Ct` is the script's cancellation token. `Delay`, `Wait` and the request methods use it and throw
<xref:System.OperationCanceledException> when the script is stopped. Put cleanup in `finally`:

```csharp
try
{
    while (Run)
    {
        UseFurni(QueryFloorItems().Named("dice").First());
        await Delay(2000);
    }
}
finally
{
    Log("stopped");
}
```

## Background work

`RunTask` starts work that runs next to the script. An exception in it faults the script, and it is
canceled when the script stops.

```csharp
RunTask(async () =>
{
    while (Run)
    {
        Log($"{Users.Count()} users");
        await Delay(10000);
    }
});
```

## Shared code

`#load` compiles another script into this one. See [Shared code](shared-code.md).

## Script files

### Name and group

Directives in the comments at the top of a script name it and file it into a group of the library:

```csharp
/// @name Dice Bot
/// @group Games
Log("ready");
```

The desktop app keeps the file named after `@name`. Only the comment lines before the first line of
code count.

### Namespaces

A type from a namespace that is not imported, such as the message contracts in `Qx.Game.Protocol`,
needs a `using` directive or its full name. The [API reference](../api/index.md) marks every
namespace as imported, add using or host.

Once `System.Diagnostics.Process` is referenced, `ThreadState` names a type in both
`System.Threading` and `System.Diagnostics`, so write `System.Threading.ThreadState` in full.

### Language limits

A `using var` declaration does not compile at the top level of a script (CS1002). Use the block
form:

```csharp
using (var writer = new StreamWriter(Path.Combine(Path.GetTempPath(), "room.txt")))
    writer.WriteLine(RoomId);
```

`PacketReader` and `PacketWriter` are ref structs and cannot be top-level variables (CS8345). Read a
packet inside a handler or a local function:

```csharp
OnIn("Chat", e =>
{
    var reader = e.Packet.Reader();
    Log(reader.ReadInt());
});
```

Nullable annotations such as `User? user` compile without a warning, and null warnings stay off
unless the script turns them on with `#nullable enable`.

### Names that look alike

| Names | Meaning |
| --- | --- |
| `InventoryItem.ItemId`, `InventoryItem.Id` | The id the inventory addresses the item by, which placing, trading and selling take, and the room id the item gets once it is placed. |
| `Furni.Kind`, `Furni.Identifier` | The furni kind number, which differs between hotels, and the class name such as `edice`, which does not. |
| `Avatar.Index`, `Avatar.Id` | The room index that room packets use, valid for the current visit, and the account id of a user or the id of a pet or bot. |
| Flash name, key, model | `ObjectUpdate` is the name the client build uses, `MessageKeys.Room.FloorItem.Updated` the key that stays the same across builds, and `FloorItemUpdate` the type the message is parsed into. |
