using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>PollOffer</c> message, received when the hotel offers the user a poll.</summary>
/// <param name="PollId">The ID of the poll.</param>
/// <param name="Type">The poll type sent by the hotel.</param>
/// <param name="Headline">The headline of the offer.</param>
/// <param name="Summary">The summary text of the offer.</param>
public sealed record PollOffer(
    Id PollId,
    string Type,
    string Headline,
    string Summary) : IParserComposer<PollOffer>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PollOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PollOffer ParseFlash(in PacketReader p) => ParseOffer(in p);

    private static PollOffer ParseOffer(in PacketReader p)
    {
        var value = new PollOffer(
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());
        PollWire.RequireEmpty(in p, nameof(PollOffer));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PollOffer value, in PacketWriter p) =>
        ComposeOffer(value, in p);

    private static void ComposeOffer(PollOffer value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int poll_id = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        PollWire.RequireString(value.Type, nameof(Type), in p);
        PollWire.RequireString(value.Headline, nameof(Headline), in p);
        PollWire.RequireString(value.Summary, nameof(Summary), in p);
        p.WriteInt(poll_id);
        p.WriteString(value.Type);
        p.WriteString(value.Headline);
        p.WriteString(value.Summary);
    }
}

/// <summary>Represents the <c>PollContents</c> message, received with the questions of a poll the user has started.</summary>
/// <param name="PollId">The ID of the poll.</param>
/// <param name="StartMessage">The message shown before the questions.</param>
/// <param name="EndMessage">The message shown after the poll is answered.</param>
/// <param name="Questions">The question groups, in the order the hotel sent them.</param>
/// <param name="IsNetPromoterScore">Whether the poll is a net promoter score poll.</param>
public sealed record PollContents(
    Id PollId,
    string StartMessage,
    string EndMessage,
    IReadOnlyList<PollQuestionGroup> Questions,
    bool IsNetPromoterScore) : IParserComposer<PollContents>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PollContents Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PollContents ParseFlash(in PacketReader p)
    {
        Id poll_id = p.ReadInt();
        string start_message = p.ReadString();
        string end_message = p.ReadString();
        int count = PollWire.ReadFlashCount(
            in p,
            PollWire.FlashGroupMinimumBytes,
            nameof(Questions),
            sizeof(byte));
        var questions = new PollQuestionGroup[count];
        for (int index = 0; index < questions.Length; index++)
            questions[index] = p.Parse<PollQuestionGroup>();
        bool is_net_promoter_score = p.ReadBool();
        PollWire.RequireEmpty(in p, nameof(PollContents));
        return new PollContents(
            poll_id,
            start_message,
            end_message,
            PollWire.Freeze(questions),
            is_net_promoter_score);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PollContents value, in PacketWriter p)
    {
        PollContents prepared = Prepare(value, in p);
        p.WriteInt(PollWire.RequireInt32Id(prepared.PollId, nameof(PollId)));
        p.WriteString(prepared.StartMessage);
        p.WriteString(prepared.EndMessage);
        p.WriteInt(prepared.Questions.Count);
        foreach (PollQuestionGroup question in prepared.Questions)
            p.Compose(question);
        p.WriteBool(prepared.IsNetPromoterScore);
    }

    private static PollContents Prepare(PollContents value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        PollWire.RequireString(value.StartMessage, nameof(StartMessage), in p);
        PollWire.RequireString(value.EndMessage, nameof(EndMessage), in p);
        PollQuestionGroup[] questions = PollWire.SnapshotReferences(
            value.Questions,
            nameof(Questions));
        for (int index = 0; index < questions.Length; index++)
            questions[index] = PollQuestionGroup.Prepare(questions[index], in p);
        return value with { Questions = PollWire.Freeze(questions) };
    }
}

/// <summary>Represents the <c>PollError</c> message, received when a poll cannot be started.</summary>
/// <remarks>The message carries no data.</remarks>
public sealed record PollError : IParserComposer<PollError>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PollError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PollError ParseFlash(in PacketReader p) => ParseError(in p);

    private static PollError ParseError(in PacketReader p)
    {
        PollWire.RequireEmpty(in p, nameof(PollError));
        return new PollError();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PollError value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);
}
