using Qx.Presentation.Navigation;

namespace Qx.Presentation.Composition;

public sealed class PageRegistrations
{
    readonly Dictionary<PageKey, Type> _pages = [];

    public IReadOnlyDictionary<PageKey, Type> Pages => _pages;

    public void Add(PageKey key, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!_pages.TryAdd(key, type))
            throw new InvalidOperationException($"{key} is registered twice.");
    }
}
