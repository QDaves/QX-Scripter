# Waiting and room scope

A script waits for a condition or a room without blocking, and binds work to the current room visit
with a room scope.

## Wait for a condition

`WaitUntil` checks a condition after every room change and at least every `pollMs`. It returns
`false` when the time runs out instead of throwing, so the script decides what a timeout means.

```csharp
Walk(10, 4);
if (!await WaitUntil(() => SelfAvatar is { X: 10, Y: 4 }, timeoutMs: 5000))
    Log("did not arrive");
```

## Wait for a room

`WaitRoomReady` waits until a room is loaded with the local avatar and its furni.

```csharp
EnterRoom(12345678);
if (await WaitRoomReady(12345678))
    Log("ready");
```

`Room.NextChange` is a task that completes after the next change to the room state.

## Room scope

`CaptureRoom` binds work to the current visit. The scope stays current until the room is left or
entered again, or the local avatar leaves.

```csharp
var scope = CaptureRoom();
while (scope.IsCurrent)
{
    UseFurni(QueryFloorItems().OfIdentifier("edice").NearestTo(SelfAvatar!.XY)!);
    await Delay(3000);
}
```

| Member | Description |
| --- | --- |
| `IsCurrent` | Whether the visit is still going on. |
| `ThrowIfChanged()` | Throws <xref:Qx.Scripting.RoomChangedException> when the visit is over. |
| `Token` | A token that is canceled when the session ends. |
| `RoomId`, `SelfIndex`, `Generation` | The visit the scope was captured in. |
