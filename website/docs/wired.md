# Wired values

The wired value methods read and write wired variables by their display name. Each call fetches the
room's variable definitions again, so read all values of a holder once and index into them.

## Read

```csharp
RoomVariables variables = await GetRoomVariables();
Log($"{variables.Count} variables");

var global = await GetGlobalValues();
int? round = global["round"];

if (SelfAvatar is { } self)
{
    var mine = await GetUserValues(self.Index);
    Log($"gold: {mine["gold"]}");
}
```

`GetFurniValues`, `GetUserValues` and `GetGlobalValues` return every value of one holder, and
`GetFurniValue`, `GetUserValue` and `GetGlobalValue` read a single one. A user is addressed by their
room index.

## Write

```csharp
await SetGlobalValue("round", 3);
```

`SetFurniValue` and `SetGlobalValue` resolve the name first and then send the write without waiting
for the hotel to apply it. The hotel ignores reads and writes from users without wired rights;
`CanReadWired` and `CanModifyWired` tell whether the local user has them.

## Variable ids

`GetFurniVariables`, `GetUserWiredVariables`, `GetGlobalVariables`, `SetFurniVariable`,
`SetGlobalVariable` and `SetObjectVariable` work with the raw variable ids the wired menu uses
instead of names. `WatchRoomVariables` calls a handler whenever the room's variable definitions
change. Values the Fx bar shows above avatars and furni
are covered in [Variable Fx](variable-fx.md).


## Complete a Wired trade

Wired trades send false to accept, then true after the three-second countdown to confirm.
False does not withdraw acceptance. Sending only true can cause failure 1, Invalid trade.

After reviewing both sides, pass the generation and revision to `CompleteWiredTrade`.
It checks freshness, performs both stages and awaits completion or failure. Offer changes prevent
confirmation; failure cancels only its own trade. Timeout includes the countdown and response.

```csharp
var reviewed = Wired;
if (reviewed.Trade.Items is { CanAccept: true } offer)
{
    Log($"Giving {offer.FirstUserNumItems}; receiving {offer.SecondUserNumItems}");
    var result = await CompleteWiredTrade(reviewed.Generation, reviewed.Revision);
    Log(result.Success ? "Trade completed" : result.Failure);
}
```

The corresponding application operation is `wired.trade.complete`, exposed as
`application_wired_trade_complete`, with required `expected_generation` and `expected_revision`.
Review items and credits first. Success confirms hotel completion, not inventory refresh.

`WiredTradeConfirm(WiredTradeConfirmationStage.Accept)` and `.Confirm`, or the boolean overload,
send one stage without waiting. Application operation: `wired.trade.stage.send`.
`WiredTradeCancel()` cancels the trade.

A pure Wired reward notification is a receipt and needs no trade confirmation.


## Wire values and chests

Log severity/source, condition quantifier and rule-node codes are signed bytes. A filter of -1
remains distinct from an omitted filter. Rule type 1 alone carries furniture-type data.
Permanent-variable storage includes its variable ID; owner-page entries omit it. Set
`IncludesVariableId` accordingly when constructing records; incompatible layouts fail before writing.

Chest fragment 0 starts a snapshot; all zero-based fragments must arrive for completeness.
Duplicate nonzero fragments are ignored. Interleaved additions and removals survive late fragments.
An add-only duplicate retains the item; remove/add replaces it.

`SetChestPreferences` takes `EveryoneCanOpen`, `EveryoneCanDonate`, `StateControlMode`,
`PreviewMode`, `PreviewAmount`, `WiredEnabled` (not inverted). `StateControl` and `Preview`
provide enum views. `SetChestNotificationPreferences` takes `NotifyWhenFull`, `NotifyOnDonation`,
`NotifyOnWithdrawal`, `NotifyWhenEmpty`, `NotifyOnWiredTransaction`; `Notifications` is its enum view.
Use the same-named script methods or `application_wired_chest_preferences_set` /
`application_wired_chest_notification_preferences_set`. Preserve all settings in these replacements.

`LockChests(locked, false)` affects your chests in the room; true affects all room chests.
`WiredContractContents.EarningsCategory` types earnings codes 11 (game) and 13 (agency).
Trade updates expose `RequirementsMetCount`; `TradeErrorId` identifies an error description.

## Account preferences and Web API keys

