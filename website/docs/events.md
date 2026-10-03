# Events

Every event method takes a handler and returns an <xref:System.IDisposable>; dispose it to remove
the handler. Every handler is removed when the script stops, so a script usually keeps none of them.

An exception thrown in a handler stops the script and shows up in its output as an error.

Handlers run on the thread that dispatches the packets, one after another in packet order, so a
handler that blocks holds up every packet behind it. Keep handlers short and move longer work into
`RunTask`.

## Room

```csharp
OnEnteredRoom(() => Log($"entered room {RoomId}"));
OnLeftRoom(() => Log("left the room"));
OnLeftRoom(exit => Log($"left room {exit.RoomId}, kicked: {exit.WasKicked}"));
```

`OnRoomLoading` runs earlier than `OnEnteredRoom`, when the server starts sending the room, so the
room is not ready yet. `await WaitRoomReady()` waits until the floor items and the local avatar
have arrived as well. When `OnLeftRoom` runs, the room is already cleared, but the exit the second
form receives still names the room that was left.

## Avatars

```csharp
OnAvatarAdded(avatar => Log($"{avatar.Name} entered"));
OnAvatarRemoved(avatar => Log($"{avatar.Name} left"));
OnChat((avatar, chat) => Log($"{avatar?.Name}: {chat.Message}"));
```

`OnAvatarDanceChanged`, `OnAvatarEffectChanged`, `OnAvatarHandItemChanged`,
`OnAvatarIdleChanged` and `OnAvatarTypingChanged` report status changes. `OnAvatarExpression`
reports expressions such as a wave.

## Furni

```csharp
OnFloorItemAdded(item => Log($"placed {FurniName(item)} at {item.Location}"));
OnFloorItemUpdated(item => Log($"{item.Id} is now in state {item.State}"));
OnFloorItemRemoved(item => Log($"removed {FurniName(item)}"));
OnWallItemAdded(item => Log($"hung {FurniName(item)}"));
```

`OnFloorItemAdded` and `OnWallItemAdded` report furni placed while the script runs. The furni a room
loads with arrive in one batch through `OnFloorItemsLoaded` and `OnWallItemsLoaded`. The avatars a room
loads with do call `OnAvatarAdded`, once for each.

## Session

```csharp
OnSessionStarted(session => Log($"connected to {session.Host}"));
OnSessionEnded(() => Log("session ended"));
```

`OnSessionEnded` runs however the session ends. `OnDisconnected` runs only when the server sends a
disconnect reason. A run started from the command line is stopped when its session ends, so
`OnSessionEnded` never runs there.

## Everything else

There are events for the inventory, friends, private messages, trades, achievements, quests,
forums, the marketplace, crafting, polls and Wired. They all start with `On`; the
<xref:Qx.Scripting.ScriptGlobals> page lists every one.

```csharp
OnPrivateMessage((sender, text) => Log($"{sender}: {text}"));
OnFriendRequest(request => Log($"friend request from {request.RequesterName}"));
OnTradeOpened(() => Log("trade opened"));
```

## Remove a handler

```csharp
IDisposable chat = OnChat(chat => Log(chat.Message));
await Delay(60000);
chat.Dispose();
```

The managers behind `Room` and `Game` also have plain .NET events, such as `Room.Entered`. A
handler added to one of them with `+=` is not removed when the script stops and keeps running in
QX Scripter, so subscribe through the `On...` methods.

## Packets

`OnIn` and `OnOut` intercept raw or parsed packets. See [Packets](packets.md).
