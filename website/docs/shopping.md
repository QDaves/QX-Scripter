# Shopping

Catalog purchases send the request and return at once. Check the price before buying and read the
hotel's answer from `OnPurchase` or `LastPurchase`.

## Find an offer

`GetCatalogIndex` returns the page tree of the catalog and `GetCatalogPage` one page with its offers;
both keep a copy for five minutes. `LoadCatalog` loads every page once, and `FindCatalogOffers` then
searches them by furni name without asking the hotel.

## Buy

An offer can cost credits, an activity currency such as duckets or diamonds, or both. `CanAfford`
checks both prices against the wallet:

```csharp
await LoadCatalog();
CatalogOfferMatch? match = FindCatalogOffers("throne").FirstOrDefault();
if (match is not null && CanAfford(match.Offer))
    BuyFromCatalog(match.Page.PageId, match.Offer.OfferId);
```

`CanAfford` throws while the activity point balances have not arrived (`IsPointsLoaded`) and the
offer costs activity points, and it cannot check a silver price because the wallet does not track
silver. A purchase with an invalid argument or without a hotel session throws at the call.

## The answer

`BuyFromCatalog` does not wait for the hotel. The answer arrives through `OnPurchase` and stays in
`LastPurchase` until the next one:

```csharp
OnPurchase(outcome =>
{
    if (outcome.Succeeded)
        Log($"bought offer {outcome.Offer?.OfferId}");
    else
        Log($"purchase failed: {outcome.Status}, error {outcome.ErrorCode}");
});
```

## Special offers

Some offers carry a choice made in the shop, in a format the hotel silently refuses when it is
wrong. These methods build it the way the client does:

| Method | Buys |
| --- | --- |
| `BuyPet(pageId, offerId, name, paletteId, color)` | A pet with its name and colors. |
| `BuyBadgeItem(pageId, offerId, badgeCode)` | An offer that displays one of the local user's badges. |
| `BuyGroupItem(pageId, offerId, groupId)` | An offer tied to one of the local user's groups. |
| `BuyEngraved(pageId, offerId, inscription)` | An engraved offer such as a trophy. |
| `BuyRoomEvent(pageId, offerId, roomId, name, description)` | A room event in the navigator. |
| `BuyGiftFromCatalog(pageId, offerId, receiverName, message, spriteId: GiftWrapping!.DefaultStuffTypes[0])` | Any giftable offer, wrapped as a gift. |

The gift wrapping ids for `spriteId`, `boxType` and `ribbonType` come from `GiftWrapping`, which is
filled after `RequestGiftWrappingConfiguration()`.

`PlaceBuildersClubFurni` and `PlaceBuildersClubWallItem` place a Builders Club item straight into
the room instead of buying it into the inventory.
