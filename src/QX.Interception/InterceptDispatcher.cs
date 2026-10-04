using Qx;
using Qx.Diagnostics;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Interception;

/// <summary>Provides the dispatch of intercepted packets to the callbacks registered for their header.</summary>
/// <remarks>
/// Registrations by identifier or semantic key are resolved to headers again after every registration
/// change or rebind, against the message resolver that is bound at that time.
/// </remarks>
public sealed class InterceptDispatcher
{
    private const string Category = "intercept";

    private sealed class Registration
    {
        public Header? Header;
        public Identifier? Identifier;
        public MessageKey? Key;
        public required Action<Intercept> Callback;
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
    private IMessageResolver? _resolver;
    private bool _messages_available;

    /// <summary>Gets the identifiers that the bound message resolver could not resolve to a header.</summary>
    /// <remarks>Callbacks registered under these identifiers are bound to nothing and never run.</remarks>
    public IReadOnlyList<Identifier> UnresolvedIdentifiers => Snapshot().Unresolved;

    /// <summary>Gets the semantic message keys that the bound resolver could not resolve to a header.</summary>
    /// <remarks>Callbacks registered under these keys are bound to nothing and never run.</remarks>
    public IReadOnlyList<MessageKey> UnresolvedKeys => Snapshot().UnresolvedKeys;

    /// <summary>Occurs when an intercept callback throws.</summary>
    /// <remarks>The failure is isolated: the remaining callbacks registered for the same header still run.</remarks>
    public event Action<Intercept, Exception>? CallbackFailed;

    /// <summary>Registers a callback for packets with a header.</summary>
    /// <param name="header">The header to intercept.</param>
    /// <param name="callback">The callback that receives each matching packet.</param>
    /// <returns>A handle that removes the callback when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is <see langword="null"/>.</exception>
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

    /// <summary>Registers a callback for packets of a named message.</summary>
    /// <param name="identifier">The message to intercept.</param>
    /// <param name="callback">The callback that receives each matching packet.</param>
    /// <param name="resolver">The resolver that maps identifiers and keys to headers, which replaces the one used for every registration.</param>
    /// <returns>A handle that removes the callback when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> has no message name or no direction.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> or <paramref name="resolver"/> is <see langword="null"/>.</exception>
    public IDisposable Add(Identifier identifier, Action<Intercept> callback, IMessageResolver resolver)
    {
        if (string.IsNullOrWhiteSpace(identifier.Name))
            throw new ArgumentException("An intercept requires a message name.", nameof(identifier));
        if (identifier.Direction is not (MessageDirection.In or MessageDirection.Out or MessageDirection.Both))
            throw new ArgumentException("An intercept requires the in, out or both direction.", nameof(identifier));
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(resolver);
        var registration = new Registration { Identifier = identifier, Callback = callback };
        lock (_sync)
        {
            _resolver = resolver;
            _registrations.Add(registration);
            _bindings = null;
        }
        return new Subscription(this, registration);
    }

    /// <summary>Registers a callback for packets of a semantic message.</summary>
    /// <param name="key">The semantic message key to intercept.</param>
    /// <param name="callback">The callback that receives each matching packet.</param>
    /// <param name="resolver">The resolver that maps identifiers and keys to headers, which replaces the one used for every registration.</param>
    /// <returns>A handle that removes the callback when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> or <paramref name="resolver"/> is <see langword="null"/>.</exception>
    public IDisposable Add(MessageKey key, Action<Intercept> callback, IMessageResolver resolver)
    {
        if (key.IsEmpty)
            throw new ArgumentException("An intercept requires a semantic message key.", nameof(key));
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(resolver);
        var registration = new Registration { Key = key, Callback = callback };
        lock (_sync)
        {
            _resolver = resolver;
            _registrations.Add(registration);
            _bindings = null;
        }
        return new Subscription(this, registration);
    }

    /// <summary>
    /// Rebinds every identifier and semantic key registration against <paramref name="resolver"/>.
    /// </summary>
    /// <param name="resolver">The resolver used to map identifiers and keys to headers.</param>
    /// <param name="messagesAvailable">
    /// Whether a message catalog is currently loaded for the active session. When false and nothing
    /// resolves, unresolved registrations are expected and are reported at debug level; otherwise each
    /// unresolved identifier or key is a real defect and is reported as a warning once until the next rebind.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolver"/> is <see langword="null"/>.</exception>
    public void Rebind(IMessageResolver resolver, bool messagesAvailable = true)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        lock (_sync)
        {
            _resolver = resolver;
            _messages_available = messagesAvailable;
            _bindings = null;
            _reported.Clear();
            _reported_keys.Clear();
        }
    }

    /// <summary>Runs every callback registered for the header of an intercepted packet.</summary>
    /// <remarks>
    /// The packet position is reset to zero before and after each callback. A callback that throws is
    /// logged and reported through <see cref="CallbackFailed"/>, and the remaining callbacks still run.
    /// </remarks>
    /// <param name="intercept">The intercepted packet.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="intercept"/> is <see langword="null"/>.</exception>
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
            IReadOnlyList<Header> headers;
            if (registration.Header is Header header)
            {
                headers = [header];
            }
            else if (registration.Identifier is { } identifier)
            {
                if (TryResolve(identifier, out IReadOnlyList<Header> resolved))
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
                if (_resolver is not null &&
                    _resolver.TryGetHeaders(key, out IReadOnlyList<Header> resolved) &&
                    resolved.Count > 0)
                {
                    headers = resolved;
                    resolved_messages++;
                }
                else
                {
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

    private bool TryResolve(Identifier identifier, out IReadOnlyList<Header> headers)
    {
        headers = [];
        if (_resolver is null)
            return false;
        if (identifier.Direction is not MessageDirection.Both)
            return _resolver.TryGetHeaders(identifier, out headers) && headers.Count > 0;

        _resolver.TryGetHeaders(identifier with { Direction = MessageDirection.In }, out IReadOnlyList<Header> incoming);
        _resolver.TryGetHeaders(identifier with { Direction = MessageDirection.Out }, out IReadOnlyList<Header> outgoing);
        headers = [.. incoming, .. outgoing];
        return headers.Count > 0;
    }

    private void ReportUnresolved(
        List<Identifier>? unresolved,
        List<MessageKey>? unresolved_keys,
        int resolved_messages)
    {
        int unresolved_count = (unresolved?.Count ?? 0) + (unresolved_keys?.Count ?? 0);
        if (unresolved_count == 0)
            return;

        if (!_messages_available && resolved_messages == 0)
        {
            Diag.Debug(
                $"No message catalog is bound; {unresolved_count} message registration(s) are unbound.",
                Category);
            return;
        }

        foreach (Identifier identifier in unresolved ?? [])
        {
            if (!_reported.Add(identifier))
                continue;
            Diag.Warn(
                $"Unresolved intercept identifier '{identifier.ToString(true)}'; " +
                "no header matched it, so its callbacks will never run.",
                Category);
        }

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
        IMessageResolver? resolver;
        lock (_sync)
            resolver = _resolver;

        string direction = header.Direction switch
        {
            MessageDirection.In => "in",
            MessageDirection.Out => "out",
            _ => "unknown"
        };

        try
        {
            if (resolver is not null && resolver.TryGetIdentifier(header, out Identifier identifier))
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
            if (_registrations.Remove(registration))
                _bindings = null;
        }
    }

    private sealed class Subscription(InterceptDispatcher dispatcher, Registration registration) : IDisposable
    {
        private Registration? _registration = registration;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _registration, null) is { } current)
                dispatcher.Remove(current);
        }
    }
}
