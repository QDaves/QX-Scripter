# Migrating scripts

This release gives every script capability one name, moves types to the namespaces their direction
and audience call for, and drops the client type, since Flash is the only client. The tables list
every old name with its replacement.

## Names kept for one release

These still compile, with an obsolete warning that names the replacement. The next release
removes them.

| Old | New |
| --- | --- |
| `Me` | `SelfAvatar` |
| `Self` | `SelfProfile` |
| `Say` | `Talk` |
| `Status` | `Log` |
| `UseGate` | `EnterOneWayDoor` |
| `FurniData` | `GameData.Furni` |
| `Move(item, location, direction)` | `MoveFloorItem(item, location, direction)` |
| `SendMessage(userId, text)` | `SendPrivateMessage(userId, text)` |
| `OnRoomReady` | `OnRoomLoading` |
| `OnRoomExited` | `OnLeftRoom(exit => ...)` |
| `OnFloorItemRemovedDetailed` | `OnFloorItemRemoved`, which now passes the item |

## Script globals

| Old | New |
| --- | --- |
| `AcceptGroupMember` | `ApproveGroupMember` |
| `AchievementState` | `Game.Achievements` |
| `Action(type)` | `Expression(type)` |
| `AddPostIt(id, location, color, text)` | `PlaceStickyWithPole(id, WallLocation.ParseString(location), color, text)` |
| `CancelOffer` | `RemoveTradeItem` |
| `CanMute`, `CanMuteInRoom` | `RoomAuthority.CanMute`, `null` until the room details arrive |
| `CanUnban`, `IsRoomOwner` | `Room.IsOwner` |
| `Client` | removed |
| `CollectMarketplaceEarnings` | `RedeemMarketplaceCredits` |
| `Controllers` | `Room.Controllers` |
| `CurrentRoomQueue` | `Room.QueueStatus` |
| `DelayAsync` | `Delay` |
| `DeleteSticky`, `RemoveItem` | `DeleteWallItem` |
| `Dismount` | `DismountPet` |
| `Dispose()` | removed; QX Scripter disposes the globals |
| `DoorTile`, `RoomEntryTile` | `Room.EntryTile` |
| `EnsureEnterRoom` | `EnterRoomAsync` |
| `Entities` | `Avatars` |
| `FaceTo` | `LookTo` |
| `FindHabbicon` | `GetHabbicon` |
| `FindUser(name)` | `GetUser(name)` |
| `FriendRequest` | `AddFriend` |
| `GetBadgeName`, `GetEffectName`, `GetHandItemName` | `BadgeName`, `EffectName`, `HandItemName` |
| `GetCatalog(type)`, `GetBcCatalog()` | `await GetCatalogIndex(type)`, `await GetCatalogIndex("BUILDERS_CLUB")` |
| `GetBcCatalogPage(pageId)` | `await GetCatalogPage(pageId, -1, "BUILDERS_CLUB")` |
| `GetEntityById(id)` | `Room.AvatarById(id)` |
| `GetEntityByIndex(index)`, `GetUser(index)`, `GetPet(index)`, `GetBot(index)` | `Room.AvatarByIndex(index)` |
| `GetUserById`, `GetPetById`, `GetBotById` | `GetUser(id)`, `GetPet(id)`, `GetBot(id)` with an `Id` |
| `GetFurniInfo`, `GetFurniName` | `FurniOf`, `FurniName` |
| `GetGroupMembers(...)` with a `GroupMemberSearchType` | `await GetGuildMembers(...)` with a `GuildMemberSearchType` |
| `GetMarketplaceInfo` | `await GetMarketplaceStats` |
| `GetNav(code, filter)` | `await SearchRooms(code, filter)` |
| `SearchNav(code, filter)`, `QueryNav(text)` | `await SearchRoomQuery(code, filter)`, `await SearchRoomQuery("query", text)` |
| `SearchNavByName`, `SearchNavByOwner`, `SearchNavByTag`, `SearchNavByGroup` | `await SearchRoomsByName`, `SearchRoomsByOwner`, `SearchRoomsByTag`, `SearchRoomsByGroup` |
| `GetUserAchievements`, `GetUserGroups`, `GetUserMarketplaceOffers` | `await GetAchievements()`, `GetGuildMemberships()`, `GetMyMarketplaceOffers()` |
| `GetUserRooms` | `GetMyRooms` |
| `HasRoomRights` | `Room.HasRights` |
| `InRoom`, `IsInRoom` | `Room.IsInRoom` |
| `IsInQueue`, `IsInRoomQueue` | `Room.IsInQueue` |
| `IsLoadingRoom` | `Room.State == RoomSessionState.Entering` |
| `IsRingingDoorbell` | `Room.IsRingingDoorbell` |
| `IsRoomSpectating` | `Room.IsSpectating` |
| `IsTradeWaitingConfirmation` | `IsTradeAwaitingConfirmation` |
| `LastRoomConnectionFailure` | `Room.ConnectionFailure` |
| `LastRoomExit`, `LastRoomExitKick`, `LastRoomKick`, `WasKickedFromRoom` | `Room.LastExit`, `Room.LastExitKick`, `Room.LastKick`, `Room.WasKicked` |
| `MarketplaceOwnOfferState`, `MarketplaceState` | `OwnMarketplaceOffers`, `Marketplace` |
| `Mount`, `Ride` | `MountPet` |
| `Move(x, y)`, `Move(location)`, `WalkTo` | `Walk` |
| `Move(wallItem, location)` | `MoveWallItem` |
| `Offer` | `OfferTradeItem`, `OfferTradeItems` |
| `OnAction` | `OnAvatarExpression` |
| `OnFlashIn`, `OnFlashOut` | `OnIn`, `OnOut` |
| `OnTradeConfirmed`, `OnTradeWaitingConfirm` | `OnTradeAwaitingConfirmation` |
| `OnWallItemRemovedDetailed` | `OnWallItemRemoved`, which now passes the item |
| `Pickup` | `PickupFurni` |
| `Place(item, location)` | `PlaceFloorItem`, `PlaceWallItem` |
| `PlacePostIt(id, location)` | `PlaceSticky(id, WallLocation.ParseString(location))` |
| `ProductData`, `Texts` | `GameData.Products`, `GameData.Texts` |
| `Purchase(pageId, offerId, count, extra)`, `PurchaseFromCatalog` | `BuyFromCatalog(pageId, offerId, extraData, quantity)` |
| `PurchaseFromCatalogAsGift` | `BuyGiftFromCatalog(pageId, offerId, receiverName, message, extraData, spriteId, boxType, ribbonType, showPurchaserName)`. `showPurchaserName` defaults to `true`, so pass `false` to keep the gift anonymous as `PurchaseFromCatalogAsGift` did by default. A gift is always one item, so `amount` is gone. |
| `Queries` | the `Query...()` methods |
| `QueuePosition`, `RoomQueuePosition` | `Room.QueuePosition`, `null` instead of -1 outside a queue |
| `RemoveGroupFavourite`, `SetGroupFavourite` | `UnsetFavouriteGroup`, `SetFavouriteGroup` |
| `RequestPollContents` | `AcceptPollAsync` |
| `Respect`, `Scratch` | `RespectUser`, `RespectPet` |
| `RollBackRoomState` | `RollbackRoomState` |
| `RoomAccessState`, `RoomAccessRoomId` | `Room.AccessState`, `Room.AccessRoomId` |
| `RoomFloor`, `RoomWallpaper`, `RoomLandscape`, `RoomAnimatedLandscape` | `Room.FloorProperty`, `Room.WallpaperProperty`, `Room.LandscapeProperty`, `Room.AnimatedLandscapeProperty` |
| `RoomProperties`, `RoomVisualization`, `RoomChatSettings` | `Room.Properties`, `Room.VisualizationSettings`, `Room.ChatSettings` |
| `RoomDetails` | `Room.Details`, or `RoomDetailsSnapshot` for a copy |
| `RoomModeration` | `Room.Details?.Moderation`, or `RoomAuthority.WhoCanMute`, `WhoCanKick` and `WhoCanBan` |
| `RoomRightsLevel`, `RoomRightsAreKnown`, `RoomState` | `Room.RightsLevel`, `Room.RightsAreKnown`, `Room.State` |
| `RoomUnbanUser` | `UnbanUser` |
| `SearchUsers(query)` that only sent the search | `RequestUserSearch(query)` |
| `SendMessage(friend, text)` | `SendPrivateMessage(friend, text)` |
| `SetUserFigure(figure, gender)`, `SetUserMotto` | `UpdateFigure(gender, figure)`, `SetMotto` |
| `ToggleFurni`, `ToggleFloorItem`, `ToggleWallItem` | `UseFurni`, `UseFloorItem`, `UseWallItem` with a state |
| `UpdateSticky` | `SetStickyData` |
| `UserAchievements`, `UserCredits`, `UserDiamonds`, `UserDuckets` | `Achievements`, `Credits`, `Diamonds`, `Duckets` |
| `UserData`, `UserNameChangeable` | `SelfProfile`, `SelfProfile?.IsNameChangeable` |
| `Variables`, `WatchVariables` | `ExternalVariables`, `WatchRoomVariables` |

