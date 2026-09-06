using Qx;
using Qx.Diagnostics;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Interception;

public sealed class InterceptDispatcher
{
    private const string Category = "intercept";

    private sealed class Registration
    {
        public Header? Header;
        public Identifier? Identifier;
        public MessageKey? Key;
        public required Action<Intercept> Callback;
        public bool Removed;
    }

    private sealed class Bindings
    {
        public required Dictionary<Header, Action<Intercept>[]> Index;
        public required IReadOnlyList<Identifier> Unresolved;
        public required IReadOnlyList<MessageKey> UnresolvedKeys;
    }

    private static readonly Bindings Empty = new()
    {
        Index = [],
        Unresolved = [],
        UnresolvedKeys = []
    };

    private readonly List<Registration> _registrations = [];
    private readonly HashSet<Identifier> _reported = [];
    private readonly HashSet<MessageKey> _reported_keys = [];
    private readonly object _sync = new();
    private volatile Bindings? _bindings;
    private IMessageManager? _manager;
    private ISemanticMessageResolver? _semantic_resolver;
    private bool _messages_available;

    /// <summary>
    /// Identifiers that the bound message manager could not resolve to a header.
    /// Callbacks registered under these identifiers are bound to nothing and never run.
    /// </summary>
    public IReadOnlyList<Identifier> UnresolvedIdentifiers => Snapshot().Unresolved;

    public IReadOnlyList<MessageKey> UnresolvedKeys => Snapshot().UnresolvedKeys;

    /// <summary>
    /// Raised when an intercept callback throws. The failure is isolated: the remaining
    /// callbacks registered for the same header still run.
    /// </summary>
    public event Action<Intercept, Exception>? CallbackFailed;

    public IDisposable Add(Header header, Action<Intercept> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var registration = new Registration { Header = header, Callback = callback };
        lock (_sync)
        {
            _registrations.Add(registration);
            _bindings = null;
        }
        return new Subscription(this, registration);
    }

