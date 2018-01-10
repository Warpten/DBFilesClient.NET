using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDC1.Structures
{
    [DBFileName("SpellEffect")]
    public sealed class SpellEffectEntry
    {
        [Index]
        public int ID { get; set; }
        public int Effect { get; set; }
        public int BasePoints { get; set; }
        public int EffectIndex { get; set; }
        public int ApplyAuraName { get; set; }
        public int DifficultyID { get; set; }
        public int Amplitude { get; set; }
        public int AuraPeriod { get; set; }
        public int BonusCoefficientFromSP { get; set; }
        public float ChainAmplitude { get; set; }
        public int ChainTargets { get; set; }
        public int DieSides { get; set; }
        public int ItemID { get; set; }
        public int Mechanic { get; set; }
        public int PointsPerResource { get; set; }
        public int RealPointsPerLevel { get; set; }
        public int TriggerSpell { get; set; }
        public float PosFacing { get; set; }
        public int Attributes { get; set; }
        public int BonusCoefficientFromAP { get; set; }
        public float EffectExtraFloat { get; set; }
        public int ScalingCoefficient { get; set; }
        public int ScalingVariance { get; set; }
        public int ScalingResourceCoefficient { get; set; }
        public float GroupSizeScaling { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 4)]
        public int[] SpellClassMask { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 2)]
        public int[] MiscValue { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 2)]
        public int[] RadiusID { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 2)]
        public int[] ImplicitTarget { get; set; }
        public int SpellID { get; set; }
    }
}