## Changed signatures

| Old | New |
| --- | --- |
| `Ban(userId, duration)` with `"RWUAM_BAN_USER_HOUR"` or `"Room_Session"`, `"RWUAM_BAN_USER_DAY"`, `"RWUAM_BAN_USER_PERM"` | `Ban(userId, BanLength.Hour)`, `BanLength.Day`, `BanLength.Permanent` |
| `BuyFromCatalog`, `BuyPet` and the other `Buy...` methods return a task and take `timeoutMs` | They return nothing; `OnPurchase` and `LastPurchase` report the outcome. |
| `BuyGiftFromCatalog(..., boxType, ribbonType, color, anonymous)` | `BuyGiftFromCatalog(..., spriteId, boxType, ribbonType, showPurchaserName)`. Positional numbers reach the same wire fields as before, a positional `anonymous` value now means the opposite, and named arguments reach the field they name. |
| `CatalogManager.PurchaseAsync` | `CatalogManager.Purchase`, which returns nothing |
| `Delay(TimeSpan)` blocks | It returns a task: `await Delay(...)`. `Sleep` blocks. |
| `GetSticky(wallItem)` and `SaveRoomSettings` block | They return tasks to await. |
| `GetMarketplaceStats(int category, ...)`, `SearchMarketplace(..., int sort, ...)` | `MarketplaceFurniCategory`, `MarketplaceSortOrder` |
| `GetMyMarketplaceOffers(timeoutMs)`, `GetMyMarketplaceOffers(category, timeoutMs)` with ints | `GetMyMarketplaceOffers(MarketplaceOwnOffersCategory, timeoutMs)` |
| `SetObjectVariable`, `CreateObjectVariable`, `DeleteObjectVariable`, `GetVariablesForObject` with an int target | a `WiredTarget` |
| `MoveFloorItem(id, location)` turned the item to 0 | `MoveFloorItem(id, location, direction)` needs the direction; `MoveFloorItem(item, location)` keeps the rotation. |
| `OfferTradeItem(long)`, `OfferTradeItems(params long[])` | `Id` and `Id[]` |
| `OnFloorItemRemoved` and `OnWallItemRemoved` pass the id | They pass the removed item: `item.Id`. |
| 17 `On...` methods returned nothing, such as `OnFriendAdded` | Every `On...` method returns an `IDisposable`. |
| `RoomAuthority`, `RoomEnvironment`, `RoomDetailsSnapshot` | They return `RoomAuthorityState`, `RoomEnvironmentState` and `RoomResultDetails`. |
| `ShowBubble(message, index, bubble)` | `ShowBubble(avatar, message, bubble)` |
| `Turn(Directions)`, `WaitRoomReady(long)` | `Turn(Direction)`, `WaitRoomReady(Id)` |
| `Keyboard` of type `ScriptKeyboard` | type `Keyboard` |
| `MessageCapabilityProbe` and `MessageCodec.Capability` with a `MessageManager` | They take an `IMessageResolver`. |
| `MessageManager.LoadVerifiedFallbackCatalog(catalog, preferred)` | `LoadVerifiedFallbackCatalog(catalog)`, which always sets the default catalog |
| `RoomAuthorityState(IsOwner, RightsLevel, RightsKnown, HasRights, IsSpectating)` | Five more positional members: `IsRoomMuted`, `CanMute`, `WhoCanMute`, `WhoCanKick` and `WhoCanBan`. |
| `CraftingProduct.Parse(reader, hasProductCode)`, a `string?` `ProductCode` | `CraftingProduct.Parse(reader)`, a `string` `ProductCode` |
| `FavoriteMembershipUpdate(Index, GroupId, Status, GroupName)` with an `int` `GroupId` | `FavoriteMembershipUpdate(RoomIndex, GroupId, Status, GroupName)` with an `Id` `GroupId` |
| `ClubGiftEligibility.IsVip`, `GiftClubEligibilityView.IsVip` as `bool?` | `bool` |
| `GuildMembers.SearchType`, `GroupMembersPage.SearchType` as `GuildMemberSearchType?` | `GuildMemberSearchType` |
| `MarketplaceCanMakeOfferResult.TokenCount` as `int?` | `int` |
| `InstalledClientMonitor.Candidates` keyed by `InstalledClientFamily` | `InstalledClientMonitor.Candidate` |

