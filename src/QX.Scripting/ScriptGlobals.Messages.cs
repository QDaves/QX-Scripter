using Qx.Interception;
using Qx.Messages;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Blocks the calling thread until a packet with one of the given message names is seen, or
    /// 10 seconds pass. The packet is not blocked and still reaches its destination.
    /// </summary>
    /// <param name="names">
    /// The message names to watch, in either direction. Unknown names simply never match.
    /// </param>
    /// <returns>A copy of the first matching packet; the caller should dispose it.</returns>
    /// <exception cref="ArgumentException">No name was given.</exception>
    /// <exception cref="OperationCanceledException">The timeout elapsed or the script was stopped.</exception>
    public IPacket Receive(params string[] names) => Receive(10000, false, names);

    /// <summary>
    /// Blocks the calling thread until a packet with one of the given message names is seen.
    /// </summary>
    /// <param name="timeoutMs">How long to wait, in milliseconds.</param>
    /// <param name="block">
    /// Whether the matching packet is blocked, so the game client or server never receives it.
    /// Only the one packet that satisfies the call is blocked.
    /// </param>
    /// <param name="names">The message names to watch, in either direction.</param>
    /// <returns>A copy of the matching packet; the caller should dispose it.</returns>
    /// <exception cref="ArgumentException">No name was given.</exception>
    /// <exception cref="OperationCanceledException">The timeout elapsed or the script was stopped.</exception>
    /// <remarks>
    /// This blocks the calling thread while it waits; prefer
    /// <see cref="ReceiveAnyAsync"/> inside async code.
    /// </remarks>
    public IPacket Receive(int timeoutMs, bool block, params string[] names) =>
        CaptureAny(names, timeoutMs, block).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously waits for a packet with one of the given message names.
    /// </summary>
    /// <param name="timeoutMs">How long to wait, in milliseconds.</param>
    /// <param name="block">Whether the matching packet is blocked from reaching its destination.</param>
    /// <param name="names">The message names to watch, in either direction.</param>
    /// <returns>A copy of the matching packet; the caller should dispose it.</returns>
    /// <exception cref="ArgumentException">No name was given.</exception>
    /// <exception cref="OperationCanceledException">The timeout elapsed or the script was stopped.</exception>
    public Task<IPacket> ReceiveAnyAsync(int timeoutMs, bool block, params string[] names) =>
        CaptureAny(names, timeoutMs, block);

    /// <summary>
    /// Waits up to 10 seconds for one of the given messages and reports whether it arrived,
    /// instead of throwing on timeout.
    /// </summary>
    /// <param name="packet">
    /// Receives a copy of the matching packet, or <see langword="null"/> on timeout. The caller
    /// should dispose it when non-null.
    /// </param>
    /// <param name="names">The message names to watch, in either direction.</param>
    /// <returns><see langword="true"/> when a packet was captured, <see langword="false"/> on timeout.</returns>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    public bool TryReceive(out IPacket? packet, params string[] names) =>
        TryReceive(10000, false, out packet, names);

    /// <summary>
    /// Waits for one of the given messages and reports whether it arrived, instead of throwing
    /// on timeout.
    /// </summary>
    /// <param name="timeoutMs">How long to wait, in milliseconds.</param>
    /// <param name="block">Whether the matching packet is blocked from reaching its destination.</param>
    /// <param name="packet">
    /// Receives a copy of the matching packet, or <see langword="null"/> on timeout. The caller
    /// should dispose it when non-null.
    /// </param>
    /// <param name="names">The message names to watch, in either direction.</param>
    /// <returns><see langword="true"/> when a packet was captured, <see langword="false"/> on timeout.</returns>
    /// <exception cref="OperationCanceledException">
    /// The script was stopped while waiting; only the timeout is swallowed.
    /// </exception>
    public bool TryReceive(int timeoutMs, bool block, out IPacket? packet, params string[] names)
    {
        packet = null;
        try
        {
            packet = Receive(timeoutMs, block, names);
            return true;
        }
        catch (OperationCanceledException) when (!Ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<IPacket> CaptureAny(string[] names, int timeoutMs, bool block)
    {
        if (names is null || names.Length == 0)
            throw new ArgumentException("At least one message name is required.", nameof(names));

        var completion = new TaskCompletionSource<IPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(Intercept intercept)
        {
            IPacket copy = intercept.Packet.Copy();
            if (completion.TrySetResult(copy))
            {
                if (block)
                    intercept.Block();
            }
            else
            {
                copy.Dispose();
            }
        }

        var subscriptions = new List<IDisposable>(names.Length * 2);
        foreach (string name in names)
        {
            subscriptions.Add(InterceptIncoming(name, ClientType.None, Handler));
            subscriptions.Add(InterceptOutgoing(name, ClientType.None, Handler));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(timeoutMs);
        await using CancellationTokenRegistration registration = timeout.Token.Register(() => completion.TrySetCanceled());

        try
        {
            return await completion.Task;
        }
        finally
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
        }
    }
}
