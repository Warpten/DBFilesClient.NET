using System.Runtime.InteropServices;

namespace Tests.Structures
{
    [DBFile("SpellItemEnchantmentCondition")]
    public class SpellItemEnchantmentConditionEntry
    {
        public uint ID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] LTOperandType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] Operator;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] RTOperandType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] RTOperand;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] Logic;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] LTOperand;
    }
}