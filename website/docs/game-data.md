# Game data

Game data is the hotel's list of furni, products, badges and effects. It turns ids into names.

| Method | Description |
| --- | --- |
| `FurniName(item)` | The display name of a floor, wall, inventory or trade item. |
| `FurniOf(item)` | The furni data entry of a floor, wall, inventory or trade item, or `null`. |
| `IsIdentifier(item, identifier)` | Whether an item is of a furni class, such as `"edice"`. |
| `ProductName(code)`, `ProductDescription(code)` | A catalog product's name and description. |
| `BadgeName(code)` | A badge's name. |
| `EffectName(id)` | An avatar effect's name. |
| `HandItemName(id)` | A hand item's name. |

```csharp
foreach (var item in FloorItems.Take(10))
    Log($"{item.Id} {FurniName(item)}");

var dice = FloorItems.Where(item => IsIdentifier(item, "edice"));
```

`GameData` gives the full data set.
