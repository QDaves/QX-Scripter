using Qx.Game.Protocol;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class WiredApplication
{
    private ValueTask<WiredDispatchResult> send_trade_stage(
        WiredTradeStageRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(MessageContracts.Wired.Trade.Confirm, new WiredTradeConfirm(request.Stage), cancellation_token);
    }

    private async ValueTask<WiredTradeCompleteResult> complete_trade(
        WiredTradeCompleteRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedGeneration);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedRevision);
        ValidateTimeout(request.TimeoutMilliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        CancellationToken operation_token = operation.Token;
        long started = time_provider.GetTimestamp();
        await EnterExclusive(deposit_lock, started, request.TimeoutMilliseconds,
            MessageKeys.Wired.Trade.Confirm.Value, "wired trade completion", operation_token).ConfigureAwait(false);
        WiredTradeSnapshot? reviewed = null;
        bool accepted = false;
        bool settled = false;
        try
        {
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            WiredSnapshot state = CaptureCurrentState(scope, operation_token);
            if (state.Generation != request.ExpectedGeneration || state.Revision != request.ExpectedRevision)
                throw new InvalidOperationException("The reviewed Wired state is no longer current.");
            reviewed = state.Trade;
            if (reviewed.Status is not WiredTradeStatus.Active || reviewed.Initiation is null || reviewed.Items?.CanAccept is not true)
                throw new InvalidOperationException("The Wired trade has no acceptable offer.");
            if (RemainingMilliseconds(started, request.TimeoutMilliseconds) <= trade_confirmation_delay.TotalMilliseconds)
                throw new RequestTimeoutException(MessageKeys.Wired.Trade.Confirm.Value,
                    MessageKeys.Wired.Trade.Completed.Value, request.TimeoutMilliseconds);

            void require_reviewed_offer()
            {
                if (RemainingMilliseconds(started, request.TimeoutMilliseconds) < 1)
                    throw new RequestTimeoutException(MessageKeys.Wired.Trade.Confirm.Value,
                        MessageKeys.Wired.Trade.Completed.Value, request.TimeoutMilliseconds);
                WiredSnapshot current = CaptureCurrentState(scope, operation_token);
                if (current.Trade.Status is not WiredTradeStatus.Active ||
                    !ReferenceEquals(current.Trade.Initiation, reviewed.Initiation) ||
                    !ReferenceEquals(current.Trade.Items, reviewed.Items))
                    throw new InvalidOperationException("The reviewed Wired trade offer changed.");
            }

            messages.Dispatch(MessageContracts.Wired.Trade.Confirm,
                new WiredTradeConfirm(WiredTradeConfirmationStage.Accept), scope.Session,
                operation_token, require_reviewed_offer);
            accepted = true;
            await Task.Delay(trade_confirmation_delay, time_provider, operation_token).ConfigureAwait(false);
            CaptureCurrentState(scope, operation_token);
            if (updates.TryTake(trade_completion_changed, state.Revision, out WiredStateUpdate? early))
                return trade_completion_result(early, false);

            messages.Dispatch(MessageContracts.Wired.Trade.Confirm,
                new WiredTradeConfirm(WiredTradeConfirmationStage.Confirm), scope.Session,
                operation_token, require_reviewed_offer);
            WiredStateUpdate result = await updates.WaitAsync(trade_completion_changed, interceptor,
                scope.Session, time_provider, started, request.TimeoutMilliseconds, state.Revision,
                MessageKeys.Wired.Trade.Confirm.Value, MessageKeys.Wired.Trade.Completed.Value,
                operation_token).ConfigureAwait(false);
            settled = result.Kind is WiredStateChangeKind.TradeCompleted or WiredStateChangeKind.TradeCancelled;
            return trade_completion_result(result, true);
        }
        finally
        {
            if (accepted && !settled && reviewed is not null && IsCurrent(scope))
            {
                WiredTradeSnapshot current = wired.Snapshot.Trade;
                if (current.Status is WiredTradeStatus.Active && ReferenceEquals(current.Initiation, reviewed.Initiation))
                {
                    try
                    {
                        messages.Dispatch(MessageContracts.Wired.Trade.Cancel, new WiredTradeCancel(), scope.Session,
                            CancellationToken.None, () =>
                            {
                                WiredSnapshot latest = CaptureCurrentState(scope, CancellationToken.None);
                                if (latest.Trade.Status is not WiredTradeStatus.Active ||
                                    !ReferenceEquals(latest.Trade.Initiation, reviewed.Initiation))
                                    throw new InvalidOperationException("The original Wired trade is no longer active.");
                            });
                    }
                    catch (Exception) { }
                }
            }
            deposit_lock.Release();
        }
    }

    private static bool trade_completion_changed(WiredStateUpdate update) => IsTradeTerminal(update) ||
        update.Kind is WiredStateChangeKind.TradeInitiated or WiredStateChangeKind.TradeItemsUpdated;

    private static WiredTradeCompleteResult trade_completion_result(WiredStateUpdate update, bool confirmed)
    {
        int? code = update.Value switch
        {
            WiredTradeCancelled cancelled => cancelled.TransactionFailureTypeId,
            WiredTransactionFail failed => failed.TransactionFailureTypeId,
            _ => null
        };
        bool success = confirmed && update.Kind is WiredStateChangeKind.TradeCompleted;
        string failure = success ? "" : code is not null
            ? $"The Wired trade failed with type {code}."
            : update.Kind is WiredStateChangeKind.TradeCompleted
                ? "The Wired trade completed before final confirmation."
                : "The reviewed Wired trade offer changed.";
        return new(success, failure, code, update.State.Generation, update.State.Revision);
    }
}
