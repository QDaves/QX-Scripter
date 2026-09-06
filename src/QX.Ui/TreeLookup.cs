using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Qx.Ui;

public static class TreeLookup
{
    /// <summary>
    /// The nearest ancestor of the given type above a hit-tested element. Walks the visual tree
    /// where it can and steps through the logical tree where it cannot, since the source of a
    /// mouse event is not always a visual.
    /// </summary>
    public static T? Ancestor<T>(object? source) where T : DependencyObject
    {
        DependencyObject? node = source as DependencyObject;

        while (node is not null and not T)
            node = node is Visual or Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);

        return node as T;
    }

    /// <summary>
    /// The first descendant of the given type below an element, breadth first.
    /// </summary>
    /// <remarks>
    /// Only valid once the element has a visual tree, so callers cache the answer rather than
    /// asking before the template has been applied.
    /// </remarks>
    public static T? FirstChild<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
            return null;

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            DependencyObject node = queue.Dequeue();
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, index);
                if (child is T match)
                    return match;
                queue.Enqueue(child);
            }
        }

        return null;
    }
}
