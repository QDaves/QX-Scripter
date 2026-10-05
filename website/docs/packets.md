# Packets

Scripts can send, intercept, block and wait for packets. Messages are named with the Flash client's
message names, such as `MoveAvatar` or `ObjectUpdate`.

## Send a packet

`SendToServer` and `SendToClient` send a message by name with its values in order:

```csharp
SendToServer("MoveAvatar", 5, 8);
SendToServer("ClickFurni", 874336973, 0);
```

`Out["Name"]` and `In["Name"]` resolve a name to its header for the current session:

```csharp
Send(Out["MoveAvatar"], 5, 8);
```

`Msg.In` and `Msg.Out` hold every Flash name as a constant, so the compiler catches a misspelled
name:

```csharp
SendToServer(Msg.Out.MoveAvatar, 5, 8);
OnIn(Msg.In.Chat, e => Log("chat"));
```

## Intercept a packet

`OnOut` and `OnIn` run a handler for every matching packet that passes through the interceptor.
Packets that scripts or QX send themselves do not reach them. The handler receives an
<xref:Qx.Interception.Intercept>:

```csharp
OnOut("MoveAvatar", e =>
{
    var reader = e.Packet.Reader();
    Log($"walking to {reader.ReadInt()}, {reader.ReadInt()}");
});
```

## Intercept a parsed message

The generic overloads parse the packet into a message type:

```csharp
OnOut<WalkRequest>("MoveAvatar", walk => Log($"walking to {walk.X}, {walk.Y}"));
OnIn<FloorItemUpdate>("ObjectUpdate", update => Log($"item {update.Item.Id} updated"));
```

A message type is often named differently from its message, like `FloorItemUpdate` for `ObjectUpdate`.
Each message type names its Flash message in its summary or remarks, the
<xref:Qx.Game.Protocol.MessageContracts> reference shows each message's type as the field type, and
the MCP tool `get_protocol_messages` returns it as `model`.

## Block a packet

```csharp
OnOut<WalkRequest>("MoveAvatar", (walk, e) =>
{
    e.Block();
    Log($"blocked a walk to {walk.X}, {walk.Y}");
});
```

## Wait for a packet

`ReceiveAsync` waits for the next matching packet and returns it. A parsed overload returns the
message:

```csharp
Walk(5, 8);
IPacket update = await ReceiveAsync("UserUpdate", timeoutMs: 5000);
UserUpdate parsed = await ReceiveAsync<UserUpdate>("UserUpdate");
```

`ReceiveAsync` and `Receive` watch both directions, so pass the message name only. A name with an
`in:` or `out:` prefix throws an `ArgumentException`. With a QX message type, `ReceiveAsync<T>`
watches only the direction that is parsed into it, so `ReceiveAsync<AvatarChat>("Chat")` never
returns the script's own outgoing chat.

The returned packet is a copy that belongs to the script.

## Message keys

Flash names belong to a client build, and a new build can rename a message. Code that has to keep
working across builds names the message by its key from `MessageKeys`, which stays the same and is
resolved to the current Flash name:

```csharp
SendToServer(MessageKeys.Room.Movement.Walk, 5, 8);
Ext.Intercept(MessageKeys.Room.FloorItem.Updated, e => Log("item updated"));
```

`Ext.Intercept` handlers are removed when the script stops, like the `On...` handlers.

## Wrong names and types

The editor, `compile_check` and every run check the message names written as text:

- An unknown name is warning `QX1001`, with the closest known name, or the direction the name
  exists in when it was used for the other one. It stays a warning because a newer client build
  can add names, and a name the connected build knows is accepted.
- A QX message type that the message is not parsed into is error `QX1002`, such as
  `OnIn<FloorItemDataUpdate>("Chat", ...)`. Types a script defines itself are not checked.

A name built at run time is checked when the handler or the wait is registered: an unknown name
writes one warning line to the script output, and a wrong QX type throws an `ArgumentException`.