Every public parameter is camelCase now, so named arguments change with it: `timeout_ms:` is
`timeoutMs:`, `item_id:` is `itemId:` and the purchase quantity, `count:` in `Purchase` and `amount:` in
`PurchaseFromCatalog`, is now `quantity:`. Constructors and `Deconstruct` methods written by hand on
records follow the same rule, so `new HabboClubOffers(Offers: offers, DaysLeft: 30)` is
`new HabboClubOffers(offers: offers, daysLeft: 30)`. Only the parameters of a positional record keep
the names of its properties.

## Types

| Old | New |
| --- | --- |
| `Qx.Direction`, `Qx.Model.Directions` | `Qx.MessageDirection`, `Qx.Model.Direction` |
| `Qx.Model.Bots`, `.Crafting`, `.Forums`, `.Marketplace`, `.Polls`, `.Quests`, `.Subscriptions` | `Qx.Model` |
| `Qx.Platform.Keyboard`, `Qx.Scripting.ScriptKeyboard` | `Qx.Platform.KeyboardReader`, `Qx.Scripting.Keyboard` |
| `ScriptEngine`, `ScriptProgram`, `ScriptExecutionContext`, `ScriptExecutionError`, `ScriptHeader`, `ScriptRunState`, `QueryJson` and the `Api...`, `ScriptApi...` and `Ui...` types of `Qx.Scripting` | `Qx.Scripting.Hosting` |
| `GroupMemberSearchType` | `GuildMemberSearchType` |
| `ISemanticMessageResolver` | `IMessageResolver` |
| `RequestIgnoreList`, `FavouriteMembershipUpdate` | `IgnoredUsers`, `FavoriteMembershipUpdate` |
| Incoming `ForumStats`, `ForumThreadMessages`, `ForumThread`, `PostForumThreadOk`, `PostForumMessageOk`, `ForumMessage` | `ForumData`, `ThreadMessages`, `PostThread`, `PostThread`, `PostMessage`, `UpdateMessage` |
| Outgoing `GetThreads`, `GetMessages`, `GetThread`, `ModerateThread`, `ModerateMessage`, `UpdateForumReadMarker` | `GetForumThreads`, `GetForumThreadMessages`, `GetForumThread`, `ModerateForumThread`, `ModerateForumMessage`, `UpdateForumReadMarkers` |
| Outgoing `PostForumMessage`, `UpdateForumThread`, `ReportForumThread`, `ReportForumMessage` | `PostMessage`, `UpdateThread`, `CallForHelpFromForumThread`, `CallForHelpFromForumMessage` |

