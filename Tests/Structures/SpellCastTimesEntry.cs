namespace Tests.Structures
{
    [DBFile("SpellCastTimes")]
    public class SpellCastTimesEntry
    {
        public uint ID;
        public int CastTime;
        public int MinCastTime;
        public short CastTimePerLevel;
    }
}