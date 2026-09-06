using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public enum Avm2ExceptionNormalizationStatus
{
    Raw,
    Normalized,
    Ambiguous
}

public enum Avm2ExceptionFromResolution
{
    Raw,
    Shifted,
    Ambiguous,
    Unproven
}

public sealed class Avm2ExceptionNormalizationCandidate
{
    public required int Shift { get; init; }
    public required int From { get; init; }
    public required int To { get; init; }
    public required int Target { get; init; }
    public required int JumpOffset { get; init; }
    public required int JumpTarget { get; init; }
    public required int NewCatchOffset { get; init; }
    public required int NewCatchExceptionIndex { get; init; }
    public required List<int> FromCandidates { get; init; }
    public required Avm2ExceptionFromResolution FromResolution { get; init; }
}

public sealed class Avm2ExceptionNormalization
{
    public required int ExceptionIndex { get; init; }
    public required int RawFrom { get; init; }
    public required int RawTo { get; init; }
    public required int RawTarget { get; init; }
    public required int From { get; init; }
    public required int To { get; init; }
    public required int Target { get; init; }
    public required Avm2ExceptionNormalizationStatus Status { get; init; }
    public int? Shift { get; init; }
    public int? JumpOffset { get; init; }
    public int? JumpTarget { get; init; }
    public int? NewCatchOffset { get; init; }
    public int? NewCatchExceptionIndex { get; init; }
    public required List<int> FromCandidates { get; init; }
    public required Avm2ExceptionFromResolution FromResolution { get; init; }
    public required List<Avm2ExceptionNormalizationCandidate> Candidates { get; init; }
}

public static class Avm2ExceptionNormalizer
{
    sealed class InstructionSpan
    {
        public required int Index { get; init; }
        public required int Offset { get; init; }
        public required int Size { get; init; }
        public required ASInstruction Instruction { get; init; }
    }

    public static IReadOnlyList<Avm2ExceptionNormalization> Normalize(
        ASMethodBody body,
        ASCode code)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(code);