These client messages moved from `Qx.Model.Messages.Incoming` to `Qx.Model.Messages.Outgoing`:
`AcceptQuest`, `ActivateQuest`, `AdvanceNewUserFlowRequest`, `BuildersClubPlaceRoomItem`,
`BuildersClubPlaceWallItem`, `BuildersClubQueryFurniCount`, `CancelQuest`, `CommandBot`, `Craft`,
`CraftSecret`, `FriendRequestQuestComplete`, `GetBotCommandConfigurationData`, `GetBotInventory`,
`GetClubGift`, `GetClubOffers`, `GetCraftableProducts`, `GetCraftingRecipe`,
`GetCraftingRecipesAvailable`, `GetDailyQuest`, `GetGiftWrappingConfiguration`, `GetIsOfferGiftable`,
`GetQuests`, `GetRoomAdPurchaseInfo`, `GetSeasonalQuests`, `NuxGetGifts`, `NuxGiftSelection`,
`OpenQuestTracker`, `PhotoCompetition`, `PlaceBot`, `PollAnswer`, `PresentOpen`, `PublishPhoto`,
`PurchaseFromCatalogAsGift`, `PurchasePhoto`, `PurchaseRoomAd`, `RejectPoll`, `RejectQuest`,
`RemoveBotFromFlat`, `RequestCameraConfiguration`, `SelectClubGift`, `StartPoll`,
`SubscriptionGetKickbackInfo`, `SubscriptionGetUserInfo` and `WalletBalanceRequest`.

