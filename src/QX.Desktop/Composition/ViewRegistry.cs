using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Qx.Desktop.Composition;

public sealed class ViewRegistry : IDataTemplate
{
    readonly Dictionary<Type, Func<Control>> _views = [];

    public ViewRegistry Register<TViewModel>(Func<Control> factory) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_views.TryAdd(typeof(TViewModel), factory))
            throw new InvalidOperationException($"A view for {typeof(TViewModel).Name} is already registered.");
        return this;
    }

    public bool Match(object? data) => data is not null && Find(data.GetType()) is not null;

    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        Func<Control> factory = Find(param.GetType())
            ?? throw new InvalidOperationException($"No view is registered for {param.GetType().Name}.");
        Control view = factory();
        view.DataContext = param;
        return view;
    }

    Func<Control>? Find(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (_views.TryGetValue(current, out Func<Control>? factory))
                return factory;
        }
        return null;
    }
}
