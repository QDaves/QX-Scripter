using Qx.Game.Protocol;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class WiredApplication
{
    private ValueTask<WiredAccountPreferencesView> read_preferences(
        WiredCommandRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new WiredAccountPreferencesView(wired.AccountPreferences));
    }

    private async ValueTask<WiredWebApiKeyResult> generate_web_api_key(
        WiredWebApiKeyRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.WiredId, nameof(request.WiredId));
        ValidateTimeout(request.TimeoutMilliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        long started = time_provider.GetTimestamp();
        await EnterExclusive(web_api_lock, started, request.TimeoutMilliseconds,
            MessageKeys.Wired.WebApi.KeyGenerate.Value, "Wired Web API key", operation.Token).ConfigureAwait(false);
        try
        {
            return await Request(
                MessageContracts.Wired.WebApi.KeyGenerate,
                new WiredGenerateWebApiKey(request.WiredId, request.ReadKey),
                MessageContracts.Wired.WebApi.KeyResult,
                value => value.WiredId == request.WiredId && value.ReadKey == request.ReadKey,
                static value => value,
                RemainingMilliseconds(started, request.TimeoutMilliseconds),
                scope,
                operation.Token).ConfigureAwait(false);
        }
        finally
        {
            web_api_lock.Release();
        }
    }
}