Removed without a replacement: `ClientType`, `ClientTypes`, `UnsupportedClientException`,
`ApplicationClientAvailability`, the `Qx.Protocol.Sulek` namespace, the outgoing schema types
(`OutgoingWireType`, `OutgoingCollectionKind`, `OutgoingParameterSchema`, `OutgoingMessageSchema`,
`OutgoingSchemaMatcher`), `MessagesJson`, `MessageEntry`, `MessageMap`, `MessageMapEntry`,
`MessageAlias`, `ClientBuildIdentity`, `ClientBuildBinding`, `ProfileUserNameRequest`, the G-Earth
codec types (`EvaWire`, `GControl`, `GControlFrame`, `GControlReader`, `GControlWriter`, `HMessage`,
`HostInfo`, `HClientType`), the `MessageRegistryQuery` types, `InstalledClientFamily` with the `Family` of
`InstalledClientCandidate` and its change event, `FigurePartType.FlashOnly` and `IsFlashOnly`,
`AchievementManager.IsFlashOnlyDataSupported`, `DailyTaskManager.IsSupported`, `EarningsManager.IsSupported`,
`SessionRules.ClientName` and `CraftingProduct.HasProductCode`. `FloatString`, `MessagesIniParser`
and `ScriptExecutionSnapshot` are internal. The update and bug report types and `ProjectLinks`
belong to the desktop app.

`Qx.Game.Snapshots` is no longer imported, and `Qx.Protocol`, `Qx.Game.Application`,
`System.Diagnostics` and `System.IO` are.

## Other members

