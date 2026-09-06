using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class GetGlobalSlotIns : ASInstruction
{
    public int SlotIndex { get; set; }

    public GetGlobalSlotIns()
        : base(OPCode.GetGlobalSlot)
    {
    }

    public GetGlobalSlotIns(ref SpanFlashReader input)
        : this()
    {
        SlotIndex = input.ReadEncodedInt();
    }

    public GetGlobalSlotIns(int slot_index)
        : this()
    {
        SlotIndex = slot_index;
    }

    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine) => machine.Values.Push(null);

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(SlotIndex);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(SlotIndex);
}
