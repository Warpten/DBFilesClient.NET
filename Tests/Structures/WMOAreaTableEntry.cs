namespace Tests.Structures
{
    [DBFile("WMOAreaTable")]
    public class WMOAreaTableEntry
    {
        public int WMOGroupID;                                              //  used in group WMO
        public string AreaName;
        public short WMOID;                                                   //  used in root WMO
        public ushort AmbienceID;
        public ushort ZoneMusic;
        public ushort IntroSound;
        public ushort AreaTableID;
        public ushort UWIntroSound;
        public ushort UWAmbience;
        public sbyte NameSet;                                                  //  used in adt file
        public byte SoundProviderPref;
        public byte SoundProviderPrefUnderwater;
        public byte Flags;
        public uint ID;
        public uint UWZoneMusic;
    }
}