    public IDisposable Add(Identifier identifier, Action<Intercept> callback, IMessageManager manager)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(manager);
        var registration = new Registration { Identifier = identifier, Callback = callback };
        lock (_sync)
        {
            _manager = manager;
            _registrations.Add(registration);
            _bindings = null;
        }
        return new Subscription(this, registration);
    }

    public IDisposable Add(MessageKey key, Action<Intercept> callback, ISemanticMessageResolver resolver)
    {
        if (key.IsEmpty)
            throw new ArgumentException("An intercept requires a semantic message key.", nameof(key));
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(resolver);
        var registration = new Registration { Key = key, Callback = callback };
        lock (_sync)
        {
            _semantic_resolver = resolver;
            _registrations.Add(registration);
            _bindings = null;
        }
        return new Subscription(this, registration);
    }

    /// <summary>
    /// Rebinds every identifier registration against <paramref name="manager"/>.
    /// </summary>
    /// <param name="manager">The message manager used to resolve identifiers to headers.</param>
    /// <param name="messages_available">
    /// Whether a message catalog is currently loaded for the active client. When false,
    /// unresolved identifiers are expected and are reported at debug level; when true an
    /// unresolved identifier is a real defect and is reported as a warning.
    /// </param>
    public void Rebind(IMessageManager manager, bool messages_available = true)
    {
        ArgumentNullException.ThrowIfNull(manager);
        lock (_sync)
        {
            _manager = manager;
            _semantic_resolver = manager as ISemanticMessageResolver;
            _messages_available = messages_available;
            _bindings = null;
        }
    }

    public void Dispatch(Intercept intercept)
    {
        ArgumentNullException.ThrowIfNull(intercept);

        Header header = intercept.Packet.Header;
        if (!Snapshot().Index.TryGetValue(header, out Action<Intercept>[]? callbacks))
            return;

        foreach (Action<Intercept> callback in callbacks)
        {
            bool failed = false;
            try
            {
                intercept.Packet.Position = 0;
                callback(intercept);
            }
            catch (Exception error)
            {
                failed = true;
                ReportCallbackFailure(header, intercept, error);
            }
            finally
            {
                try
                {
                    intercept.Packet.Position = 0;
                }
                catch (Exception error)
                {
                    if (!failed)
                        ReportCallbackFailure(header, intercept, error);
                }
            }
        }
    }

    private Bindings Snapshot()
    {
        Bindings? bindings = _bindings;
        if (bindings is not null)
            return bindings;
        lock (_sync)
            return _bindings ??= Rebuild();
    }

    private Bindings Rebuild()
    {
        var index = new Dictionary<Header, List<Action<Intercept>>>();
        List<Identifier>? unresolved = null;
        List<MessageKey>? unresolved_keys = null;
        int resolved_messages = 0;

        foreach (Registration registration in _registrations)
        {
            if (registration.Removed)
                continue;

            IReadOnlyList<Header> headers;
            if (registration.Header is Header header)
            {
                headers = [header];
            }
            else if (registration.Identifier is { } identifier)
            {
                if (_manager is not null &&
                    _manager.TryGetHeaders(identifier, out IReadOnlyList<Header> resolved) &&
                    resolved.Count > 0)
                {
                    headers = resolved;
                    resolved_messages++;
                }
                else
                {
                    (unresolved ??= []).Add(identifier);
                    continue;
                }
            }
            else if (registration.Key is { } key)
            {
                if (_semantic_resolver is not null &&
                    _semantic_resolver.TryGetHeaders(key, out IReadOnlyList<Header> resolved) &&
                    resolved.Count > 0)
                {
                    headers = resolved;
                    resolved_messages++;
                }
                else
                {
                    if (_semantic_resolver is not null &&
                        _semantic_resolver.IsKnown(key) &&
                        !_semantic_resolver.IsApplicable(key))
                    {
                        continue;
                    }
                    (unresolved_keys ??= []).Add(key);
                    continue;
                }
            }
            else
            {
                continue;
            }

            foreach (Header resolved_header in headers)
            {
                if (!index.TryGetValue(resolved_header, out List<Action<Intercept>>? list))
                {
                    list = [];
                    index[resolved_header] = list;
                }
                list.Add(registration.Callback);
            }
        }

        ReportUnresolved(unresolved, unresolved_keys, resolved_messages);

        if (index.Count == 0 && unresolved is null && unresolved_keys is null)
            return Empty;

        var frozen = new Dictionary<Header, Action<Intercept>[]>(index.Count);
        foreach ((Header key, List<Action<Intercept>> list) in index)
            frozen[key] = [.. list];

        return new Bindings
        {
            Index = frozen,
            Unresolved = unresolved is null ? [] : [.. unresolved.Distinct()],
            UnresolvedKeys = unresolved_keys is null ? [] : [.. unresolved_keys.Distinct()]
        };
    }

    private void ReportUnresolved(
        List<Identifier>? unresolved,
        List<MessageKey>? unresolved_keys,
        int resolved_messages)
    {
        int unresolved_count = (unresolved?.Count ?? 0) + (unresolved_keys?.Count ?? 0);
        if (unresolved_count == 0)
        {
            _reported.Clear();
            _reported_keys.Clear();
            return;
        }

        if (!_messages_available && resolved_messages == 0)
        {
            _reported.Clear();
            _reported_keys.Clear();
            Diag.Debug(
                $"No message catalog is bound; {unresolved_count} message registration(s) are unbound.",
                Category);
            return;
        }

        _reported.IntersectWith(unresolved ?? []);
        foreach (Identifier identifier in unresolved ?? [])
        {
            if (!_reported.Add(identifier))
                continue;
            Diag.Warn(
                $"Unresolved intercept identifier '{identifier.ToString(true)}'; " +
                "no header matched it, so its callbacks will never run.",
                Category);
        }

        _reported_keys.IntersectWith(unresolved_keys ?? []);
        foreach (MessageKey key in unresolved_keys ?? [])
        {
            if (!_reported_keys.Add(key))
                continue;
            Diag.Warn(
                $"Unresolved semantic intercept '{key}'; no header matched it, so its callbacks will never run.",
                Category);
        }
    }

    private void ReportCallbackFailure(Header header, Intercept intercept, Exception error)
    {
        Diag.Error($"Intercept callback for {Describe(header)} threw: {error}", Category);

        if (CallbackFailed is not { } subscribers)
            return;

        foreach (Action<Intercept, Exception> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(intercept, error);
            }
            catch (Exception subscriber_error)
            {
                Diag.Error(
                    $"Intercept failure subscriber threw: {subscriber_error}",
                    Category);
            }
        }
    }

    private string Describe(Header header)
    {
        IMessageManager? manager;
        lock (_sync)
            manager = _manager;

        string direction = header.Direction switch
        {
            Direction.In => "in",
            Direction.Out => "out",
            _ => "unknown"
        };

        try
        {
            if (manager is not null && manager.TryGetIdentifier(header, out Identifier identifier))
                return $"{identifier.ToString(true)} ({direction}:{header.Value})";
        }
        catch
        {
        }

        return $"{direction}:{header.Value}";
    }

    private void Remove(Registration registration)
    {
        lock (_sync)
        {
            registration.Removed = true;
            _bindings = null;
        }
    }

    private sealed class Subscription(InterceptDispatcher dispatcher, Registration registration) : IDisposable
    {
        public void Dispose() => dispatcher.Remove(registration);
    }
}
