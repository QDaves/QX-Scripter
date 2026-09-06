using System.Collections.ObjectModel;

namespace Qx.Game.Application;

internal sealed class ApplicationCatalog
{
    private readonly IReadOnlyDictionary<string, IApplicationBinding> by_id;

    public ApplicationCatalog(IEnumerable<IApplicationBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var values = new Dictionary<string, IApplicationBinding>(StringComparer.Ordinal);
        foreach (IApplicationBinding binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            if (!values.TryAdd(binding.Descriptor.Id, binding))
                throw new InvalidDataException($"Application member '{binding.Descriptor.Id}' is declared more than once.");
        }
        if (values.Count == 0)
            throw new ArgumentException("An application catalog requires at least one member.", nameof(bindings));

        Bindings = Array.AsReadOnly(values.Values.OrderBy(value => value.Descriptor.Id, StringComparer.Ordinal).ToArray());
        Descriptors = Array.AsReadOnly(Bindings.Select(value => value.Descriptor).ToArray());
        by_id = new ReadOnlyDictionary<string, IApplicationBinding>(values);
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }
    public IReadOnlyList<ApplicationDescriptor> Descriptors { get; }

    public bool TryGet(string id, out IApplicationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(id);
        return by_id.TryGetValue(id, out binding!);
    }

    public IApplicationBinding Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return by_id.TryGetValue(id, out IApplicationBinding? binding)
            ? binding
            : throw new KeyNotFoundException($"Unknown application member '{id}'.");
    }

    public ApplicationDescriptor Describe(string id) => Get(id).Descriptor;

    public ValueTask<object?> InvokeAsync(
        string id,
        object? request,
        CancellationToken cancellation_token = default) =>
        Get(id).InvokeAsync(request, cancellation_token);

    public IDisposable Subscribe(string id, Action<object?> receiver) =>
        Get(id).Subscribe(receiver);
}