`WiredAccountPreferences` reads connection-scoped preferences, initially null, retained across
rooms and cleared on disconnect. Omitted optional fields remain null; opaque fields are retained.
`WiredSetPreferences` writes the supported Wired settings.

`GenerateWiredWebApiKey(wiredId, readKey, timeoutMs)` requests a read key (true) or write key (false).
Calls are serialized and match both ID and access type. The timeout includes queue time;
room/session changes invalidate the call. Keys are not stored in general Wired state.
Application/MCP operations: `wired.preferences.get` / `application_wired_preferences_get` and
`wired.web_api.key.generate` / `application_wired_web_api_key_generate`.

## Typed configuration forms

`WiredForm.Read(configuration)` copies a received `WiredConfig` into a concrete editor.
`WiredFormRegistry.Definitions` covers 176 forms (177 registrations), negative aliases and
569 named fields with constraints, dependencies and evidence. Named properties use enums for
finite choices/flags. Unknown numeric values remain representable; `UnknownWiredForm` retains
unknown forms. `ToUpdate()` returns the existing category save model and preserves untouched fields.

`GetWiredForm` opens a configuration and returns named values, defaults, choices and a review token.
`CreateForm()` returns its editor. `SaveWiredForm` saves only changed controls and awaits the result.
Any intervening Wired revision or room change invalidates the review, including during queueing;
read again before retrying.

```csharp
var review = await GetWiredForm(123);
var form = (WiredAvatarSaysSomethingTriggerForm)review.CreateForm();
form.MatchMode = WiredMatchModeOption.ExactMatch;
form.Text = "open";
var result = await SaveWiredForm(review, form);
Log(result.Success);
```

`SelectFurniture` checks received selection limits and wall permission. `SelectFurnitureSource`
and `SelectUserSource` check received allowed choices; enums include selectors and signals.
Default source lists and `GetDefaultField` expose hotel defaults separately from UI initial values.
`WiredValueReference` models shared value/variable controls. `WiredFormPacking` supplies neighborhood
coordinates and reward rows. Editing signed values updates their paired high word.

Generic editors use named `GetField`, `SetField` or atomic `SetFields`. A `WiredFormValue` supplies
exactly one of `Integer`, `Boolean`, `Text`, `Tiles`, `Rewards`, `Choice`.
`new WiredFormValue(Choice: "ExactMatch")` selects a named option; flags accept comma-separated names.

`GetWiredFormDefinitions(category, code)` reads the local catalog. Definition `Usage` provides
English setup/targeting advice, pitfalls and related furniture, including alternatives.
Rules label client, hotel or unverified community evidence. Received source/selection limits apply.
Application/MCP operations:
`wired.form.definitions`, `wired.form.get`, `wired.form.save` (prefix `application_`, dots become
underscores). Save takes `furni_id`, `expected_generation`, `expected_revision`, `patch`,
`timeout_milliseconds`. `WiredFormPatch` supports named fields, both furniture selections,
indexed furniture/user sources, action delay, condition quantifier and selector flags.

## Area-hide furniture

`GetAreaHide(id)` reads the local furniture data and current room generation. Edit while off:

```csharp
var view = GetAreaHide(123);
var update = view.Settings.ToUpdate() with { Width = 4, Length = 3 };
await SetAreaHide(update, view.RoomGeneration);
```

`ToggleAreaHide(id, roomGeneration)` uses the client's on/off command. These writes return dispatch
receipts; hotel permissions still apply. `FloorPlan.HiddenAreas` follows live region updates.
Application/MCP operations are `wired.area_hide.get`, `.set`, `.toggle` (prefix `application_`,
dots become underscores). Rectangle bounds are server-defined; no guessed numeric limits apply.

## Contract editing

`Wired.Contract.Contents` retains the last received contract. Type 0 carries payment mode/text/layout,
type 1 trade rules, type 2 reward category/dialog/text. Preserve the complete record when editing:

```csharp
if (Wired.Contract.Contents is { ContractType: WiredContractContents.TypeReward } contract)
{
    var result = await UpdateContract(contract with { ShowDialog = true, RewardText = "Thank you!" });
    Log(result.IsSuccess ? "Saved" : result.FailCode);
}
```

`wired.contract.open` requests contents; `wired.contract.update` saves and correlates the result by
contract ID. Both have MCP tools using the `application_` prefix and underscores in place of dots.
