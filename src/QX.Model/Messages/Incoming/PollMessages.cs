using Qx.Messages;
using Qx.Model.Polls;

namespace Qx.Model.Messages.Incoming;

public sealed record PollOffer(
    Id PollId,
    string Type,
    string Headline,
    string Summary) : IParserComposer<PollOffer>
{
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

public sealed record PollContents(
    Id PollId,
    string StartMessage,
    string EndMessage,
    IReadOnlyList<PollQuestionGroup> Questions,
    bool IsNetPromoterScore) : IParserComposer<PollContents>
{
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

public sealed record PollError : IParserComposer<PollError>
{
    public static PollError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PollError ParseFlash(in PacketReader p) => ParseError(in p);

    private static PollError ParseError(in PacketReader p)
    {
        PollWire.RequireEmpty(in p, nameof(PollError));
        return new PollError();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PollError value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);
}

public sealed record StartPoll(Id PollId) : IParserComposer<StartPoll>
{
    public static StartPoll Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static StartPoll ParseFlash(in PacketReader p)
    {
        var value = new StartPoll(p.ReadInt());
        PollWire.RequireEmpty(in p, nameof(StartPoll));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(StartPoll value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int poll_id = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        p.WriteInt(poll_id);
    }
}

public sealed record RejectPoll(Id PollId) : IParserComposer<RejectPoll>
{
    public static RejectPoll Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RejectPoll ParseFlash(in PacketReader p)
    {
        var value = new RejectPoll(p.ReadInt());
        PollWire.RequireEmpty(in p, nameof(RejectPoll));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RejectPoll value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int poll_id = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        p.WriteInt(poll_id);
    }
}

public sealed record PollAnswer(
    Id PollId,
    IReadOnlyList<PollResponse> Responses) : IParserComposer<PollAnswer>
{
    public static PollAnswer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PollAnswer ParseFlash(in PacketReader p)
    {
        Id poll_id = p.ReadInt();
        Id question_id = p.ReadInt();
        int count = PollWire.ReadFlashCount(
            in p,
            PollWire.StringMinimumBytes,
            nameof(PollResponse.Answers));
        var answers = new string[count];
        for (int index = 0; index < answers.Length; index++)
            answers[index] = p.ReadString();
        PollWire.RequireEmpty(in p, nameof(PollAnswer));
        var response = new PollResponse(question_id, PollWire.Freeze(answers));
        return new PollAnswer(poll_id, PollWire.Freeze(new[] { response }));
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PollAnswer value, in PacketWriter p)
    {
        PollAnswer prepared = Prepare(value, in p);
        PollResponse response = prepared.Responses[0];
        int poll_id = PollWire.RequireInt32Id(prepared.PollId, nameof(PollId));
        int question_id = PollWire.RequireInt32Id(response.QuestionId, nameof(PollResponse.QuestionId));
        p.WriteInt(poll_id);
        p.WriteInt(question_id);
        p.WriteInt(response.Answers.Count);
        foreach (string answer in response.Answers)
            p.WriteString(answer);
    }

    private static PollAnswer Prepare(PollAnswer value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        PollResponse[] responses = PollWire.SnapshotReferences(value.Responses, nameof(Responses));
        if (responses.Length != 1)
            throw new InvalidDataException("Flash PollAnswer requires exactly one question response.");
        for (int index = 0; index < responses.Length; index++)
            responses[index] = PollResponse.Prepare(responses[index], in p);
        return value with { Responses = PollWire.Freeze(responses) };
    }
}
