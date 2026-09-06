using Qx.Messages;

namespace Qx.Model.Polls;

public enum PollQuestionType
{
    RadioButtons = 1,
    Checkboxes = 2,
    TextLine = 3,
    TextArea = 4
}

public sealed record PollChoice(string Value, string Text, int Type) : IParserComposer<PollChoice>
{
    public static PollChoice Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PollChoice ParseFlash(in PacketReader p) => ParseChoice(in p);

    private static PollChoice ParseUnity(in PacketReader p) => ParseChoice(in p);

    private static PollChoice ParseChoice(in PacketReader p) =>
        new(p.ReadString(), p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PollChoice value, in PacketWriter p) =>
        ComposeChoice(Prepare(value, in p), in p);

    private static void ComposeUnity(PollChoice value, in PacketWriter p) =>
        ComposeChoice(Prepare(value, in p), in p);

    private static void ComposeChoice(PollChoice value, in PacketWriter p)
    {
        p.WriteString(value.Value);
        p.WriteString(value.Text);
        p.WriteInt(value.Type);
    }

    internal static PollChoice Prepare(PollChoice value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        PollWire.RequireString(value.Value, nameof(Value), in p);
        PollWire.RequireString(value.Text, nameof(Text), in p);
        return value;
    }
}

public sealed record PollQuestion(
    Id QuestionId,
    int SortOrder,
    PollQuestionType Type,
    string Text,
    int Category,
    IReadOnlyList<PollChoice> Choices,
    int? FlashAnswerType = null,
    int? FlashAnswerCount = null) : IParserComposer<PollQuestion>
{
    public bool AllowsMultipleAnswers => Type is PollQuestionType.Checkboxes;
    public bool HasChoices => Type is PollQuestionType.RadioButtons or PollQuestionType.Checkboxes;

    public static PollQuestion Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PollQuestion ParseFlash(in PacketReader p)
    {
        Id question_id = p.ReadInt();
        int sort_order = p.ReadInt();
        var type = (PollQuestionType)p.ReadInt();
        string text = p.ReadString();
        int category = p.ReadInt();
        int answer_type = p.ReadInt();
        int answer_count = p.ReadInt();
        PollWire.RequireNonNegative(answer_count, nameof(FlashAnswerCount));

        PollChoice[] choices;
        if (HasChoiceLayout(type))
        {
            PollWire.RequireCount(
                answer_count,
                p.Available,
                PollWire.ChoiceMinimumBytes,
                nameof(Choices));
            choices = new PollChoice[answer_count];
            for (int index = 0; index < choices.Length; index++)
                choices[index] = p.Parse<PollChoice>();
        }
        else
        {
            choices = [];
        }

        return new PollQuestion(
            question_id,
            sort_order,
            type,
            text,
            category,
            PollWire.Freeze(choices),
            answer_type,
            answer_count);
    }

    private static PollQuestion ParseUnity(in PacketReader p)
    {
        Id question_id = p.ReadLong();
        int sort_order = p.ReadInt();
        var type = (PollQuestionType)p.ReadInt();
        string text = p.ReadString();
        int category = p.ReadInt();
        int count = PollWire.ReadUnityCount(
            in p,
            PollWire.ChoiceMinimumBytes,
            nameof(Choices));
        var choices = new PollChoice[count];
        for (int index = 0; index < choices.Length; index++)
            choices[index] = p.Parse<PollChoice>();
        return new PollQuestion(
            question_id,
            sort_order,
            type,
            text,
            category,
            PollWire.Freeze(choices));
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PollQuestion value, in PacketWriter p)
    {
        PollQuestion prepared = Prepare(value, true, in p);
        int question_id = PollWire.RequireInt32Id(prepared.QuestionId, nameof(QuestionId));
        int answer_count = prepared.FlashAnswerCount ?? prepared.Choices.Count;
        p.WriteInt(question_id);
        p.WriteInt(prepared.SortOrder);
        p.WriteInt((int)prepared.Type);
        p.WriteString(prepared.Text);
        p.WriteInt(prepared.Category);
        p.WriteInt(prepared.FlashAnswerType ?? 0);
        p.WriteInt(answer_count);
        foreach (PollChoice choice in prepared.Choices)
            p.Compose(choice);
    }

    private static void ComposeUnity(PollQuestion value, in PacketWriter p)
    {
        PollQuestion prepared = Prepare(value, false, in p);
        p.WriteLong(prepared.QuestionId);
        p.WriteInt(prepared.SortOrder);
        p.WriteInt((int)prepared.Type);
        p.WriteString(prepared.Text);
        p.WriteInt(prepared.Category);
        PollWire.WriteUnityCount(prepared.Choices.Count, in p);
        foreach (PollChoice choice in prepared.Choices)
            p.Compose(choice);
    }

    internal static PollQuestion Prepare(PollQuestion value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        PollWire.RequireString(value.Text, nameof(Text), in p);
        PollChoice[] choices = PollWire.SnapshotReferences(value.Choices, nameof(Choices));
        for (int index = 0; index < choices.Length; index++)
            choices[index] = PollChoice.Prepare(choices[index], in p);

        if (flash)
        {
            _ = PollWire.RequireInt32Id(value.QuestionId, nameof(QuestionId));
            int answer_count = value.FlashAnswerCount ?? choices.Length;
            PollWire.RequireNonNegative(answer_count, nameof(FlashAnswerCount));
            if (HasChoiceLayout(value.Type) && answer_count != choices.Length)
            {
                throw new InvalidDataException(
                    "Flash choice questions require AnswerCount to match Choices.");
            }
            if (!HasChoiceLayout(value.Type) && choices.Length != 0)
                throw new InvalidDataException("Flash text questions cannot contain choice payloads.");
        }
        else
        {
            if (value.FlashAnswerType is not null || value.FlashAnswerCount is not null)
            {
                throw new InvalidDataException(
                    "Unity poll questions cannot contain Flash answer metadata.");
            }
            PollWire.RequireUnityCount(choices.Length, nameof(Choices));
        }

        return value with { Choices = PollWire.Freeze(choices) };
    }

    internal static bool HasChoiceLayout(PollQuestionType type) =>
        type is PollQuestionType.RadioButtons or PollQuestionType.Checkboxes;
}

