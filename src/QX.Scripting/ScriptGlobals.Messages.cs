using Qx.Interception;
using Qx.Messages;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    private readonly HashSet<Identifier> _unknown_names = [];
    private MessageNames? _names;

    /// <summary>
    /// Blocks the calling thread until a packet with one of the given message names is seen, or
    /// 10 seconds pass.
    /// </summary>
    /// <remarks>
    /// The packet is not blocked and still reaches its destination.
    /// </remarks>
    /// <param name="names">
    /// The message names to watch, in either direction. An unknown name never matches and writes a
    /// warning line to the script output.
    /// </param>
    /// <returns>A copy of the first matching packet; the caller should dispose it.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no name was given, or a name is empty or carries an <c>in:</c> or <c>out:</c> prefix.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the timeout elapsed or the script was stopped.</exception>
    public IPacket Receive(params string[] names) => Receive(10000, false, names);

    /// <summary>
    /// Blocks the calling thread until a packet with one of the given message names is seen.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds; -1 waits without a limit.</param>
    /// <param name="block">
    /// <see langword="true"/> to block the matching packet, so the game client or server never
    /// receives it; otherwise, <see langword="false"/>. Only the one packet that satisfies the
    /// call is blocked.
    /// </param>
    /// <param name="names">
    /// The message names to watch, in either direction. An unknown name never matches and writes a
    /// warning line to the script output.
    /// </param>
    /// <returns>A copy of the matching packet; the caller should dispose it.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no name was given, or a name is empty or carries an <c>in:</c> or <c>out:</c> prefix.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the timeout elapsed or the script was stopped.</exception>
    /// <remarks>
    /// This blocks the calling thread while it waits; prefer
    /// <see cref="ReceiveAnyAsync"/> inside async code.
    /// </remarks>
    public IPacket Receive(int timeoutMs, bool block, params string[] names) =>
        CaptureAny(names, timeoutMs, block).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously waits for a packet with one of the given message names.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds; -1 waits without a limit.</param>
    /// <param name="block">
    /// <see langword="true"/> to block the matching packet from reaching its destination;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="names">
    /// The message names to watch, in either direction. An unknown name never matches and writes a
    /// warning line to the script output.
    /// </param>
    /// <returns>A copy of the matching packet; the caller should dispose it.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no name was given, or a name is empty or carries an <c>in:</c> or <c>out:</c> prefix.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the timeout elapsed or the script was stopped.</exception>
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
    /// <param name="names">
    /// The message names to watch, in either direction. An unknown name never matches and writes a
    /// warning line to the script output.
    /// </param>
    /// <returns><see langword="true"/> when a packet was captured, <see langword="false"/> on timeout.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no name was given, or a name is empty or carries an <c>in:</c> or <c>out:</c> prefix.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    public bool TryReceive(out IPacket? packet, params string[] names) =>
        TryReceive(10000, false, out packet, names);

    /// <summary>
    /// Waits for one of the given messages and reports whether it arrived, instead of throwing
    /// on timeout.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds; -1 waits without a limit.</param>
    /// <param name="block">
    /// <see langword="true"/> to block the matching packet from reaching its destination;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="packet">
    /// Receives a copy of the matching packet, or <see langword="null"/> on timeout. The caller
    /// should dispose it when non-null.
    /// </param>
    /// <param name="names">
    /// The message names to watch, in either direction. An unknown name never matches and writes a
    /// warning line to the script output.
    /// </param>
    /// <returns><see langword="true"/> when a packet was captured, <see langword="false"/> on timeout.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no name was given, or a name is empty or carries an <c>in:</c> or <c>out:</c> prefix.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the script was stopped while waiting; only the timeout is swallowed.
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

    private async Task<IPacket> CaptureAny(string[] names, int timeout_ms, bool block)
    {
        if (names is null || names.Length == 0)
            throw new ArgumentException("At least one message name is required.", nameof(names));
        return await Capture([.. names.Select(ReceiveIdentifier)], timeout_ms, block);
    }

    private async Task<IPacket> Capture(Identifier[] identifiers, int timeout_ms, bool block)
    {
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

        var subscriptions = new List<IDisposable>(identifiers.Length);
        try
        {
            foreach (Identifier identifier in identifiers)
            {
                subscriptions.Add(_interceptor.Intercept(identifier, Handler));
                WarnIfUnknown(identifier);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(timeout_ms);
            await using CancellationTokenRegistration registration = timeout.Token.Register(() => completion.TrySetCanceled());
            return await completion.Task;
        }
        finally
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
        }
    }

    private static Identifier ReceiveIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Message names cannot be empty.", nameof(name));
        if (name.Contains(':'))
        {
            throw new ArgumentException(
                $"'{name}' carries a prefix; pass the message name only, for example '{name[(name.LastIndexOf(':') + 1)..]}'.",
                nameof(name));
        }
        return new Identifier(MessageDirection.Both, name);
    }

    private MessageNames Names => _names ??= new MessageNames(_interceptor.Messages);

    private IDisposable InterceptMessage(Identifier identifier, Action<Intercept> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        IDisposable registration = Track(_interceptor.Intercept(identifier, Guarded(handler)));
        WarnIfUnknown(identifier);
        return registration;
    }

    private void WarnIfUnknown(Identifier identifier)
    {
        if (Names.IsKnown(identifier.Direction, identifier.Name))
            return;
        lock (_unknown_names)
        {
            if (!_unknown_names.Add(identifier))
                return;
        }
        _log($"Warning QX1001: {Names.Unknown(identifier.Direction, identifier.Name)}");
    }

    private MessageDirection ModelDirections<T>(MessageDirection direction, string name)
    {
        if (!MessageNames.IsModelAssembly(typeof(T).Assembly.GetName().Name))
            return direction;
        MessageDirection directions = Names.ModelDirections(direction, name, IsModel<T>);
        if (directions == MessageDirection.None)
            throw new ArgumentException(Names.Mismatch(direction, name, typeof(T).Name, IsModel<T>), nameof(name));
        return directions;
    }

    private static bool IsModel<T>(Type model) => model == typeof(T);
}
