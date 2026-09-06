using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class SetGlobalSlotIns : ASInstruction
{
    public int SlotIndex { get; set; }

    public SetGlobalSlotIns()
        : base(OPCode.SetGlobalSlot)
    {
    }

    public SetGlobalSlotIns(ref SpanFlashReader input)
        : this()
    {
        SlotIndex = input.ReadEncodedInt();
    }

    public SetGlobalSlotIns(int slot_index)
        : this()
    {
        SlotIndex = slot_index;
    }

    public override int GetPopCount() => 1;

    public override void Execute(ASMachine machine) => machine.Values.Pop();

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(SlotIndex);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(SlotIndex);
}
