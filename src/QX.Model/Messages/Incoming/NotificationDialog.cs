using Qx.Messages;
using System.Collections.ObjectModel;

namespace Qx.Model.Messages.Incoming;

public sealed record NotificationDialog(string Type, IReadOnlyDictionary<string, string> Parameters)
    : IParserComposer<NotificationDialog>
{
    public bool IsPlacementRateLimit => Parameters.Values.Append(Type).Any(value =>
        value.Contains("room.error.placements_too_fast", StringComparison.Ordinal));

    public static NotificationDialog Parse(in PacketReader p) => FlashWire.Parse(in p, parse);

    private static NotificationDialog parse(in PacketReader p)
    {
        string type = p.ReadString();
        int count = p.ReadInt();
        if (count < 0 || (long)count * 4 > p.Available)
            throw new InvalidDataException("Notification parameter count exceeds the payload.");
        var parameters = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++) parameters[p.ReadString()] = p.ReadString();
        if (p.Available != 0) throw new InvalidDataException("Unexpected notification payload.");
        return new(type, new ReadOnlyDictionary<string, string>(parameters));
    }

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, compose);

    private static void compose(NotificationDialog value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Type);
        ArgumentNullException.ThrowIfNull(value.Parameters);
        var parameters = value.Parameters.ToArray();
        foreach (string text in parameters.SelectMany(pair => new[] { pair.Key, pair.Value }).Prepend(value.Type))
        {
            ArgumentNullException.ThrowIfNull(text);
            if (p.Encoding.GetByteCount(text) > ushort.MaxValue)
                throw new ArgumentException("Notification string exceeds the protocol limit.");
        }
        p.WriteString(value.Type);
        p.WriteInt(parameters.Length);
        foreach (var pair in parameters)
        {
            p.WriteString(pair.Key);
            p.WriteString(pair.Value);
        }
    }
}
