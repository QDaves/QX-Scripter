namespace Qx.Headers.Flash;

public sealed class AbcExceptionNormalizationInventory
{
    public required string Status { get; init; }
    public int? Shift { get; init; }
    public int? JumpOffset { get; init; }
    public int? JumpTarget { get; init; }
    public int? NewCatchOffset { get; init; }
    public int? NewCatchExceptionIndex { get; init; }
    public required List<int> FromCandidates { get; init; }
    public required string FromResolution { get; init; }
    public required List<AbcExceptionNormalizationCandidateInventory> Candidates { get; init; }
}

public sealed class AbcExceptionNormalizationCandidateInventory
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
    public required string FromResolution { get; init; }
}

public sealed class AbcExceptionNormalizationArchiveRecord
{
    public int FormatVersion { get; init; } = 1;
    public required int AbcIndex { get; init; }
    public required int MethodIndex { get; init; }
    public required int ExceptionIndex { get; init; }
    public required int RawFrom { get; init; }
    public required int RawTo { get; init; }
    public required int RawTarget { get; init; }
    public required int From { get; init; }
    public required int To { get; init; }
    public required int Target { get; init; }
    public required AbcExceptionNormalizationInventory Normalization { get; init; }
}

public sealed class Avm2InstructionInventory
{
    public required int Index { get; init; }
    public required int Offset { get; init; }
    public required string Opcode { get; init; }
    public required int PopCount { get; init; }
    public required int PushCount { get; init; }
    public required bool CanThrow { get; init; }
    public required int Block { get; init; }
    public required Dictionary<string, string?> Operands { get; init; }
}

public sealed class Avm2ControlFlowInventory
{
    public required int EntryBlock { get; init; }
    public required List<Avm2BasicBlockInventory> Blocks { get; init; }
    public required List<Avm2ControlFlowEdgeInventory> Edges { get; init; }
    public required bool HasLoop { get; init; }
    public required bool Complete { get; init; }
    public List<Avm2DominatorInventory> Dominators { get; init; } = [];
    public List<Avm2NaturalLoopInventory> NaturalLoops { get; init; } = [];
    public List<Avm2IrreducibleCycleInventory> IrreducibleCycles { get; init; } = [];
}

public sealed class Avm2DominatorInventory
{
    public required int Block { get; init; }
    public int? ImmediateDominator { get; init; }
    public required List<int> Dominators { get; init; }
}

public sealed class Avm2NaturalLoopInventory
{
    public required int Id { get; init; }
    public required int HeaderBlock { get; init; }
    public required List<int> LatchBlocks { get; init; }
    public required List<int> Blocks { get; init; }
    public required List<int> ExitingBlocks { get; init; }
    public required List<int> ExitBlocks { get; init; }
    public int? ParentLoop { get; init; }
    public required int Depth { get; init; }
    public required List<int> Ancestors { get; init; }
}

public sealed class Avm2IrreducibleCycleInventory
{
    public required int Id { get; init; }
    public required List<int> Blocks { get; init; }
    public required List<int> EntryBlocks { get; init; }
}

public sealed class Avm2BasicBlockInventory
{
    public required int Id { get; init; }
    public required int FirstInstruction { get; init; }
    public required int LastInstruction { get; init; }
    public required int StartOffset { get; init; }
    public required int EndOffset { get; init; }
    public required bool Reachable { get; init; }
    public int? EntryStackDepth { get; init; }
    public int? ExitStackDepth { get; init; }
    public int? EntryScopeDepth { get; init; }
    public int? ExitScopeDepth { get; init; }
}

public sealed class Avm2ControlFlowEdgeInventory
{
    public required int FromBlock { get; init; }
    public int? ToBlock { get; init; }
    public required int SourceInstruction { get; init; }
    public required int SourceOffset { get; init; }
    public required int TargetOffset { get; init; }
    public required string Kind { get; init; }
    public int? CaseIndex { get; init; }
    public int? ExceptionIndex { get; init; }
    public string? ExceptionType { get; init; }
}

public sealed class Avm2ReferenceInventory
{
    public required int Instruction { get; init; }
    public required int Offset { get; init; }
    public required string Kind { get; init; }
    public required string Target { get; init; }
    public string? SymbolIdentity { get; init; }
    public string? EncodingSymbolIdentity { get; init; }
    public string? RuntimeSymbolIdentity { get; init; }
    public string? NormalizedSymbolIdentity { get; init; }
    public int? ArgumentCount { get; init; }
    public int? MethodIndex { get; init; }
    public int? ClassIndex { get; init; }
}
