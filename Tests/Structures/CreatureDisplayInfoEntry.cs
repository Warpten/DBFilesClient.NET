using System.Runtime.InteropServices;

namespace Tests.Structures
{
    [DBFile("CreatureDisplayInfo")]
    public class CreatureDisplayInfoEntry
    {
        public int ID;
        public float CreatureModelScale;
        public short ModelID;
        public short NPCSoundID;
        public byte SizeClass;
        public byte Flags;
        public sbyte Gender;
        public uint ExtendedDisplayInfoID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] TextureVariation;
        public uint PortraitTextureFileDataID;
        public byte CreatureModelAlpha;
        public short SoundID;
        public float PlayerModelScale;
        public int PortraitCreatureDisplayInfoID;
        public byte BloodID;
        public short ParticleColorID;
        public uint CreatureGeosetData;
        public short ObjectEffectPackageID;
        public short AnimReplacementSetID;
        public sbyte UnarmedWeaponSubclass;
        public int StateSpellVisualKitID;
        public float InstanceOtherPlayerPetScale;                             // scale of not own player pets inside dungeons/raids/scenarios
        public int MountSpellVisualKitID;
    }
}
