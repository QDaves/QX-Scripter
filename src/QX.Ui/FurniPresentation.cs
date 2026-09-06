using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Qx.Ui;

internal static class FurniSearch
{
    internal static int? Rank(string name, string identifier, string description, string text)
    {
        string term = text.Trim();
        if (term.Length == 0)
            return 0;
        if (name.Equals(term, StringComparison.CurrentCultureIgnoreCase))
            return 0;
        if (name.StartsWith(term, StringComparison.CurrentCultureIgnoreCase))
            return 1;
        if (name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            return 2;
        if (identifier.Equals(term, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (identifier.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (identifier.Contains(term, StringComparison.OrdinalIgnoreCase))
            return 5;
        return description.Contains(term, StringComparison.CurrentCultureIgnoreCase) ? 6 : null;
    }
}

internal static class VisibleItems
{
    internal static IReadOnlyList<T> Rows<T>(DataGrid grid) =>
        Find<T, DataGridRow>(grid);

    internal static IReadOnlyList<T> Tiles<T>(ListBox list) =>
        Find<T, ListBoxItem>(list);

    private static IReadOnlyList<T> Find<T, TContainer>(ItemsControl control)
        where TContainer : FrameworkElement
    {
        var items = new List<(double Top, T Item)>();
        foreach (TContainer container in Descendants<TContainer>(control))
        {
            if (!container.IsVisible || container.DataContext is not T item)
                continue;

            try
            {
                Rect bounds = container.TransformToAncestor(control)
                    .TransformBounds(new Rect(container.RenderSize));
                if (bounds.Bottom >= 0 && bounds.Top <= control.ActualHeight)
                    items.Add((bounds.Top, item));
            }
            catch (InvalidOperationException)
            {
            }
        }

        return items
            .OrderBy(entry => entry.Top)
            .Select(entry => entry.Item)
            .ToArray();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (T nested in Descendants<T>(child))
                yield return nested;
        }
    }
}