| Old | New |
| --- | --- |
| `Client` on `Session`, `Identifier`, `Packet`, `IPacket`, `PacketReader`, `PacketWriter`, `ConnectionSnapshot` and the application records | removed |
| A `null` `Client` on the change events of `Qx.Game.Application` | `Connected` is `false` |
| `Header.All` | `Ext.Intercepted` sees every packet |
| `Ext.Messages` as a `MessageManager` | an `IMessageResolver` that only reads |
| `MessageManager.LoadCatalog`, `LoadFallbackCatalog`, `ClearCatalog`, `BindCatalogBuild`, `HasCatalogBuild`, `TryReplaceSessionCatalog`, `TryGetOutgoingSchemas`, `Map` | removed; the session binds its catalog |
| `GetWireProfile`, `HasCatalog`, `HasMessage`, `TryGetHeader` and the other lookups with a client | the same without the client |
| `MessageDescriptor.NameFor`, `NamesFor`, `Aliases` | `Name`, `Names` |
| `MessageRegistry.AliasCount` | `NameCount` |
| `IMessageContract.Supports` | removed |
| `CatalogPurchaseStatus.Dispatched` | removed |
| `MarketplaceOffer.Average`, `ExtraLong`, `SoldOut`, `MarketplaceTradeInfo.TradeVolume` | `AveragePrice`, `StatusTimeMilliseconds`, `IsUsed`, `SoldAmount` |
| `Ui.Invoke`, `Ui.SetClicked`, `Ui.HasClickHandlers`, `Ui.HandledButtons` and the `Ui` events | `ScriptUiHost.Of(Ui)` for hosts; scripts use `Ui.OnClick` and `Ui.OnChange` |
| `Dispose()` on `Game`, `Room` and the other managers | explicit `IDisposable`, for the host |
| `RoomManager.EnrichFurni`, `RoomVisitorLog.Watch`, the `RoomManager.GameData` setter | internal |
| An override of `Dispose()` in a manager derived from `GameStateManager` | an override of the protected `Close()` |
| `MessageContracts.Leaderboards.TotalRequest`, `TotalSnapshot` and the `Friends`, `Groups`, `WeeklyTotal`, `WeeklyFriends` and `WeeklyGroups` pairs | `Leaderboards.Total.Request`, `Total.Snapshot` and the same paths for the others |
| `MessageContracts.Catalog.Accepted`, `Failed`, `Forbidden` | `PurchaseAccepted`, `PurchaseFailed`, `PurchaseForbidden` |
| `MessageContracts.Room.FloorItemUse`, `FloorItemMove`, `WallItemUse`, `WallItemMove`, `WallItemRemove` | `Room.FloorItem.Use`, `FloorItem.Move`, `WallItem.Use`, `WallItem.Move`, `WallItem.Remove` |
| `MessageContracts.Room.ItemPlace`, `ItemPickup`, `ItemClick`, `ItemPickupConfirmation` | `Room.Item.Place`, `Item.Pickup`, `Item.Click`, `Item.PickupConfirmation` |
| `MessageContracts.Room.Moderation.UserMute`, `UserKick`, `UserBan`, `UserUnban` | `Mute`, `Kick`, `Ban`, `Unban` |
| `Msg.In.FavouriteMembershipUpdate`, `SanctionStatus`, `FriendsListFragment`, second constants for messages that already had one | `Msg.In.FavoriteMembershipUpdate`, `MySanctionStatus`, `FriendListFragment`; the other names still resolve as strings |
| `WiredUpdateRoom.RollBack`, `WiredUpdateRoom.Rollback` | `WiredUpdateRoom.Rollback`, `WiredUpdateRoom.IsRollback` |

## Behavior changes

- `MoveFloorItem(item, location)` keeps the item's rotation.
- `Session == other` means the same connection, so a reconnect gives a different session.
- `Diamonds`, `Duckets` and `Points` return 0 until the balances arrive; `IsPointsLoaded` tells.
- `Delay(timeSpan)` and `SaveRoomSettings(...)` without `await` no longer wait, with only a CS4014
  warning.
- An old `GetUser(5)` now looks up the user with id 5 and returns `null`.
- Handlers registered through `Ext` and `Application` end when the script stops and their errors
  stop the run; `Application.Invoke` without a token is canceled at stop.
- A purchase with an invalid argument throws at the call.
- `(Id)"abc"` throws a `FormatException`.
- Logs show `in:Chat` instead of `in:flash:Chat`.
- An unknown message name writes a warning line to the script output.
- `OnRoomLoading`, formerly `OnRoomReady`, runs before `IsRoomReady` is `true`, as it always did.
- `OnRoomAuthorityChanged` also runs when the room's mute or moderation settings change.
- `OnFloorItemRemoved(id => Log($"{id}"))` still compiles but logs the item.
- `(Direction)1` is the compass direction `NorthEast`, and `SelfAvatar.Direction == Direction.North`
  fails with CS0019 because facing properties are `int`; compare with `(int)Direction.North`.
