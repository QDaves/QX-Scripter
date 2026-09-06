using System.Buffers.Binary;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum InstantMessageContentType
{
    Text,
    Habbicon
}

public enum ConsoleMessageWireFormat
{
    Legacy,
    ContentEnvelope
}

public sealed record InstantMessageContent(
    InstantMessageContentType Type,
    string MessageText,
    int HabbiconId) : IParserComposer<InstantMessageContent>
{
    public static InstantMessageContent Parse(in PacketReader p)
    {
        InstantMessageContentType type = (InstantMessageContentType)p.ReadInt();
        return type switch
        {
            InstantMessageContentType.Text => new(type, p.ReadString(), 0),
            InstantMessageContentType.Habbicon => new(type, string.Empty, p.ReadInt()),
            _ => new(InstantMessageContentType.Text, string.Empty, 0)
        };
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteInt((int)Type);
        switch (Type)
        {
            case InstantMessageContentType.Text:
                p.WriteString(MessageText);
                break;
            case InstantMessageContentType.Habbicon:
                p.WriteInt(HabbiconId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Type));
        }
    }
}

public sealed record ConsoleMessage(
    Id ChatId,
    InstantMessageContent Content,
    int SecondsSinceSent,
    string MessageId,
    int ConfirmationId,
    Id SenderId,
    string SenderName,
    string SenderFigure,
    ConsoleMessageWireFormat WireFormat = ConsoleMessageWireFormat.ContentEnvelope) : IParserComposer<ConsoleMessage>
{
    public InstantMessageContentType ContentType => Content.Type;
    public string MessageText => Content.MessageText;
    public int HabbiconId => Content.HabbiconId;

    public static ConsoleMessage Parse(in PacketReader p)
    {
        Id chat_id = p.ReadId();
        bool content_envelope = HasContentEnvelope(in p);
        InstantMessageContent content = content_envelope
            ? p.Parse<InstantMessageContent>()
            : new(InstantMessageContentType.Text, p.ReadString(), 0);

        return new ConsoleMessage(
            chat_id,
            content,
            p.ReadInt(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadId(),
            p.ReadString(),
            p.ReadString(),
            content_envelope ? ConsoleMessageWireFormat.ContentEnvelope : ConsoleMessageWireFormat.Legacy);
    }

    public void Compose(in PacketWriter p)
    {
        p.WriteId(ChatId);
        if (WireFormat is ConsoleMessageWireFormat.ContentEnvelope)
            p.Compose(Content);
        else
            p.WriteString(MessageText);
        p.WriteInt(SecondsSinceSent);
        p.WriteString(MessageId);
        p.WriteInt(ConfirmationId);
        p.WriteId(SenderId);
        p.WriteString(SenderName);
        p.WriteString(SenderFigure);
    }

    private static bool HasContentEnvelope(in PacketReader p)
    {
        int start = p.Pos;
        if (!TryReadInt(p.Span, ref start, out int content_type))
            return false;

        int content_end = start;
        bool content_valid = content_type switch
        {
            (int)InstantMessageContentType.Text => TrySkipString(p.Span, ref content_end),
            (int)InstantMessageContentType.Habbicon => TrySkipInt(p.Span, ref content_end),
            _ => true
        };
        content_valid = content_valid && TrySkipTail(in p, ref content_end);

        int legacy_end = p.Pos;
        bool legacy_valid = TrySkipString(p.Span, ref legacy_end) && TrySkipTail(in p, ref legacy_end);
        bool known_type = content_type is (int)InstantMessageContentType.Text or (int)InstantMessageContentType.Habbicon;

        return content_valid && (!legacy_valid || known_type);
    }

    private static bool TrySkipTail(in PacketReader p, ref int pos)
    {
        if (!TrySkipInt(p.Span, ref pos) ||
            !TrySkipString(p.Span, ref pos) ||
            !TrySkipInt(p.Span, ref pos))
            return false;

        int id_size = p.Client switch
        {
            ClientType.Unity => sizeof(long),
            ClientType.Flash => sizeof(int),
            _ => 0
        };
        if (id_size == 0 || pos > p.Length - id_size)
            return false;

        pos += id_size;
        return TrySkipString(p.Span, ref pos) &&
               TrySkipString(p.Span, ref pos) &&
               pos == p.Length;
    }

    private static bool TryReadInt(ReadOnlySpan<byte> span, ref int pos, out int value)
    {
        if (pos > span.Length - sizeof(int))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32BigEndian(span.Slice(pos, sizeof(int)));
        pos += sizeof(int);
        return true;
    }

    private static bool TrySkipInt(ReadOnlySpan<byte> span, ref int pos)
    {
        if (pos > span.Length - sizeof(int))
            return false;
        pos += sizeof(int);
        return true;
    }

    private static bool TrySkipString(ReadOnlySpan<byte> span, ref int pos)
    {
        if (pos > span.Length - sizeof(short))
            return false;

        int length = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(pos, sizeof(short)));
        pos += sizeof(short);
        if (pos > span.Length - length)
            return false;
        pos += length;
        return true;
    }
}
