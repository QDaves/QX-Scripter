using Qx.Messages;
using Qx.Protocol;

namespace Qx.Presentation.Services.Logging;

public sealed class InterceptFailureLog(int limit = 200)
{
    readonly HashSet<string> _seen = [];
    readonly Lock _gate = new();

    public bool Saturated { get; private set; }

    public bool ShouldReport(Header packet_header, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string key = $"{packet_header.Direction}:{packet_header.Value}:{error.GetType().FullName}:{error.Message}";
        lock (_gate)
        {
            if (_seen.Count >= limit)
            {
                Saturated = true;
                return false;
            }
            return _seen.Add(key);
        }
    }

    public static string Describe(Header packet_header, IMessageManager? messages)
    {
        try
        {
            if (messages is not null && messages.TryGetIdentifier(packet_header, out Identifier identifier))
                return $"{identifier.ToString(true)} ({packet_header.Value})";
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
        }
        string direction = packet_header.Direction switch
        {
            Qx.Direction.In => "in",
            Qx.Direction.Out => "out",
            _ => "unknown"
        };
        return $"{direction} header {packet_header.Value}";
    }

    public static string Format(string described, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return $"Handler for {described} failed: {error.GetType().Name}: {error.Message} " +
            "(state carried by this message is now stale; identical failures are not repeated)";
    }
}