        List<InstructionSpan> instructions = ReadInstructions(code);
        Dictionary<int, InstructionSpan> by_offset = instructions
            .ToDictionary(value => value.Offset);
        var result = new List<Avm2ExceptionNormalization>(
            body.Exceptions.Count);
        for (int exception_index = 0;
            exception_index < body.Exceptions.Count;
            exception_index++)
        {
            ASException exception = body.Exceptions[exception_index];
            if (HandlerNewCatch(
                    instructions,
                    by_offset,
                    exception.Target,
                    exception_index)
                is not null)
            {
                result.Add(Raw(exception_index, exception));
                continue;
            }

            List<Avm2ExceptionNormalizationCandidate> candidates =
                CandidateShifts(
                    body,
                    instructions,
                    by_offset,
                    exception_index,
                    exception);
            if (candidates.Count != 1)
            {
                result.Add(new Avm2ExceptionNormalization
                {
                    ExceptionIndex = exception_index,
                    RawFrom = exception.From,
                    RawTo = exception.To,
                    RawTarget = exception.Target,
                    From = exception.From,
                    To = exception.To,
                    Target = exception.Target,
                    Status = candidates.Count == 0
                        ? Avm2ExceptionNormalizationStatus.Raw
                        : Avm2ExceptionNormalizationStatus.Ambiguous,
                    FromCandidates = [],
                    FromResolution = Avm2ExceptionFromResolution.Raw,
                    Candidates = candidates
                });
                continue;
            }

            Avm2ExceptionNormalizationCandidate candidate = candidates[0];
            result.Add(new Avm2ExceptionNormalization
            {
                ExceptionIndex = exception_index,
                RawFrom = exception.From,
                RawTo = exception.To,
                RawTarget = exception.Target,
                From = candidate.From,
                To = candidate.To,
                Target = candidate.Target,
                Status = Avm2ExceptionNormalizationStatus.Normalized,
                Shift = candidate.Shift,
                JumpOffset = candidate.JumpOffset,
                JumpTarget = candidate.JumpTarget,
                NewCatchOffset = candidate.NewCatchOffset,
                NewCatchExceptionIndex =
                    candidate.NewCatchExceptionIndex,
                FromCandidates = candidate.FromCandidates,
                FromResolution = candidate.FromResolution,
                Candidates = candidates
            });
        }
        return result;
    }

    static List<InstructionSpan> ReadInstructions(ASCode code)
    {
        var result = new List<InstructionSpan>(code.Count);
        for (int index = 0; index < code.Count; index++)
        {
            ASInstruction instruction = code[index];
            int size = instruction.DecodedSize > 0
                ? instruction.DecodedSize
                : instruction.GetSize();
            result.Add(new InstructionSpan
            {
                Index = index,
                Offset = instruction.DecodedOffset,
                Size = size,
                Instruction = instruction
            });
        }
        return result;
    }

    static List<Avm2ExceptionNormalizationCandidate> CandidateShifts(
        ASMethodBody body,
        IReadOnlyList<InstructionSpan> instructions,
        IReadOnlyDictionary<int, InstructionSpan> by_offset,
        int exception_index,
        ASException exception)
    {
        var candidates =
            new List<Avm2ExceptionNormalizationCandidate>();
        foreach (InstructionSpan jump_span in instructions)
        {
            if (jump_span.Instruction is not Jumper jump ||
                jump.OP != OPCode.Jump)
            {
                continue;
            }
            int shift = jump_span.Offset - exception.To;
            if (shift <= 0)
                continue;
            int jump_end = jump_span.Offset + jump_span.Size;
            if (exception.Target < jump_span.Offset ||
                exception.Target >= jump_end ||
                !instructions.Any(value =>
                    value.Index < jump_span.Index &&
                    value.Offset <= exception.To &&
                    exception.To < value.Offset + value.Size))
            {
                continue;
            }
            int target;
            try
            {
                target = checked(exception.Target + shift);
            }
            catch (OverflowException)
            {
                continue;
            }
            if (jump_end != target ||
                !by_offset.ContainsKey(target))
            {
                continue;
            }
            int jump_target = jump_span.Offset +
                jump_span.Size +
                SignedOffset(jump.Offset);
            if (jump_target <= target ||
                jump_target > body.Code.Length ||
                jump_target < body.Code.Length &&
                !by_offset.ContainsKey(jump_target))
            {
                continue;
            }
            InstructionSpan? new_catch = HandlerNewCatch(
                instructions,
                by_offset,
                target,
                exception_index);
            if (new_catch is null ||
                jump_target < new_catch.Offset + new_catch.Size)
                continue;

            List<int> from_candidates = FromCandidates(
                by_offset,
                exception.From,
                shift);
            (int from, Avm2ExceptionFromResolution resolution) =
                ResolveFrom(exception.From, from_candidates);
            if (from < 0 || from > jump_span.Offset)
                continue;
            candidates.Add(new Avm2ExceptionNormalizationCandidate
            {
                Shift = shift,
                From = from,
                To = jump_span.Offset,
                Target = target,
                JumpOffset = jump_span.Offset,
                JumpTarget = jump_target,
                NewCatchOffset = new_catch.Offset,
                NewCatchExceptionIndex = exception_index,
                FromCandidates = from_candidates,
                FromResolution = resolution
            });
        }
        return candidates;
    }

    static InstructionSpan? HandlerNewCatch(
        IReadOnlyList<InstructionSpan> instructions,
        IReadOnlyDictionary<int, InstructionSpan> by_offset,
        int target,
        int exception_index)
    {
        if (!by_offset.TryGetValue(target, out InstructionSpan? first))
            return null;
        if (first.Instruction is NewCatchIns immediate &&
            immediate.ExceptionIndex == exception_index)
        {
            return first;
        }
        int index = first.Index;
        int next_offset = target;
        while (index + 1 < instructions.Count &&
            IsGetLocal(instructions[index].Instruction.OP))
        {
            InstructionSpan local = instructions[index];
            InstructionSpan push_scope = instructions[index + 1];
            if (local.Offset != next_offset ||
                local.Offset + local.Size != push_scope.Offset ||
                push_scope.Instruction.OP != OPCode.PushScope)
            {
                return null;
            }
            next_offset = push_scope.Offset + push_scope.Size;
            index += 2;
        }
        if (index >= instructions.Count)
            return null;
        InstructionSpan new_catch = instructions[index];
        return new_catch.Offset == next_offset &&
            new_catch.Instruction is NewCatchIns value &&
            value.ExceptionIndex == exception_index
                ? new_catch
                : null;
    }

    static bool IsGetLocal(OPCode opcode) =>
        opcode is
            OPCode.GetLocal or
            OPCode.GetLocal_0 or
            OPCode.GetLocal_1 or
            OPCode.GetLocal_2 or
            OPCode.GetLocal_3;

    static List<int> FromCandidates(
        IReadOnlyDictionary<int, InstructionSpan> by_offset,
        int raw_from,
        int shift)
    {
        var candidates = new List<int>(2);
        if (by_offset.ContainsKey(raw_from))
            candidates.Add(raw_from);
        int shifted;
        try
        {
            shifted = checked(raw_from + shift);
        }
        catch (OverflowException)
        {
            return candidates;
        }
        if (shifted != raw_from && by_offset.ContainsKey(shifted))
            candidates.Add(shifted);
        return candidates;
    }

    static (int From, Avm2ExceptionFromResolution Resolution) ResolveFrom(
        int raw_from,
        IReadOnlyList<int> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0] == raw_from
                ? (raw_from, Avm2ExceptionFromResolution.Raw)
                : (candidates[0], Avm2ExceptionFromResolution.Shifted);
        }
        if (candidates.Count > 1)
            return (raw_from, Avm2ExceptionFromResolution.Ambiguous);
        return (raw_from, Avm2ExceptionFromResolution.Unproven);
    }

    static Avm2ExceptionNormalization Raw(
        int exception_index,
        ASException exception) =>
        new()
        {
            ExceptionIndex = exception_index,
            RawFrom = exception.From,
            RawTo = exception.To,
            RawTarget = exception.Target,
            From = exception.From,
            To = exception.To,
            Target = exception.Target,
            Status = Avm2ExceptionNormalizationStatus.Raw,
            FromCandidates = [exception.From],
            FromResolution = Avm2ExceptionFromResolution.Raw,
            Candidates = []
        };

    static int SignedOffset(uint value) =>
        (value & 0x00800000) == 0
            ? (int)value
            : unchecked((int)(value | 0xff000000));
}

