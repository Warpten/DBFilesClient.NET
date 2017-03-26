using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("CreatureDisplayInfo")]
    public sealed class CreatureDisplayInfoEntry
    {
        public int ID;
        public float CreatureModelScale;
        public short Model;
        public short NPCSoundID;
        public byte SizeClass;
        public byte   F;
        public byte   G;
        public int ExtendedDisplayInfo;
        public int[] TextureVariation;
        public int    J;
        public byte   K;
        public short SoundID;
        public float PlayerModelScale;
        public int    N;
        public byte BloodID;
        public short ParticleColorID;
        public int CreatureGeosetData;
        public short ObjectEffectPackageID;
        public short AnimReplacementSetID;
        public byte   T;
        public int StateSpellVisualKitID;
        public float  V;
        public int    W; 
    }
}