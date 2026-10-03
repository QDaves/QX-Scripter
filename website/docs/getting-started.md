# Getting started

Connect QX Scripter to the hotel, run a first script and keep a script running.

## Connect

1. Start G-Earth and connect it to the hotel on the Flash client.
2. Start QX Scripter from the G-Earth extension list, or run the desktop app directly. It finds
   G-Earth on its own.
3. Log in to the hotel. The status bar shows when the session is ready.

## Run a script

Create a new tab and write:

```csharp
Log($"Hello {SelfAvatar?.Name}, you are in room {RoomId}.");
```

Press **Run** or F5. The output console under the editor shows the line.

## Keep a script running

A script ends when its last statement has run. A script that reacts to the game registers handlers
and then waits:

```csharp
OnChat((avatar, chat) => Log($"{avatar?.Name}: {chat.Message}"));

await Wait();
```

`Wait` returns when the script is stopped. Handlers registered with `On...` methods are removed at
the same time. Press **Stop** to end the script.

## Next steps

- [Writing scripts](scripts.md) covers the basics of a script.
- [Room state](room-state.md) shows what a script can read.
- [Panels](panels.md) gives a script its own user interface.
