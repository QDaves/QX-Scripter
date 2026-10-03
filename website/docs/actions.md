# Actions

Actions send what the game client would send. They return at once and do not wait for the hotel to
answer. Use [events](events.md) or `WaitUntil` to see the result.

An action throws <xref:System.InvalidOperationException> when there is no hotel session, and a room
action also when there is no ready room.

## Chat

```csharp
Talk("hello");
Shout("HELLO");
Whisper("friend", "psst");
Talk("styled", bubble: 3);
```

## Avatar

```csharp
Walk(5, 8);
LookTo(6, 8);
Dance(2);
Wave();
Sign(7);
Sit();
Stand();
SetMotto("scripting");
```

`Dance(0)` stops dancing. `Expression(type)` plays any expression; `Wave()` is expression 1.

## Rooms

```csharp
EnterRoom(12345678);
EnterRoom(12345678, "password");
LeaveRoom();
```

## Furni

| Method | Description |
| --- | --- |
| `UseFurni(item)` | Uses a floor or wall item, the way a double click does. |
| `ClickFurni(item)` | Clicks an item the way the client does, for example walking up to a teleporter. |
| `UseFloorItem(id, state)`, `UseWallItem(id, state)` | Uses an item by id. |
| `ClickFloorItem(id)`, `ClickWallItem(id)` | Clicks an item by id. |
| `PlaceFloorItem(item, location, direction)`, `PlaceWallItem(item, location)` | Places an item from the inventory. |
| `MoveFloorItem(id, x, y, direction)` | Moves or rotates a placed item. |
| `PickupFurni(item)` | Picks an item up into the inventory. |

```csharp
var dice = QueryFloorItems().OfIdentifier("edice").NearestTo(SelfAvatar!.XY);
if (dice is not null)
    UseFurni(dice);
```

## People

```csharp
var user = GetUser("somebody");
if (user is not null)
{
    RespectUser(user);
    AddFriend(user);
    OpenTrade(user);
}
```

With rights in the room: `Kick(userId)`, `Mute(userId, minutes)`, `Ban(userId)`,
`GiveRights(userId)` and `LetIn(name)` for a user at the doorbell.
