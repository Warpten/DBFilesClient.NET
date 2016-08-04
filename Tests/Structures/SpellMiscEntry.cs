using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("SpellMisc")]
    public sealed class SpellMiscEntry
    {
        public uint[] Attributes;
        public float Speed;
        public float MultistrikeSpeedMod;
        public ushort CastingTimeIndex;
        public ushort DurationIndex;
        public ushort RangeIndex;
        public ushort SpellIconID;
        public ushort ActiveIconID;
        public byte SchoolMask;
    }
}