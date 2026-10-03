using Qx.Messages;
using Qx.Protocol;

namespace Qx.Presentation.Services.Logging;

public sealed class InterceptFailureLog(int limit = 200)
{
    readonly HashSet<string> _seen = [];
    readonly Lock _gate = new();

    public bool Saturated { get; private set; }

    public bool ShouldReport(Header packetHeader, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string key = $"{packetHeader.Direction}:{packetHeader.Value}:{error.GetType().FullName}:{error.Message}";
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

    public static string Describe(Header packetHeader, IMessageManager? messages)
    {
        try
        {
            if (messages is not null && messages.TryGetIdentifier(packetHeader, out Identifier identifier))
                return $"{identifier.ToString(true)} ({packetHeader.Value})";
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
        }
        string direction = packetHeader.Direction switch
        {
            MessageDirection.In => "in",
            MessageDirection.Out => "out",
            _ => "unknown"
        };
        return $"{direction} header {packetHeader.Value}";
    }

    public static string Format(string described, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return $"Handler for {described} failed: {error.GetType().Name}: {error.Message} " +
            "(state carried by this message is now stale; identical failures are not repeated)";
    }
}
