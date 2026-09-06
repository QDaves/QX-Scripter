namespace Qx.Game.Snapshots;

public sealed class SnapshotSourceLimitExceededException : InvalidOperationException
{
    public string SourceName { get; }
    public int SourceItemLimit { get; }

    public SnapshotSourceLimitExceededException(string sourceName, int sourceItemLimit)
        : base($"Snapshot source '{sourceName}' exceeded its explicit limit of {sourceItemLimit} items.")
    {
        SourceName = sourceName;
        SourceItemLimit = sourceItemLimit;
    }
}

public static partial class SnapshotFactory
{
    public const int DefaultSourceItemLimit = 100_000;

    private static CappedSource<T> SelectCapped<T>(
        IEnumerable<T> source,
        int maxItems,
        int sourceItemLimit,
        string sourceName,
        IComparer<T> comparer,
        Predicate<T>? completed = null)
    {
        ArgumentNullException.ThrowIfNull(source, sourceName);
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentOutOfRangeException.ThrowIfNegative(maxItems);
        ValidateSourceItemLimit(sourceItemLimit);
        RejectKnownOversizeSource(source, sourceItemLimit, sourceName);

        IComparer<RankedSourceItem<T>> rankedComparer = Comparer<RankedSourceItem<T>>.Create(
            (left, right) =>
            {
                int comparison = comparer.Compare(left.Value, right.Value);
                return comparison != 0 ? comparison : left.Index.CompareTo(right.Index);
            });
        IComparer<RankedSourceItem<T>> worstFirst = Comparer<RankedSourceItem<T>>.Create(
            (left, right) => rankedComparer.Compare(right, left));
        var selected = new PriorityQueue<RankedSourceItem<T>, RankedSourceItem<T>>(worstFirst);
        int total = 0;
        int completedCount = 0;

        using IEnumerator<T> enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (total == sourceItemLimit)
                throw new SnapshotSourceLimitExceededException(sourceName, sourceItemLimit);

            T value = enumerator.Current;
            if (completed?.Invoke(value) == true)
                completedCount++;

            if (maxItems > 0)
            {
                var candidate = new RankedSourceItem<T>(value, total);
                if (selected.Count < maxItems)
                {
                    selected.Enqueue(candidate, candidate);
                }
                else if (rankedComparer.Compare(candidate, selected.Peek()) < 0)
                {
                    selected.Dequeue();
                    selected.Enqueue(candidate, candidate);
                }
            }

            total++;
        }

        RankedSourceItem<T>[] ranked = selected.UnorderedItems
            .Select(item => item.Element)
            .ToArray();
        Array.Sort(ranked, rankedComparer);

        var items = new T[ranked.Length];
        for (int index = 0; index < ranked.Length; index++)
            items[index] = ranked[index].Value;

        return new CappedSource<T>(total, completedCount, items);
    }

    private static List<T> MaterializeBounded<T>(
        IEnumerable<T> source,
        int sourceItemLimit,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(source, sourceName);
        ValidateSourceItemLimit(sourceItemLimit);
        RejectKnownOversizeSource(source, sourceItemLimit, sourceName);

        int capacity = source.TryGetNonEnumeratedCount(out int count)
            ? count
            : Math.Min(sourceItemLimit, 256);
        var items = new List<T>(capacity);
        using IEnumerator<T> enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (items.Count == sourceItemLimit)
                throw new SnapshotSourceLimitExceededException(sourceName, sourceItemLimit);

            items.Add(enumerator.Current);
        }

        return items;
    }

    private static void ValidateSourceItemLimit(int sourceItemLimit) =>
        ArgumentOutOfRangeException.ThrowIfNegative(sourceItemLimit);

    private static void RejectKnownOversizeSource<T>(
        IEnumerable<T> source,
        int sourceItemLimit,
        string sourceName)
    {
        if (source.TryGetNonEnumeratedCount(out int count) && count > sourceItemLimit)
            throw new SnapshotSourceLimitExceededException(sourceName, sourceItemLimit);
    }

    private readonly record struct RankedSourceItem<T>(T Value, int Index);

    private readonly record struct CappedSource<T>(
        int Total,
        int Completed,
        T[] Items);
}
