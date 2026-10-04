using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the outgoing <c>PollStart</c> message, sent to start a poll the hotel offered.</summary>
/// <param name="PollId">The ID of the poll to start.</param>
public sealed record StartPoll(Id PollId) : IParserComposer<StartPoll>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static StartPoll Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static StartPoll ParseFlash(in PacketReader p)
    {
        var value = new StartPoll(p.ReadInt());
        PollWire.RequireEmpty(in p, nameof(StartPoll));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(StartPoll value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int poll_id = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        p.WriteInt(poll_id);
    }
}

/// <summary>Represents the outgoing <c>PollReject</c> message, sent to decline a poll the hotel offered.</summary>
/// <param name="PollId">The ID of the poll to decline.</param>
public sealed record RejectPoll(Id PollId) : IParserComposer<RejectPoll>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RejectPoll Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RejectPoll ParseFlash(in PacketReader p)
    {
        var value = new RejectPoll(p.ReadInt());
        PollWire.RequireEmpty(in p, nameof(RejectPoll));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RejectPoll value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int poll_id = PollWire.RequireInt32Id(value.PollId, nameof(PollId));
        p.WriteInt(poll_id);
    }
}

/// <summary>Represents the outgoing <c>PollAnswer</c> message, sent to answer one question of a poll.</summary>
/// <param name="PollId">The ID of the poll.</param>
/// <param name="Responses">
/// The answers to the question. The Flash message carries exactly one response, so composing
/// throws <see cref="InvalidDataException"/> for any other count.
/// </param>
public sealed record PollAnswer(
    Id PollId,
    IReadOnlyList<PollResponse> Responses) : IParserComposer<PollAnswer>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