public static class Avm2ExceptionArchiveProjection
{
    public static AbcExceptionNormalizationInventory Create(
        Avm2ExceptionNormalization normalization) =>
        new()
        {
            Status = normalization.Status.ToString(),
            Shift = normalization.Shift,
            JumpOffset = normalization.JumpOffset,
            JumpTarget = normalization.JumpTarget,
            NewCatchOffset = normalization.NewCatchOffset,
            NewCatchExceptionIndex =
                normalization.NewCatchExceptionIndex,
            FromCandidates = normalization.FromCandidates.ToList(),
            FromResolution = normalization.FromResolution.ToString(),
            Candidates = normalization.Candidates.Select(candidate =>
                new AbcExceptionNormalizationCandidateInventory
                {
                    Shift = candidate.Shift,
                    From = candidate.From,
                    To = candidate.To,
                    Target = candidate.Target,
                    JumpOffset = candidate.JumpOffset,
                    JumpTarget = candidate.JumpTarget,
                    NewCatchOffset = candidate.NewCatchOffset,
                    NewCatchExceptionIndex =
                        candidate.NewCatchExceptionIndex,
                    FromCandidates = candidate.FromCandidates.ToList(),
                    FromResolution =
                        candidate.FromResolution.ToString()
                }).ToList()
        };

    public static AbcExceptionNormalizationArchiveRecord Create(
        int abc_index,
        int method_index,
        Avm2ExceptionNormalization normalization) =>
        new()
        {
            AbcIndex = abc_index,
            MethodIndex = method_index,
            ExceptionIndex = normalization.ExceptionIndex,
            RawFrom = normalization.RawFrom,
            RawTo = normalization.RawTo,
            RawTarget = normalization.RawTarget,
            From = normalization.From,
            To = normalization.To,
            Target = normalization.Target,
            Normalization = Create(normalization)
        };
}
