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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static ConsoleMessageContent ParseUnity(in PacketReader p)
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
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(ConsoleMessageContent value, in PacketWriter p)
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static LegacyCompactConsoleMessage ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt(), p.ReadString());

    private static LegacyCompactConsoleMessage ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(LegacyCompactConsoleMessage value, in PacketWriter p)
    {
        p.WriteId(value.FirstId);
        p.WriteString(value.FirstText);
        p.WriteInt(value.Value);
        p.WriteString(value.SecondText);
    }

    private static void ComposeUnity(LegacyCompactConsoleMessage value, in PacketWriter p)
    {
        p.WriteId(value.FirstId);
        p.WriteString(value.FirstText);
        p.WriteInt(value.Value);
        p.WriteString(value.SecondText);
    }
}

/// <summary>
/// A private message from a friend, as shown in the console.
/// </summary>
/// <remarks>
/// id 468. This is the message a script has to watch to react to someone writing privately;
/// room chat arrives separately.
/// </remarks>
/// <param name="ChatId">Which conversation the message belongs to.</param>
/// <param name="Content">The message itself, either text or an icon.</param>
/// <param name="SecondsSinceSent">How long ago it was sent, which is non-zero for offline messages.</param>
/// <param name="MessageId">The hotel's identifier for the message.</param>
/// <param name="ConfirmationId">Correlates the hotel's delivery confirmation.</param>
/// <param name="SenderId">Who wrote it.</param>
/// <param name="SenderName">Their name.</param>
/// <param name="SenderFigure">Their figure string.</param>
/// <param name="LegacyCompact">The raw fields when Unity used the legacy compact layout.</param>
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static NewConsoleMessage ParseUnity(in PacketReader p)
    {
        ConsoleMessageWireLayout layout = UnityLayout(in p);
        if (layout is ConsoleMessageWireLayout.LegacyCompact)
        {
            LegacyCompactConsoleMessage compact = p.Parse<LegacyCompactConsoleMessage>();
            return new NewConsoleMessage(
                compact.FirstId,
                new ConsoleMessageContent(ConsoleMessageContent.TypeText, compact.FirstText, 0),
                0,
                "",
                0,
                0,
                "",
                "",
                compact);
        }

        Id chat_id = p.ReadId();
        ConsoleMessageContent content = layout is ConsoleMessageWireLayout.TaggedHabbicon
            ? p.Parse<ConsoleMessageContent>()
            : new ConsoleMessageContent(ConsoleMessageContent.TypeText, p.ReadString(), 0);
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
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(NewConsoleMessage value, in PacketWriter p)
    {
        ConsoleMessageWireLayout layout = UnityLayout(in p);
        if (layout is ConsoleMessageWireLayout.LegacyCompact)
        {
            p.Compose(value.LegacyCompact ??
                throw new InvalidOperationException("The legacy compact console layout requires its raw fields."));
            return;
        }
        p.WriteId(value.ChatId);
        if (layout is ConsoleMessageWireLayout.TaggedHabbicon)
        {
            p.Compose(value.Content);
        }
        else
        {
            if (value.Content.Type != ConsoleMessageContent.TypeText)
                throw new InvalidOperationException("The legacy console layout cannot carry a habbicon message.");
            p.WriteString(value.Content.Text);
        }
        p.WriteInt(value.SecondsSinceSent);
        p.WriteString(value.MessageId);
        p.WriteInt(value.ConfirmationId);
        p.WriteId(value.SenderId);
        p.WriteString(value.SenderName);
        p.WriteString(value.SenderFigure);
    }

    private static ConsoleMessageWireLayout UnityLayout(in PacketReader p) =>
        p.Context?.WireProfile.RequireUnityConsoleMessageLayout() ??
        throw new NotSupportedException("The active Unity session has no compatible console message wire layout.");

    private static ConsoleMessageWireLayout UnityLayout(in PacketWriter p) =>
        p.Context?.WireProfile.RequireUnityConsoleMessageLayout() ??
        throw new NotSupportedException("The active Unity session has no compatible console message wire layout.");
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MessengerError ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static MessengerError ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MessengerError value, in PacketWriter p)
    {
        p.WriteInt(value.ClientMessageId);
        p.WriteInt(value.ErrorCode);
    }

    private static void ComposeUnity(MessengerError value, in PacketWriter p)
    {
        p.WriteInt(value.ClientMessageId);
        p.WriteInt(value.ErrorCode);
    }
}

/// <summary>
/// A private message could not be delivered.
/// </summary>
/// <param name="ErrorCode">Why it failed.</param>
/// <param name="UserId">Who it was meant for.</param>
/// <param name="Message">The hotel's explanation.</param>
/// <summary>
/// Why a message to somebody did not go through.
/// </summary>
/// <remarks>
/// The middle field names the person, so it is as wide as a user id: four bytes on Flash and eight
/// on Unity. Read as a plain integer it took half the id and left the rest to be mistaken for the
/// message that follows.
/// </remarks>
public sealed record InstantMessageError(int ErrorCode, Id UserId, string Message)
    : IParserComposer<InstantMessageError>
{
    public static InstantMessageError Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static InstantMessageError ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadString());

    private static InstantMessageError ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(InstantMessageError value, in PacketWriter p)
    {
        p.WriteInt(value.ErrorCode);
        p.WriteId(value.UserId);
        p.WriteString(value.Message);
    }

    private static void ComposeUnity(InstantMessageError value, in PacketWriter p)
    {
        p.WriteInt(value.ErrorCode);
        p.WriteId(value.UserId);
        p.WriteString(value.Message);
    }
}
