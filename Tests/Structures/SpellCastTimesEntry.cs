using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("SpellCastTimes")]
    public sealed class SpellCastTimesEntry
    {
        public int CastTime;
        public int MinCastTime;
        public short CastTimePerLevel;
    }
}