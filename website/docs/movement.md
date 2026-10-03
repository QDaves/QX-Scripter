# Movement

Movement events report every move of avatars and furni together with its cause, and the geometry
helpers measure tiles and directions.

## Movement events

`OnAvatarMovement`, `OnSelfMovement` and `OnFloorItemMovement` report every move together with its
cause.

```csharp
OnSelfMovement(move => Log($"{move.Source}: {move.From} to {move.To}"));
OnAvatarMovement((avatar, move) => Log($"{avatar.Name} {move.Source}"));
OnFloorItemMovement((item, move) => Log($"{item.Id} {move.Source} to {move.To}"));
```

`Source` is `Walk` for every status update of a walking avatar, `Roller` or `Wired` for a slide,
and `Update` for furni moved by a user. A walk that starts or stops on the same tile is reported as
well.

An <xref:Qx.Game.AvatarMovement> carries `From`, `To`, `MovingTo` (the next step of a walk),
`IsStop`, `Relocated`, `Duration` (the wired animation time), `IsSelf`, `Revision` and `Timestamp`.
A <xref:Qx.Game.FloorItemMovement> carries `From`, `To`, `Direction`, `Source`, `Duration`,
`Revision` and `Timestamp`. Wired and roller moves are reported even when they leave the item where
it was.

## Fencing

Read `Room.Revision` before sending a command and keep only movements with a higher `Revision`.
Those are the answer to it.

```csharp
long fence = Room.Revision;
Walk(10, 4);
bool arrived = await WaitUntil(() => SelfAvatar is { X: 10, Y: 4 } && Room.Revision > fence, 5000);
```

`Timestamp` is a <xref:System.Diagnostics.Stopwatch> timestamp taken when the packet arrived:

```csharp
OnSelfMovement(move => Log($"{Stopwatch.GetElapsedTime(move.Timestamp).TotalMilliseconds} ms ago"));
```

## Avatar state

| Member | Description |
| --- | --- |
| `Avatar.MovingTo` | The tile a walking avatar steps onto next, cleared when a roller or Wired moves it. |
| `Avatar.IsMoving` | Whether the avatar is walking. |
| `Avatar.IsSettledAt(point)` | Whether the last status update left the avatar standing on the point. |

`SelfAvatar` is resolved from the room directly, so reading it in a loop is cheap.

## Geometry

| Member | Description |
| --- | --- |
| `Point.Offset(direction)` | The one-tile offset of a direction. |
| `point.Step(direction, distance)` | The tile a number of steps away in a direction. |
| `a.StepsTo(b)` | The number of walking steps between two tiles, with diagonals counting as one. |
| `a.IsNextTo(b)` | Whether two tiles touch, diagonals included. |
| `a.DirectionTo(b)` | The direction from one tile to another. |
| `FloorItem.Front` | The tile the item faces. |

Directions go clockwise from 0 north: 2 is east, 4 south and 6 west.
