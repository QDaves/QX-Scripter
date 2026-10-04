using Qx.Game;
using Qx.Game.Application;
using Qx.Model;

namespace Qx.Scripting;

/// <content>
/// The phase of the trading window and where each side stands in it, on top of the main trade API.
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the phase of the open trade.
    /// </summary>
    /// <remarks>
    /// The phase is <see cref="Qx.Game.TradePhase.Idle"/> when no trade is open,
    /// <see cref="Qx.Game.TradePhase.Trading"/> while offers may still change, and
    /// <see cref="Qx.Game.TradePhase.AwaitingConfirmation"/> once both sides accepted and the
    /// offers are locked. It reverts to <see cref="Qx.Game.TradePhase.Trading"/> if either side
    /// withdraws their acceptance.
    /// </remarks>
    public TradePhase TradePhase => Trade.Active?.Phase ?? Qx.Game.TradePhase.Idle;

    /// <summary>
    /// Gets whether the trade has reached the final confirmation step, where the offers are
    /// locked and both sides still have to confirm.
    /// </summary>
    /// <remarks>
    /// <see cref="OnTradeAwaitingConfirmation"/> runs when it becomes <see langword="true"/>.
    /// </remarks>
    public bool IsTradeAwaitingConfirmation =>
        Trade.Active?.Phase is Qx.Game.TradePhase.AwaitingConfirmation;

    /// <summary>
    /// Gets whether the local user is the side that opened the trade rather than the side that
    /// was invited.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> when no trade is open.
    /// </remarks>
    public bool IsTrader =>
        Trade.Active?.FirstParticipant.UserId == UserId;

    /// <summary>
    /// Gets whether the local user has accepted the current offer.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> when no trade is open. Accepting is reset whenever either side
    /// changes their offer.
    /// </remarks>
    public bool HasAcceptedTrade
    {
        get
        {
            TradeEpochView? trade = Trade.Active;
            return trade is not null &&
                TradeParticipantOf(trade, UserId)?.Accepted == true;
        }
    }

    /// <summary>
    /// Gets whether the trading partner has accepted the current offer.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> when no trade is open.
    /// </remarks>
    public bool HasPartnerAcceptedTrade
    {
        get
        {
            TradeEpochView? trade = Trade.Active;
            return trade is not null && TradePartnerOf(trade)?.Accepted == true;
        }
    }

    /// <summary>
    /// Gets the user on the other side of the trade, looked up in the room.
    /// </summary>
    /// <value>
    /// The partner, or <see langword="null"/> when no trade is open or that user is no longer in
    /// the room.
    /// </value>
    public User? TradePartner
    {
        get
        {
            TradeEpochView? trade = Trade.Active;
            return trade is not null && TradePartnerOf(trade)?.UserId is Id partner_id
                ? GetUser(partner_id)
                : null;
        }
    }

    /// <summary>
    /// Gets the local user's side of the trade: the items offered and the credit amount.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> when no trade is open or until the server has sent the first item
    /// list.
    /// </remarks>
    public TradeOfferView? OwnTradeOffer
    {
        get
        {
            TradeEpochView? trade = Trade.Active;
            return trade is null ? null : TradeOfferOf(trade, UserId);
        }
    }

    /// <summary>
    /// Gets the trading partner's side of the trade.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> when no trade is open or until the server has sent the first item
    /// list.
    /// </remarks>
    public TradeOfferView? PartnerTradeOffer
    {
        get
        {
            TradeEpochView? trade = Trade.Active;
            TradeParticipantView? partner = trade is null ? null : TradePartnerOf(trade);
            return trade is null || partner is null
                ? null
                : TradeOfferOf(trade, partner.UserId);
        }
    }

    private static TradeParticipantView? TradeParticipantOf(
        TradeEpochView trade,
        Id user_id) =>
        trade.FirstParticipant.UserId == user_id
            ? trade.FirstParticipant
            : trade.SecondParticipant.UserId == user_id
                ? trade.SecondParticipant
                : null;

    private TradeParticipantView? TradePartnerOf(TradeEpochView trade) =>
        trade.FirstParticipant.UserId == UserId
            ? trade.SecondParticipant
            : trade.SecondParticipant.UserId == UserId
                ? trade.FirstParticipant
                : null;

    private static TradeOfferView? TradeOfferOf(TradeEpochView trade, Id user_id) =>
        trade.FirstOffer?.UserId == user_id
            ? trade.FirstOffer
            : trade.SecondOffer?.UserId == user_id
                ? trade.SecondOffer
                : null;
}
