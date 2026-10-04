# Variable Fx

The Fx bar shows Wired variable values above avatars and furni. QX Scripter keeps every value the
room shows, so a script started later still knows them.

## Read values

```csharp
foreach (var value in VariableFxOf(SelfAvatar!))
    Log($"{value.Icon}: {value.Value}");

long? gold = VariableFxOf(SelfAvatar!, "gold")?.Value;
```

`VariableFx` holds every value in the room and `VariableFxConfigs` the configurations. The second
argument of `VariableFxOf` is a variable id or the icon its configuration names.

A <xref:Qx.Game.VariableFxValue> carries:

| Member | Description |
| --- | --- |
| `Value` | The current value. |
| `MinValue`, `MaxValue` | The range, from the server or from the configuration. |
| `Level`, `MaxLevel`, `IsMaxed` | The level of a leveling variable. |
| `IsUser`, `EntityId` | Whether it belongs to an avatar, and the room index or the item id. |
| `ConfigId`, `VariableId`, `Config`, `Icon`, `Extra` | Where the value comes from. |
| `Revision`, `Timestamp` | When the value arrived. |

## Changes

```csharp
OnVariableFxChanged((value, previous) =>
    Log($"{value.Icon}: {previous?.Value} to {value.Value}"));

OnVariableFxRemoved(value => Log($"{value.Icon} removed"));
```

`previous` is `null` for a new value. When a configuration arrives, changes or goes, its values are
reported again with the new configuration.

## Values without an Fx bar

Without Wired read rights the Fx bar is the only variable data the hotel sends. `GetVariableValue`
reads the Fx bar first and asks the Wired menu when `CanReadWired` is `true`; there the variable may
also be its display name.

```csharp
long? points = await GetVariableValue(SelfAvatar!, "points");
```

## Edit the display

`WiredFxStyles` defines 38 styles across six categories with typed style/color/width/renderer enums.

```csharp
var review = await GetWiredForm(123);
var form = (WiredVariableFxProgressBarAddonForm)review.CreateForm();
WiredFxStyles.Get(WiredFxProgressStyle.BlockBar).ApplyTo(form, color: WiredFxColor.Blue);
form.Segments = 10;
Log((await SaveWiredForm(review, form)).Success);
```

`ApplyTo` validates choices and atomically sets defaults/dependencies, including level sub-renderers
and segment support. Reads preserve unknown values. `StandardIcons` lists number-display icons;
`CampaignIcons` requires the campaign setting or security permission 4.
`GetWiredFxStyles(category)` / `wired.fx.styles` / `application_wired_fx_styles` reads the catalog.
