using System.Collections.ObjectModel;

namespace Qx.Presentation.Collections;

public static class CollectionSync
{
    public const int OrderedLimit = 2000;

    public static void Sync<TRow, TSource, TKey>(
        ObservableCollection<TRow> rows,
        IReadOnlyList<TSource> source,
        Func<TRow, TKey> row_key,
        Func<TSource, TKey> source_key,
        Func<TSource, TRow> create,
        Action<TRow, TSource> update)
        where TKey : notnull
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(row_key);
        ArgumentNullException.ThrowIfNull(source_key);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(update);
        var wanted = new HashSet<TKey>(source.Count);
        foreach (TSource item in source)
            wanted.Add(source_key(item));
        for (int index = rows.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(row_key(rows[index])))
                rows.RemoveAt(index);
        }
        var existing = new Dictionary<TKey, TRow>(rows.Count);
        foreach (TRow row in rows)
            existing.TryAdd(row_key(row), row);
        for (int index = 0; index < source.Count; index++)
        {
            TSource item = source[index];
            TKey key = source_key(item);
            if (existing.TryGetValue(key, out TRow? row))
            {
                update(row, item);
                if (index >= rows.Count || !ReferenceEquals(rows[index], row))
                    rows.Move(Position(rows, row, index), index);
                continue;
            }
            TRow created = create(item);
            existing[key] = created;
            rows.Insert(index, created);
        }
        while (rows.Count > source.Count)
            rows.RemoveAt(rows.Count - 1);
    }

    static int Position<TRow>(ObservableCollection<TRow> rows, TRow row, int start) where TRow : class
    {
        for (int at = start; at < rows.Count; at++)
        {
            if (ReferenceEquals(rows[at], row))
                return at;
        }
        return rows.IndexOf(row);
    }
}
