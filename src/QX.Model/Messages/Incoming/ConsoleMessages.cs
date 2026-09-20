using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// What a console message carries: either text or an icon, never both.
/// </summary>
/// <remarks>
/// A tagged union on the wire. Type 0 carries the text and no icon, type 1 carries an icon
/// identifier and no text. The client's own reader has a default branch that consumes nothing,
/// which would leave the stream misaligned for everything after it, so an unknown tag is rejected
/// here rather than silently skipped.
/// </remarks>
/// <param name="Type">0 for text, 1 for an icon.</param>
/// <param name="Text">The message text, empty for an icon message.</param>
/// <param name="HabbiconId">The icon, zero for a text message.</param>
public sealed record ConsoleMessageContent(int Type, string Text, int HabbiconId)
    : IParserComposer<ConsoleMessageContent>
{
    /// <summary>A written message.</summary>
    public const int TypeText = 0;

    /// <summary>An icon rather than text.</summary>
    public const int TypeHabbicon = 1;

    /// <summary>Whether this is an icon rather than written text.</summary>
    public bool IsHabbicon => Type == TypeHabbicon;

    public static ConsoleMessageContent Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ConsoleMessageContent ParseFlash(in PacketReader p)
    {
        int type = p.ReadInt();
        return type switch
        {
            TypeText => new ConsoleMessageContent(TypeText, p.ReadString(), 0),
            TypeHabbicon => new ConsoleMessageContent(TypeHabbicon, "", p.ReadInt()),
            _ => throw new InvalidDataException(
                $"Unknown console message content type {type} — the stream would desync.")
        };
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ConsoleMessageContent value, in PacketWriter p)
    {
        p.WriteInt(value.Type);
        switch (value.Type)
        {
            case TypeText:
                p.WriteString(value.Text);
                break;
            case TypeHabbicon:
                p.WriteInt(value.HabbiconId);
                break;
            default:
                throw new InvalidDataException($"Unknown console message content type {value.Type}.");
        }
    }
}

public sealed record LegacyCompactConsoleMessage(Id FirstId, string FirstText, int Value, string SecondText)
    : IParserComposer<LegacyCompactConsoleMessage>
{
    public static LegacyCompactConsoleMessage Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static LegacyCompactConsoleMessage ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(LegacyCompactConsoleMessage value, in PacketWriter p)
    {
        p.WriteId(value.FirstId);
        p.WriteString(value.FirstText);
        p.WriteInt(value.Value);
        p.WriteString(value.SecondText);
    }
}

public sealed record NewConsoleMessage(
    Id ChatId,
    ConsoleMessageContent Content,
    int SecondsSinceSent,
    string MessageId,
    int ConfirmationId,
    Id SenderId,
    string SenderName,
    string SenderFigure,
    LegacyCompactConsoleMessage? LegacyCompact = null) : IParserComposer<NewConsoleMessage>
{
    /// <summary>The message text, empty when the message is an icon.</summary>
    public string Text => Content.Text;

    /// <summary>Whether the message was waiting rather than sent just now.</summary>
    public bool IsOffline => SecondsSinceSent > 0;

    public static NewConsoleMessage Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NewConsoleMessage ParseFlash(in PacketReader p)
    {
        Id chat_id = p.ReadId();
        ConsoleMessageContent content = p.Parse<ConsoleMessageContent>();
        int secondsSinceSent = p.ReadInt();
        string messageId = p.ReadString();
        int confirmationId = p.ReadInt();
        Id senderId = p.ReadId();
        string senderName = p.ReadString();
        string senderFigure = p.ReadString();
        return new NewConsoleMessage(
            chat_id,
            content,
            secondsSinceSent,
            messageId,
            confirmationId,
            senderId,
            senderName,
            senderFigure);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NewConsoleMessage value, in PacketWriter p)
    {
        p.WriteId(value.ChatId);
        p.Compose(value.Content);
        p.WriteInt(value.SecondsSinceSent);
        p.WriteString(value.MessageId);
        p.WriteInt(value.ConfirmationId);
        p.WriteId(value.SenderId);
        p.WriteString(value.SenderName);
        p.WriteString(value.SenderFigure);
    }
}

/// <summary>
/// The hotel refused a messenger operation.
/// </summary>
/// <param name="ClientMessageId">Which of the client's requests failed.</param>
/// <param name="ErrorCode">Why it failed.</param>
public sealed record MessengerError(int ClientMessageId, int ErrorCode)
    : IParserComposer<MessengerError>
{
    public static MessengerError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MessengerError ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MessengerError value, in PacketWriter p)
    {
        p.WriteInt(value.ClientMessageId);
        p.WriteInt(value.ErrorCode);
    }
}

public sealed record InstantMessageError(int ErrorCode, Id UserId, string Message)
    : IParserComposer<InstantMessageError>
{
    public static InstantMessageError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static InstantMessageError ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(InstantMessageError value, in PacketWriter p)
    {
        p.WriteInt(value.ErrorCode);
        p.WriteId(value.UserId);
        p.WriteString(value.Message);
    }
}
