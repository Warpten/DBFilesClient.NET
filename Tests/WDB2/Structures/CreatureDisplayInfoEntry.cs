using DBFilesClient2.NET.Attributes;

namespace DBFilesClient.NET.UnitTests.WDB2.Structures
{
    [DBFileName("CreatureDisplayInfo")]
    public sealed class CreatureDisplayInfoEntry
    {
        [Index]
        public uint ID { get; set; }
        public uint ModelId { get; set; }
        public uint SoundId { get; set; }
        public uint ExtendedDisplayInfoId { get; set; }
        public float ModelScale { get; set; }
        public uint ModelAlpha { get; set; }
        [StoragePresence(StoragePresence.Include, SizeConst = 3)]
        public string[] TextureVariations { get; set; }
        public string PortraitTextureName { get; set; }
        public int SizeClass { get; set; }
        public uint BloodId { get; set; }
        public uint NPCSoundId { get; set; }
        public uint ParticleColorId { get; set; }
        public uint CreatureGeosetData { get; set; }
        public uint ObjectEffectPackageId { get; set; }
        public uint AnimReplacementSetId { get; set; }
    }
}
