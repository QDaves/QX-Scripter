using System.Text;
using Qx.Game.Protocol;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class GiftApplication
{
    public ValueTask<GiftPresentOpenDispatchReceipt> OpenPresent(
        GiftPresentOpenRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(OpenPresentCore(request, token)));

    private GiftPresentOpenDispatchReceipt OpenPresentCore(
        GiftPresentOpenRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((long)request.FurniId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.FurniId));
        GiftRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Gifts.PresentOpen,
            new PresentOpen(request.FurniId),
            scope.Session,
            cancellation_token,
            () => RequireRoomScope(scope));
        return new GiftPresentOpenDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.RoomId,
            scope.RoomGeneration,
            scope.RoomRevision,
            request.FurniId,
            1);
    }

    public ValueTask<GiftPurchaseDispatchReceipt> PurchaseGift(
        GiftPurchaseRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(PurchaseGiftCore(request, token)));

    private GiftPurchaseDispatchReceipt PurchaseGiftCore(
        GiftPurchaseRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateActionWireString(request.ExtraData, nameof(request.ExtraData));
        ValidateActionWireString(request.GiftMessage, nameof(request.GiftMessage));
        ValidateActionWireString(request.ReceiverName, nameof(request.ReceiverName));
        if (request.Quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity));
        GiftPurchaseScope scope = CapturePurchaseScope(
            request.ExpectedSessionGeneration,
            request.ExpectedCatalogGeneration,
            cancellation_token);
        bool unity_client = UsesUnityGiftWire(scope.Session.Client);
        int effective_quantity = unity_client
            ? request.Quantity
            : 1;
        int? wire_quantity = unity_client
            ? effective_quantity
            : null;
        var wire_request = new PurchaseFromCatalogAsGift(
            request.PageId,
            request.OfferId,
            request.ExtraData,
            request.ReceiverName,
            request.GiftMessage,
            request.SpriteId,
            request.BoxType,
            request.RibbonType,
            request.ShowPurchaserName,
            wire_quantity);
        message_dispatcher.Dispatch(
            MessageContracts.Gifts.Purchase,
            wire_request,
            scope.Session,
            cancellation_token,
            () => RequirePurchaseScope(scope));
        return new GiftPurchaseDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.CatalogGeneration,
            request.PageId,
            request.OfferId,
            effective_quantity,
            request.ShowPurchaserName,
            1);
    }

    public ValueTask<GiftClubSelectDispatchReceipt> SelectClubGift(
        GiftClubSelectRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(SelectClubGiftCore(request, token, true)));

    private GiftClubSelectDispatchReceipt SelectClubGiftCore(
        GiftClubSelectRequest request,
        CancellationToken cancellation_token,
        bool require_product_code)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (require_product_code)
        {
            ValidateActionRequiredWireString(
                request.ProductCode,
                nameof(request.ProductCode));
        }
        else
        {
            ValidateActionWireString(request.ProductCode, nameof(request.ProductCode));
        }
        GiftRevisionScope scope = CaptureRevisionScope(
            request.ExpectedSessionGeneration,
            request.ExpectedClubInfoRevision,
            static state => state.ClubInfoRevision,
            static state => state.ClubInfo is not null,
            "club-gift info",
            cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Gifts.ClubSelect,
            new SelectClubGift(request.ProductCode),
            scope.Session,
            cancellation_token,
            () => RequireRevisionScope(
                scope,
                static state => state.ClubInfoRevision,
                "club-gift info"));
        return new GiftClubSelectDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.SourceRevision,
            request.ProductCode,
            1);
    }

    public ValueTask<GiftNewUserSelectDispatchReceipt> SelectNewUserGifts(
        GiftNewUserSelectRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(SelectNewUserGiftsCore(request, token)));

    private GiftNewUserSelectDispatchReceipt SelectNewUserGiftsCore(
        GiftNewUserSelectRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Selections);
        if (request.Selections.Count > 21845)
            throw new ArgumentOutOfRangeException(nameof(request.Selections));
        var wire_request = new NuxGetGifts(request.Selections);
        GiftRevisionScope scope = CaptureRevisionScope(
            request.ExpectedSessionGeneration,
            request.ExpectedNewUserOfferRevision,
            static state => state.NewUserOfferRevision,
            static state => state.NewUserOffer is not null,
            "new-user gift offer",
            cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Gifts.NewUserSelect,
            wire_request,
            scope.Session,
            cancellation_token,
            () => RequireRevisionScope(
                scope,
                static state => state.NewUserOfferRevision,
                "new-user gift offer"));
        return new GiftNewUserSelectDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.SourceRevision,
            wire_request.Selections.Count,
            1);
    }

    public ValueTask<GiftNewUserAdvanceDispatchReceipt> AdvanceNewUserFlow(
        GiftNewUserAdvanceRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(AdvanceNewUserFlowCore(request, token)));

    private GiftNewUserAdvanceDispatchReceipt AdvanceNewUserFlowCore(
        GiftNewUserAdvanceRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        GiftRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Gifts.NewUserAdvance,
            new AdvanceNewUserFlowRequest(),
            scope.Session,
            cancellation_token,
            () => RequireRoomScope(scope));
        return new GiftNewUserAdvanceDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.RoomId,
            scope.RoomGeneration,
            scope.RoomRevision,
            1);
    }

    void IGiftOperations.RequestWrappingConfiguration()
    {
        InvokeLegacy(cancellation_token =>
        {
            GiftOperationScope scope = CaptureScope(null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Gifts.WrappingConfigurationRequest,
                new GetGiftWrappingConfiguration(),
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        });
    }

    void IGiftOperations.OpenPresent(Id furni_id)
    {
        InvokeLegacy(cancellation_token =>
        {
            GiftRoomScope scope = CaptureRoomScope(null, null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Gifts.PresentOpen,
                new PresentOpen(furni_id),
                scope.Session,
                cancellation_token,
                () => RequireRoomScope(scope));
        });
    }

    void IGiftOperations.Purchase(PurchaseFromCatalogAsGift request)
    {
        InvokeLegacy(cancellation_token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateActionWireString(request.ExtraData, nameof(request.ExtraData));
            ValidateActionWireString(request.ReceiverName, nameof(request.ReceiverName));
            ValidateActionWireString(request.GiftMessage, nameof(request.GiftMessage));
            GiftPurchaseScope scope = CapturePurchaseScope(
                null,
                null,
                cancellation_token);
            bool unity_client = UsesUnityGiftWire(scope.Session.Client);
            if (!unity_client && request.Quantity is not null)
                throw new InvalidDataException("Flash gift purchases do not include a quantity.");
            int? wire_quantity = unity_client
                ? request.Quantity ?? 1
                : null;
            var wire_request = new PurchaseFromCatalogAsGift(
                request.PageId,
                request.OfferId,
                request.ExtraData,
                request.ReceiverName,
                request.GiftMessage,
                request.BoxType,
                request.RibbonType,
                request.Color,
                !request.IsIncognito,
                wire_quantity);
            message_dispatcher.Dispatch(
                MessageContracts.Gifts.Purchase,
                wire_request,
                scope.Session,
                cancellation_token,
                () => RequirePurchaseScope(scope));
        });
    }

    void IGiftOperations.RequestClubGifts()
    {
        InvokeLegacy(cancellation_token =>
        {
            GiftOperationScope scope = CaptureScope(null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Gifts.ClubInfoRequest,
                new GetClubGift(),
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        });
    }

    void IGiftOperations.SelectClubGift(string product_code)
    {
        InvokeLegacy(cancellation_token =>
        {
            ArgumentNullException.ThrowIfNull(product_code);
            SelectClubGiftCore(
                new GiftClubSelectRequest(product_code),
                cancellation_token,
                false);
        });
    }

    void IGiftOperations.RequestOfferGiftability(int offer_id)
    {
        InvokeLegacy(cancellation_token =>
        {
            GiftOperationScope scope = CaptureScope(null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Gifts.OfferGiftabilityRequest,
                new GetIsOfferGiftable(offer_id),
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        });
    }

    void IGiftOperations.SelectNewUserGifts(
        IReadOnlyList<NuxGiftSelection> selections)
    {
        InvokeLegacy(cancellation_token =>
        {
            ArgumentNullException.ThrowIfNull(selections);
            SelectNewUserGiftsCore(
                new GiftNewUserSelectRequest(selections),
                cancellation_token);
        });
    }

    void IGiftOperations.AdvanceNewUserFlow()
    {
        InvokeLegacy(cancellation_token =>
        {
            AdvanceNewUserFlowCore(
                new GiftNewUserAdvanceRequest(),
                cancellation_token);
        });
    }

    private static void ValidateActionWireString(string value, string argument_name)
    {
        ArgumentNullException.ThrowIfNull(value, argument_name);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidateActionRequiredWireString(
        string value,
        string argument_name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, argument_name);
        ValidateActionWireString(value, argument_name);
    }
}
