using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
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
    /// Buys a pet, which needs a name, a color palette and a color.
    /// </summary>
    /// <remarks>
    /// The hotel expects these as one string: the name, the palette id and the color as six
    /// upper-case hexadecimal digits, separated by newlines (see <see cref="PetPurchaseData"/>).
    /// The name is validated by the server for length and for characters, so a refusal here is
    /// usually the name rather than the funds. It returns once the request is sent, as with
    /// <see cref="BuyFromCatalog"/>.
    /// </remarks>
    /// <param name="pageId">The catalog page the pet offer sits on.</param>
    /// <param name="offerId">The pet offer.</param>
    /// <param name="name">The pet's name.</param>
    /// <param name="paletteId">The color palette, taken from the offer's available palettes.</param>
    /// <param name="color">The color, as a 24-bit RGB value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageId"/> or <paramref name="offerId"/> is negative, or <paramref name="name"/> is too long for the wire.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyPet(
        int pageId,
        int offerId,
        string name,
        int paletteId,
        int color = 0xFFFFFF)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        BuyFromCatalog(pageId, offerId, PetPurchaseData(name, paletteId, color));
    }

    /// <summary>
    /// Builds the extra-data string a pet purchase carries.
    /// </summary>
    /// <remarks>
    /// Exposed on its own so a script can send the purchase through another path and still get the
    /// format right. Mirrors the client's own construction.
    /// </remarks>
    /// <param name="name">The pet's name.</param>
    /// <param name="paletteId">The color palette.</param>
    /// <param name="color">The color, as a 24-bit RGB value; higher bits are ignored.</param>
    /// <returns>
    /// The name, the palette id and the color as six upper-case hexadecimal digits, separated by
    /// newlines.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or white space.</exception>
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
    /// <param name="badgeCode">The badge to show, as its code appears in the badge inventory.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="badgeCode"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageId"/> or <paramref name="offerId"/> is negative, or <paramref name="badgeCode"/> is too long for the wire.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyBadgeItem(
        int pageId,
        int offerId,
        string badgeCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(badgeCode);
        BuyFromCatalog(pageId, offerId, badgeCode);
    }

    /// <summary>
    /// Buys an offer tied to one of the local user's groups, such as group furni.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="groupId">The group the item belongs to, sent as the offer's extra data.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageId"/> or <paramref name="offerId"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyGroupItem(
        int pageId,
        int offerId,
        Id groupId) =>
        BuyFromCatalog(pageId, offerId, groupId.ToString());

    /// <summary>
    /// Buys an engraved offer such as a trophy, where the buyer supplies the inscription.
    /// </summary>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="inscription">The text to engrave; <see langword="null"/> is sent as an empty string.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageId"/> or <paramref name="offerId"/> is negative, or <paramref name="inscription"/> is too long for the wire.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyEngraved(
        int pageId,
        int offerId,
        string inscription) =>
        BuyFromCatalog(pageId, offerId, inscription ?? "");

    /// <summary>
    /// Requests which rooms may be advertised, and whether the account's membership extends an event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Worth reading before <see cref="BuyRoomEvent"/>: only the rooms listed here are eligible, and
    /// the extended form needs the membership this reports. The client drops the extended flag
    /// when the membership has run out, so a script that assumes it buys the short form instead.
    /// </para>
    /// <para>
    /// The request is sent once without a retry, and the reply is not blocked from the game client.
    /// </para>
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The membership flag and the rooms that may be advertised.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the request was not sent exactly once.</exception>
    public async Task<RoomAdPurchaseInfo> GetRoomEventInfo(int timeoutMs = 10000)
    {
        RoomAdInfoReadResult result = await _application
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
    /// <remarks>
    /// It returns once the request is sent and does not wait for the server's answer.
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="roomId">The room to advertise.</param>
    /// <param name="name">The event's title as shown in the navigator.</param>
    /// <param name="description">The event's description.</param>
    /// <param name="categoryId">The navigator category the event is listed under.</param>
    /// <param name="extended"><see langword="true"/> to run the longer form, which needs the membership; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void BuyRoomEvent(
        int pageId,
        int offerId,
        Id roomId,
        string name,
        string description = "",
        int categoryId = 0,
        bool extended = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Game.Catalog.Purchase(
            Msg.Out.PurchaseRoomAd,
            new PurchaseRoomAd(pageId, offerId, roomId, name, extended, description, categoryId),
            Ct);
    }

    /// <summary>
    /// Places a Builders Club floor offer directly into a spot in the current room.
    /// </summary>
    /// <remarks>
    /// Builders Club does not stock the inventory: the item is placed as it is bought, so the tile
    /// and rotation are needed up front. Nothing confirms the placement: it arrives as an ordinary
    /// floor item add, and a rejected spot produces nothing.
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to place.</param>
    /// <param name="x">The target tile x coordinate.</param>
    /// <param name="y">The target tile y coordinate.</param>
    /// <param name="direction">The rotation to place the item at.</param>
    /// <param name="extraData">The offer's selection data, or empty when it takes none.</param>
    /// <param name="isRetry"><see langword="true"/> when the placement is a retry; otherwise, <see langword="false"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or no ready room.</exception>
    public void PlaceBuildersClubFurni(
        int pageId,
        int offerId,
        int x,
        int y,
        int direction = 0,
        string extraData = "",
        bool isRetry = false) =>
        _ = _application.Invoke<
            SubscriptionBuildersClubFloorPlaceRequest,
            SubscriptionBuildersClubPlacementDispatchReceipt>(
                ApplicationMemberIds.SubscriptionsBuildersClubFloorOfferPlace,
                new SubscriptionBuildersClubFloorPlaceRequest(
                    pageId,
                    offerId,
                    x,
                    y,
                    direction,
                    extraData,
                    isRetry),
                Ct);

    /// <summary>
    /// Places a Builders Club wall offer directly onto a wall in the current room.
    /// </summary>
    /// <remarks>
    /// Nothing confirms the placement: it arrives as an ordinary wall item add.
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to place.</param>
    /// <param name="wallLocation">The position on the wall, in the <c>:w=x,y l=x,y r</c> form.</param>
    /// <param name="extraData">The offer's selection data, or empty when it takes none.</param>
    /// <param name="isRetry"><see langword="true"/> when the placement is a retry; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="wallLocation"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or no ready room.</exception>
    public void PlaceBuildersClubWallItem(
        int pageId,
        int offerId,
        string wallLocation,
        string extraData = "",
        bool isRetry = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wallLocation);
        _ = _application.Invoke<
            SubscriptionBuildersClubWallPlaceRequest,
            SubscriptionBuildersClubPlacementDispatchReceipt>(
                ApplicationMemberIds.SubscriptionsBuildersClubWallOfferPlace,
                new SubscriptionBuildersClubWallPlaceRequest(
                    pageId,
                    offerId,
                    wallLocation,
                    extraData,
                    isRetry),
                Ct);
    }
}
