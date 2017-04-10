using System.Runtime.InteropServices;

namespace Tests.Structures
{
    [DBFile("SpellInterrupts")]
    public class SpellInterruptsEntry
    {
        public uint SpellID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] AuraInterruptFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] ChannelInterruptFlags;
        public ushort InterruptFlags;
        public byte DifficultyID;
    }
}