public sealed record PollQuestionGroup(
    PollQuestion Question,
    IReadOnlyList<PollQuestion> Children) : IParserComposer<PollQuestionGroup>
{
    public static PollQuestionGroup Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PollQuestionGroup ParseFlash(in PacketReader p)
    {
        PollQuestion question = p.Parse<PollQuestion>();
        int count = PollWire.ReadFlashCount(
            in p,
            PollWire.FlashQuestionMinimumBytes,
            nameof(Children));
        var children = new PollQuestion[count];
        for (int index = 0; index < children.Length; index++)
            children[index] = p.Parse<PollQuestion>();
        return new PollQuestionGroup(question, PollWire.Freeze(children));
    }

    private static PollQuestionGroup ParseUnity(in PacketReader p)
    {
        PollQuestion question = p.Parse<PollQuestion>();
        int count = PollWire.ReadUnityCount(
            in p,
            PollWire.UnityQuestionMinimumBytes,
            nameof(Children));
        var children = new PollQuestion[count];
        for (int index = 0; index < children.Length; index++)
            children[index] = p.Parse<PollQuestion>();
        return new PollQuestionGroup(question, PollWire.Freeze(children));
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PollQuestionGroup value, in PacketWriter p)
    {
        PollQuestionGroup prepared = Prepare(value, true, in p);
        p.Compose(prepared.Question);
        p.WriteInt(prepared.Children.Count);
        foreach (PollQuestion child in prepared.Children)
            p.Compose(child);
    }

    private static void ComposeUnity(PollQuestionGroup value, in PacketWriter p)
    {
        PollQuestionGroup prepared = Prepare(value, false, in p);
        p.Compose(prepared.Question);
        PollWire.WriteUnityCount(prepared.Children.Count, in p);
        foreach (PollQuestion child in prepared.Children)
            p.Compose(child);
    }

    internal static PollQuestionGroup Prepare(
        PollQuestionGroup value,
        bool flash,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        PollQuestion question = PollQuestion.Prepare(value.Question, flash, in p);
        PollQuestion[] children = PollWire.SnapshotReferences(value.Children, nameof(Children));
        if (!flash)
            PollWire.RequireUnityCount(children.Length, nameof(Children));
        for (int index = 0; index < children.Length; index++)
            children[index] = PollQuestion.Prepare(children[index], flash, in p);
        return new PollQuestionGroup(question, PollWire.Freeze(children));
    }
}

