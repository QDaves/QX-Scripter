using System.Buffers.Binary;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Specifies what a private console message carries.</summary>
public enum InstantMessageContentType
{
    /// <summary>A written message.</summary>
    Text,
    /// <summary>A habbicon instead of text.</summary>
    Habbicon
}

/// <summary>Specifies the wire layout of a console message.</summary>
public enum ConsoleMessageWireFormat
{
    /// <summary>The layout with the message text in place of the content.</summary>
    Legacy,
    /// <summary>The layout with a typed content block that holds either text or a habbicon.</summary>
    ContentEnvelope
}

/// <summary>Represents the content of a private console message, either text or a habbicon.</summary>
/// <param name="Type">The kind of content.</param>
/// <param name="MessageText">The message text, empty for a habbicon.</param>
/// <param name="HabbiconId">The identifier of the habbicon, 0 for a text message.</param>
public sealed record InstantMessageContent(
    InstantMessageContentType Type,
    string MessageText,
    int HabbiconId) : IParserComposer<InstantMessageContent>
{
    /// <summary>Parses the content from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>An unknown content type is read as empty text and nothing after the type is consumed.</remarks>
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

    /// <summary>Composes the content into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="Type"/> is not a known content type.
    /// </exception>
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

/// <summary>Represents a private console message in either the legacy or the content envelope layout.</summary>
/// <param name="ChatId">The identifier of the conversation the message belongs to.</param>
/// <param name="Content">The content of the message.</param>
/// <param name="SecondsSinceSent">The number of seconds since the message was sent.</param>
/// <param name="MessageId">The message identifier sent by the hotel.</param>
/// <param name="ConfirmationId">The confirmation identifier sent by the hotel.</param>
/// <param name="SenderId">The identifier of the user who sent the message.</param>
/// <param name="SenderName">The name of the user who sent the message.</param>
/// <param name="SenderFigure">The figure string of the user who sent the message.</param>
/// <param name="WireFormat">The layout the message was read in and is written in.</param>
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
    /// <summary>Gets the kind of content the message carries.</summary>
    public InstantMessageContentType ContentType => Content.Type;
    /// <summary>Gets the message text, empty for a habbicon.</summary>
    public string MessageText => Content.MessageText;
    /// <summary>Gets the identifier of the habbicon, 0 for a text message.</summary>
    public int HabbiconId => Content.HabbiconId;

    /// <summary>Parses a console message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>
    /// The layout is detected from the payload. The content envelope is used when it fits the whole message and
    /// either the legacy layout does not fit or the content type is known.
    /// </remarks>
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

    /// <summary>Composes the console message into a packet in the layout given by <see cref="WireFormat"/>.</summary>
    /// <param name="p">The packet writer.</param>
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

        if (pos > p.Length - sizeof(int))
            return false;

        pos += sizeof(int);
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
