# Room state

QX Scripter keeps the game state of the session up to date. A script reads it at any time without
sending anything.

## Session

| Member | Description |
| --- | --- |
| `Session` | The current hotel session, or `null` before login. |
| `SelfProfile` | The local user's account data: id, name, figure, motto. |
| `Credits`, `Diamonds`, `Duckets` | The wallet balances, 0 until loaded (`IsCreditsLoaded`, `IsPointsLoaded`). |

## Room

| Member | Description |
| --- | --- |
| `Room` | The <xref:Qx.Game.RoomManager> with the full room state. |
| `RoomId` | The id of the current room. |
| `IsRoomReady` | Whether the room is ready and the entry is confirmed. Avatars, furni and the heightmap arrive separately. |
| `SelfAvatar` | The local user's avatar in the room, or `null`. |
| `Users`, `Pets`, `Bots` | The avatars in the room. |
| `FloorItems`, `WallItems` | The furni in the room. |
| `Room.Controllers` | The users with rights in the room. |
| `FloorPlan`, `Heightmap` | The room's floor plan and heightmap. |

`WaitRoomReady` waits until the room is ready, its floor items are loaded and the local avatar is in
it:

```csharp
await WaitRoomReady();

Log($"{Users.Count()} users and {FloorItems.Count()} floor items in room {RoomId}");
```

## Inventory and friends

| Member | Description |
| --- | --- |
| `InventoryItems`, `InventoryPets` | The inventory as far as it is loaded. |
| `Friends` | The friend list as far as it is loaded. |
| `Achievements` | The achievement progress. |

The hotel sends the inventory and the friend list in fragments. These methods load them completely
and return the result:

```csharp
var items = await EnsureInventoryLoaded();
var friends = await EnsureFriendsLoaded();
var pets = await EnsurePetInventoryLoaded();
```

## Loaded and stale

State that the hotel only sends on request can be empty because nothing is there or because it has
not arrived yet. The managers and snapshots carry `Loaded` and `Stale` flags that tell the two apart.
