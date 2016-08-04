using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("SpellItemEnchantmentCondition")]
    public sealed class SpellItemEnchantmentConditionEntry
    {
        public byte[] LTOperandType;
        public byte[] Operator;
        public byte[] RTOperandType;
        public byte[] RTOperand;
        public byte[] Logic;
        public uint[] LTOperand;
    }
}