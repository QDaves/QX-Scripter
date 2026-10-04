# Requests

Requests ask the hotel for data it does not send on its own and return the answer. They are
awaited, take a timeout in milliseconds and stop when the script is stopped.

| Method | Returns |
| --- | --- |
| `GetProfile(userId)` | A user's extended profile. |
| `GetGroup(groupId)` | A group's details. |
| `GetBadges(userId)` | The badges a user wears. |
| `GetRelationship(userId)` | A user's relationship counts. |
| `GetPetInfo(petId)` | A pet's statistics. |
| `GetSticky(itemId)` | A sticky note's color and text. |
| `GetRoomData(roomId)` | A room's navigator data. |
| `GetRoomSettings(roomId)` | The settings of an owned room. |
| `GetRights()` | The users with rights in the current room. |
| `GetBadgeInventory()` | The badges the local user owns. |
| `GetAchievements()` | The achievement progress. |
| `GetGuildMembers(groupId, pageIndex)` | One page of a group's members. |
| `GetAllGuildMembers(groupId)` | Every member of a group, as a query. |
| `GetGuildMemberships()` | The groups the local user is in. |
| `GetWardrobe()` | The saved outfits. |
| `GetCatalogIndex()`, `GetCatalogPage(pageId)` | The catalog index and one catalog page. |
| `SearchRooms(code, filter)`, `SearchRoomQuery(code, filter)` | Navigator results. |
| `SearchUser(name)` | One user from the user search, or `null`. |
| `SearchMarketplace(name, minPrice, maxPrice)` | Marketplace offers. |
| `GetMarketplaceStats(furniCategory, kind)` | The price history of one item. |

## Look up a user

```csharp
UserSearchResult? found = await SearchUser("somebody");
if (found is null)
{
    Log("no such user");
    return;
}

UserProfile profile = await GetProfile(found.Id);
Log($"{profile.Name}, created {profile.Created}, score {profile.AchievementScore}");
```

## Timeouts

Every request takes `timeoutMs`, 10000 by default. A request that gets no answer in time throws
<xref:Qx.Game.RequestTimeoutException>.

```csharp
try
{
    var group = await GetGroup(123456, timeoutMs: 5000);
    Log(group.Name);
}
catch (RequestTimeoutException)
{
    Log("no answer");
}
```

Requests of the same kind are sent one after another and matched to their own answer, so two
scripts asking at the same time do not get each other's result.
