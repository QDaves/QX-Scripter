# Shared code

Scripts share code by loading other scripts with `#load` and .NET assemblies with `#r`.

## Load a script

`#load` compiles another script from the library into the current one. Its methods, types and
variables are available as if they were written in place.

`Maze Engine.csx`:

```csharp
async Task WalkAndWait(int x, int y)
{
    Walk(x, y);
    await WaitUntil(() => SelfAvatar?.X == x && SelfAvatar?.Y == y, 5000);
}
```

A script that uses it:

```csharp
#load "Maze Engine.csx"

await WalkAndWait(4, 7);
await WalkAndWait(4, 12);
```

A fix in the loaded file reaches every script that loads it the next time they run. Loops in a
loaded file stop with the script, the same way the script's own loops do.

A method a script declares hides every global of the same name, so a helper named `Walk` would
make the global `Walk` overloads unreachable. Give helpers names of their own.

## Reference an assembly

`#r` references a .NET assembly:

```csharp
#r "libs/Newtonsoft.Json.dll"

var data = Newtonsoft.Json.JsonConvert.SerializeObject(new { RoomId });
Log(data);
```

## Paths

Relative paths in `#load` and `#r` are resolved from the folder of the script that contains them,
or from the script library for a script that has no file. Absolute paths work as well.

## Test code

A test that loads an engine is only a few lines. Run it with the MCP tool `run_code`, and nothing is
left in the library afterwards.
