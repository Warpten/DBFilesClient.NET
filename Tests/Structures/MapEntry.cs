using System.Runtime.InteropServices;

namespace Tests.Structures
{
    public class MapEntry
    {
        public string directory;
        public int[] flags;
        public float minimapIconScale;
        public float[] corpseCoords;
        public string mapname_lang;
        public string mapDescription0_lang;
        public string mapDescription1_lang;
        public string unk0;
        public string unk1;
        public ushort areaTableID;
        public short loadingScreenID;
        public short corpseMapID;
        public short timeOfDayoverride;
        public short parentMapID;
        public short cosmeticParentMapID;
        public short windsettingsID;
        public byte instanceType;
        public byte mapType;
        public byte expansionID;
        public byte maxPlayers;
        public byte timeOffset;
    }
}