- Compile errors in the editor and in `compile_check` are in English.
- `SendToServer(key, values)` and `SendToClient(key, values)` resolve the key like the overloads that
  take a model: a key without exactly one header in the active catalog throws instead of sending under
  the last header that matched.
- `CanAfford` needs the activity point balances only when the offer costs activity points.
- `Ext.Messages.HasCatalog()` is `false` before a session until the installed client's catalog is
  loaded.
- An application descriptor whose parameters do not match the single public constructor of its
  request type is rejected.
- Messages without a stable key get new generated `legacy.*` keys; use the Flash name or a
  `MessageKeys` key instead.
- MCP:
  - `search_types` and `search_members` return an object with `items`, `total` and `nextOffset`.
  - `list_api` pages: it starts with a groups line and ends with `total N, nextOffset M`.
  - `list_application_members` returns compact rows; `detail=true` returns 10 full descriptions by
    default and at most 50 per page.
  - `describe_application_member` has no `clients` block, and `availability.client` is
    `availability.connected`.
  - `get_protocol_messages` entries carry `name` and `names` instead of `clients.flash` with
    `supported`, `primary_name` and `aliases`, and gain `model`, `key_member` and `contract`.
    `active.client`, `active.supported`, `session.active_client`, `session.schema_fingerprint` and
    `filters.client` are gone, and the registry count `aliases` is `names`.
  - `compile_check` writes `error CS0103 2:5:` instead of `Error CS0103 (line 2):` and adds a using
    hint.
  - `get_type` leaves obsolete members out of the member index of a large type; a member filter still
    lists them with their message.
  - The crafting output has no `has_product_code`.
  - Unknown arguments such as `client` are rejected, `get_avatar` returns the documented envelope, and
    contract values follow the key paths.

## Wired trade confirmation

`WiredTradeConfirm(false)` means initial acceptance, and `WiredTradeConfirm(true)` means final
confirmation after the countdown. Earlier documentation incorrectly described false as withdrawing
confirmation. The boolean overload and its default remain unchanged for existing scripts.
Use `CompleteWiredTrade(reviewed.Generation, reviewed.Revision)` for the full guarded sequence,
or use the `WiredTradeConfirmationStage` overload for explicit stages. The corresponding application
operations are `wired.trade.complete` and `wired.trade.stage.send`.

`TradeItem.WireType` preserves the original case of the packet's floor/wall marker. `Type` remains
the normal typed view. Changing `Type` to an incompatible kind uses that kind's default marker.
The room item `Id` now preserves every signed 32-bit wire value; it no longer rejects zero or
negative values that the Flash reader accepts. Inventory identity remains in `ItemId`.


## Signed Wired fields and storage context

Wired log levels/sources, optional log filters, condition quantifier types and trade-rule node
types now preserve signed byte values. Code that previously interpreted 255 as the -1 sentinel
must use -1 directly. Values outside -128 through 127 are rejected on composition.
Unknown trade-rule node types retain their amount; only type 1 accepts a furniture-type payload.

Manually constructed `WiredUserVariablesPage` entries must have `IncludesVariableId = false`.
`WiredUserPermanentVariablesList` entries must have `IncludesVariableId = true`.
Incompatible layouts now throw before writing instead of producing a shifted packet.


## Chest and Wired trade field names

The following old properties forward for one release with obsolete warnings. Constructor named
arguments and JSON fields use the new names; positional argument order and wire encodings remain
unchanged. Old properties are omitted from JSON to avoid duplicate settings.

