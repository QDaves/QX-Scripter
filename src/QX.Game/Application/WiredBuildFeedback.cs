using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class WiredBuildFeedback : IDisposable
{
    private readonly GameState game;
    private readonly object sync = new();
    private readonly IDisposable notifications;
    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? failure;
    private long throttles;
    private (int Page, int Offer, int X, int Y)? placement;
    private readonly IReadOnlyDictionary<string, int> errors;

    public WiredBuildFeedback(GameState game, IInterceptor interceptor, IReadOnlyList<WiredError> baseline)
    {
        this.game = game;
        errors = baseline.GroupBy(error => error.ErrorName).ToDictionary(group => group.Key, group => group.Max(error => error.ThrowCount));
        notifications = interceptor.Intercept(MessageKeys.Notifications.Dialog, message => {
            NotificationDialog notification;
            try { notification = NotificationDialog.Parse(message.Packet.Reader()); }
            catch (Exception error) when (error is InvalidDataException or IndexOutOfRangeException or ArgumentException) { return; }
            if (!notification.IsPlacementRateLimit) return;
            lock (sync) throttles++;
            signal();
        });
        game.Room.PlacementStateCommitted += room_changed;
        game.Room.FloorItemUpdated += item_changed;
        game.Room.FloorItemDataChanged += data_changed;
        game.Room.FloorItemsLoaded += signal;
        game.Subscriptions.PlacementWarningReceived += placement_warning;
        game.Wired.StateChanged += wired_changed;
    }

    public long Throttles { get { lock (sync) return throttles; } }
    public Task Changed { get { lock (sync) return changed.Task; } }

    public void Placement(int page, int offer, int x, int y)
    {
        lock (sync) placement = (page, offer, x, y);
    }

    public void Check()
    {
        lock (sync)
            if (failure is not null) throw new InvalidOperationException(failure);
    }

    private void placement_warning(BuildersClubPlacementWarning warning)
    {
        lock (sync)
        {
            if (placement is not { } expected || warning.PageId != expected.Page || warning.OfferId != expected.Offer ||
                warning.Placement is not BuildersClubFloorPlacement floor || floor.X != expected.X || floor.Y != expected.Y) return;
            failure = "The hotel requires Builders Club placement confirmation despite the retry flag. Construction stopped without repeating the placement.";
        }
        signal();
    }

    private void wired_changed(WiredStateUpdate update)
    {
        if (update.Value is not WiredErrorLogs logs) return;
        string[] added = logs.Errors.Where(error => error.ThrowCount > errors.GetValueOrDefault(error.ErrorName))
            .Select(error => error.ErrorName).ToArray();
        if (added.Length == 0) return;
        lock (sync) failure = "Construction stopped after new Wired errors: " + string.Join(", ", added);
        signal();
    }

    private void room_changed(RoomPlacementStateCommit _) => signal();
    private void item_changed(FloorItem _) => signal();
    private void data_changed(FloorItem _, ItemData before, ItemData after) => signal();

    private void signal()
    {
        TaskCompletionSource previous;
        lock (sync)
        {
            previous = changed;
            changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        previous.TrySetResult();
    }

    public void Dispose()
    {
        notifications.Dispose();
        game.Room.PlacementStateCommitted -= room_changed;
        game.Room.FloorItemUpdated -= item_changed;
        game.Room.FloorItemDataChanged -= data_changed;
        game.Room.FloorItemsLoaded -= signal;
        game.Subscriptions.PlacementWarningReceived -= placement_warning;
        game.Wired.StateChanged -= wired_changed;
    }
}

internal sealed class WiredBuildPacing
{
    private readonly TimeProvider clock;
    private long? sent;
    private double latency;
    private double variance;
    private double spacing;

    public WiredBuildPacing(TimeProvider clock) => this.clock = clock;

    public int ConfirmationTimeout(int maximum) => Math.Min(maximum,
        (int)Math.Ceiling(Math.Clamp(Math.Max(latency + 4 * variance, spacing), 500, 10000)));

    public TimeSpan RemainingSpacing(int minimum = 0) => sent is { } previous
        ? TimeSpan.FromMilliseconds(Math.Max(0, Math.Max(minimum, spacing) - clock.GetElapsedTime(previous).TotalMilliseconds))
        : TimeSpan.Zero;

    public void Sent() => sent = clock.GetTimestamp();

    public void Confirmed(bool retried)
    {
        if (!retried && sent is { } previous)
        {
            double sample = Math.Max(1, clock.GetElapsedTime(previous).TotalMilliseconds);
            variance = latency == 0 ? sample / 2 : variance * 0.75 + Math.Abs(latency - sample) * 0.25;
            latency = latency == 0 ? sample : latency * 0.875 + sample * 0.125;
        }
        spacing = spacing < 1 ? 0 : spacing * 0.9;
    }

    public void Backoff(int maximum)
    {
        spacing = Math.Min(maximum, Math.Max(spacing * 2, ConfirmationTimeout(maximum)));
        sent = clock.GetTimestamp();
    }
}
