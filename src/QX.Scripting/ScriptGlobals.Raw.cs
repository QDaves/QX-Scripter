using Qx;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

/// <summary>
/// Resolves message names to wire headers for one direction. Indexing it is the shorthand
/// behind <c>Out["Move"]</c> and <c>In["Chat"]</c>.
/// </summary>
/// <remarks>
/// Resolution goes through the catalog loaded for the active session, so the same name can map
/// to different header values on different hotels or client builds. Never hard-code a header
/// number; look it up here, or use the constants on <see cref="Msg"/>.
/// </remarks>
public sealed class HeaderIndex(MessageManager messages, Direction direction)
{
    /// <summary>
    /// The header the given message name resolves to on the active client.
    /// </summary>
    /// <param name="name">The message name as spelled in the catalog.</param>
    /// <exception cref="InvalidOperationException">
    /// The name is not in the catalog for this direction and client. Unlike an intercept
    /// registration, which binds nothing and stays silent, a lookup failure is always thrown.
    /// </exception>
    public Header this[string name] =>
        messages.TryGetHeader(new Identifier(ClientType.None, direction, name), out Header header)
            ? header
            : throw new InvalidOperationException($"Unknown {(direction == Direction.Out ? "outgoing" : "incoming")} message '{name}'.");
}

public partial class ScriptGlobals
{
    private HeaderIndex? _out;
    private HeaderIndex? _in;

    private WalletStateView ReadWalletState(int? point_type = null, int point_limit = 1) =>
        Application.Invoke<WalletStateRequest, WalletStateView>(
            ApplicationMemberIds.WalletState,
            new WalletStateRequest(PointLimit: point_limit, PointType: point_type),
            Ct);

    private int ReadWalletPoint(int type) => WalletPoint(ReadWalletState(type), type);

    private static int WalletPoint(WalletStateView state, int type)
    {
        WalletPointBalance? point = state.ActivityPoints.Points.FirstOrDefault(
            candidate => candidate.Type == type);
        if (point is not null)
            return point.Amount;
        return state.PointsLoaded
            ? 0
            : throw new InvalidOperationException($"Activity point type {type} has not been loaded.");
    }

    /// <summary>
    /// Header lookup for outgoing (client to server) messages, for example <c>Out["Move"]</c>.
    /// </summary>
    public HeaderIndex Out => _out ??= new HeaderIndex(Ext.Messages, Direction.Out);

    /// <summary>
    /// Header lookup for incoming (server to client) messages, for example <c>In["Chat"]</c>.
    /// </summary>
    public HeaderIndex In => _in ??= new HeaderIndex(Ext.Messages, Direction.In);

    /// <summary>
    /// Whether the local user's own account data has been received. Until it is,
    /// <see cref="UserId"/> is -1 and the other <c>User...</c> properties are empty.
    /// </summary>
    public bool IsIdentityLoaded => Profile.Identity is not null;

    /// <summary>
    /// Whether a wallet balance has been observed. <see cref="Credits"/> reads 0 both for a
    /// genuinely empty wallet and for one that has not been reported yet; this tells the two
    /// apart.
    /// </summary>
    public bool IsCreditsLoaded => ReadWalletState().CreditsLoaded;

    /// <summary>
    /// Whether the complete activity-point balance snapshot has been observed.
    /// </summary>
    public bool IsPointsLoaded => ReadWalletState().PointsLoaded;

    /// <summary>The local user's account id, or -1 before the identity has been received.</summary>
    public Id UserId => Profile.Identity?.Id ?? -1;

    /// <summary>The local user's name, or an empty string before the identity has been received.</summary>
    public string UserName => Self?.Name ?? "";

    /// <summary>The local user's figure string, or an empty string when not yet known.</summary>
    public string UserFigure => Self?.Figure ?? "";

    /// <summary>The local user's motto, or an empty string when not yet known.</summary>
    public string UserMotto => Self?.Motto ?? "";

    /// <summary>
    /// The local user's gender, or <see cref="Gender.Unisex"/> when the identity has not been
    /// received.
    /// </summary>
    public Gender UserGender => Self?.Gender ?? Gender.Unisex;

    /// <summary>
    /// The game server host the session is connected to, for example
    /// <c>"game-de.habbo.com"</c>. Empty before a connection has been observed.
    /// </summary>
    public string Host => Session?.Host ?? "";

    /// <summary>
    /// The website host matching <see cref="Host"/>, for example <c>"www.habbo.de"</c>. Falls
    /// back to <c>"www.habbo.com"</c> for hosts that are not in the mapping table.
    /// </summary>
    public string WebHost => Qx.Game.GameData.WebHostFor(Host);

    /// <summary>The current room's id, or 0 when the user is not in a room.</summary>
    public long RoomId => Room.RoomId;

    /// <summary>
    /// The credit balance last reported by the server. Reads 0 until a wallet update has been
    /// seen - check <see cref="IsCreditsLoaded"/> before trusting a zero.
    /// </summary>
    public int Credits => ReadWalletState().Credits ?? 0;

    /// <summary>
    /// The diamond balance (activity point type 5).
    /// </summary>
    public int Diamonds => ReadWalletPoint(WalletPointTypes.Diamonds);

    /// <summary>
    /// The ducket balance (activity point type 0).
    /// </summary>
    public int Duckets => ReadWalletPoint(WalletPointTypes.Duckets);

