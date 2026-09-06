using Qx.Game;

namespace Qx.Scripting;

/// <content>
/// The phase of the trading window, on top of the main trade API. Available on both clients.
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// How far the open trade has got: <c>Idle</c> when no trade is open, <c>Trading</c> while
    /// offers may still change, and <c>AwaitingConfirmation</c> once both sides accepted and the
    /// offers are locked. Reverts to <c>Trading</c> if either side withdraws their acceptance.
    /// </summary>
    public TradePhase TradePhase => Trade.Active?.Phase ?? Qx.Game.TradePhase.Idle;

    /// <summary>
    /// Whether the trade has reached the final confirmation step, where the offers are locked and
    /// both sides still have to confirm.
    /// </summary>
    public bool IsTradeWaitingConfirmation =>
        Trade.Active?.Phase is Qx.Game.TradePhase.AwaitingConfirmation;

    /// <summary>
    /// Subscribes to the trade entering the final confirmation phase. Same subscription as
    /// <see cref="OnTradeConfirmed"/>, under the name that matches the phase it reports.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnTradeWaitingConfirm(Action handler) => OnTradeConfirmed(handler);
}
