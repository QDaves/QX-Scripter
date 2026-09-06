using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;
using System.Globalization;

namespace Qx.Scripting;

/// <content>
/// The catalog offers that are not a plain "buy this furni".
/// <para>
/// Several offer kinds carry a selection the buyer makes in the shop, and the hotel expects it in
/// the purchase's extra-data string in a format that differs per kind. Getting that string wrong is
/// silently refused rather than reported, so each kind gets its own helper that builds it the way
/// the client does.
/// </para>
/// <para>
/// Builders Club is the other exception: it does not buy into the inventory at all. The placement
/// is part of the purchase, so the target tile or wall travels with it.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Buys a pet, which needs a name, a colour palette and a colour.
    /// </summary>
    /// <remarks>
    /// The hotel expects these as one string: the name, the palette id and the colour as six
    /// upper-case hexadecimal digits, separated by newlines. The name is validated server-side for
    /// length and for characters, so a refusal here is usually the name rather than the funds.
    /// </remarks>
    /// <param name="pageId">The catalog page the pet offer sits on.</param>
    /// <param name="offerId">The pet offer.</param>
    /// <param name="name">The pet's name.</param>
    /// <param name="paletteId">The colour palette, taken from the offer's available palettes.</param>
    /// <param name="color">The colour, as a 24-bit RGB value.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyPet(
        int pageId,
        int offerId,
        string name,
        int paletteId,
        int color = 0xFFFFFF,
        int timeoutMs = 10000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return BuyFromCatalog(pageId, offerId, PetPurchaseData(name, paletteId, color), 1, timeoutMs);
    }

    /// <summary>
    /// Builds the extra-data string a pet purchase carries.
    /// </summary>
    /// <remarks>
    /// Exposed on its own so a script can send the purchase through another path and still get the
    /// format right. Mirrors the client's own construction.
    /// </remarks>
    /// <param name="name">The pet's name.</param>
    /// <param name="paletteId">The colour palette.</param>
    /// <param name="color">The colour, as a 24-bit RGB value.</param>
    public static string PetPurchaseData(string name, int paletteId, int color = 0xFFFFFF)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string hex = (color & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);
        return $"{name}\n{paletteId.ToString(CultureInfo.InvariantCulture)}\n{hex}";
    }

    /// <summary>
    /// Buys an offer that displays one of the local user's badges, such as a badge display furni.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="badgeCode">Which badge to show; the code as it appears in the badge inventory.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyBadgeItem(
        int pageId,
        int offerId,
        string badgeCode,
        int timeoutMs = 10000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(badgeCode);
        return BuyFromCatalog(pageId, offerId, badgeCode, 1, timeoutMs);
    }

    /// <summary>
    /// Buys an offer tied to one of the local user's groups, such as group furni.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="groupId">Which group it belongs to.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyGroupItem(
        int pageId,
        int offerId,
        Id groupId,
        int timeoutMs = 10000) =>
        BuyFromCatalog(pageId, offerId, groupId.ToString(), 1, timeoutMs);

    /// <summary>
    /// Buys an engraved offer such as a trophy, where the buyer supplies the inscription.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="inscription">The text to engrave.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyEngraved(
        int pageId,
        int offerId,
        string inscription,
        int timeoutMs = 10000) =>
        BuyFromCatalog(pageId, offerId, inscription ?? "", 1, timeoutMs);

    /// <summary>
    /// Reads which rooms may be advertised, and whether the account's membership extends an event.
    /// </summary>
    /// <remarks>
    /// Worth reading before <see cref="BuyRoomEvent"/>: only the rooms listed here are eligible, and
    /// the extended form needs the membership this reports. The client silently drops the extended
    /// flag when the membership has run out, so a script that assumes it buys the short form instead.
    /// </remarks>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public async Task<RoomAdPurchaseInfo> GetRoomEventInfo(int timeoutMs = 10000)
    {
        RoomAdInfoReadResult result = await Application
            .InvokeAsync<RoomAdInfoReadRequest, RoomAdInfoReadResult>(
                ApplicationMemberIds.CatalogRoomAdInfoGet,
                new RoomAdInfoReadRequest(timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (result.MessagesDispatched != 1)
            throw new InvalidOperationException("The room advertisement request was not dispatched exactly once.");
        var rooms = new RoomAdRoom[result.Rooms.Count];
        for (int index = 0; index < rooms.Length; index++)
        {
            RoomAdRoomView room = result.Rooms[index];
            rooms[index] = new RoomAdRoom(room.RoomId, room.RoomName, room.HasControllers);
        }
        return new RoomAdPurchaseInfo(result.IsVip, Array.AsReadOnly(rooms));
    }

    /// <summary>
    /// Buys a room event, which advertises a room in the navigator for a while.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="roomId">Which room to advertise.</param>
    /// <param name="name">The event's title as shown in the navigator.</param>
    /// <param name="description">The event's description.</param>
    /// <param name="categoryId">Which navigator category it is listed under.</param>
    /// <param name="extended">Whether to run the longer form, which needs the membership.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyRoomEvent(
        int pageId,
        int offerId,
        Id roomId,
        string name,
        string description = "",
        int categoryId = 0,
        bool extended = false,
        int timeoutMs = 10000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Game.Catalog.PurchaseAsync(
            Msg.Out.PurchaseRoomAd,
            new PurchaseRoomAd(pageId, offerId, roomId, name, extended, description, categoryId),
            timeoutMs,
            Ct);
    }

    /// <summary>
    /// Buys a Builders Club floor offer directly into a spot in the current room.
    /// </summary>
    /// <remarks>
    /// Builders Club does not stock the inventory: the item is placed as it is bought, so this
    /// needs the tile and rotation up front. Fire-and-forget - the placement arrives as an ordinary
    /// floor item add, and a rejected spot simply produces nothing.
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to place.</param>
    /// <param name="x">Target tile column.</param>
    /// <param name="y">Target tile row.</param>
    /// <param name="direction">Rotation to place it at.</param>
    /// <param name="extraData">The offer's selection data, empty when it takes none.</param>
    public void PlaceBuildersClubFurni(
        int pageId,
        int offerId,
        int x,
        int y,
        int direction = 0,
        string extraData = "") =>
        _ = Application.Invoke<
            SubscriptionBuildersClubFloorPlaceRequest,
            SubscriptionBuildersClubPlacementDispatchReceipt>(
                ApplicationMemberIds.SubscriptionsBuildersClubFloorOfferPlace,
                new SubscriptionBuildersClubFloorPlaceRequest(
                    pageId,
                    offerId,
                    x,
                    y,
                    direction,
                    extraData),
                Ct);

    /// <summary>
    /// Buys a Builders Club wall offer directly onto a wall in the current room.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to place.</param>
    /// <param name="wallLocation">Where on the wall it goes, in the <c>:w=x,y l=x,y r</c> form.</param>
    /// <param name="extraData">The offer's selection data, empty when it takes none.</param>
    public void PlaceBuildersClubWallItem(
        int pageId,
        int offerId,
        string wallLocation,
        string extraData = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wallLocation);
        _ = Application.Invoke<
            SubscriptionBuildersClubWallPlaceRequest,
            SubscriptionBuildersClubPlacementDispatchReceipt>(
                ApplicationMemberIds.SubscriptionsBuildersClubWallOfferPlace,
                new SubscriptionBuildersClubWallPlaceRequest(
                    pageId,
                    offerId,
                    wallLocation,
                    extraData),
                Ct);
    }
}