    /// <summary>
    /// The balance of an arbitrary activity-point currency.
    /// </summary>
    /// <param name="type">
    /// The currency type id: 0 duckets, 5 diamonds; hotels define further ids for seasonal
    /// currencies.
    /// </param>
    /// <returns>The reported balance.</returns>
    public int Points(int type) => ReadWalletPoint(type);

    /// <summary>
    /// Whether the script is still allowed to run. Turns <see langword="false"/> as soon as the
    /// script is asked to stop, which makes it the idiomatic loop condition:
    /// <c>while (Run) { ... }</c>.
    /// </summary>
    public bool Run => !Ct.IsCancellationRequested;

    /// <summary>
    /// Blocks the calling thread for the given number of milliseconds, waking early if the
    /// script is stopped.
    /// </summary>
    /// <param name="milliseconds">How long to sleep.</param>
    /// <exception cref="OperationCanceledException">The script was stopped while sleeping.</exception>
    /// <remarks>
    /// This blocks a thread. Prefer <see cref="Delay(int)"/> inside async code; use this one in
    /// straight-line script bodies.
    /// </remarks>
    public void Sleep(int milliseconds)
    {
        if (Ct.WaitHandle.WaitOne(milliseconds))
            Ct.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Blocks the calling thread for the given interval, waking early if the script is stopped.
    /// </summary>
    /// <exception cref="OperationCanceledException">The script was stopped while sleeping.</exception>
    public void Sleep(TimeSpan timeout)
    {
        if (Ct.WaitHandle.WaitOne(timeout))
            Ct.ThrowIfCancellationRequested();
    }

    public void Send(Header header, params object[] values)
    {
        using var packet = new Packet(header, CurrentClient);
        packet.Writer().WriteValues(values);
        Ext.Send(packet);
    }

    /// <summary>
    /// Runs an action on a background thread, without waiting for it. Use it for a loop that
    /// should keep going while the main script body does something else.
    /// </summary>
    /// <param name="action">The work to run. It is cancelled together with the script.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The task is observed: an exception escaping it is reported as a script error and stops
    /// the run, rather than being swallowed. Cancellation and <see cref="Finish"/> are treated
    /// as a normal end.
    /// </remarks>
    public void RunTask(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        StartObservedTask(() => Task.Run(action, Ct), Ct);
    }

    /// <summary>
    /// Runs an asynchronous operation in the background, without waiting for it.
    /// </summary>
    /// <param name="action">The work to run. It is cancelled together with the script.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The task is observed: an exception escaping it is reported as a script error and stops
    /// the run.
    /// </remarks>
    public void RunTask(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        StartObservedTask(() => Task.Run(action, Ct), Ct);
    }

    private void StartObservedTask(Func<Task> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!TryTrackBackgroundTask(completion.Task, cancellationToken))
            return;
        _ = ObserveTask(action, cancellationToken, completion);
    }

    private async Task ObserveTask(
        Func<Task> action,
        CancellationToken cancellationToken,
        TaskCompletionSource completion)
    {
        using IDisposable scope = ScriptExecutionContext.Enter(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
        }
        catch (ScriptFinishedException)
        {
            if (!cancellationToken.IsCancellationRequested)
                ReportBackgroundFinished();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            ReportBackgroundError(error);
        }
        finally
        {
            completion.TrySetResult();
        }
    }

    private void ReportBackgroundError(Exception error)
    {
        lock (_subscriptions)
            if (_disposed)
                return;

        if (_backgroundError is not null)
            _backgroundError(error);
        else
            _log(ScriptExecutionError.FromException(error, "background").Format());
    }

    /// <summary>
    /// Waits forever, until the script is stopped. Use it at the end of an event-driven script
    /// so the run stays alive while its handlers do the work.
    /// </summary>
    /// <exception cref="OperationCanceledException">The script was stopped.</exception>
    public Task Wait() => Task.Delay(Timeout.Infinite, Ct);

    /// <summary>
    /// Asynchronously waits for the given number of milliseconds. Same as
    /// <see cref="Delay(int)"/>.
    /// </summary>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    public Task DelayAsync(int milliseconds) => Task.Delay(milliseconds, Ct);

    /// <summary>
    /// A random integer in the half-open range <c>[min, max)</c>.
    /// </summary>
    /// <param name="min">Inclusive lower bound.</param>
    /// <param name="max">Exclusive upper bound.</param>
    public int Rand(int min, int max) => Random.Shared.Next(min, max);

    /// <summary>A random integer from 0 up to but not including <paramref name="max"/>.</summary>
    public int Rand(int max) => Random.Shared.Next(max);

    /// <summary>A random double in the half-open range <c>[0, 1)</c>.</summary>
    public double RandDouble() => Random.Shared.NextDouble();

    /// <summary>
    /// A random element of the sequence.
    /// </summary>
    /// <param name="items">
    /// The candidates. A sequence that is not already a list is enumerated once into one.
    /// </param>
    /// <returns>A random element, or <c>default</c> when the sequence is empty.</returns>
    public T? Rand<T>(IEnumerable<T> items)
    {
        IList<T> list = items as IList<T> ?? items.ToList();
        return list.Count == 0 ? default : list[Random.Shared.Next(list.Count)];
    }
}
