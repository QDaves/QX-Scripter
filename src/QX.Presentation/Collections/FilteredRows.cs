using Qx.Presentation.Threading;

namespace Qx.Presentation.Collections;

public sealed class FilteredRows<T> where T : class
{
    public const int InlineLimit = 2000;

    readonly SerialOperation _filtering = new();

    public ResettableCollection<T> Visible { get; } = [];

    public int SourceCount { get; private set; }

    public event Action? Applied;

    public async Task ApplyAsync(IReadOnlyCollection<T> source, Func<T, bool> keep, IComparer<T>? order, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keep);
        T[] snapshot = [.. source];
        OperationLease lease = await _filtering.StartAsync(cancellation_token);
        try
        {
            T[] result = snapshot.Length <= InlineLimit
                ? Select(snapshot, keep, order, lease.Token)
                : await Task.Run(() => Select(snapshot, keep, order, lease.Token), lease.Token);
            if (!_filtering.IsCurrent(lease))
                return;
            SourceCount = snapshot.Length;
            if (!result.SequenceEqual(Visible, ReferenceEqualityComparer.Instance))
                Visible.ReplaceAll(result);
            Applied?.Invoke();
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
        }
        finally
        {
            _filtering.Complete(lease);
        }
    }

    static T[] Select(T[] snapshot, Func<T, bool> keep, IComparer<T>? order, CancellationToken cancellation_token)
    {
        var kept = new List<T>(snapshot.Length);
        for (int index = 0; index < snapshot.Length; index++)
        {
            if ((index & 1023) == 0)
                cancellation_token.ThrowIfCancellationRequested();
            if (keep(snapshot[index]))
                kept.Add(snapshot[index]);
        }
        return order is null ? [.. kept] : [.. kept.Order(order)];
    }
}
