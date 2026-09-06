using Qx.Messages;

namespace Qx.Ui;

/// <summary>
/// Decides which intercept handler failures are worth showing. A broken parser fails on every
/// packet of its kind, so identical failures are reported once and the total is capped.
/// </summary>
public sealed class InterceptFailureLog(int limit = 200)
{
    private readonly HashSet<string> _seen = [];
    private readonly object _sync = new();

    /// <summary>Whether the cap has been reached and further failures are being dropped.</summary>
    public bool Saturated { get; private set; }

    /// <summary>Distinct failures reported so far.</summary>
    public int Count
    {
        get
        {
            lock (_sync)
                return _seen.Count;
        }
    }

    /// <summary>
    /// Whether this failure has not been seen before and the cap still allows reporting it.
    /// </summary>
    public bool ShouldReport(Header header, Exception error)
    {
        string key = $"{header.Direction}:{header.Value}:{error.GetType().FullName}:{error.Message}";
        lock (_sync)
        {
            if (_seen.Count >= limit)
            {
                Saturated = true;
                return false;
            }
            return _seen.Add(key);
        }
    }

    /// <summary>
    /// Names the message a failure belongs to, falling back to the raw header when the catalogue
    /// cannot resolve it or throws.
    /// </summary>
    public static string Describe(Header header, IMessageManager? messages)
    {
        try
        {
            if (messages is not null && messages.TryGetIdentifier(header, out Identifier identifier))
                return $"{identifier.ToString(true)} ({header.Value})";
        }
        catch
        {
        }
        return $"{Direction(header)} header {header.Value}";
    }

    /// <summary>The line shown for a failure, describing what it costs the caller.</summary>
    public static string Format(string described, Exception error) =>
        $"Handler for {described} failed: {error.GetType().Name}: {error.Message} " +
        "(state carried by this message is now stale; identical failures are not repeated)";

    private static string Direction(Header header) => header.Direction switch
    {
        Qx.Direction.In => "in",
        Qx.Direction.Out => "out",
        _ => "unknown"
    };
}