public sealed record PollResponse(
    Id QuestionId,
    IReadOnlyList<string> Answers) : IParserComposer<PollResponse>
{
    public static PollResponse Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PollResponse ParseFlash(in PacketReader p)
    {
        Id question_id = p.ReadInt();
        int count = PollWire.ReadFlashCount(
            in p,
            PollWire.StringMinimumBytes,
            nameof(Answers));
        return new PollResponse(question_id, ReadAnswers(count, in p));
    }

    private static PollResponse ParseUnity(in PacketReader p)
    {
        Id question_id = p.ReadLong();
        int count = PollWire.ReadUnityCount(
            in p,
            PollWire.StringMinimumBytes,
            nameof(Answers));
        return new PollResponse(question_id, ReadAnswers(count, in p));
    }

    private static IReadOnlyList<string> ReadAnswers(int count, in PacketReader p)
    {
        var answers = new string[count];
        for (int index = 0; index < answers.Length; index++)
            answers[index] = p.ReadString();
        return PollWire.Freeze(answers);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PollResponse value, in PacketWriter p)
    {
        PollResponse prepared = Prepare(value, true, in p);
        p.WriteInt(PollWire.RequireInt32Id(prepared.QuestionId, nameof(QuestionId)));
        p.WriteInt(prepared.Answers.Count);
        foreach (string answer in prepared.Answers)
            p.WriteString(answer);
    }

    private static void ComposeUnity(PollResponse value, in PacketWriter p)
    {
        PollResponse prepared = Prepare(value, false, in p);
        p.WriteLong(prepared.QuestionId);
        PollWire.WriteUnityCount(prepared.Answers.Count, in p);
        foreach (string answer in prepared.Answers)
            p.WriteString(answer);
    }

    internal static PollResponse Prepare(PollResponse value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash)
            _ = PollWire.RequireInt32Id(value.QuestionId, nameof(QuestionId));
        string[] answers = PollWire.SnapshotStrings(value.Answers, nameof(Answers), in p);
        if (!flash)
            PollWire.RequireUnityCount(answers.Length, nameof(Answers));
        return value with { Answers = PollWire.Freeze(answers) };
    }
}

internal static class PollWire
{
    internal const int StringMinimumBytes = sizeof(short);
    internal const int ChoiceMinimumBytes = sizeof(short) + sizeof(short) + sizeof(int);
    internal const int FlashQuestionMinimumBytes =
        sizeof(int) + sizeof(int) + sizeof(int) + sizeof(short) + sizeof(int) + sizeof(int) + sizeof(int);
    internal const int UnityQuestionMinimumBytes =
        sizeof(long) + sizeof(int) + sizeof(int) + sizeof(short) + sizeof(int) + sizeof(short);
    internal const int FlashGroupMinimumBytes = FlashQuestionMinimumBytes + sizeof(int);
    internal const int UnityGroupMinimumBytes = UnityQuestionMinimumBytes + sizeof(short);
    internal const int UnityResponseMinimumBytes = sizeof(long) + sizeof(short);

    internal static int ReadFlashCount(
        in PacketReader p,
        int minimum_bytes,
        string name,
        int trailing_bytes = 0)
    {
        int count = p.ReadInt();
        return RequireCount(count, p.Available - trailing_bytes, minimum_bytes, name);
    }

    internal static int ReadUnityCount(
        in PacketReader p,
        int minimum_bytes,
        string name,
        int trailing_bytes = 0)
    {
        int count = unchecked((ushort)p.ReadShort());
        return RequireCount(count, p.Available - trailing_bytes, minimum_bytes, name);
    }

    internal static int RequireCount(int count, int available, int minimum_bytes, string name)
    {
        RequireNonNegative(count, name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_bytes);
        if (available < 0 || count > available / minimum_bytes)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
        }
        return count;
    }

    internal static void RequireNonNegative(int count, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
    }

    internal static int RequireInt32Id(Id value, string name)
    {
        try
        {
            return checked((int)(long)value);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{name} does not fit the 32-bit wire format.", exception);
        }
    }

    internal static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
    }

    internal static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }

    internal static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    internal static void WriteUnityCount(int count, in PacketWriter p)
    {
        RequireUnityCount(count, nameof(count));
        p.WriteShort(unchecked((short)(ushort)count));
    }

    internal static IReadOnlyList<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);

    internal static T[] SnapshotReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        T[] copy = values.ToArray();
        foreach (T value in copy)
            ArgumentNullException.ThrowIfNull(value, name);
        return copy;
    }

    internal static string[] SnapshotStrings(
        IReadOnlyList<string> values,
        string name,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        string[] copy = values.ToArray();
        foreach (string value in copy)
            RequireString(value, name, in p);
        return copy;
    }
}
