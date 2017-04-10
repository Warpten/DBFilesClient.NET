using System.Runtime.InteropServices;

namespace Tests.Structures
{
    [DBFile("SpellMisc")]
    public class SpellMiscEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public uint[] Attributes;
        public float Speed;
        public float MultistrikeSpeedMod;
        public ushort CastingTimeIndex;
        public ushort DurationIndex;
        public ushort RangeIndex;
        public byte SchoolMask;
        public uint IconFileDataID;
        public uint ActiveIconFileDataID;
    }
}