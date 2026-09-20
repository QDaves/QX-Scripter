namespace Qx.Presentation.Collections;

public sealed class KeyedRows<TKey, TRow>
    where TKey : notnull
    where TRow : class
{
    readonly Dictionary<TKey, TRow> _rows;

    public KeyedRows(IEqualityComparer<TKey>? comparer = null) => _rows = new Dictionary<TKey, TRow>(comparer);

    public IReadOnlyCollection<TRow> Rows => _rows.Values;

    public int Count => _rows.Count;

    public TRow? Find(TKey key) => _rows.GetValueOrDefault(key);

    public RowChanges Sync<TSource>(IReadOnlyList<TSource> source, Func<TSource, TKey> source_key, Func<TSource, TRow> create, Action<TRow, TSource> update)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source_key);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(update);
        var seen = new HashSet<TKey>(source.Count, _rows.Comparer);
        int added = 0;
        int updated = 0;
        for (int index = 0; index < source.Count; index++)
        {
            TSource item = source[index];
            TKey key = source_key(item);
            if (!seen.Add(key))
                continue;
            if (_rows.TryGetValue(key, out TRow? row))
            {
                update(row, item);
                updated++;
                continue;
            }
            _rows[key] = create(item);
            added++;
        }
        int removed = 0;
        if (_rows.Count != seen.Count)
        {
            foreach (TKey stale in _rows.Keys.Where(key => !seen.Contains(key)).ToArray())
            {
                _rows.Remove(stale);
                removed++;
            }
        }
        return new RowChanges(added, updated, removed);
    }

    public void Clear() => _rows.Clear();
}