| Record | Old | New |
| --- | --- | --- |
| `SetChestPreferences` | `PrefFlagA` | `EveryoneCanOpen` |
| `SetChestPreferences` | `PrefFlagB` | `EveryoneCanDonate` |
| `SetChestPreferences` | `ChestState` | `StateControlMode` |
| `SetChestPreferences` | `OpenState` | `PreviewMode` |
| `SetChestPreferences` | `AmountPreview` | `PreviewAmount` |
| `SetChestPreferences` | `DisabledFlag` | `WiredEnabled` (same polarity) |
| `SetChestNotificationPreferences` | `NotifyFlagA` | `NotifyWhenFull` |
| `SetChestNotificationPreferences` | `NotifyFlagB` | `NotifyOnDonation` |
| `SetChestNotificationPreferences` | `EventFlagA` | `NotifyOnWithdrawal` |
| `SetChestNotificationPreferences` | `EventFlagB` | `NotifyWhenEmpty` |
| `SetChestNotificationPreferences` | `EventFlagC` | `NotifyOnWiredTransaction` |
| `WiredTradeItemsUpdate`, `WiredTradingItemsSnapshot` | `Extra` | `RequirementsMetCount` |
| `WiredTradeTransactionNotification` | `TradeTransactionNotificationId` | `TradeErrorId` |

Chest fragment numbers are zero-based. A list that starts at 1 no longer appears complete.
Only fragment 0 starts a new list; repeated later fragments do not clear received contents.

### Wired account preferences and Web API keys

Added `AccountPreferences`, `WiredGenerateWebApiKey` and `WiredWebApiKeyResult` in `Qx.Model.Wired`. `MessageContracts.Wired.Account.Preferences` receives the account settings; `MessageContracts.Wired.WebApi.KeyGenerate` and `KeyResult` provide the correlated key messages. The optional account tail is represented with nullable fields, and composition rejects gaps between present fields before writing.

`WiredManager.AccountPreferences`, the script property `WiredAccountPreferences`, and application `wired.preferences.get` read the retained account value. The script method `GenerateWiredWebApiKey` and application `wired.web_api.key.generate` add key generation without changing existing preference writes.

## Typed Wired forms and FX styles

`WiredForm.Read` adds concrete editors for all 176 effective forms in the client's 177 registrations.
Existing `WiredConfig`, raw arrays and category save methods remain available. Unknown definitions
return `UnknownWiredForm`; unknown numeric enum values and untouched trailing fields are retained.
Typed field edits validate received source/selection limits and documented bounds. Derived high
words follow edited signed values; reading alone does not normalize received data.

`GetWiredForm` / `SaveWiredForm` and `wired.form.get` / `wired.form.save` add a reviewed edit workflow.
Saves require the generation and revision from the read; any intervening Wired change requires a
fresh read. Only changed controls are patched. Named field values may use `Choice` for finite enum
options or comma-separated flags. `GetWiredFormDefinitions` / `wired.form.definitions` lists the catalog.

`WiredFxStyles` adds six typed style enums and verified color/width/renderer choices for 38 styles.
`ApplyTo` edits a matching FX form atomically and updates dependent visualization controls.
`GetWiredFxStyles` / `wired.fx.styles` lists the same definitions. Existing runtime FX APIs are unchanged.

## Area-hide room updates and editor

`MessageContracts.Room.Environment.AreaHide` reuses `AreaHideData` for live region updates, so
`FloorPlan.HiddenAreas` now follows changes after the initial floor plan. Updates before the map
arrives are retained within that room generation. A new floor-plan instance preserves previously
read snapshots and camera/map metadata.

`WiredAreaHideSettings.Read` decodes the eight furniture-data slots. `SetAreaHideData` writes the
rectangle and three flags; it does not carry an on/off field. `GetAreaHide`, `SetAreaHide` and
`ToggleAreaHide` expose the separate local read, edit and switch operations. Writes require the
reviewed room generation; settings edits reject an active effect. Their application equivalents
are `wired.area_hide.get`, `.set`, `.toggle`. A write result confirms dispatch, not server acceptance.
Generic furniture use and raw message access remain available.
