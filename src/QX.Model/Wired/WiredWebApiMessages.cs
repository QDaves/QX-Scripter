using Qx.Messages;

namespace Qx.Model.Wired;

/// <summary>Requests generation of a read or write key for a Wired.</summary>
/// <param name="WiredId">The Wired furniture ID.</param>
/// <param name="ReadKey">True for a read key; false for a write key.</param>
public sealed record WiredGenerateWebApiKey(Id WiredId, bool ReadKey) : IParserComposer<WiredGenerateWebApiKey>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGenerateWebApiKey Parse(in PacketReader p) =>
        FlashWire.Parse(in p, read);

    private static WiredGenerateWebApiKey read(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, write);

    private static void write(WiredGenerateWebApiKey value, in PacketWriter p)
    {
        p.WriteInt(WiredWire.FlashId(value.WiredId));
        p.WriteBool(value.ReadKey);
    }
}

/// <summary>The generated key for a Wired and key access type.</summary>
/// <param name="WiredId">The Wired furniture ID.</param>
/// <param name="ReadKey">True for a read key; false for a write key.</param>
/// <param name="Key">The key returned by the hotel.</param>
public sealed record WiredWebApiKeyResult(Id WiredId, bool ReadKey, string Key) : IParserComposer<WiredWebApiKeyResult>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredWebApiKeyResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, read);

    private static WiredWebApiKeyResult read(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool(), p.ReadString());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, write);

    private static void write(WiredWebApiKeyResult value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Key);
        p.WriteInt(WiredWire.FlashId(value.WiredId));
        p.WriteBool(value.ReadKey);
        p.WriteString(value.Key);
    }
}
