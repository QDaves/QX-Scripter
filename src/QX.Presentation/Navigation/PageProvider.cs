using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Mvvm;

namespace Qx.Presentation.Navigation;

public sealed class PageProvider(IServiceProvider services, IReadOnlyDictionary<PageKey, Type> pages) : IPageProvider
{
    readonly Dictionary<PageKey, PageViewModel> _created = [];
    readonly HashSet<PageKey> _creating = [];

    public PageViewModel Get(PageKey key)
    {
        if (_created.TryGetValue(key, out PageViewModel? page))
            return page;
        if (!pages.TryGetValue(key, out Type? type))
            throw new InvalidOperationException($"No page is registered for {key}.");
        if (!_creating.Add(key))
            throw new InvalidOperationException($"The {key} page needs itself while it is being created. Resolve pages lazily, never from a constructor.");
        try
        {
            page = (PageViewModel)services.GetRequiredService(type);
        }
        finally
        {
            _creating.Remove(key);
        }
        _created[key] = page;
        return page;
    }

    public bool IsCreated(PageKey key) => _created.ContainsKey(key);
}
