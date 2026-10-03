# Application layer

Every game operation of QX Scripter is an application member with an id such as `room.chat.talk`.
The script globals wrap many of them; `Application` reaches all of them, including the ones without
a global.

## Call a member

`ApplicationMemberIds` names every member. `InvokeAsync` takes the member's request and returns its
result:

```csharp
await Application.InvokeAsync<ProfileUserRequest, ProfileDispatchResult>(
    ApplicationMemberIds.ProfileBlockAdd,
    new ProfileUserRequest(12345));
```

The request and result types must be the ones the member declares; any other pair throws an
`ArgumentException` that names the right ones. A member that is not available in the current
session, for example without a room, throws `ApplicationUnavailableException`.

## Subscribe to an event

```csharp
Application.Subscribe<WalletChanged>(ApplicationMemberIds.WalletChanged, change =>
    Log($"credits: {change.Credits}"));
```

Handlers added through `Application` are removed when the script stops, and calls without a
cancellation token stop with the script.

## Ids and tools

`Application.Describe(id)` returns a member's title, request, result and availability. The same
members are MCP tools named `application_` plus the id with dots turned into underscores, so
`room.chat.talk` is `application_room_chat_talk`. The MCP tool `describe_application_member`
shows the C# call for any member.
