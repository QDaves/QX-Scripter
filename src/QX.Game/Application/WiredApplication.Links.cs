using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Wired;

namespace Qx.Game.Application;

/// <summary>Identifies a WIRED Link in the current room.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="HideWindow">Whether the client is kept from opening or updating its Link window.</param>
/// <param name="TimeoutMilliseconds">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
public sealed record WiredLinkRequest(Id LinkId, bool HideWindow = true, int TimeoutMilliseconds = 10000);

/// <summary>Connects or disconnects a WIRED Link and sets own-room receiving.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier, or <see cref="WiredLinkNetworks.Disconnected"/>.</param>
/// <param name="ReceiveLocal">Whether the Link receives messages and dataframes sent from its own room.</param>
/// <param name="HideWindow">Whether the client is kept from updating its Link window.</param>
/// <param name="TimeoutMilliseconds">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
public sealed record WiredLinkSaveRequest(Id LinkId, string NetworkId, bool ReceiveLocal, bool HideWindow = true,
    int TimeoutMilliseconds = 10000);

/// <summary>Creates a network owned by the account through one of its Links.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="Name">The exact network name, 1 to 255 characters with visible text.</param>
/// <param name="HideWindow">Whether the client is kept from updating its Link window.</param>
/// <param name="TimeoutMilliseconds">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
public sealed record WiredLinkNetworkCreateRequest(Id LinkId, string Name, bool HideWindow = true, int TimeoutMilliseconds = 10000);

/// <summary>Renames a network owned by the account.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier.</param>
/// <param name="Name">The new exact network name, 1 to 255 characters with visible text.</param>
/// <param name="HideWindow">Whether the client is kept from updating its Link window.</param>
/// <param name="TimeoutMilliseconds">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
public sealed record WiredLinkNetworkRenameRequest(Id LinkId, string NetworkId, string Name, bool HideWindow = true,
    int TimeoutMilliseconds = 10000);

/// <summary>Identifies a network for deletion or a connection listing.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier.</param>
/// <param name="HideWindow">Whether the client is kept from updating its Link window.</param>
/// <param name="TimeoutMilliseconds">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
public sealed record WiredLinkNetworkRequest(Id LinkId, string NetworkId, bool HideWindow = true, int TimeoutMilliseconds = 10000);

internal sealed partial class WiredApplication
{
    private const string link_class = "wf_link";

    private ValueTask<WiredLinkConfiguration> get_link_configuration(WiredLinkRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return link_call(request.LinkId, request.HideWindow, request.TimeoutMilliseconds,
            MessageContracts.Wired.Link.ConfigurationGet, new WiredLinkGetConfiguration(request.LinkId),
            MessageContracts.Wired.Link.Configuration, value => value.LinkId == request.LinkId, cancellation_token);
    }

    private ValueTask<WiredLinkOperationResult> save_link_configuration(WiredLinkSaveRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return link_operation(request.LinkId, request.HideWindow, request.TimeoutMilliseconds,
            MessageContracts.Wired.Link.ConfigurationSave,
            new WiredLinkSaveConfiguration(request.LinkId, request.NetworkId, request.ReceiveLocal),
            WiredLinkOperation.SaveConfiguration, cancellation_token);
    }

    private ValueTask<WiredLinkOperationResult> create_link_network(WiredLinkNetworkCreateRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return link_operation(request.LinkId, request.HideWindow, request.TimeoutMilliseconds,
            MessageContracts.Wired.Link.NetworkCreate, new WiredLinkCreateNetwork(request.LinkId, request.Name),
            WiredLinkOperation.CreateNetwork, cancellation_token);
    }

    private ValueTask<WiredLinkOperationResult> rename_link_network(WiredLinkNetworkRenameRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return link_operation(request.LinkId, request.HideWindow, request.TimeoutMilliseconds,
            MessageContracts.Wired.Link.NetworkRename, new WiredLinkRenameNetwork(request.LinkId, request.NetworkId, request.Name),
            WiredLinkOperation.RenameNetwork, cancellation_token);
    }

    private ValueTask<WiredLinkOperationResult> delete_link_network(WiredLinkNetworkRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return link_operation(request.LinkId, request.HideWindow, request.TimeoutMilliseconds,
            MessageContracts.Wired.Link.NetworkDelete, new WiredLinkDeleteNetwork(request.LinkId, request.NetworkId),
            WiredLinkOperation.DeleteNetwork, cancellation_token);
    }

    private ValueTask<WiredLinkConnections> get_link_connections(WiredLinkNetworkRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return link_call(request.LinkId, request.HideWindow, request.TimeoutMilliseconds,
            MessageContracts.Wired.Link.ConnectionsGet, new WiredLinkGetConnections(request.LinkId, request.NetworkId),
            MessageContracts.Wired.Link.Connections,
            value => value.LinkId == request.LinkId && string.Equals(value.NetworkId, request.NetworkId, StringComparison.Ordinal),
            cancellation_token);
    }

    private ValueTask<WiredLinkOperationResult> link_operation<TRequest>(Id link_id, bool hide, int timeout_milliseconds,
        MessageContract<TRequest> request_contract, TRequest message, WiredLinkOperation operation,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest> =>
        link_call(link_id, hide, timeout_milliseconds, request_contract, message, MessageContracts.Wired.Link.Result,
            value => value.LinkId == link_id && value.Operation == operation, cancellation_token);

    private async ValueTask<TResponse> link_call<TRequest, TResponse>(Id link_id, bool hide, int timeout_milliseconds,
        MessageContract<TRequest> request_contract, TRequest message, MessageContract<TResponse> response_contract,
        Func<TResponse, bool> match, CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        ThrowIfDisposed();
        ValidateId(link_id, nameof(link_id));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)link_id, int.MaxValue, nameof(link_id));
        ValidateTimeout(timeout_milliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        long started = time_provider.GetTimestamp();
        await EnterExclusive(link_lock, started, timeout_milliseconds, request_contract.Key.Value, response_contract.Key.Value,
            operation.Token).ConfigureAwait(false);
        try
        {
            FloorItem item = game.Room.Capture(room => room.FloorItem(link_id))
                ?? throw new InvalidOperationException("The WIRED Link is not in the current room.");
            if (game.GameData.Furni?.GetInfo(item) is { } info &&
                !string.Equals(info.Identifier, link_class, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Furniture {link_id} is {info.Identifier}, not a WIRED Link ({link_class}).", nameof(link_id));
            return await Request(request_contract, message, response_contract, match, static value => value,
                RemainingMilliseconds(started, timeout_milliseconds), scope, operation.Token, hide).ConfigureAwait(false);
        }
        finally
        {
            link_lock.Release();
        }
    }
}
