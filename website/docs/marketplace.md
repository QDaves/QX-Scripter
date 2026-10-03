# Marketplace

The marketplace methods search, buy and sell offers and report the hotel's verdict. The awaited
methods return it as a result code instead of throwing on a refusal.

## Search and buy

```csharp
var page = await SearchMarketplace("throne", maxPrice: 500, sort: MarketplaceSortOrder.LowestPrice);
Log($"{page.TotalItemsFound} offers");

var cheapest = page.Offers.FirstOrDefault();
if (cheapest is not null)
{
    MarketplaceBuyResult result = await BuyMarketplaceOfferAsync(cheapest.OfferId);
    Log(result.ResultCode);
}
```

`BuyMarketplaceOffer` and `CancelMarketplaceOffer` only send the request; their `...Async` forms
wait for the answer.

## Sell

`SellOnMarketplace` lists inventory items at a price per item. Check `CanSellOnMarketplace` first,
since the hotel refuses once the account has reached its offer limit:

```csharp
var sellable = InventoryItems.FirstOrDefault(item => item.IsSellable);
if (sellable is not null)
    Log((await SellOnMarketplace(150, sellable)).Result);
```

`GetMyMarketplaceOffers` returns the local user's own offers with the credits waiting to be
redeemed, and `RedeemMarketplaceCredits` moves those credits into the wallet. `GetMarketplaceStats`
returns the price history of one furni kind.

## State and events

`LatestMarketplaceSearch`, `OwnMarketplaceOffers` and `MarketplaceSettings` hold what the hotel
sent last, without asking again. The `OnMarketplace...` events fire for every marketplace message on
the connection, including the ones the game client asked for.
