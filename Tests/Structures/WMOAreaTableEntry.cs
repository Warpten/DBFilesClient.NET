using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("WMOAreaTable")]
    public sealed class WMOAreaTableEntry
    {
        public int WMOGroupID;
        public string AreaName;
        public short WMOID;
        public ushort AmbienceID;
        public ushort ZoneMusic;
        public ushort IntroSound;
        public ushort AreaTableID;
        public ushort UWIntroSound;
        public ushort UWAmbience;
        public sbyte NameSet;
        public byte SoundProviderPref;
        public byte SoundProviderPrefUnderwater;
        public byte Flags;
        public uint ID;
        public uint UWZoneMusic;
    }
}